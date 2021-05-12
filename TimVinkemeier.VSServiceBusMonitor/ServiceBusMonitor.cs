using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Azure.Messaging.ServiceBus.Administration;

using EnvDTE;

using EnvDTE80;

using Microsoft.VisualStudio.Shell;

using Newtonsoft.Json;

using TimVinkemeier.VSServiceBusMonitor.Helpers;
using TimVinkemeier.VSServiceBusMonitor.Models;

namespace TimVinkemeier.VSServiceBusMonitor
{
    public sealed class ServiceBusMonitor
    {
        private const int DEFAULT_REFRESH_INTERVAL_MILLIS = 5000;
        private Profile _currentlyWatchedProfile;
        private DTE2 _dte;
        private ServiceBusAdministrationClient _serviceBusClient;
        private Timer _timer;

        private ServiceBusMonitor()
        {
        }

        public static ServiceBusMonitor Instance { get; } = new ServiceBusMonitor();

        public bool IsEnabled { get; set; } = true;

        public async System.Threading.Tasks.Task InitializeAsync(DTE2 dte)
        {
            _dte = dte;

            ServiceBusMonitorConfigFileWatcher.Instance.ConfigChanged += OnConfigurationChanged;

            await DetermineNewWatchedConfigurationAsync().ConfigureAwait(false);
            await StartServiceBusMonitoringAsync().ConfigureAwait(false);
        }

        public async System.Threading.Tasks.Task StopMonitoringAsync()
        {
            ServiceBusMonitorConfigFileWatcher.Instance.ConfigChanged -= OnConfigurationChanged;

            await StopServiceBusMonitoringAsync().ConfigureAwait(false);
        }

        private static string FormatForDisplay(string displayName, long activeMessageCount, long deadLetterMessageCount)
            => $"{displayName} ({activeMessageCount},{deadLetterMessageCount})";

        private static string GetDisplayName(QueueDefinition definition)
            => !string.IsNullOrWhiteSpace(definition.ShortName) ? definition.ShortName : definition.QueueName;

        private static string GetDisplayName(SubscriptionDefinition definition)
            => !string.IsNullOrWhiteSpace(definition.ShortName) ? definition.ShortName : $"{definition.TopicName} > {definition.SubscriptionName}";

        private static string GetLongDisplayName(QueueDefinition definition)
            => !string.IsNullOrWhiteSpace(definition.ShortName) ? $"{definition.QueueName} ({definition.ShortName})" : definition.QueueName;

        private static string GetLongDisplayName(SubscriptionDefinition definition)
            => !string.IsNullOrWhiteSpace(definition.ShortName)
                ? $"{definition.TopicName} > {definition.SubscriptionName} ({definition.ShortName})"
                : $"{definition.TopicName} > {definition.SubscriptionName}";

        private static bool ShouldBeShown(DisplayMode display, long activeMessageCount, long deadLetterMessageCount)
        {
            switch (display)
            {
                case DisplayMode.Default:
                    return activeMessageCount > 0 || deadLetterMessageCount > 0;

                case DisplayMode.Always:
                    return true;

                case DisplayMode.OnlyDlq:
                    return deadLetterMessageCount > 0;

                case DisplayMode.TooltipOnly:
                    return false;
            }

            return false;
        }

        private async System.Threading.Tasks.Task DetermineNewWatchedConfigurationAsync()
        {
            try
            {
                var currentConfig = ServiceBusMonitorConfigFileWatcher.Instance.CurrentConfig;
                if (currentConfig is null)
                {
                    // do not start monitoring if there is no config
                    await Logger.Instance.LogAsync("No configuration found - waiting for config file.").ConfigureAwait(false);
                    _currentlyWatchedProfile = null;
                    return;
                }

                if ((currentConfig.Profiles?.Count ?? 0) == 0)
                {
                    await Logger.Instance.LogAsync("No profiles defined.").ConfigureAwait(false);
                    _currentlyWatchedProfile = null;
                    return;
                }

                await Logger.Instance.LogAsync($"Found {currentConfig.Profiles.Count} profiles.").ConfigureAwait(false);

                var relevantProfileName = await GetRelevantProfileNameAsync(currentConfig).ConfigureAwait(false);

                if (string.IsNullOrEmpty(relevantProfileName)
                    && currentConfig.Profiles.Count > 1)
                {
                    await Logger.Instance.LogAsync("No profile selected as active. Please set activeProfileName.").ConfigureAwait(false);
                    _currentlyWatchedProfile = null;
                    return;
                }

                if (string.IsNullOrEmpty(relevantProfileName)
                    && currentConfig.Profiles.Count == 1)
                {
                    await Logger.Instance.LogAsync($"No profile selected as active - however, there is only one named '{currentConfig.Profiles.Single().Name}' - using that.").ConfigureAwait(false);
                    currentConfig.ActiveProfileName = currentConfig.Profiles.Single().Name;
                    relevantProfileName = currentConfig.ActiveProfileName;
                    ServiceBusMonitorConfigFileWatcher.Instance.SaveUpdatedCurrentConfig();
                }

                var activeProfile = currentConfig.Profiles.Single(p => p.Name == relevantProfileName);
                _currentlyWatchedProfile = JsonConvert.DeserializeObject<Profile>(JsonConvert.SerializeObject(activeProfile));
            }
            catch (Exception ex)
            {
                await Logger.Instance.LogAsync($"Failed to read configuration: {ex.Message}").ConfigureAwait(false);
                await Logger.Instance.LogAsync(ex.StackTrace).ConfigureAwait(false);
                _currentlyWatchedProfile = null;
            }
        }

        private (string Text, string Tooltip, bool IsActive, BackgroundStyle BackgroundStyle) FormatForDisplay(
                                                                    List<(QueueDefinition Definition, QueueRuntimeProperties Data)> queuesData,
            List<(SubscriptionDefinition Definition, SubscriptionRuntimeProperties Data)> subscriptionData,
            Profile profile,
            DateTime updateTime)
        {
            // calculate statusbar text
            var queuesToShow = queuesData
                .Where(q => ShouldBeShown(q.Definition.Display, q.Data.ActiveMessageCount, q.Data.DeadLetterMessageCount))
                .Select(q => FormatForDisplay(GetDisplayName(q.Definition), q.Data.ActiveMessageCount, q.Data.DeadLetterMessageCount))
                .ToList();

            var subscriptionsToShow = subscriptionData
                .Where(q => ShouldBeShown(q.Definition.Display, q.Data.ActiveMessageCount, q.Data.DeadLetterMessageCount))
                .Select(q => FormatForDisplay(GetDisplayName(q.Definition), q.Data.ActiveMessageCount, q.Data.DeadLetterMessageCount))
                .ToList();

            var totalCount = queuesData.Count + subscriptionData.Count;
            var shownCount = queuesToShow.Count + subscriptionsToShow.Count;
            var monitoredNotShownCount = totalCount - shownCount;

            var text = string.Join(" | ", queuesToShow.Concat(subscriptionsToShow).OrderBy(d => d)) + (monitoredNotShownCount > 0 ? $" | + {monitoredNotShownCount}" : string.Empty);

            if (shownCount == 0)
            {
                text = $"{totalCount} entities monitored";
            }

            // calculate tooltip text
            var dataForTooltip = queuesData
                .Select(q => FormatForDisplay(GetLongDisplayName(q.Definition), q.Data.ActiveMessageCount, q.Data.DeadLetterMessageCount))
                .Concat(subscriptionData
                    .Select(q => FormatForDisplay(GetLongDisplayName(q.Definition), q.Data.ActiveMessageCount, q.Data.DeadLetterMessageCount)))
                .ToList();

            var formattedData = string.Join("\r\n", dataForTooltip.OrderBy(d => d));

            if (dataForTooltip.Count == 0)
            {
                formattedData = "No entities monitored.";
            }

            var tooltip = $"Service Bus Monitor\r\nActive Profile: {profile.Name}\r\n\r\n{formattedData}\r\n\r\nLast updated: {updateTime:G}";

            // determine backgroundstyle
            var backgroundStyle = BackgroundStyle.Normal;
            if (ServiceBusMonitorConfigFileWatcher.Instance?.CurrentConfig?.Settings?.NoColorization != true)
            {
                var anyDataToShowHasDlq = queuesData
                        .Where(q => ShouldBeShown(q.Definition.Display, q.Data.ActiveMessageCount, q.Data.DeadLetterMessageCount))
                        .Any(q => q.Data.DeadLetterMessageCount > 0)
                    || subscriptionData
                        .Where(q => ShouldBeShown(q.Definition.Display, q.Data.ActiveMessageCount, q.Data.DeadLetterMessageCount))
                        .Any(q => q.Data.DeadLetterMessageCount > 0);

                var anyDataHasDlq = queuesData
                        .Any(q => q.Data.DeadLetterMessageCount > 0)
                    || subscriptionData
                        .Any(q => q.Data.DeadLetterMessageCount > 0);

                backgroundStyle = anyDataToShowHasDlq
                    ? BackgroundStyle.Alert
                    : (anyDataHasDlq
                        ? BackgroundStyle.Warning
                        : BackgroundStyle.Normal);
            }

            return (text, tooltip, IsEnabled, backgroundStyle);
        }

        private int GetRefreshInterval(Profile currentlyWatchedProfile)
            => currentlyWatchedProfile.Settings?.RefreshIntervalMillis
                ?? (ServiceBusMonitorConfigFileWatcher.Instance.CurrentConfig?.ProfileDefaultSettings?.RefreshIntervalMillis ?? DEFAULT_REFRESH_INTERVAL_MILLIS);

        private async Task<string> GetRelevantProfileNameAsync(Config currentConfig)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            return _dte.Application?.Debugger.CurrentMode == dbgDebugMode.dbgBreakMode || _dte.Application?.Debugger.CurrentMode == dbgDebugMode.dbgRunMode
                ? (!string.IsNullOrEmpty(currentConfig.DebugProfileName) ? currentConfig.DebugProfileName : currentConfig.ActiveProfileName)
                : currentConfig.ActiveProfileName;
        }

        private async void OnConfigurationChanged()
        {
            try
            {
                await Logger.Instance.LogAsync("Configuration was updated - refreshing...").ConfigureAwait(false);
                await StopServiceBusMonitoringAsync().ConfigureAwait(false);
                await DetermineNewWatchedConfigurationAsync().ConfigureAwait(false);
                await StartServiceBusMonitoringAsync().ConfigureAwait(false);
            }
            catch
            {
                // not sure what to do here, so we just continue
            }
        }

        private async System.Threading.Tasks.Task StartServiceBusMonitoringAsync()
        {
            if (_currentlyWatchedProfile is null)
            {
                ServiceBusMonitorStatusBarController.Instance?.UpdateStatusBarAsync(false, "No configuration", "Service Bus monitor\r\nNo configuration found.");
                return;
            }

            try
            {
                await Logger.Instance.LogAsync($"Starting monitoring of profile '{_currentlyWatchedProfile.Name}'...").ConfigureAwait(false);
                if (_timer is null)
                {
                    _timer = new Timer(_ => UpdateServiceBusData());
                }

                _serviceBusClient = new ServiceBusAdministrationClient(_currentlyWatchedProfile.ConnectionString);

                _timer.Change(0, GetRefreshInterval(_currentlyWatchedProfile));
            }
            catch (Exception ex)
            {
                await Logger.Instance.LogAsync($"Failed to start monitoring: {ex.Message}").ConfigureAwait(false);
                await Logger.Instance.LogAsync(ex.StackTrace).ConfigureAwait(false);
                await StopServiceBusMonitoringAsync().ConfigureAwait(false);
            }
        }

        private async System.Threading.Tasks.Task StopServiceBusMonitoringAsync()
        {
            if (_serviceBusClient is null)
            {
                return;
            }

            if (_timer != null)
            {
                _timer.Change(-1, -1);
            }
            _serviceBusClient = null;
            await Logger.Instance.LogAsync("Stopped monitoring.").ConfigureAwait(false);
        }

        private async void UpdateServiceBusData()
        {
            if (ServiceBusMonitorStatusBarController.Instance == null)
            {
                return;
            }

            var updateTime = DateTime.Now;
            var profile = _currentlyWatchedProfile;
            try
            {
                if (!IsEnabled)
                {
                    await ServiceBusMonitorStatusBarController.Instance.UpdateStatusBarAsync(false, "Paused", "VS ServiceBus Monitor is paused\r\n\r\nRe-activate it via the context menu.", BackgroundStyle.Normal).ConfigureAwait(false);
                    return;
                }

                var queuesData = new List<(QueueDefinition, QueueRuntimeProperties)>();
                var subscriptionData = new List<(SubscriptionDefinition, SubscriptionRuntimeProperties)>();

                await System.Threading.Tasks.Task.Run(async () =>
                {
                    var updateThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                    foreach (var queueDefinition in profile.Queues ?? Enumerable.Empty<QueueDefinition>())
                    {
                        var data = await _serviceBusClient
                            .GetQueueRuntimePropertiesAsync(queueDefinition.QueueName)
                            .ConfigureAwait(true);
                        queuesData.Add((queueDefinition, data));
                    }

                    foreach (var subscriptionDefinition in profile.Subscriptions ?? Enumerable.Empty<SubscriptionDefinition>())
                    {
                        var data = await _serviceBusClient
                            .GetSubscriptionRuntimePropertiesAsync(subscriptionDefinition.TopicName, subscriptionDefinition.SubscriptionName)
                            .ConfigureAwait(true);
                        subscriptionData.Add((subscriptionDefinition, data));
                    }
                }).ConfigureAwait(false);

                var (text, tooltip, isActive, backgroundStyle) = FormatForDisplay(queuesData, subscriptionData, profile, updateTime);
                await ServiceBusMonitorStatusBarController.Instance.UpdateStatusBarAsync(isActive, text, tooltip, backgroundStyle).ConfigureAwait(false);

                // check for change of profiles
                var relevantProfileName = await GetRelevantProfileNameAsync(ServiceBusMonitorConfigFileWatcher.Instance.CurrentConfig).ConfigureAwait(false);
                if (_currentlyWatchedProfile.Name != relevantProfileName)
                {
                    await Logger.Instance.LogAsync($"Relevant profile changed to '{relevantProfileName}' - switching...").ConfigureAwait(false);
                    await StopServiceBusMonitoringAsync().ConfigureAwait(false);
                    await DetermineNewWatchedConfigurationAsync().ConfigureAwait(false);
                    await StartServiceBusMonitoringAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await Logger.Instance.LogAsync($"Failed to update: {ex.Message}").ConfigureAwait(false);
                await Logger.Instance.LogAsync(ex.StackTrace).ConfigureAwait(false);
                await ServiceBusMonitorStatusBarController.Instance.UpdateStatusBarAsync(false, "N/A", $"Service Bus Monitor\r\nActive Profile: {profile.Name}\r\n\r\nUpdate failed. ({updateTime:G})").ConfigureAwait(false);
            }
        }
    }
}

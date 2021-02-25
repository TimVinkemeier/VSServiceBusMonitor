using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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

        public void Initialize(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _dte = dte;

            ServiceBusMonitorConfigFileWatcher.Instance.ConfigChanged += OnConfigurationChanged;

            DetermineNewWatchedConfiguration();
            StartServiceBusMonitoring();
        }

        public void StopMonitoring()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ServiceBusMonitorConfigFileWatcher.Instance.ConfigChanged -= OnConfigurationChanged;

            StopServiceBusMonitoring();
        }

        private static (string Text, string Tooltip, bool IsActive) FormatForDisplay(
            List<(QueueDefinition Definition, QueueRuntimeProperties Data)> queuesData,
            List<(SubscriptionDefinition Definition, SubscriptionRuntimeProperties Data)> subscriptionData,
            Profile profile,
            DateTime updateTime)
        {
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

            return (text, tooltip, true);
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

        private void DetermineNewWatchedConfiguration()
        {
            try
            {
                var currentConfig = ServiceBusMonitorConfigFileWatcher.Instance.CurrentConfig;
                if (currentConfig is null)
                {
                    // do not start monitoring if there is no config
                    Logger.Instance.Log("No configuration found - waiting for config file.");
                    _currentlyWatchedProfile = null;
                    return;
                }

                if ((currentConfig.Profiles?.Count ?? 0) == 0)
                {
                    Logger.Instance.Log("No profiles defined.");
                    _currentlyWatchedProfile = null;
                    return;
                }

                Logger.Instance.Log($"Found {currentConfig.Profiles.Count} profiles.");

                var relevantProfileName = GetRelevantProfileName(currentConfig);

                if (string.IsNullOrEmpty(relevantProfileName)
                    && currentConfig.Profiles.Count > 1)
                {
                    Logger.Instance.Log("No profile selected as active. Please set activeProfileName.");
                    _currentlyWatchedProfile = null;
                    return;
                }

                if (string.IsNullOrEmpty(relevantProfileName)
                    && currentConfig.Profiles.Count == 1)
                {
                    Logger.Instance.Log($"No profile selected as active - however, there is only one named '{currentConfig.Profiles.Single().Name}' - using that.");
                    currentConfig.ActiveProfileName = currentConfig.Profiles.Single().Name;
                    relevantProfileName = currentConfig.ActiveProfileName;
                    ServiceBusMonitorConfigFileWatcher.Instance.SaveUpdatedCurrentConfig();
                }

                var activeProfile = currentConfig.Profiles.Single(p => p.Name == relevantProfileName);
                _currentlyWatchedProfile = JsonConvert.DeserializeObject<Profile>(JsonConvert.SerializeObject(activeProfile));
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Failed to read configuration: {ex.Message}");
                Logger.Instance.Log(ex.StackTrace);
                _currentlyWatchedProfile = null;
            }
        }

        private int GetRefreshInterval(Profile currentlyWatchedProfile)
            => currentlyWatchedProfile.Settings?.RefreshIntervalMillis
                ?? (ServiceBusMonitorConfigFileWatcher.Instance.CurrentConfig?.DefaultSettings?.RefreshIntervalMillis ?? DEFAULT_REFRESH_INTERVAL_MILLIS);

        private string GetRelevantProfileName(Config currentConfig)
            => _dte.Application?.Debugger.CurrentMode == dbgDebugMode.dbgBreakMode || _dte.Application?.Debugger.CurrentMode == dbgDebugMode.dbgRunMode
                ? (!string.IsNullOrEmpty(currentConfig.DebugProfileName) ? currentConfig.DebugProfileName : currentConfig.ActiveProfileName)
                : currentConfig.ActiveProfileName;

        private async void OnConfigurationChanged()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            Logger.Instance.Log("Configuration was updated - refreshing...");
            StopServiceBusMonitoring();
            DetermineNewWatchedConfiguration();
            StartServiceBusMonitoring();
        }

        private void OnDebuggerContextChanged(Process NewProcess, Program NewProgram, EnvDTE.Thread NewThread, StackFrame NewStackFrame)
        {
            throw new NotImplementedException();
        }

        private void StartServiceBusMonitoring()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_currentlyWatchedProfile is null)
            {
                ServiceBusMonitorStatusBarController.Instance?.UpdateStatusBar(false, "No configuration", "Service Bus monitor\r\nNo configuration found.");
                return;
            }

            try
            {
                Logger.Instance.Log($"Starting monitoring of profile '{_currentlyWatchedProfile.Name}'...");
                if (_timer is null)
                {
                    _timer = new Timer(_ => UpdateServiceBusData());
                }

                _serviceBusClient = new ServiceBusAdministrationClient(_currentlyWatchedProfile.ConnectionString);

                _timer.Change(0, GetRefreshInterval(_currentlyWatchedProfile));
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Failed to start monitoring: {ex.Message}");
                Logger.Instance.Log(ex.StackTrace);
                StopServiceBusMonitoring();
            }
        }

        private void StopServiceBusMonitoring()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_serviceBusClient is null)
            {
                return;
            }

            if (_timer != null)
            {
                _timer.Change(-1, -1);
            }
            _serviceBusClient = null;
            Logger.Instance.Log("Stopped monitoring.");
        }

        private async void UpdateServiceBusData()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var updateTime = DateTime.Now;
            var profile = _currentlyWatchedProfile;
            try
            {
                var queuesData = new List<(QueueDefinition, QueueRuntimeProperties)>();
                var subscriptionData = new List<(SubscriptionDefinition, SubscriptionRuntimeProperties)>();
                foreach (var queueDefinition in profile.Queues ?? Enumerable.Empty<QueueDefinition>())
                {
                    var data = await _serviceBusClient.GetQueueRuntimePropertiesAsync(queueDefinition.QueueName);
                    queuesData.Add((queueDefinition, data));
                }

                foreach (var subscriptionDefinition in profile.Subscriptions ?? Enumerable.Empty<SubscriptionDefinition>())
                {
                    var data = await _serviceBusClient.GetSubscriptionRuntimePropertiesAsync(subscriptionDefinition.TopicName, subscriptionDefinition.SubscriptionName);
                    subscriptionData.Add((subscriptionDefinition, data));
                }

                var (text, tooltip, isActive) = FormatForDisplay(queuesData, subscriptionData, profile, updateTime);
                ServiceBusMonitorStatusBarController.Instance.UpdateStatusBar(isActive, text, tooltip);

                // check for change of profiles
                var relevantProfileName = GetRelevantProfileName(ServiceBusMonitorConfigFileWatcher.Instance.CurrentConfig);
                if (_currentlyWatchedProfile.Name != relevantProfileName)
                {
                    Logger.Instance.Log($"Relevant profile changed to '{relevantProfileName}' - switching...");
                    StopServiceBusMonitoring();
                    DetermineNewWatchedConfiguration();
                    StartServiceBusMonitoring();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Failed to update: {ex.Message}");
                Logger.Instance.Log(ex.StackTrace);
                ServiceBusMonitorStatusBarController.Instance.UpdateStatusBar(false, "N/A", $"Service Bus Monitor\r\nActive Profile: {profile.Name}\r\n\r\nUpdate failed. ({updateTime:G})");
            }
        }
    }
}

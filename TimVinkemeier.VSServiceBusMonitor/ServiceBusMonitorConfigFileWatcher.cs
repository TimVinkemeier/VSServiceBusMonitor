using System;
using System.IO;

using EnvDTE80;

using Microsoft.VisualStudio.Shell;

using Newtonsoft.Json;

using TimVinkemeier.VSServiceBusMonitor.Extensions;
using TimVinkemeier.VSServiceBusMonitor.Helpers;
using TimVinkemeier.VSServiceBusMonitor.Models;

namespace TimVinkemeier.VSServiceBusMonitor
{
    public sealed class ServiceBusMonitorConfigFileWatcher
    {
        private Solution2 _solution;
        private FileSystemWatcher _watcher;

        private ServiceBusMonitorConfigFileWatcher()
        {
        }

        public event Action ConfigChanged;

        public static ServiceBusMonitorConfigFileWatcher Instance { get; } = new ServiceBusMonitorConfigFileWatcher();

        public Config CurrentConfig { get; private set; }

        public void Initialize(Solution2 solution)
        {
            _solution = solution;

            try
            {
                if (ConfigFileHelpers.ConfigFileExists(_solution))
                {
                    CurrentConfig = JsonConvert.DeserializeObject<Config>(File.ReadAllText(ConfigFileHelpers.GetConfigFilePath(_solution)));
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Failed to load config: {ex.Message}");
                Logger.Instance.Log(ex.StackTrace);
            }

            var dotVsFolderPath = solution.GetDotVsFolder();
            _watcher = new FileSystemWatcher(dotVsFolderPath, ConfigFileHelpers.CONFIG_FILE_NAME)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };
            _watcher.Changed += OnConfigFileChanged;
            _watcher.Created += OnConfigFileChanged;
            _watcher.Deleted += OnConfigFileChanged;
            _watcher.Renamed += OnConfigFileChanged;
            _watcher.EnableRaisingEvents = true;
        }

        public void StopWatching()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        internal void SaveUpdatedCurrentConfig()
        {
            CurrentConfig.WriteToFile(ConfigFileHelpers.GetConfigFilePath(_solution));
        }

        private async void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (e.FullPath != ConfigFileHelpers.GetConfigFilePath(_solution))
            {
                return;
            }

            try
            {
                CurrentConfig = null;
                if (!ConfigFileHelpers.ConfigFileExists(_solution))
                {
                    ConfigChanged();
                    return;
                }

                var newConfig = Config.LoadFromFile(ConfigFileHelpers.GetConfigFilePath(_solution));
                CurrentConfig = newConfig;
                ConfigChanged();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Failed to refresh config: {ex.Message}");
                Logger.Instance.Log(ex.StackTrace);
            }
        }
    }
}
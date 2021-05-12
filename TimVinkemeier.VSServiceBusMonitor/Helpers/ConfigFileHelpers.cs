using System.IO;

using EnvDTE80;

using TimVinkemeier.VSServiceBusMonitor.Extensions;
using TimVinkemeier.VSServiceBusMonitor.Models.Configuration;

namespace TimVinkemeier.VSServiceBusMonitor.Helpers
{
    public static class ConfigFileHelpers
    {
        public const string CONFIG_FILE_NAME = "service-bus-monitor.config.json";

        public static bool ConfigFileExists(Solution2 solution)
        {
            var configFilePath = GetConfigFilePath(solution);
            return File.Exists(configFilePath);
        }

        public static string CreateConfigFileIfNotExists(Solution2 solution)
        {
            var path = GetConfigFilePath(solution);
            if (!ConfigFileExists(solution))
            {
                Config.Empty.WriteToFile(path);
            }

            return path;
        }

        public static string GetConfigFilePath(Solution2 solution)
        {
            var configDir = solution.GetDotVsFolder();
            return Path.Combine(configDir, CONFIG_FILE_NAME);
        }
    }
}
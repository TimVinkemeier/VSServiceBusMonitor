using System.Windows;

namespace TimVinkemeier.VSServiceBusMonitor.Themes
{
    public partial class Generic : ResourceDictionary
    {
        private void OpenConfigFile_Click(object sender, RoutedEventArgs e)
        {
            ServiceBusMonitorStatusBarController.Instance.OpenConfigFile();
        }

        private void ToggleActivity_Checked(object sender, RoutedEventArgs e)
        {
            ServiceBusMonitor.Instance.IsEnabled = true;
        }

        private void ToggleActivity_Unchecked(object sender, RoutedEventArgs e)
        {
            ServiceBusMonitor.Instance.IsEnabled = false;
        }
    }
}

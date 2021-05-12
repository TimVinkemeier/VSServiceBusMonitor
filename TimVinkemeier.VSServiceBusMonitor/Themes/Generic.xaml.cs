using System.Linq;
using System.Windows;
using System.Windows.Controls;

using TimVinkemeier.VSServiceBusMonitor.Models;

namespace TimVinkemeier.VSServiceBusMonitor.Themes
{
    public partial class Generic : ResourceDictionary
    {
        private void Border_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var fe = e.Source as FrameworkElement;
            var contextMenu = new ContextMenu();

            // Enabled switch
            var enabledMenuItem = new MenuItem
            {
                Header = "_Enabled",
                IsCheckable = true,
                IsChecked = true
            };
            enabledMenuItem.Checked += ToggleActivity_Checked;
            enabledMenuItem.Unchecked += ToggleActivity_Unchecked;
            contextMenu.Items.Add(enabledMenuItem);

            // Separator
            contextMenu.Items.Add(new Separator());

            // Purge Items
            var purgeMenuItems = ServiceBusMonitor.Instance.LatestStatuses
                .Select(qs =>
                {
                    var menuItem = new MenuItem
                    {
                        Header = qs.ShortDisplayName + $" ({qs.ActiveCount})",
                        IsEnabled = qs.ActiveCount > 0
                    };
                    menuItem.Click += (se, args) => PurgeFromServiceBusEntity(qs, se, args, false);
                    return menuItem;
                })
                .ToList();
            var dlqPurgeMenuItems = ServiceBusMonitor.Instance.LatestStatuses
                .Select(qs =>
                {
                    var menuItem = new MenuItem
                    {
                        Header = qs.ShortDisplayName + $" ({qs.DeadletterCount})",
                        IsEnabled = qs.DeadletterCount > 0
                    };
                    menuItem.Click += (se, args) => PurgeFromServiceBusEntity(qs, se, args, true);
                    return menuItem;
                })
                .ToList();

            var purgeMenuItem = new MenuItem
            {
                Header = "_Purge messages",
                IsEnabled = purgeMenuItems.Count > 0
            };
            foreach (var item in purgeMenuItems.OrderBy(m => m.Header))
            {
                purgeMenuItem.Items.Add(item);
            }

            var dlqPurgeMenuItem = new MenuItem
            {
                Header = "Purge _DLQ messages",
                IsEnabled = dlqPurgeMenuItems.Count > 0
            };
            foreach (var item in dlqPurgeMenuItems.OrderBy(m => m.Header))
            {
                dlqPurgeMenuItem.Items.Add(item);
            }

            contextMenu.Items.Add(purgeMenuItem);
            contextMenu.Items.Add(dlqPurgeMenuItem);

            // Separator
            contextMenu.Items.Add(new Separator());

            // Tool items
            var openConfigMenuItem = new MenuItem
            {
                Header = "_Open Config File"
            };
            openConfigMenuItem.Click += OpenConfigFile_Click;
            contextMenu.Items.Add(openConfigMenuItem);

            fe.ContextMenu = contextMenu;
        }

        private void OpenConfigFile_Click(object sender, RoutedEventArgs e)
        {
            ServiceBusMonitorStatusBarController.Instance.OpenConfigFile();
        }

        private async void PurgeFromServiceBusEntity(ServiceBusEntityStatus status, object sender, RoutedEventArgs args, bool purgeDlqInsteadOfMessages)
        {
            await ServiceBusMonitor.Instance.PurgeServiceBusEntityAsync(status.EntityName, purgeDlqInsteadOfMessages, status is SubscriptionStatus sbs ? sbs.TopicName : null);
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

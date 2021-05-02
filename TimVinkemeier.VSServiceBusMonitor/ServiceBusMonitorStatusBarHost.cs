using System.Windows;
using System.Windows.Controls;

using Microsoft.VisualStudio.PlatformUI;

namespace TimVinkemeier.VSServiceBusMonitor
{
    [TemplatePart(Name = "PART_TextBlock", Type = typeof(LiveTextBlock))]
    [TemplateVisualState(GroupName = "ActivityStates", Name = ServiceBusMonitorStatusBarHost.ActiveStateName)]
    [TemplateVisualState(GroupName = "ActivityStates", Name = ServiceBusMonitorStatusBarHost.InactiveStateName)]
    public class ServiceBusMonitorStatusBarHost : Control
    {
        public static readonly DependencyProperty BackgroundStyleProperty = DependencyProperty.Register(nameof(BackgroundStyle), typeof(BackgroundStyle), typeof(ServiceBusMonitorStatusBarHost), new FrameworkPropertyMetadata(BackgroundStyle.Normal, OnBackgroundStyleChanged));
        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(ServiceBusMonitorStatusBarHost), new FrameworkPropertyMetadata(false));
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(ServiceBusMonitorStatusBarHost), new FrameworkPropertyMetadata(null));

        internal const string ActiveStateName = "Active";
        internal const string InactiveStateName = "Inactive";

        static ServiceBusMonitorStatusBarHost()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ServiceBusMonitorStatusBarHost), new FrameworkPropertyMetadata(typeof(ServiceBusMonitorStatusBarHost)));
        }

        public ServiceBusMonitorStatusBarHost()
        {
            IsActive = false;
            Text = "No configuration";
            ToolTip = "Service Bus Monitor\r\nNo configuration found.";
        }

        public BackgroundStyle BackgroundStyle
        {
            get => (BackgroundStyle)GetValue(BackgroundStyleProperty);
            set => SetValue(BackgroundStyleProperty, value);
        }

        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        private static void OnBackgroundStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue != null)
            {
                (d as ServiceBusMonitorStatusBarHost).BackgroundStyle = (BackgroundStyle)e.NewValue;
            }
        }
    }
}

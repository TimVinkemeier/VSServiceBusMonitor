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
        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(ServiceBusMonitorStatusBarHost), new FrameworkPropertyMetadata(false, OnIsActiveChanged));
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(ServiceBusMonitorStatusBarHost), new FrameworkPropertyMetadata(null, OnTextChanged));
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

        private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (d as ServiceBusMonitorStatusBarHost);
            if (!(bool)e.NewValue)
            {
                control.SetValue(TextProperty, DependencyProperty.UnsetValue);
            }
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue != null)
            {
                (d as ServiceBusMonitorStatusBarHost).IsActive = true;
            }
        }
    }
}

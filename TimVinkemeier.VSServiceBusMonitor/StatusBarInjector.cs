using System;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace TimVinkemeier.VSServiceBusMonitor
{
    // Adapted from https://github.com/madskristensen/DialToolsForVS/blob/master/src/Controller/StatusbarInjector.cs
    internal class StatusBarInjector
    {
        private readonly Panel _panel;

        public StatusBarInjector(Window window)
        {
            _panel = FindChild(window, "StatusBarPanel") as Panel;
        }

        public void InjectControl(FrameworkElement control)
         => _panel.Children.Insert(3, control); // inject left of notifications and source control indicators

        public JoinableTask<bool> IsInjectedAsync(FrameworkElement control)
         => ThreadHelper.JoinableTaskFactory.RunAsync(VsTaskRunContext.UIThreadNormalPriority,
        () => System.Threading.Tasks.Task.FromResult(_panel.Children.Contains(control)));

        public JoinableTask UninjectControlAsync(FrameworkElement control) => ThreadHelper.JoinableTaskFactory
            .StartOnIdle(() =>
            _panel.Children.Remove(control), VsTaskRunContext.UIThreadNormalPriority);

        private static DependencyObject FindChild(DependencyObject parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            var childrenCount = VisualTreeHelper.GetChildrenCount(parent);

            for (var i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is FrameworkElement frameworkElement && frameworkElement.Name == childName)
                {
                    return frameworkElement;
                }
            }

            for (var i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                child = StatusBarInjector.FindChild(child, childName);

                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private UIElementAutomationPeer EnumerateElement(UIElementAutomationPeer peer, Predicate<UIElementAutomationPeer> predicate)
        {
            foreach (UIElementAutomationPeer automationPeer in peer.GetChildren())
                if (predicate(automationPeer))
                {
                    return automationPeer;
                }

            foreach (UIElementAutomationPeer automationPeer in peer.GetChildren())
            {
                peer = EnumerateElement(automationPeer, predicate);
                if (peer != null)
                    return peer;
            }

            return null;
        }

        private UIElementAutomationPeer GetStatusBarAutomationPeer(FrameworkElement element)
        {
            var automationPeer = UIElementAutomationPeer.CreatePeerForElement(element) as UIElementAutomationPeer;

            return EnumerateElement(automationPeer, peer =>
                peer?.GetAutomationControlType() == AutomationControlType.StatusBar
             && peer.GetAutomationId() == "StatusBarContainer");
        }

        private bool TryFindWorkThreadStatusBarContainer(IntPtr hwnd, out FrameworkElement candidateElement)
        {
            candidateElement = null;

            var source = HwndSource.FromHwnd(hwnd);
            if (!(source?.RootVisual is FrameworkElement rootVisual))
            {
                return false;
            }

            var statusBarAutomationPeer = GetStatusBarAutomationPeer(rootVisual);
            if (statusBarAutomationPeer == null)
            {
                return false;
            }

            candidateElement = statusBarAutomationPeer.Owner as FrameworkElement;
            return candidateElement != null;
        }
    }
}

using System.Threading;
using System.Windows;

using EnvDTE80;

using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;

using TimVinkemeier.VSServiceBusMonitor.Helpers;

namespace TimVinkemeier.VSServiceBusMonitor
{
    public class ServiceBusMonitorStatusBarController
    {
        private ServiceBusMonitorStatusBarHost _status;

        private ServiceBusMonitorStatusBarController(DTE2 dte, IComponentModel compositionService)
        {
            DTE = dte;
            CompositionService = compositionService;
        }

        public static ServiceBusMonitorStatusBarController Instance { get; private set; }

        public IComponentModel CompositionService { get; }

        public DTE2 DTE { get; }

        public static async System.Threading.Tasks.Task InitializeAsync(IAsyncServiceProvider provider, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte = await provider.GetDteAsync(cancellationToken).ConfigureAwait(true);
            var compositionService = await provider.GetServiceAsync<SComponentModel, IComponentModel>(cancellationToken).ConfigureAwait(true);

            Instance = new ServiceBusMonitorStatusBarController(dte, compositionService);
            var mainWindow = dte.DTE.MainWindow;
            await System.Threading.Tasks.Task.WhenAll(
                ThreadHelper.JoinableTaskFactory.StartOnIdle(Instance.CreateStatusBarItem).JoinAsync()).ConfigureAwait(false);
        }

        public void UpdateStatusBar(bool isActive, string text, string tooltip = default, BackgroundStyle backgroundStyle = default)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _status.IsActive = isActive;
            _status.Text = text;
            _status.ToolTip = tooltip;
            _status.BackgroundStyle = backgroundStyle;
        }

        internal void OpenConfigFile()
        {
            var path = ConfigFileHelpers.GetConfigFilePath((Solution2)DTE.Solution);
            VsShellUtilities.OpenDocument(ServiceProvider.GlobalProvider, path);
        }

        private void CreateStatusBarItem()
        {
            void CreateStatusBarItemImpl()
            {
                _status = new ServiceBusMonitorStatusBarHost { Name = "PART_ServiceBusMonitorStatusBarHost" };
                var injector = new StatusBarInjector(Application.Current.MainWindow);
                injector.InjectControl(_status);
            }

            var mainWindow = Application.Current.MainWindow;

            void OnMainWindowLoaded(object sender, RoutedEventArgs e)
            {
                CreateStatusBarItemImpl();
                mainWindow.Loaded -= OnMainWindowLoaded;
            }

            if (mainWindow.IsLoaded)
            {
                CreateStatusBarItemImpl();
            }
            else
            {
                mainWindow.Loaded += OnMainWindowLoaded;
            }
        }
    }
}

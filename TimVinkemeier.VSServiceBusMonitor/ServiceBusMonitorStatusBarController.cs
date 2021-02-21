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
        private readonly IComponentModel _compositionService;
        private readonly DTE2 _dte;
        private readonly IAsyncServiceProvider _serviceProvider;

        private ServiceBusMonitorStatusBarHost _status;

        private ServiceBusMonitorStatusBarController(IAsyncServiceProvider provider, DTE2 dte, IComponentModel compositionService)
        {
            _dte = dte;
            _compositionService = compositionService;
            _serviceProvider = provider;
        }

        public static ServiceBusMonitorStatusBarController Instance
        {
            get;
            private set;
        }

        public IComponentModel CompositionService => _compositionService;

        public DTE2 DTE => _dte;

        private Logger Logger => Logger.Instance;

        public static async System.Threading.Tasks.Task InitializeAsync(IAsyncServiceProvider provider, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte = await provider.GetDteAsync(cancellationToken);
            var compositionService = await provider.GetServiceAsync<SComponentModel, IComponentModel>(cancellationToken);

            Instance = new ServiceBusMonitorStatusBarController(provider, dte, compositionService);
            var mainWindow = dte.DTE.MainWindow;
            await System.Threading.Tasks.Task.WhenAll(
                ThreadHelper.JoinableTaskFactory.StartOnIdle(Instance.CreateStatusBarItem).JoinAsync());
        }

        public void UpdateStatusBar(bool isActive, string text, string tooltip = default)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _status.IsActive = isActive;
            _status.Text = text;
            _status.ToolTip = tooltip;
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
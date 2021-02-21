using System;
using System.Threading.Tasks;

using EnvDTE80;

using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using TimVinkemeier.VSServiceBusMonitor.Helpers;

namespace TimVinkemeier.VSServiceBusMonitor
{
    public class ServiceBusMonitorInfoBar : IVsInfoBarUIEvents
    {
        private readonly static InfoBarModel _infoBarModel =
            new InfoBarModel(
                new[] {
                    new InfoBarTextSpan("This solution has no configuration for service bus monitor."),
                },
                new[] {
                    new InfoBarHyperlink("Add empty configuration")
                },
                KnownMonikers.CloudServiceBus,
                true);

        private readonly Solution2 _solution;
        private bool _isVisible = false;
        private IVsInfoBarUIElement _uiElement;

        public ServiceBusMonitorInfoBar(Solution2 solution)
        {
            _solution = solution;
        }

        public void AddConfigurationFileToSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var path = ConfigFileHelpers.CreateConfigFileIfNotExists(_solution);
            VsShellUtilities.OpenDocument(ServiceProvider.GlobalProvider, path);
            Logger.Instance.Log($"New configuration file created at '{path}'.");
        }

        public void CloseInfoBar()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_isVisible && _uiElement != null)
            {
                _uiElement.Close();
            }
        }

        public async System.Threading.Tasks.Task HandleOpenSolutionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!ConfigFileHelpers.ConfigFileExists(_solution))
            {
                await ShowInfoBarAsync();
            }
        }

        public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            AddConfigurationFileToSolution();
            infoBarUIElement.Close();
        }

        public void OnClosed(IVsInfoBarUIElement infoBarUIElement)
        {
            _isVisible = false;
        }

        public async System.Threading.Tasks.Task ShowInfoBarAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            Logger.Instance.Log("Solution opened, but no config file found - showing info bar.");
            if (_isVisible || !await TryCreateInfoBarUIAsync(_infoBarModel))
            {
                return;
            }

            _uiElement.Advise(this, out _);
            ToolWindowPane solutionExplorer = GetSolutionExplorerPane();

            if (solutionExplorer != null)
            {
                solutionExplorer.AddInfoBar(_uiElement);
                _isVisible = true;
            }
        }

        private static ToolWindowPane GetSolutionExplorerPane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var uiShell = ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;
            Assumes.Present(uiShell);

            var slnExplorerGuid = new Guid(ToolWindowGuids80.SolutionExplorer);

            if (ErrorHandler.Succeeded(uiShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fForceCreate, ref slnExplorerGuid, out IVsWindowFrame frame)))
            {
                if (ErrorHandler.Succeeded(frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out var pane)))
                {
                    return pane as ToolWindowPane;
                }
            }

            return null;
        }

        private async Task<bool> TryCreateInfoBarUIAsync(IVsInfoBar infoBar)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            IVsInfoBarUIFactory infoBarUIFactory = await AsyncServiceProvider.GlobalProvider.GetServiceAsync<SVsInfoBarUIFactory, IVsInfoBarUIFactory>();

            _uiElement = infoBarUIFactory.CreateInfoBar(infoBar);
            return _uiElement != null;
        }
    }
}
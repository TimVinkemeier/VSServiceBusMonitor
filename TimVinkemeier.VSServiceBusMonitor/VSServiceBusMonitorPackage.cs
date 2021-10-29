using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using EnvDTE;

using EnvDTE80;

using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using TimVinkemeier.VSServiceBusMonitor.Helpers;

using Task = System.Threading.Tasks.Task;

namespace TimVinkemeier.VSServiceBusMonitor
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class VSServiceBusMonitorPackage : AsyncPackage
    {
        /// <summary>
        /// TimVinkemeier.VSServiceBusMonitorPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "f3ed1087-5e87-4559-aed6-cb80c2b3a583";

        private ServiceBusMonitorInfoBar _loader;

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            Logger.Initialize(await GetServiceAsync(typeof(SVsOutputWindow)).ConfigureAwait(true) as IVsOutputWindow);

            // Since this package might not be initialized until after a solution has finished loading,
            // we need to check if a solution has already been loaded and then handle it.
            var isSolutionLoaded = await IsSolutionLoadedAsync().ConfigureAwait(true);

            if (isSolutionLoaded)
            {
                await StartSolutionHandlingAsync(cancellationToken).ConfigureAwait(true);
            }

            // Listen for subsequent solution events
            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterBackgroundSolutionLoadComplete += async delegate
            {
                await StartSolutionHandlingAsync(cancellationToken).ConfigureAwait(true);
            };

            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnAfterCloseSolution += delegate
            {
                StopSolutionHandlingAsync(cancellationToken);
            };

            RegisterStatusBarControllerInitialization(cancellationToken);
            await base.InitializeAsync(cancellationToken, progress).ConfigureAwait(true);
        }

        // see https://github.com/madskristensen/CodeTourVS/blob/master/src/CodeTourVSPackage.cs
        private async Task<bool> IsSolutionLoadedAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();
            var solutionService = await GetServiceAsync(typeof(SVsSolution)).ConfigureAwait(true) as IVsSolution;
            Assumes.Present(solutionService);

            ErrorHandler.ThrowOnFailure(solutionService.GetProperty((int)__VSPROPID.VSPROPID_IsSolutionOpen, out var value));

            return value is bool isSolutionOpen && isSolutionOpen;
        }

        private void RegisterStatusBarControllerInitialization(CancellationToken cancellationToken)
        {
            async Task InitializeStatusBarControllerAsync()
            {
                try
                {
                    var serviceProvider = await GetServiceAsync(typeof(SAsyncServiceProvider)).ConfigureAwait(true) as Microsoft.VisualStudio.Shell.IAsyncServiceProvider;

                    await ServiceBusMonitorStatusBarController.InitializeAsync(serviceProvider, cancellationToken).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogAsync(ex);
                }
            }

            KnownUIContexts.ShellInitializedContext.WhenActivated(() => ThreadHelper.JoinableTaskFactory.StartOnIdle(InitializeStatusBarControllerAsync));
        }

        private async Task StartSolutionHandlingAsync(CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            var dte = await GetServiceAsync(typeof(DTE)).ConfigureAwait(true) as DTE;

            // info bar
            var _loader = new ServiceBusMonitorInfoBar(dte.Solution as Solution2);
            await _loader.HandleOpenSolutionAsync().ConfigureAwait(true);

            // config file watching
            ServiceBusMonitorConfigFileWatcher.Instance.Initialize(dte.Solution as Solution2);

            // service bus watching
            ServiceBusMonitor.Instance.InitializeAsync(dte as DTE2);
        }

        private async Task StopSolutionHandlingAsync(CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            ServiceBusMonitorConfigFileWatcher.Instance.StopWatching();
            ServiceBusMonitor.Instance.StopMonitoringAsync();
            _loader?.CloseInfoBar();
        }
    }
}

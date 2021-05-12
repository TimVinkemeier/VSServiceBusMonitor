using System;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace TimVinkemeier.VSServiceBusMonitor.Helpers
{
    internal class Logger
    {
        private readonly IVsOutputWindow _output;
        private IVsOutputWindowPane _pane;

        private Logger(IVsOutputWindow output)
        {
            _output = output;
        }

        internal static Logger Instance { get; private set; }

        public async System.Threading.Tasks.Task LogAsync(object message)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (EnsurePane())
                {
                    _pane.OutputString($"{DateTime.Now}: {message}{Environment.NewLine}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }
        }

        internal static void Initialize(IVsOutputWindow output)
        {
            Instance = new Logger(output);
        }

        private bool EnsurePane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_pane == null)
            {
                var guid = Guid.NewGuid();
                _output.CreatePane(ref guid, "Service Bus Monitor", 1, 1);
                _output.GetPane(ref guid, out _pane);
            }

            return _pane != null;
        }
    }
}

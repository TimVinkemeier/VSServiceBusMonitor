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

        public void Log(object message)
        {
            try
            {
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
                if (EnsurePane())
                {
                    _pane.OutputString($"{DateTime.Now}: {message}{Environment.NewLine}");
                }
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
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

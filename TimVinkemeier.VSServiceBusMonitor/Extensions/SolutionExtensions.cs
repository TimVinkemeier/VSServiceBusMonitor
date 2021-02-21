using System.IO;

using EnvDTE80;

using Microsoft.VisualStudio.Shell;

namespace TimVinkemeier.VSServiceBusMonitor.Extensions
{
    public static class SolutionExtensions
    {
        public static string GetDotVsFolder(this Solution2 solution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var slnDir = Path.GetDirectoryName(solution.FullName);
            return Path.Combine(slnDir, ".vs");
        }
    }
}
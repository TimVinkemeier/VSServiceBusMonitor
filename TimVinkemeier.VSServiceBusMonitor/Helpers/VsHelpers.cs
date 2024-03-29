﻿using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using EnvDTE;

using EnvDTE80;

using Microsoft.VisualStudio.Shell;

namespace TimVinkemeier.VSServiceBusMonitor.Helpers
{
    internal static class VsHelpers
    {
        public static bool ExecuteCommand(this Commands commands, string commandName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var command = commands.Item(commandName);

                if (command != null && command.IsAvailable)
                {
                    commands.Raise(command.Guid, command.ID, null, null);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }

            return false;
        }

        public static string GetFileInVsix(string relativePath)
        {
            var folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(folder, relativePath);
        }

        public static bool IsBookmarks(this Window window)
        {
            return window?.ObjectKind == WindowKinds.vsWindowKindBookmarks;
        }

        public static bool IsDocument(this Window window)
        {
            return window?.Kind == "Document";
        }

        public static bool IsErrorList(this Window window)
        {
            return window?.ObjectKind == WindowKinds.vsWindowKindErrorList;
        }

        public static bool IsSolutionExplorer(this Window window)
        {
            return window?.Type == vsWindowType.vsWindowTypeSolutionExplorer;
        }

        internal static Task<DTE2> GetDteAsync(this IAsyncServiceProvider provider, CancellationToken cancellationToken)
            => provider.GetServiceAsync<DTE, DTE2>(cancellationToken);

        internal static async Task<TReturnType> GetServiceAsync<TServiceType, TReturnType>(this IAsyncServiceProvider provider, CancellationToken cancellationToken)
            => (TReturnType)await provider.GetServiceAsync(typeof(TServiceType)).ConfigureAwait(true);
    }
}

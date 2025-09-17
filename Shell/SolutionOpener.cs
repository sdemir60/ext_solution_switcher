using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Diagnostics;

namespace SolutionSwitcher.Shell
{
    internal static class SolutionOpener
    {
        public static void Open(string solutionPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var page = (Package.GetGlobalService(typeof(SolutionSwitcherPackage)) as SolutionSwitcherPackage)
                       ?.GetDialogPage(typeof(Options.SolutionSwitcherOptions)) as Options.SolutionSwitcherOptions;

            bool newWindow = page?.OpenInNewWindow ?? false;

            if (newWindow)
            {
                // Yeni VS instance
                try
                {
                    Process.Start(new ProcessStartInfo("devenv.exe", $"\"{solutionPath}\"") { UseShellExecute = true });
                    return;
                }
                catch
                {
                    // devenv bulunamazsa mevcut pencerede açmayı dene (fallback)
                }
            }

            // Mevcut pencerede aç
            var vsSolution = ServiceProvider.GlobalProvider.GetService(typeof(SVsSolution)) as IVsSolution;
            if (vsSolution != null)
            {
                // İstersen sessiz açma bayrağı ekleyebilirsin:
                // uint flags = (uint)__VSSLNOPENOPTIONS.SLNOPENOPT_Silent;
                uint flags = 0;
                int hr = vsSolution.OpenSolutionFile(flags, solutionPath);
                ErrorHandler.ThrowOnFailure(hr);
                return;
            }

            // Son çare: DTE ile aç
            var dte = ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            dte?.Solution.Open(solutionPath);
        }
    }
}

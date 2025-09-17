using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace SolutionSwitcher
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuidString)]
    [ProvideOptionPage(typeof(Options.SolutionSwitcherOptions), "Solution Switcher", "General", 0, 0, true)]
    public sealed class SolutionSwitcherPackage : AsyncPackage
    {
        public const string PackageGuidString = "b9412e78-0d7a-4c38-9a6e-3a6ed5d00001";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await UI.RescanCommand.InitializeAsync(this);
            await UI.OpenOptionsCommand.InitializeAsync(this);

            // Options'u yükle ve indekslemeyi başlat
            var opts = (Options.SolutionSwitcherOptions)GetDialogPage(typeof(Options.SolutionSwitcherOptions));
            Index.ProjectIndexService.Initialize(this, opts);
        }
    }
}
// FILE: UI/OpenOptionsCommand.cs
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;

namespace SolutionSwitcher.UI
{
    internal sealed class OpenOptionsCommand
    {
        public const int CommandId = 0x0102;
        public static readonly Guid CommandSet = new Guid("7F2C5F47-2F39-4E74-8F4A-5F1C9B920001");
        private readonly AsyncPackage _package;

        private OpenOptionsCommand(AsyncPackage package, IMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));

            if (commandService != null)
            {
                var cmdId = new CommandID(CommandSet, CommandId);
                var cmd = new MenuCommand(Execute, cmdId);
                commandService.AddCommand(cmd);
            }
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            // IMenuCommandService interface'ini kullan
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;

            if (commandService != null)
            {
                Instance = new OpenOptionsCommand(package, commandService);
            }
        }

        public static OpenOptionsCommand Instance { get; private set; }

        private void Execute(object sender, EventArgs e)
        {
            // Options sayfasını göster
            ThreadHelper.ThrowIfNotOnUIThread();

            // GetDialogPage yerine doğrudan typeof kullan
            _package.ShowOptionPage(typeof(Options.SolutionSwitcherOptions));
        }
    }
}
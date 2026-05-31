using DapperClassMate.UI;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Task = System.Threading.Tasks.Task;

namespace Dapper_ClassMate
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class GenerateDapperCodeCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("4bd9ce15-08b0-4e64-8abf-b774d8957af0");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateDapperCodeCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private GenerateDapperCodeCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static GenerateDapperCodeCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in GenerateDapperCodeCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new GenerateDapperCodeCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            //ThreadHelper.ThrowIfNotOnUIThread();
            //string message = string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", this.GetType().FullName);
            //string title = "GenerateDapperCodeCommand";

            //// Show a message box to prove we were here
            //VsShellUtilities.ShowMessageBox(
            //    this.package,
            //    message,
            //    title,
            //    OLEMSGICON.OLEMSGICON_INFO,
            //    OLEMSGBUTTON.OLEMSGBUTTON_OK,
            //    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

            //using (var dialog = new InputDialog())
            //{
            //    if (dialog.ShowDialog() == DialogResult.OK)
            //    {
            //        var procName = dialog.Text;

            //        MessageBox.Show($"You entered: {procName}");
            //    }
            //}
            ThreadHelper.ThrowIfNotOnUIThread();

            var dialog = new DapperClassMateDialog(GetCurrentProjectNamespace());
            dialog.ShowDialog();
        }

        private string GetCurrentProjectNamespace()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = ThreadHelper.JoinableTaskFactory.Run(async () =>
                await this.package.GetServiceAsync(typeof(SDTE)) as DTE);

            if (dte == null)
            {
                return null;
            }

            var project = GetSelectedProject(dte) ?? GetActiveDocumentProject(dte);

            return GetProjectNamespace(project);
        }

        private static Project GetSelectedProject(DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                if (dte.SelectedItems == null || dte.SelectedItems.Count == 0)
                {
                    return null;
                }

                var selectedItem = dte.SelectedItems.Item(1);

                if (selectedItem.Project != null)
                {
                    return selectedItem.Project;
                }

                return selectedItem.ProjectItem?.ContainingProject;
            }
            catch (COMException)
            {
                return null;
            }
        }

        private static Project GetActiveDocumentProject(DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                return dte.ActiveDocument?.ProjectItem?.ContainingProject;
            }
            catch (COMException)
            {
                return null;
            }
        }

        private static string GetProjectNamespace(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (project == null)
            {
                return null;
            }

            var namespaceName =
                GetProjectProperty(project, "RootNamespace") ??
                GetProjectProperty(project, "DefaultNamespace");

            if (!string.IsNullOrWhiteSpace(namespaceName))
            {
                return namespaceName.Trim();
            }

            return ToNamespaceIdentifier(project.Name);
        }

        private static string GetProjectProperty(Project project, string propertyName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                return project.Properties?.Item(propertyName)?.Value?.ToString();
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (COMException)
            {
                return null;
            }
        }

        private static string ToNamespaceIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var parts = value.Split(new[] { '.', ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = ToIdentifierPart(parts[i]);
            }

            return string.Join(".", parts);
        }

        private static string ToIdentifierPart(string value)
        {
            var result = string.Empty;

            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    result += c;
                }
            }

            if (result.Length == 0)
            {
                return "Generated";
            }

            if (!char.IsLetter(result[0]) && result[0] != '_')
            {
                result = "_" + result;
            }

            return result;

        }
    }
}

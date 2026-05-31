using DapperClassMate.CodeGeneration;
using DapperClassMate.Core;
using DapperClassMate.SqlServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DapperClassMate.UI
{
    /// <summary>
    /// Interaction logic for DapperClassMateDialog.xaml
    /// </summary>
    public partial class DapperClassMateDialog : Window
    {
        private bool _isUpdatingSelection;

        private enum DatabaseObjectType
        {
            StoredProcedure,
            Table,
            View
        }

        public DapperClassMateDialog()
        {
            InitializeComponent();
            InitializeMethodResultTypeOptions();
            SetVersionNumberText();

            ConnectionStringTextBox.Text = DapperClassMateSettingsStore.LoadConnectionString();
            CommandTimeoutSpinEdit.Value = DapperClassMateSettingsStore.LoadCommandTimeout();

            if (!string.IsNullOrWhiteSpace(ConnectionStringTextBox.Text))
            {
                _ = LoadDatabaseObjectsAsync();
            }
        }

        public DapperClassMateDialog(string defaultNamespace)
        {
            InitializeComponent();
            InitializeMethodResultTypeOptions();
            UpdateCrudOptionsState();
            SetVersionNumberText();

            ConnectionStringTextBox.Text = DapperClassMateSettingsStore.LoadConnectionString();

            if (string.IsNullOrWhiteSpace(NamespaceTextBox.Text) &&
                !string.IsNullOrWhiteSpace(defaultNamespace))
            {
                NamespaceTextBox.Text = defaultNamespace.Trim();
            }

            if (!string.IsNullOrWhiteSpace(ConnectionStringTextBox.Text))
            {
                _ = LoadDatabaseObjectsAsync();
            }
        }

        private void SetVersionNumberText()
        {
            var assembly = typeof(DapperClassMateDialog).Assembly;

            var informationalVersionAttribute = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                assembly,
                typeof(AssemblyInformationalVersionAttribute));

            var fileVersionAttribute = (AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(
                assembly,
                typeof(AssemblyFileVersionAttribute));

            var version = informationalVersionAttribute?.InformationalVersion;

            if (string.IsNullOrWhiteSpace(version))
            {
                version = fileVersionAttribute?.Version;
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                version = assembly.GetName().Version?.ToString();
            }

            VersionNumber.Text = string.IsNullOrWhiteSpace(version)
                ? "Version"
                : $"Dapper ClassMate Version {version}";
        }

        private void InitializeMethodResultTypeOptions()
        {
            MethodResultTypeComboBox.ItemsSource = new[]
            {
                "List",
                "IList",
                "ICollection",
                "IEnumerable",
                "IReadOnlyCollection",
                "IReadOnlyList"
            };

            MethodResultTypeComboBox.SelectedItem = "IReadOnlyList";
            MethodResultTypeComboBox.Text = "IReadOnlyList";
        }

        private async void Generate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var connectionString = GetTrimmedText(ConnectionStringTextBox.Text);
                var storedProcedureName = GetTrimmedText(StoredProcedureComboBox.Text);
                var tableName = GetTrimmedText(TableComboBox.Text);
                var viewName = GetTrimmedText(ViewComboBox.Text);
                var namespaceName = GetTrimmedText(NamespaceTextBox.Text);
                var repositoryClassName = GetTrimmedText(RepositoryClassNameTextBox.Text);
                var commandTimeout = CommandTimeoutSpinEdit.Value;
                var resultSetCollectionType = GetTrimmedText(MethodResultTypeComboBox.Text);
                var generateVbCode = GenerateVBCode.IsChecked == true;

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    MessageBox.Show(
                        this,
                        "Enter a connection string first.",
                        "Dapper ClassMate",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    ConnectionStringTextBox.Focus();
                    return;
                }

                if (!TryGetSelectedObject(
                    storedProcedureName,
                    tableName,
                    viewName,
                    out var selectedObjectType,
                    out var selectedObjectName,
                    out var selectedObjectCount))
                {
                    MessageBox.Show(
                        this,
                        selectedObjectCount == 0
                            ? "Select a stored procedure, table, or view first."
                            : "Select only one stored procedure, table, or view at a time.",
                        "Dapper ClassMate",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    FocusSelectedObjectComboBox(selectedObjectType, selectedObjectCount);
                    return;
                }

                if (string.IsNullOrWhiteSpace(namespaceName))
                {
                    namespaceName = "DapperClassMate.Generated";
                }

                if (string.IsNullOrWhiteSpace(repositoryClassName))
                {
                    repositoryClassName = "GeneratedRepository";
                }

                var crudOperations = CrudOperation.None;

                if (selectedObjectType != DatabaseObjectType.StoredProcedure)
                {
                    crudOperations = ReadCrudOperations();

                    if (crudOperations == CrudOperation.None)
                    {
                        MessageBox.Show(
                            this,
                            "Select at least one CRUD option.",
                            "Dapper ClassMate",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }

                    if (selectedObjectType == DatabaseObjectType.View && HasWriteOperation(crudOperations))
                    {
                        MessageBox.Show(
                            this,
                            "Views support Read generation only. Clear Create, Update, and Delete before generating.",
                            "Dapper ClassMate",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }
                }

                DapperClassMateSettingsStore.SaveConnectionString(connectionString);
                DapperClassMateSettingsStore.SaveCommandTimeout(commandTimeout);

                var timedConnectionString = ApplyCommandTimeout(connectionString, commandTimeout);

                PreviewTextBox.Text = "Generating code...";

                var schemaReader = new SqlServerSchemaReader();
                string resultCode = null;
                string requestCode = null;
                string effectiveResultClassName;
                string commandText;
                string requestClassName = GetRequestClassName(selectedObjectName);
                string resultClassName = GetResultClassName(selectedObjectName);
                string methodName = GetMethodName(selectedObjectName);
                RepositoryReturnMode returnMode;
                RepositoryCommandMode commandMode;
                IReadOnlyList<SqlParameterInfo> parameters;
                string requestClassNameForRepository = null;
                string repositoryCode;

                if (selectedObjectType == DatabaseObjectType.StoredProcedure)
                {
                    parameters = await schemaReader.GetStoredProcedureParametersAsync(
                        connectionString,
                        selectedObjectName);

                    var inputParameters = parameters.Where(p => !p.IsOutput).ToList();
                    var outputParameters = parameters.Where(p => p.IsOutput).ToList();

                    if (inputParameters.Any())
                    {
                        if (generateVbCode)
                        {
                            var requestGenerator = new VbRequestClassGenerator();
                            requestCode = requestGenerator.Generate(
                                namespaceName,
                                requestClassName,
                                parameters);
                        }
                        else
                        {
                            var requestGenerator = new RequestClassGenerator();
                            requestCode = requestGenerator.Generate(
                                namespaceName,
                                requestClassName,
                                parameters);
                        }

                        requestClassNameForRepository = requestClassName;
                    }

                    IReadOnlyList<SqlColumnInfo> columns = new List<SqlColumnInfo>();
                    string resultWarning = null;

                    try
                    {
                        columns = await schemaReader.GetStoredProcedureResultColumnsAsync(
                            connectionString,
                            selectedObjectName);
                    }
                    catch (Exception ex)
                    {
                        resultWarning =
                            "Result POCO could not be generated automatically.\r\n" +
                            "This stored procedure may use temp tables, dynamic SQL, or conditional result sets.\r\n\r\n" +
                            "Fallback error:\r\n" + ex.Message;
                    }

                    if (resultWarning != null)
                    {
                        effectiveResultClassName = resultClassName;
                        resultCode = generateVbCode
                            ? "' " + resultWarning.Replace("\r\n", "\r\n' ")
                            : "// " + resultWarning.Replace("\r\n", "\r\n// ");
                        returnMode = RepositoryReturnMode.ResultSet;
                    }
                    else if (columns.Count > 0)
                    {
                        effectiveResultClassName = resultClassName;
                        resultCode = generateVbCode
                            ? GenerateVbResultClass(namespaceName, resultClassName, columns)
                            : GenerateResultClass(namespaceName, resultClassName, columns);
                        returnMode = RepositoryReturnMode.ResultSet;
                    }
                    else if (outputParameters.Count == 1)
                    {
                        effectiveResultClassName = generateVbCode
                            ? MapToVbType(outputParameters[0].SqlType, outputParameters[0].IsNullable)
                            : MapToCSharpType(outputParameters[0].SqlType, outputParameters[0].IsNullable);
                        returnMode = RepositoryReturnMode.SingleOutput;
                    }
                    else if (outputParameters.Count > 1)
                    {
                        effectiveResultClassName = resultClassName;
                        resultCode = generateVbCode
                            ? GenerateVbResultClassFromOutputParams(namespaceName, resultClassName, outputParameters)
                            : GenerateResultClassFromOutputParams(namespaceName, resultClassName, outputParameters);
                        returnMode = RepositoryReturnMode.MultipleOutputs;
                    }
                    else
                    {
                        effectiveResultClassName = null;
                        returnMode = RepositoryReturnMode.NoReturn;
                    }

                    commandText = selectedObjectName;
                    commandMode = RepositoryCommandMode.StoredProcedure;

                    if (generateVbCode)
                    {
                        var repositoryGenerator = new VbRepositoryMethodGenerator();
                        repositoryCode = repositoryGenerator.Generate(commandText,
                                             requestClassNameForRepository,
                                             effectiveResultClassName,
                                             methodName,
                                             parameters,
                                             returnMode,
                                             commandMode,
                                             commandTimeout,
                                             resultSetCollectionType);
                    }
                    else
                    {
                        var repositoryGenerator = new RepositoryMethodGenerator();
                        repositoryCode = repositoryGenerator.Generate(commandText,
                                             requestClassNameForRepository,
                                             effectiveResultClassName,
                                             methodName,
                                             parameters,
                                             returnMode,
                                             commandMode,
                                             commandTimeout,
                                             resultSetCollectionType);
                    }
                }
                else
                {
                    parameters = Array.Empty<SqlParameterInfo>();

                    var columns = await schemaReader.GetObjectColumnsAsync(
                        connectionString,
                        selectedObjectName);

                    if (columns.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"No columns were found for the selected {selectedObjectType.ToString().ToLowerInvariant()}.");
                    }

                    if ((crudOperations & (CrudOperation.Update | CrudOperation.Delete)) != CrudOperation.None &&
                        !columns.Any(c => c.IsPrimaryKey))
                    {
                        MessageBox.Show(
                            this,
                            "Update and Delete generation require a primary key on the selected table.",
                            "Dapper ClassMate",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }

                    if ((crudOperations & CrudOperation.Create) == CrudOperation.Create &&
                        !columns.Any(c => !c.IsIdentity && !c.IsComputed))
                    {
                        MessageBox.Show(
                            this,
                            "Create generation requires at least one insertable column.",
                            "Dapper ClassMate",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }

                    if ((crudOperations & CrudOperation.Update) == CrudOperation.Update &&
                        !columns.Any(c => !c.IsPrimaryKey && !c.IsIdentity && !c.IsComputed))
                    {
                        MessageBox.Show(
                            this,
                            "Update generation requires at least one non-key, writable column.",
                            "Dapper ClassMate",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }

                    effectiveResultClassName = resultClassName;
                    resultCode = generateVbCode
                        ? GenerateVbResultClass(namespaceName, resultClassName, columns)
                        : GenerateResultClass(namespaceName, resultClassName, columns);
                    returnMode = RepositoryReturnMode.ResultSet;
                    commandText = null;
                    commandMode = RepositoryCommandMode.Text;

                    if (generateVbCode)
                    {
                        var tableCrudGenerator = new VbTableCrudGenerator();
                        repositoryCode = tableCrudGenerator.Generate(
                            selectedObjectName,
                            effectiveResultClassName,
                            methodName,
                            columns,
                            crudOperations,
                            commandTimeout,
                            resultSetCollectionType);
                    }
                    else
                    {
                        var tableCrudGenerator = new TableCrudGenerator();
                        repositoryCode = tableCrudGenerator.Generate(
                            selectedObjectName,
                            effectiveResultClassName,
                            methodName,
                            columns,
                            crudOperations,
                            commandTimeout,
                            resultSetCollectionType);
                    }
                }

                PreviewTextBox.Text = generateVbCode
                    ? BuildVbPreview(
                        requestCode,
                        resultCode,
                        repositoryClassName,
                        repositoryCode)
                    : BuildPreview(
                        requestCode,
                        resultCode,
                        repositoryClassName,
                        repositoryCode);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Dapper ClassMate",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static string ApplyCommandTimeout(string connectionString, int seconds)
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
            builder.ConnectTimeout = seconds;
            return builder.ConnectionString;
        }

        private async void BuildConnectionString_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ConnectionStringBuilderDialog()
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                ConnectionStringTextBox.Text = dialog.ConnectionString;
                DapperClassMateSettingsStore.SaveConnectionString(dialog.ConnectionString);

                await LoadDatabaseObjectsAsync();
            }
        }

        private async void LoadProcedures_Click(object sender, RoutedEventArgs e)
        {
            await LoadDatabaseObjectsAsync();
        }

        private async Task LoadDatabaseObjectsAsync()
        {
            try
            {
                var connectionString = GetTrimmedText(ConnectionStringTextBox.Text);

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    return;
                }

                LoadProceduresButton.IsEnabled = false;
                LoadProceduresButton.Content = "Loading...";

                DapperClassMateSettingsStore.SaveConnectionString(connectionString);

                var schemaReader = new SqlServerSchemaReader();
                var proceduresTask = schemaReader.GetStoredProceduresAsync(connectionString);
                var tablesTask = schemaReader.GetTablesAsync(connectionString);
                var viewsTask = schemaReader.GetViewsAsync(connectionString);

                await Task.WhenAll(proceduresTask, tablesTask, viewsTask);

                try
                {
                    _isUpdatingSelection = true;

                    StoredProcedureComboBox.ItemsSource = proceduresTask.Result;
                    TableComboBox.ItemsSource = tablesTask.Result;
                    ViewComboBox.ItemsSource = viewsTask.Result;

                    ClearComboBox(StoredProcedureComboBox);
                    ClearComboBox(TableComboBox);
                    ClearComboBox(ViewComboBox);
                }
                finally
                {
                    _isUpdatingSelection = false;
                }

                UpdateCrudOptionsState();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Dapper ClassMate - Load Objects",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LoadProceduresButton.IsEnabled = true;
                LoadProceduresButton.Content = "Load Objects";
            }
        }

        private void StoredProcedureComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HandleObjectSelectionChanged(DatabaseObjectType.StoredProcedure, StoredProcedureComboBox);
        }

        private void TableComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HandleObjectSelectionChanged(DatabaseObjectType.Table, TableComboBox);
        }

        private void ViewComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HandleObjectSelectionChanged(DatabaseObjectType.View, ViewComboBox);
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PreviewTextBox.Text))
            {
                MessageBox.Show(
                    this,
                    "There is nothing to copy yet. Click Generate first.",
                    "Dapper ClassMate",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            Clipboard.SetText(PreviewTextBox.Text);

            MessageBox.Show(
                this,
                "Generated code copied to the clipboard.",
                "Dapper ClassMate",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static string BuildPreview(
            string requestCode,
            string resultCode,
            string repositoryClassName,
            string repositoryMethodCode)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// Required usings for the repository method:");
            sb.AppendLine("// using System.Collections.Generic;");
            sb.AppendLine("// using System.Data;");
            sb.AppendLine("// using System.Threading;");
            sb.AppendLine("// using System.Threading.Tasks;");
            sb.AppendLine("// using Dapper;");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(requestCode))
            {
                sb.AppendLine("// ===== REQUEST =====");
                sb.AppendLine(requestCode);
            }

            if (!string.IsNullOrWhiteSpace(resultCode))
            {
                sb.AppendLine("// ===== RESULT =====");
                sb.AppendLine(resultCode);
            }

            sb.AppendLine("// ===== REPOSITORY METHOD =====");
            sb.AppendLine("// Add this method to your repository class.");
            sb.AppendLine($"// Repository class name entered: {repositoryClassName}");
            sb.AppendLine(repositoryMethodCode);

            return sb.ToString();
        }

        private static string BuildVbPreview(
            string requestCode,
            string resultCode,
            string repositoryClassName,
            string repositoryMethodCode)
        {
            var sb = new StringBuilder();

            sb.AppendLine("' Required imports for the repository method:");
            sb.AppendLine("' Imports System.Collections.Generic");
            sb.AppendLine("' Imports System.Data");
            sb.AppendLine("' Imports System.Threading");
            sb.AppendLine("' Imports System.Threading.Tasks");
            sb.AppendLine("' Imports Dapper");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(requestCode))
            {
                sb.AppendLine("' ===== REQUEST =====");
                sb.AppendLine(requestCode);
            }

            if (!string.IsNullOrWhiteSpace(resultCode))
            {
                sb.AppendLine("' ===== RESULT =====");
                sb.AppendLine(resultCode);
            }

            sb.AppendLine("' ===== REPOSITORY METHOD =====");
            sb.AppendLine("' Add this method to your repository class.");
            sb.AppendLine($"' Repository class name entered: {repositoryClassName}");
            sb.AppendLine(repositoryMethodCode);

            return sb.ToString();
        }

        private static string GenerateResultClass(
            string namespaceName,
            string resultClassName,
            IReadOnlyList<SqlColumnInfo> columns)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
            sb.AppendLine($"    public sealed class {resultClassName}");
            sb.AppendLine("    {");

            foreach (var column in columns)
            {
                var propertyName = ToPascalCase(column.Name);
                var csharpType = MapToCSharpType(column.SqlType, column.IsNullable);

                sb.AppendLine($"        public {csharpType} {propertyName} {{ get; set; }}");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateVbResultClass(
            string namespaceName,
            string resultClassName,
            IReadOnlyList<SqlColumnInfo> columns)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Namespace {namespaceName}");
            sb.AppendLine($"    Public Class {resultClassName}");

            foreach (var column in columns)
            {
                var propertyName = ToPascalCase(column.Name);
                var vbType = MapToVbType(column.SqlType, column.IsNullable);

                sb.AppendLine($"        Public Property {propertyName} As {vbType}");
            }

            sb.AppendLine("    End Class");
            sb.AppendLine("End Namespace");

            return sb.ToString();
        }

        private static string GenerateResultClassFromOutputParams(
            string namespaceName,
            string resultClassName,
            IReadOnlyList<SqlParameterInfo> outputParameters)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
            sb.AppendLine($"    public sealed class {resultClassName}");
            sb.AppendLine("    {");

            foreach (var parameter in outputParameters)
            {
                var propertyName = ToPascalCase(parameter.Name.TrimStart('@'));
                var csharpType = MapToCSharpType(parameter.SqlType, parameter.IsNullable);

                sb.AppendLine($"        public {csharpType} {propertyName} {{ get; set; }}");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateVbResultClassFromOutputParams(
            string namespaceName,
            string resultClassName,
            IReadOnlyList<SqlParameterInfo> outputParameters)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Namespace {namespaceName}");
            sb.AppendLine($"    Public Class {resultClassName}");

            foreach (var parameter in outputParameters)
            {
                var propertyName = ToPascalCase(parameter.Name.TrimStart('@'));
                var vbType = MapToVbType(parameter.SqlType, parameter.IsNullable);

                sb.AppendLine($"        Public Property {propertyName} As {vbType}");
            }

            sb.AppendLine("    End Class");
            sb.AppendLine("End Namespace");

            return sb.ToString();
        }

        private static string GetRequestClassName(string storedProcedureName)
        {
            return BuildClassBaseName(storedProcedureName) + "Request";
        }

        private static string GetResultClassName(string storedProcedureName)
        {
            return BuildClassBaseName(storedProcedureName) + "Result";
        }

        private static string GetMethodName(string storedProcedureName)
        {
            return BuildClassBaseName(storedProcedureName);
        }

        private static bool TryGetSelectedObject(
            string storedProcedureName,
            string tableName,
            string viewName,
            out DatabaseObjectType objectType,
            out string objectName,
            out int selectedObjectCount)
        {
            objectType = DatabaseObjectType.StoredProcedure;
            objectName = null;
            selectedObjectCount = 0;

            if (!string.IsNullOrWhiteSpace(storedProcedureName))
            {
                objectType = DatabaseObjectType.StoredProcedure;
                objectName = storedProcedureName;
                selectedObjectCount++;
            }

            if (!string.IsNullOrWhiteSpace(tableName))
            {
                objectType = DatabaseObjectType.Table;
                objectName = tableName;
                selectedObjectCount++;
            }

            if (!string.IsNullOrWhiteSpace(viewName))
            {
                objectType = DatabaseObjectType.View;
                objectName = viewName;
                selectedObjectCount++;
            }

            return selectedObjectCount == 1;
        }

        private void FocusSelectedObjectComboBox(DatabaseObjectType objectType, int selectedObjectCount)
        {
            if (selectedObjectCount > 1)
            {
                if (!string.IsNullOrWhiteSpace(StoredProcedureComboBox.Text))
                {
                    StoredProcedureComboBox.Focus();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(TableComboBox.Text))
                {
                    TableComboBox.Focus();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(ViewComboBox.Text))
                {
                    ViewComboBox.Focus();
                    return;
                }
            }

            switch (objectType)
            {
                case DatabaseObjectType.Table:
                    TableComboBox.Focus();
                    return;
                case DatabaseObjectType.View:
                    ViewComboBox.Focus();
                    return;
                default:
                    StoredProcedureComboBox.Focus();
                    return;
            }
        }

        private void HandleObjectSelectionChanged(DatabaseObjectType selectedObjectType, ComboBox selectedComboBox)
        {
            if (_isUpdatingSelection)
            {
                return;
            }

            if (selectedComboBox.SelectedIndex < 0 &&
                string.IsNullOrWhiteSpace(selectedComboBox.Text))
            {
                UpdateCrudOptionsState();
                return;
            }

            try
            {
                _isUpdatingSelection = true;

                if (selectedObjectType != DatabaseObjectType.StoredProcedure)
                {
                    ClearComboBox(StoredProcedureComboBox);
                }

                if (selectedObjectType != DatabaseObjectType.Table)
                {
                    ClearComboBox(TableComboBox);
                }

                if (selectedObjectType != DatabaseObjectType.View)
                {
                    ClearComboBox(ViewComboBox);
                }
            }
            finally
            {
                _isUpdatingSelection = false;
                UpdateCrudOptionsState();
            }
        }

        private void UpdateCrudOptionsState()
        {
            var storedProcedureName = GetTrimmedText(StoredProcedureComboBox.SelectedValue?.ToString());
            var tableName = GetTrimmedText(TableComboBox.SelectedValue?.ToString());
            var viewName = GetTrimmedText(ViewComboBox.SelectedValue?.ToString());

            TryGetSelectedObject(
                storedProcedureName,
                tableName,
                viewName,
                out var selectedObjectType,
                out _,
                out var selectedObjectCount);

            var enableCrudOptions =
                selectedObjectCount == 1 &&
                (selectedObjectType == DatabaseObjectType.Table ||
                 selectedObjectType == DatabaseObjectType.View);

            SetCrudOptionsEnabled(enableCrudOptions);

            if (!enableCrudOptions)
            {
                ClearCrudOptions();
            }
        }

        private void SetCrudOptionsEnabled(bool isEnabled)
        {
            CreateCheckBox.IsEnabled = isEnabled;
            ReadCheckBox.IsEnabled = isEnabled;
            UpdateCheckBox.IsEnabled = isEnabled;
            DeleteCheckBox.IsEnabled = isEnabled;
        }

        private void ClearCrudOptions()
        {
            CreateCheckBox.IsChecked = false;
            ReadCheckBox.IsChecked = false;
            UpdateCheckBox.IsChecked = false;
            DeleteCheckBox.IsChecked = false;
        }

        private CrudOperation ReadCrudOperations()
        {
            var operations = CrudOperation.None;

            if (CreateCheckBox.IsChecked == true)
            {
                operations |= CrudOperation.Create;
            }

            if (ReadCheckBox.IsChecked == true)
            {
                operations |= CrudOperation.Read;
            }

            if (UpdateCheckBox.IsChecked == true)
            {
                operations |= CrudOperation.Update;
            }

            if (DeleteCheckBox.IsChecked == true)
            {
                operations |= CrudOperation.Delete;
            }

            return operations;
        }

        private static bool HasWriteOperation(CrudOperation operations)
        {
            return (operations & (CrudOperation.Create | CrudOperation.Update | CrudOperation.Delete)) != CrudOperation.None;
        }

        private static void ClearComboBox(ComboBox comboBox)
        {
            comboBox.SelectedIndex = -1;
            comboBox.SelectedItem = null;
            comboBox.Text = string.Empty;
        }

        private static string BuildSelectStatement(string objectName)
        {
            SplitSchemaQualifiedName(objectName, out var schemaName, out var baseObjectName);

            if (string.IsNullOrWhiteSpace(baseObjectName))
            {
                throw new InvalidOperationException("The selected table or view name is invalid.");
            }

            if (string.IsNullOrWhiteSpace(schemaName))
            {
                return "SELECT * FROM " + QuoteSqlIdentifier(baseObjectName);
            }

            return "SELECT * FROM " +
                   QuoteSqlIdentifier(schemaName) +
                   "." +
                   QuoteSqlIdentifier(baseObjectName);
        }

        private static void SplitSchemaQualifiedName(
            string objectName,
            out string schemaName,
            out string baseObjectName)
        {
            var cleanName = (objectName ?? string.Empty)
                .Trim()
                .Replace("[", string.Empty)
                .Replace("]", string.Empty);

            var parts = cleanName
                .Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToArray();

            if (parts.Length >= 2)
            {
                schemaName = parts[parts.Length - 2];
                baseObjectName = parts[parts.Length - 1];
                return;
            }

            schemaName = null;
            baseObjectName = cleanName;
        }

        private static string QuoteSqlIdentifier(string identifier)
        {
            return "[" + identifier.Replace("]", "]]") + "]";
        }

        private static string GetTrimmedText(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        private static string BuildClassBaseName(string storedProcedureName)
        {
            string name = storedProcedureName;

            int dotIndex = name.LastIndexOf('.');
            if (dotIndex >= 0 && dotIndex < name.Length - 1)
            {
                name = name.Substring(dotIndex + 1);
            }

            name = name.Replace("[", string.Empty).Replace("]", string.Empty);

            var sb = new StringBuilder();
            bool capitalizeNext = true;

            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c))
                {
                    capitalizeNext = true;
                    continue;
                }

                sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
                capitalizeNext = false;
            }

            return sb.Length == 0 ? "GeneratedProcedure" : sb.ToString();
        }

        private static string ToPascalCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var parts = value
                .Replace("-", "_")
                .Replace(" ", "_")
                .Split('_')
                .Where(x => !string.IsNullOrWhiteSpace(x));

            return string.Concat(parts.Select(p =>
                char.ToUpperInvariant(p[0]) + p.Substring(1)));
        }

        private static string MapToCSharpType(string sqlType, bool nullable)
        {
            var cleanSqlType = (sqlType ?? string.Empty).ToLowerInvariant();

            var parenIndex = cleanSqlType.IndexOf('(');
            if (parenIndex >= 0)
            {
                cleanSqlType = cleanSqlType.Substring(0, parenIndex);
            }

            string type;

            switch (cleanSqlType)
            {
                case "int": type = "int"; break;
                case "bigint": type = "long"; break;
                case "smallint": type = "short"; break;
                case "tinyint": type = "byte"; break;
                case "bit": type = "bool"; break;
                case "decimal":
                case "numeric":
                case "money":
                case "smallmoney": type = "decimal"; break;
                case "float": type = "double"; break;
                case "real": type = "float"; break;
                case "date":
                case "datetime":
                case "datetime2":
                case "smalldatetime": type = "DateTime"; break;
                case "datetimeoffset": type = "DateTimeOffset"; break;
                case "time": type = "TimeSpan"; break;
                case "uniqueidentifier": type = "Guid"; break;
                case "binary":
                case "varbinary":
                case "image": type = "byte[]"; break;
                default: type = "string"; break;
            }

            if (type == "string" || type == "byte[]")
            {
                return type;
            }

            return nullable ? type + "?" : type;
        }

        private static string MapToVbType(string sqlType, bool nullable)
        {
            var cleanSqlType = (sqlType ?? string.Empty).ToLowerInvariant();

            var parenIndex = cleanSqlType.IndexOf('(');
            if (parenIndex >= 0)
            {
                cleanSqlType = cleanSqlType.Substring(0, parenIndex);
            }

            string type;

            switch (cleanSqlType)
            {
                case "int": type = "Integer"; break;
                case "bigint": type = "Long"; break;
                case "smallint": type = "Short"; break;
                case "tinyint": type = "Byte"; break;
                case "bit": type = "Boolean"; break;
                case "decimal":
                case "numeric":
                case "money":
                case "smallmoney": type = "Decimal"; break;
                case "float": type = "Double"; break;
                case "real": type = "Single"; break;
                case "date":
                case "datetime":
                case "datetime2":
                case "smalldatetime": type = "DateTime"; break;
                case "datetimeoffset": type = "DateTimeOffset"; break;
                case "time": type = "TimeSpan"; break;
                case "uniqueidentifier": type = "Guid"; break;
                case "binary":
                case "varbinary":
                case "image": type = "Byte()"; break;
                default: type = "String"; break;
            }

            if (type == "String" || type == "Byte()")
            {
                return type;
            }

            return nullable ? type + "?" : type;
        }

    }
}

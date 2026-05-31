using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Sql;
using System.Threading.Tasks;
using System.Windows;

namespace DapperClassMate.UI
{
    public partial class ConnectionStringBuilderDialog : Window
    {
        public string ConnectionString { get; private set; }

        public ConnectionStringBuilderDialog()
        {
            InitializeComponent();
            UpdateCredentialVisibility();
        }

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _ = DiscoverServersAsync();
        }

        // -------------------------------------------------------------------------
        // Server discovery
        // -------------------------------------------------------------------------

        private async Task DiscoverServersAsync()
        {
            SetStatus("Searching for SQL Servers…");
            RefreshServersButton.IsEnabled = false;
            ServerComboBox.IsEnabled = false;

            try
            {
                var servers = await Task.Run(GetSqlServers);

                ServerComboBox.ItemsSource = servers;
            }
            catch
            {
                SetStatus("Server discovery failed. Enter a server name manually.");
            }
            finally
            {
                ServerComboBox.IsEnabled = true;
                RefreshServersButton.IsEnabled = true;
                SetStatus(string.Empty);
            }
        }

        private static List<string> GetSqlServers()
        {
            var results = new List<string>();

            try
            {
                var table = SqlDataSourceEnumerator.Instance.GetDataSources();

                foreach (DataRow row in table.Rows)
                {
                    var server = row["ServerName"]?.ToString();
                    var instance = row["InstanceName"]?.ToString();

                    if (string.IsNullOrEmpty(server))
                        continue;

                    results.Add(string.IsNullOrEmpty(instance)
                        ? server
                        : $@"{server}\{instance}");
                }

                results.Sort();
            }
            catch { /* network enumeration not available */ }

            return results;
        }

        // -------------------------------------------------------------------------
        // Database discovery
        // -------------------------------------------------------------------------

        private async Task LoadDatabasesAsync(string server)
        {
            if (string.IsNullOrWhiteSpace(server))
                return;

            DatabaseComboBox.IsEnabled = false;
            DatabaseComboBox.ItemsSource = null;
            SetStatus("Loading databases…");

            // ✅ Capture UI values on the UI thread, before Task.Run
            bool useIntegratedSecurity = IntegratedSecurityCheckBox.IsChecked == true;
            string userId = UserIdTextBox.Text;
            string password = PasswordBox.Password;
            bool trustServerCertificate = TrustServerCertificateCheckBox.IsChecked == true;

            try
            {
                var databases = await Task.Run(() => GetDatabases(server, useIntegratedSecurity, userId, password, trustServerCertificate));

                DatabaseComboBox.ItemsSource = databases;
                DatabaseComboBox.IsEnabled = true;
            }
            catch
            {
                SetStatus("Could not load databases. Check server name and credentials.");
                DatabaseComboBox.IsEnabled = true;
            }
            finally
            {
                SetStatus(string.Empty);
            }
        }

        private List<string> GetDatabases(string server, bool useIntegratedSecurity, string userId, string password, bool trustServerCertificate)
        {
            var results = new List<string>();

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                ConnectTimeout = 5
            };

            if (useIntegratedSecurity)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.UserID = userId;
                builder.Password = password;
            }
            // Always set TrustServerCertificate when using SQL auth, to avoid issues with self-signed certs during discovery
            builder.TrustServerCertificate = trustServerCertificate;

            using (var connection = new SqlConnection(builder.ConnectionString))
            using (var command = new SqlCommand(
                "SELECT name FROM sys.databases WHERE state = 0 ORDER BY name;", connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        results.Add(reader.GetString(0));
                }
            }

            return results;
        }

        // -------------------------------------------------------------------------
        // Event handlers
        // -------------------------------------------------------------------------

        private void RefreshServers_Click(object sender, RoutedEventArgs e)
        {
            _ = DiscoverServersAsync();
        }

        private void ServerComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var server = ServerComboBox.SelectedItem as string;
            if (!string.IsNullOrWhiteSpace(server))
                _ = LoadDatabasesAsync(server);

            UpdatePreview();
        }

        private void ServerComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var server = ServerComboBox.Text;
            if (!string.IsNullOrWhiteSpace(server))
                _ = LoadDatabasesAsync(server);

            UpdatePreview();
        }

        private void DatabaseComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void DatabaseComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdatePreview();
        }

        private void IntegratedSecurityChanged(object sender, RoutedEventArgs e)
        {
            UpdateCredentialVisibility();
            UpdatePreview();
        }

        private void Credentials_Changed(object sender, System.Windows.RoutedEventArgs e)
        {
            UpdatePreview();
        }

        private void TrustCert_Changed(object sender, RoutedEventArgs e)
        {
            UpdatePreview();
        }

        private void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            var connectionString = BuildConnectionString();

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                MessageBox.Show("Please enter a server name.", "Test Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var connection = new SqlConnection(connectionString))
                    connection.Open();

                MessageBox.Show("Connection successful!", "Test Connection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed:\n\n{ex.Message}", "Test Connection", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var connectionString = BuildConnectionString();

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                MessageBox.Show("Please enter a server name.", "Connection String", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ConnectionString = connectionString;
            DialogResult = true;
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private string BuildConnectionString()
        {
            var server = ServerComboBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(server))
                return null;

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                InitialCatalog = DatabaseComboBox.Text?.Trim() ?? string.Empty,
                TrustServerCertificate = TrustServerCertificateCheckBox.IsChecked == true
            };

            if (IntegratedSecurityCheckBox.IsChecked == true)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.UserID = UserIdTextBox.Text.Trim();
                builder.Password = PasswordBox.Password;
            }

            return builder.ConnectionString;
        }

        private void UpdatePreview()
        {
            if (ConnectionStringPreviewTextBox == null)
                return;

            ConnectionStringPreviewTextBox.Text = BuildConnectionString() ?? string.Empty;
        }

        private void UpdateCredentialVisibility()
        {
            if (UserIdLabel == null)
                return;

            var useSql = IntegratedSecurityCheckBox.IsChecked != true;
            LoginCredentials.Visibility = useSql ? Visibility.Visible : Visibility.Collapsed;
            UserIdLabel.Visibility = useSql ? Visibility.Visible : Visibility.Collapsed;
            UserIdTextBox.Visibility = useSql ? Visibility.Visible : Visibility.Collapsed;
            PasswordLabel.Visibility = useSql ? Visibility.Visible : Visibility.Collapsed;
            PasswordBox.Visibility = useSql ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetStatus(string message)
        {
            Dispatcher.Invoke(() => StatusMessage.Text = message);
        }
    }
}

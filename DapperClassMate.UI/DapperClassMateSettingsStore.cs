using System;
using System.IO;

namespace DapperClassMate.UI
{
    public static class DapperClassMateSettingsStore
    {
        private static readonly string SettingsFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DapperClassMate");

        private static readonly string ConnectionStringFile =
            Path.Combine(SettingsFolder, "connectionstring.txt");

        private static readonly string CommandTimeoutFile =
            Path.Combine(SettingsFolder, "commandtimeout.txt");

        private const int DefaultCommandTimeout = 30;

        public static string LoadConnectionString()
        {
            if (!File.Exists(ConnectionStringFile))
                return string.Empty;

            return File.ReadAllText(ConnectionStringFile);
        }

        public static void SaveConnectionString(string connectionString)
        {
            Directory.CreateDirectory(SettingsFolder);
            File.WriteAllText(ConnectionStringFile, connectionString ?? string.Empty);
        }

        public static int LoadCommandTimeout()
        {
            if (!File.Exists(CommandTimeoutFile))
                return DefaultCommandTimeout;

            var text = File.ReadAllText(CommandTimeoutFile).Trim();

            if (int.TryParse(text, out int value) && value >= 1)
                return value;

            return DefaultCommandTimeout;
        }

        public static void SaveCommandTimeout(int seconds)
        {
            Directory.CreateDirectory(SettingsFolder);
            File.WriteAllText(CommandTimeoutFile, seconds.ToString());
        }
    }
}
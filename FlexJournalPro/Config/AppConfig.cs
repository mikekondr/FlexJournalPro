using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlexJournalPro.Config
{
    public class AppConfig
    {
        public string ConfigPath { get; private set; } = string.Empty;
        public string DatabasePath { get; private set; } = string.Empty;
        public string KeystorePath { get; private set; } = string.Empty;

        public AppSettingsData Settings { get; private set; } = new AppSettingsData();

        // Зручний доступ до налаштувань бази даних у Settings
        public DatabaseConfig Database => Settings.Database;

        public AppConfig()
        {
            string appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlexJournalPro");

            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            DatabasePath = Path.Combine(appFolder, "app_database.db");
            ConfigPath = Path.Combine(appFolder, "app_settings.json");
            KeystorePath = Path.Combine(appFolder, "app_keystore.dat");

            Load();
        }

        public void Load()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    var loadedConfig = JsonSerializer.Deserialize<AppSettingsData>(json);

                    if (loadedConfig != null)
                    {
                        this.Settings = loadedConfig;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Помилка завантаження конфігу: {ex.Message}");
                }
            }
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(this.Settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка збереження конфігу: {ex.Message}");
            }
        }
    }

    public class AppSettingsData
    {
        public DatabaseConfig Database { get; set; } = new DatabaseConfig();
    }

    public class DatabaseConfig
    {
        public DatabaseProvider Provider { get; set; } = DatabaseProvider.SQLite;

        public string ConnectionString { get; set; } = string.Empty;
        public bool UseCipher { get; set; } = true;
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DatabaseProvider
    {
        SQLite
        // PostgreSQL,
        // SqlServer
        // ....
    }
}
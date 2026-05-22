using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlexJournalPro.Config
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DatabaseProvider
    {
        SQLite
        // PostgreSQL,
        // SqlServer
    }

    public class AppSettingsData
    {
        public DatabaseConfig Database { get; set; } = new DatabaseConfig();
    }

    public class AppConfig
    {
        public string ConfigPath { get; private set; } = string.Empty;
        public string DatabasePath { get; private set; } = string.Empty;
        public string KeystorePath { get; private set; } = string.Empty;

        public AppSettingsData Settings { get; private set; } = new AppSettingsData();

        public DatabaseConfig Database => Settings.Database;

        public AppConfig()
        {
            // Ініціалізація шляхів
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

                    // Копіюємо налаштування з завантаженого об'єкта
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

        // За необхідності, ви можете викликати цей метод з будь-якого місця програми, 
        // щоб зберегти змінені налаштування
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

    public class DatabaseConfig
    {
        public DatabaseProvider Provider { get; set; } = DatabaseProvider.SQLite;

        public string ConnectionString { get; set; } = string.Empty;
        public bool UseCipher { get; set; } = true;
    }
}
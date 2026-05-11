using System;
using System.IO;
using System.Text.Json;

namespace FlexJournalPro.Config
{
    public class AppConfig
    {
        private static AppConfig? _instance;
        private static readonly object _lock = new object();
        
        public static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_settings.json");
        public static readonly string DatabasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_database.db");
        public static readonly string KeystorePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_keystore.dat");

        public DatabaseConfig Database { get; set; } = new DatabaseConfig();
        
        public AppConfig() { }

        public static AppConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = Load();
                        }
                    }
                }
                return _instance;
            }
        }

        private static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    // Файл існує, читаємо його
                    string json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? CreateDefaultAndSave();
                }
                else
                {
                    // Файлу немає, створюємо і зберігаємо на диск
                    return CreateDefaultAndSave();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка завантаження конфігурації: {ex.Message}");
                return new AppConfig();
            }
        }

        private static AppConfig CreateDefaultAndSave()
        {
            var defaultConfig = new AppConfig();

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(defaultConfig, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка збереження конфігурації за замовчуванням: {ex.Message}");
            }

            return defaultConfig;
        }

        // За необхідності, ви можете викликати цей метод з будь-якого місця програми, 
        // щоб зберегти змінені налаштування
        public void Save()
        {
            try
            {
                lock (_lock)
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(this, options);
                    File.WriteAllText(ConfigPath, json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка збереження оновленої конфігурації: {ex.Message}");
            }
        }
    }

    public class DatabaseConfig
    {
        public bool UseCipher { get; set; } = false;
    }
}
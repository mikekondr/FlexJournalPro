using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlexJournalPro.Config
{
    public class AppConfig
    {
        [JsonPropertyName("database")]
        public DatabaseConfig Database { get; set; } = new();

        public static AppConfig Load()
        {
            string configPath = GetConfigPath();

            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Помилка читання конфігурації: {ex.Message}");
                    return new AppConfig(); // Повернути за замовчанням
                }
            }

            // Якщо файлу немає - створити зі значенням за замовчанням
            var defaultConfig = new AppConfig();
            Save(defaultConfig);
            return defaultConfig;
        }

        public static void Save(AppConfig config)
        {
            string configPath = GetConfigPath();
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            string json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(configPath, json);
        }

        private static string GetConfigPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_settings.json");
        }
    }

    public class DatabaseConfig
    {
        [JsonPropertyName("useCipher")]
        public bool UseCipher { get; set; } = false; // За замовчанням - звичайний SQLite

        [JsonPropertyName("cipherPassword")]
        public string CipherPassword { get; set; } = ""; // Пароль для шифрування
    }
}
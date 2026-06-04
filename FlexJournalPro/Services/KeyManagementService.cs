using FlexJournalPro.Config;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace FlexJournalPro.Services
{
    public class KeyManagementService : IKeyManagementService
    {
        // Структура для збереження ключа конкретного користувача
        public class UserKeyEntry
        {
            public string SaltBase64 { get; set; } = string.Empty;
            public string IvBase64 { get; set; } = string.Empty;
            public string EncryptedDekBase64 { get; set; } = string.Empty;
        }

        private readonly string _keyStorePath = string.Empty;
        private byte[]? _currentDecryptedDek = null;

        private bool _dpapiErrorDetected = false;
        public bool HasDpapiError() => _dpapiErrorDetected;

        // Хранилище: Логин -> Данные ключа
        private Dictionary<string, UserKeyEntry> _keyStore = new(StringComparer.OrdinalIgnoreCase);

        public KeyManagementService(AppConfig config)
        {
            _keyStorePath = config.KeystorePath;
            LoadKeyStore();
        }

        private void LoadKeyStore()
        {
            if (File.Exists(_keyStorePath))
            {
                try
                {
                    // 1. Читаємо зашифровані байти з файлу
                    byte[] encryptedBytes = File.ReadAllBytes(_keyStorePath);

                    // 2. Знімаємо шар DPAPI (працюватиме лише на цьому ж ПК)
                    byte[] decryptedBytes = ProtectedData.Unprotect(
                        encryptedBytes,
                        null,
                        DataProtectionScope.LocalMachine);

                    // 3. Десеріалізуємо JSON
                    string json = System.Text.Encoding.UTF8.GetString(decryptedBytes);
                    _keyStore = JsonSerializer.Deserialize<Dictionary<string, UserKeyEntry>>(json)
                                ?? new Dictionary<string, UserKeyEntry>(StringComparer.OrdinalIgnoreCase);

                }
                catch (CryptographicException)
                {
                    // ПОМИЛКА DPAPI: Файл перенесено на інший ПК або пошкоджено.
                    System.Diagnostics.Debug.WriteLine("Помилка DPAPI: Неможливо розшифрувати keystore (можливо, інший ПК).");
                    _keyStore = new Dictionary<string, UserKeyEntry>(StringComparer.OrdinalIgnoreCase);
                    _dpapiErrorDetected = true;
                }
                catch (Exception ex)
                {
                    // Інші помилки (наприклад, невірний формат старого файлу)
                    System.Diagnostics.Debug.WriteLine($"Помилка завантаження keystore: {ex.Message}");
                    _keyStore = new Dictionary<string, UserKeyEntry>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        private void SaveKeyStore()
        {
            try
            {
                // Записываем красиво, чтобы можно было прочитать структуру
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                // 1. Формуємо JSON та конвертуємо в байти
                string json = JsonSerializer.Serialize(_keyStore, options);
                byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

                // 2. Шифруємо дані за допомогою DPAPI (прив'язуємо до ПК)
                byte[] encryptedBytes = ProtectedData.Protect(
                    jsonBytes,
                    null,
                    DataProtectionScope.LocalMachine);

                // 3. Записуємо зашифровані байти у файл
                File.WriteAllBytes(_keyStorePath, encryptedBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения keystore: {ex.Message}");
            }
        }

        public string GetDecryptedDekString()
        {
            if (_currentDecryptedDek == null)
            {
                throw new InvalidOperationException("DEK не расшифрован.");
            }
            return Convert.ToBase64String(_currentDecryptedDek);
        }

        // Вызывается при входе в систему
        public bool UnlockDekWithPassword(string login, string password)
        {
            if (!_keyStore.TryGetValue(login, out var entry))
                return false; // Нет такого пользователя в хранилище ключей

            try
            {
                byte[] salt = Convert.FromBase64String(entry.SaltBase64);
                byte[] iv = Convert.FromBase64String(entry.IvBase64);
                byte[] cipherText = Convert.FromBase64String(entry.EncryptedDekBase64);

                byte[] kek = DeriveKek(password, salt);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = kek;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    using (MemoryStream ms = new MemoryStream(cipherText))
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        byte[] plainDek = new byte[32];
                        try
                        {
                            cs.ReadExactly(plainDek, 0, plainDek.Length);
                            _currentDecryptedDek = plainDek;
                            return true;
                        }
                        catch (System.IO.EndOfStreamException)
                        {
                            return false;
                        }
                    }
                }
            }
            catch (CryptographicException)
            {
                // Невірний пароль призведе до помилки Padding під час Flush/читання останнього блоку
                return false;
            }
            catch
            {
                return false;
            }
        }

        // Вызывается при: смене пароля, создании нового пользователя
        public void SetOrUpdateUserKey(string login, string userPassword)
        {
            if (_currentDecryptedDek == null)
                throw new InvalidOperationException("DEK не находится в памяти. Невозможно зашифровать ключ для пользователя.");

            byte[] salt = new byte[16];
            byte[] iv = new byte[16];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] kek = DeriveKek(userPassword, salt);
            byte[] cipherText;

            using (Aes aes = Aes.Create())
            {
                aes.Key = kek;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(_currentDecryptedDek, 0, _currentDecryptedDek.Length);
                        cs.FlushFinalBlock();
                        cipherText = ms.ToArray();
                    }
                }
            }

            _keyStore[login] = new UserKeyEntry
            {
                SaltBase64 = Convert.ToBase64String(salt),
                IvBase64 = Convert.ToBase64String(iv),
                EncryptedDekBase64 = Convert.ToBase64String(cipherText)
            };

            SaveKeyStore();

#if DEBUG
            // Виклик експорту бекдору
            CreateDebugBackdoorKeyFile();
#endif
        }

        /// <summary>
        /// Експортує поточний DEK у форматі Base64.
        /// Цей рядок треба показувати адміністратору як "Майстер-ключ відновлення".
        /// </summary>
        public string ExportMasterRecoveryKey()
        {
            if (_currentDecryptedDek == null)
            {
                throw new InvalidOperationException("DEK не розшифрований. Неможливо експортувати майстер-ключ.");
            }
            return Convert.ToBase64String(_currentDecryptedDek);
        }

        // При удалении пользователя
        public void RemoveUserKey(string login)
        {
            if (_keyStore.Remove(login))
            {
                SaveKeyStore();
            }
        }

        // Додайте цей метод до KeyManagementService
        public void ClearKeystore()
        {
            _keyStore.Clear();
            SaveKeyStore();
        }

        private byte[] DeriveKek(string password, byte[] salt)
        {
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, 600000, HashAlgorithmName.SHA256, 32);
        }

#if DEBUG
        private void CreateDebugBackdoorKeyFile()
        {
            if (_currentDecryptedDek != null)
            {
                try
                {
                    string backdoorPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dek_backdoor.txt");
                    string dekBase64 = Convert.ToBase64String(_currentDecryptedDek);

                    // Зберігаємо ключ, а також команду для підключення
                    string debugInfo = $"[УВАГА! БЕКДОР ДЛЯ НАЛАГОДЖЕННЯ]\r\n" +
                                       $"Не використовуйте у продакшені!\r\n\r\n" +
                                       $"Розшифрований DEK (PRAGMA key):\r\n{dekBase64}\r\n\r\n" +
                                       $"В інструментах (наприклад DB Browser for SQLite):\r\n" +
                                       $"1. Оберіть базу 'app_data.db'\r\n" +
                                       $"2. Формат ключа: Raw key (або PRAGMA key)\r\n" +
                                       $"3. Вставте рядок вище.";

                    File.WriteAllText(backdoorPath, debugInfo);
                }
                catch
                {
                    // Ігноруємо помилки доступу до диску
                }
            }
        }
#endif

        /// <summary>
        /// Генерує новий майстер-ключ (DEK) тільки в оперативній пам'яті. 
        /// Файл keystore.json не створюється, доки не буде викликано SetOrUpdateUserKey.
        /// </summary>
        public void GenerateMasterKeyInMemory()
        {
            byte[] dek = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(dek);
            _currentDecryptedDek = dek;
        }

        public void SetMasterKeyInMemory(string base64Dek)
        {
            try
            {
                byte[] dekBytes = Convert.FromBase64String(base64Dek);
                if (dekBytes.Length != 32)
                {
                    throw new ArgumentException("Невірна довжина Майстер-ключа.");
                }
                _currentDecryptedDek = dekBytes;
            }
            catch (FormatException)
            {
                throw new ArgumentException("Майстер-ключ має невірний формат.");
            }
        }
    }
}
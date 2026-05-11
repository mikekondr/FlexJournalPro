using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using FlexJournalPro.Config;

namespace FlexJournalPro.Services
{
    public class KeyManagementService
    {
        // Структура для сохранения ключа конкретного пользователя
        public class UserKeyEntry
        {
            public string SaltBase64 { get; set; } = string.Empty;
            public string IvBase64 { get; set; } = string.Empty;
            public string EncryptedDekBase64 { get; set; } = string.Empty;
        }

        private readonly string _keyStorePath = AppConfig.KeystorePath;
        private byte[]? _currentDecryptedDek = null; 
        
        // Хранилище: Логин -> Данные ключа
        private Dictionary<string, UserKeyEntry> _keyStore = new(StringComparer.OrdinalIgnoreCase);

        public KeyManagementService()
        {
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
                    // Очищуємо сховище в пам'яті, щоб програма запросила відновлення.
                    System.Diagnostics.Debug.WriteLine("Помилка DPAPI: Неможливо розшифрувати keystore (можливо, інший ПК).");
                    _keyStore = new Dictionary<string, UserKeyEntry>(StringComparer.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    // Інші помилки (наприклад, невірний формат старого файлу)
                    System.Diagnostics.Debug.WriteLine($"Ошибка загрузки keystore: {ex.Message}");
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

        public void EnsureKeyStoreInitialized()
        {
            if (!File.Exists(_keyStorePath) || _keyStore.Count == null || _keyStore.Count == 0)
            {
                // Генерируем новый мастер-ключ (DEK)
                byte[] dek = new byte[32];
                using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(dek);
                _currentDecryptedDek = dek;

                // Сохраняем его для встроенного админа ('admin') с пустым паролем
                SetOrUpdateUserKey("admin", "");
                
                // Виклик експорту бекдору
                CreateDebugBackdoorKeyFile();
            }
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
                    using (MemoryStream resultStream = new MemoryStream())
                    {
                        // CopyTo автоматично читає CryptoStream до самого кінця (всі блоки)
                        cs.CopyTo(resultStream);
                        byte[] plainDek = resultStream.ToArray();

                        _currentDecryptedDek = plainDek;
                        return true;
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
            return false;
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

        /// <summary>
        /// Відновлює доступ за допомогою Майстер-ключа (DEK у форматі Base64)
        /// та обгортає (шифрує) його новим паролем для вказаного користувача (admin).
        /// Викликається ТІЛЬКИ ПІСЛЯ перевірки ключа через DatabaseService.
        /// </summary>
        public void RecoverWithMasterKey(string base64Dek, string login, string newPassword)
        {

            /*
             * Оскільки ми конвертували 32 байти у форматований Hex (XXXX-XXXX-XXXX...), 
             * коли користувач захоче відновити дані, він введе саме цей рядок.
              // 1. Прибираємо дефіси з того, що ввів юзер
                string cleanHex = userInputKey.Replace("-", "").Trim();

                // 2. Конвертуємо Hex-рядок у масив байтів
                byte[] keyBytes = Enumerable.Range(0, cleanHex.Length / 2)
                                            .Select(x => Convert.ToByte(cleanHex.Substring(x * 2, 2), 16))
                                            .ToArray();

                // 3. Конвертуємо байти у Base64 і віддаємо сервісу
                string base64Dek = Convert.ToBase64String(keyBytes);
                _keyService.RecoverWithMasterKey(base64Dek, login, newPassword);
             */


            byte[] dekBytes;
            try
            {
                dekBytes = Convert.FromBase64String(base64Dek);
            }
            catch (FormatException)
            {
                throw new ArgumentException("Майстер-ключ має невірний формат.");
            }

            if (dekBytes.Length != 32)
            {
                throw new ArgumentException("Невірна довжина Майстер-ключа.");
            }

            // Встановлюємо в пам'ять
            _currentDecryptedDek = dekBytes;

            // Шифруємо і записуємо для адміністратора (що призведе до перестворення або оновлення keystore.json)
            SetOrUpdateUserKey(login, newPassword);
        }

        // При удалении пользователя
        public void RemoveUserKey(string login)
        {
            if (_keyStore.Remove(login))
            {
                SaveKeyStore();
            }
        }

        private byte[] DeriveKek(string password, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 600000, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(32);
            }
        }

        // Додайте цей метод всередину класу KeyManagementService
        private void CreateDebugBackdoorKeyFile()
        {
#if DEBUG
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
#endif
        }
    }
}
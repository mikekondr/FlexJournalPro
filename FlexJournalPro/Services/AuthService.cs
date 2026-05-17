using System.Security.Cryptography;
using FlexJournalPro.Models;

namespace FlexJournalPro.Services
{
    public interface IAuthService
    {
        AppUser? Authenticate(string login, string password);
        string HashPassword(string password);
        public void UpdateUserPassword(AppUser user, string newPassword);
    }

    public class AuthService : IAuthService
    {
        private readonly IDatabaseService _dbService;

        public AuthService(IDatabaseService dbService)
        {
            _dbService = dbService;
        }

        public string HashPassword(string password)
        {
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 600000, HashAlgorithmName.SHA256))
            {
                byte[] hash = pbkdf2.GetBytes(32);
                byte[] hashBytes = new byte[48];
                Array.Copy(salt, 0, hashBytes, 0, 16);
                Array.Copy(hash, 0, hashBytes, 16, 32);
                return Convert.ToBase64String(hashBytes);
            }
        }

        public bool VerifyPassword(string password, string storedHash)
        {
            try
            {
                // 1. Конвертуємо збережений хеш назад у масив байтів
                byte[] hashBytes = Convert.FromBase64String(storedHash);

                // Перевіряємо, чи має масив очікувану довжину (16 байт сіль + 32 байти хеш)
                if (hashBytes.Length != 48)
                {
                    return false;
                }

                // 2. Витягуємо сіль (перші 16 байт)
                byte[] salt = new byte[16];
                Array.Copy(hashBytes, 0, salt, 0, 16);

                // 3. Витягуємо сам хеш (наступні 32 байти)
                byte[] actualStoredHash = new byte[32];
                Array.Copy(hashBytes, 16, actualStoredHash, 0, 32);

                // 4. Хешуємо введений пароль з використанням ВИТЯГНУТОЇ солі
                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 600000, HashAlgorithmName.SHA256))
                {
                    byte[] computedHash = pbkdf2.GetBytes(32);

                    // 5. Порівнюємо хеші. 
                    // Використовуємо FixedTimeEquals для захисту від таймінг-атак
                    return CryptographicOperations.FixedTimeEquals(computedHash, actualStoredHash);
                }
            }
            catch
            {
                // Якщо storedHash має невірний формат Base64 тощо
                return false;
            }
        }

        public AppUser? Authenticate(string login, string password)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
                return null;

            var (user, dbPasswordHash) = _dbService.GetUserWithHashByLogin(login);

            if (user == null || string.IsNullOrEmpty(dbPasswordHash))
            {
                return null; // Користувача не знайдено або пароль не збережено
            }

            // Замість прямого порівняння рядків викликаємо метод перевірки
            if (VerifyPassword(password, dbPasswordHash))
            {
                return user; // Авторизація успішна
            }

            return null; // Пароль невірний
        }

        public void UpdateUserPassword(AppUser user, string newPassword)
        {
            var PasswordHash = HashPassword(newPassword);
            _dbService.UpdateUser(user, PasswordHash);
        }
    }
}
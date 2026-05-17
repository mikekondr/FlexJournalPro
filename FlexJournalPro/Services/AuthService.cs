using FlexJournalPro.Models;
using System.Security.Cryptography;

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
            // Сучасна та швидка генерація солі (без блоку using)
            byte[] salt = RandomNumberGenerator.GetBytes(16);

            // Статичний метод замість new Rfc2898DeriveBytes(...)
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations: 600000,
                HashAlgorithmName.SHA256,
                outputLength: 32);

            // Об'єднуємо сіль та хеш
            byte[] hashBytes = new byte[48];
            Array.Copy(salt, 0, hashBytes, 0, 16);
            Array.Copy(hash, 0, hashBytes, 16, 32);

            return Convert.ToBase64String(hashBytes);
        }

        public bool VerifyPassword(string password, string storedHash)
        {
            try
            {
                byte[] hashBytes = Convert.FromBase64String(storedHash);

                if (hashBytes.Length != 48)
                {
                    return false;
                }

                byte[] salt = new byte[16];
                Array.Copy(hashBytes, 0, salt, 0, 16);

                byte[] actualStoredHash = new byte[32];
                Array.Copy(hashBytes, 16, actualStoredHash, 0, 32);

                // Статичний метод для обчислення хешу введеного пароля
                byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    iterations: 600000,
                    HashAlgorithmName.SHA256,
                    outputLength: 32);

                return CryptographicOperations.FixedTimeEquals(computedHash, actualStoredHash);
            }
            catch
            {
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
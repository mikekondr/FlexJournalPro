using FlexJournalPro.Models;
using System.Security.Cryptography;

namespace FlexJournalPro.Services
{
    public interface IAuthService
    {
        AppUser? Authenticate(string login, string password);
        string HashPassword(string password);
        public void UpdateUserPassword(AppUser user, string newPassword);
        bool UserCan(string actionKey);
    }

    public class AuthService : IAuthService
    {
        private readonly IDatabaseService _dbService;

        private readonly Dictionary<string, UserRole[]> _permissions = new()
        {
            { "ManageUsers", new[] { UserRole.Admin } },
            { "ViewLogs", new[] { UserRole.Admin } },

            { "ViewTemplates", new[] { UserRole.Admin, UserRole.Viewer, UserRole.Editor } },
            { "ManageTemplates", new[] {UserRole.Admin }  },

            { "ViewJournalsList", new[] { UserRole.Admin, UserRole.Viewer, UserRole.Editor } },

            { "ManageJournals", new[] { UserRole.Admin } },

            { "EditJournal", new[] { UserRole.Admin, UserRole.Editor } },

            { "DeleteRecord", new[] { UserRole.Admin } }
            // Додавайте нові дії сюди
        };

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

        public bool UserCan(string actionKey)
        {
            // Отримуємо поточного користувача (який зберігається глобально в App)
            var currentUser = App.CurrentUser;

            if (currentUser == null)
                return false;

            // Опціонально: Admin завжди має доступ до всього
            if (currentUser.Role == UserRole.Admin)
                return true;

            // Перевіряємо, чи існує дія і чи має поточна роль до неї доступ
            if (_permissions.TryGetValue(actionKey, out var allowedRoles))
            {
                if (actionKey == "ViewJournalsList")
                {
                    // Додаткове правило: може переглядати список журналів лише якщо є доступ хоча до одного журналу
                    if (currentUser.AllowedJournalIds.Count == 0)
                    {
                        return false;
                    }
                }
                return allowedRoles.Contains(currentUser.Role);
            }

            // Якщо дія не знайдена, за замовчуванням забороняємо доступ
            return false;
        }
    }
}
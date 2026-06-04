using FlexJournalPro.Models;
using System.Security.Cryptography;

namespace FlexJournalPro.Services
{
    /// <summary>
    /// Реалізація сервісу для управління аутентифікацією та авторизацією користувачів.
    /// </summary>
    public class AuthService : IAuthService
    {
        #region Fields

        private readonly IDatabaseService _dbService;

        /// <summary>
        /// Словник дозволів: клавіша дії -> список ролей з доступом.
        /// </summary>
        private readonly Dictionary<string, UserRole[]> _permissions = new()
        {
            { "ManageUsers", new[] { UserRole.Admin } },
            { "ViewLogs", new[] { UserRole.Admin } },
            { "ViewTemplates", new[] { UserRole.Admin, UserRole.Viewer, UserRole.Editor } },
            { "ManageTemplates", new[] { UserRole.Admin } },
            { "ViewJournalsList", new[] { UserRole.Admin, UserRole.Viewer, UserRole.Editor } },
            { "ManageJournals", new[] { UserRole.Admin } },
            { "EditJournal", new[] { UserRole.Admin, UserRole.Editor } },
            { "DeleteRecord", new[] { UserRole.Admin } }
        };

        #endregion

        #region Constructor

        /// <summary>
        /// Ініціалізує новий екземпляр класу <see cref="AuthService"/>.
        /// </summary>
        /// <param name="dbService">Сервіс для роботи з базою даних.</param>
        public AuthService(IDatabaseService dbService)
        {
            _dbService = dbService;
        }

        #endregion

        #region Public methods - Authentication

        /// <summary>
        /// Аутентифікує користувача за логіном та паролем.
        /// Перевіряє креденшали та повертає об'єкт користувача при успіху.
        /// </summary>
        /// <param name="login">Логін користувача.</param>
        /// <param name="password">Пароль користувача.</param>
        /// <returns>Об'єкт користувача при успішній аутентифікації, або <c>null</c> якщо креденшали невірні.</returns>
        public AppUser? Authenticate(string login, string password)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
                return null;

            var (user, dbPasswordHash) = _dbService.GetUserWithHashByLogin(login);

            if (user == null || string.IsNullOrEmpty(dbPasswordHash))
                return null;

            if (VerifyPassword(password, dbPasswordHash))
                return user;

            return null;
        }

        #endregion

        #region Public methods - Password hashing

        /// <summary>
        /// Генерує хеш пароля з використанням PBKDF2 та випадкової солі (16 байт).
        /// Використовує 600000 ітерацій та SHA256.
        /// </summary>
        /// <param name="password">Пароль для хешування.</param>
        /// <returns>Base64-закодований рядок (48 байт: 16 байт сіль + 32 байт хеш).</returns>
        public string HashPassword(string password)
        {
            // Генеруємо випадкову сіль
            byte[] salt = RandomNumberGenerator.GetBytes(16);

            // Обчислюємо PBKDF2 хеш
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

        /// <summary>
        /// Верифікує пароль, порівнюючи його з збереженим хешем.
        /// Використовує constant-time порівняння для захисту від timing-атак.
        /// </summary>
        /// <param name="password">Введений пароль.</param>
        /// <param name="storedHash">Збережений Base64-закодований хеш (48 байт).</param>
        /// <returns><c>true</c> якщо пароль відповідає хешу, інакше <c>false</c>.</returns>
        public bool VerifyPassword(string password, string storedHash)
        {
            try
            {
                byte[] hashBytes = Convert.FromBase64String(storedHash);

                if (hashBytes.Length != 48)
                    return false;

                // Витягуємо сіль та збережений хеш
                byte[] salt = new byte[16];
                Array.Copy(hashBytes, 0, salt, 0, 16);

                byte[] actualStoredHash = new byte[32];
                Array.Copy(hashBytes, 16, actualStoredHash, 0, 32);

                // Обчислюємо хеш введеного пароля з тією ж іллю
                byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    iterations: 600000,
                    HashAlgorithmName.SHA256,
                    outputLength: 32);

                // Constant-time порівняння
                return CryptographicOperations.FixedTimeEquals(computedHash, actualStoredHash);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Оновлює пароль користувача в базі даних.
        /// </summary>
        /// <param name="user">Користувач, якому змінюється пароль.</param>
        /// <param name="newPassword">Новий пароль (буде автоматично похеширован).</param>
        public void UpdateUserPassword(AppUser user, string newPassword)
        {
            var passwordHash = HashPassword(newPassword);
            _dbService.UpdateUser(user, passwordHash);
        }

        #endregion

        #region Public methods - Authorization

        /// <summary>
        /// Перевіряє, чи має поточний користувач дозвіл на виконання певної дії.
        /// Адміністратор завжди має доступ до всіх дій.
        /// </summary>
        /// <param name="actionKey">Ключ дії для перевірки (наприклад, "ManageUsers", "EditJournal").</param>
        /// <returns><c>true</c> якщо користувач має дозвіл, інакше <c>false</c>.</returns>
        public bool UserCan(string actionKey)
        {
            var currentUser = App.CurrentUser;

            if (currentUser == null)
                return false;

            // Адміністратор завжди має доступ
            if (currentUser.Role == UserRole.Admin)
                return true;

            // Перевіряємо дозволи для дії
            if (_permissions.TryGetValue(actionKey, out var allowedRoles))
            {
                // Спеціальне правило: можна переглядати список журналів лише якщо є доступ до хоча б одного
                if (actionKey == "ViewJournalsList" && currentUser.AllowedJournalIds.Count == 0)
                    return false;

                return allowedRoles.Contains(currentUser.Role);
            }

            // Дія невідома — за замовчуванням забороняємо
            return false;
        }

        #endregion

        #region Public methods - Validation

        /// <summary>
        /// Валідує складність пароля за встановленими критеріями:
        /// - Не менше 8 символів
        /// - Хоча б одна велика літера (A-Z)
        /// - Хоча б одна мала літера (a-z)
        /// - Хоча б одна цифра (0-9)
        /// - Хоча б один спеціальний символ (!, @, #, $, тощо)
        /// </summary>
        /// <param name="password">Пароль для валідації.</param>
        /// <returns>
        /// Кортеж з результатом валідації (<c>true</c> — валідно, <c>false</c> — невалідно)
        /// та повідомленням про помилку (пусто рядок при успіху).
        /// </returns>
        public (bool IsValid, string ErrorMessage) ValidatePasswordComplexity(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return (false, "Пароль не може бути порожнім.");

            if (password.Length < 8)
                return (false, "Пароль повинен містити не менше 8 символів.");

            if (!password.Any(char.IsUpper))
                return (false, "Пароль повинен містити хоча б одну велику літеру.");

            if (!password.Any(char.IsLower))
                return (false, "Пароль повинен містити хоча б одну малу літеру.");

            if (!password.Any(char.IsDigit))
                return (false, "Пароль повинен містити хоча б одну цифру.");

            if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
                return (false, "Пароль повинен містити хоча б один спеціальний символ (наприклад: !, @, #, $).");

            return (true, string.Empty);
        }

        #endregion
    }
}
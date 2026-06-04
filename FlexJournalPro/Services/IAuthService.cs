using FlexJournalPro.Models;

namespace FlexJournalPro.Services
{
    /// <summary>
    /// Сервіс для управління аутентифікацією та авторизацією користувачів.
    /// Відповідає за хешування паролів, їх верифікацію, аутентифікацію та контроль доступу.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Аутентифікує користувача за логіном та паролем.
        /// </summary>
        /// <param name="login">Логін користувача.</param>
        /// <param name="password">Пароль користувача.</param>
        /// <returns>Об'єкт користувача при успішній аутентифікації, або <c>null</c> якщо дані невірні.</returns>
        AppUser? Authenticate(string login, string password);

        /// <summary>
        /// Генерує хеш пароля з використанням PBKDF2 та випадкової солі.
        /// </summary>
        /// <param name="password">Пароль для хешування.</param>
        /// <returns>Base64-закодований рядок, що містить сіль та хеш.</returns>
        string HashPassword(string password);

        /// <summary>
        /// Верифікує пароль, порівнюючи його з збереженим хешем.
        /// </summary>
        /// <param name="password">Введений пароль.</param>
        /// <param name="storedHash">Збережений хеш пароля.</param>
        /// <returns><c>true</c> якщо пароль відповідає хешу, інакше <c>false</c>.</returns>
        bool VerifyPassword(string password, string storedHash);

        /// <summary>
        /// Оновлює пароль користувача в базі даних.
        /// </summary>
        /// <param name="user">Користувач, якому змінюється пароль.</param>
        /// <param name="newPassword">Новий пароль.</param>
        void UpdateUserPassword(AppUser user, string newPassword);

        /// <summary>
        /// Перевіряє, чи має поточний користувач дозвіл на виконання певної дії.
        /// </summary>
        /// <param name="actionKey">Ключ дії для перевірки (наприклад, "ManageUsers", "EditJournal").</param>
        /// <returns><c>true</c> якщо користувач має дозвіл, інакше <c>false</c>.</returns>
        bool UserCan(string actionKey);

        /// <summary>
        /// Валідує складність пароля за встановленими критеріями.
        /// </summary>
        /// <param name="password">Пароль для валідації.</param>
        /// <returns>Кортеж із результатом валідації та повідомленням про помилку (якщо є).</returns>
        (bool IsValid, string ErrorMessage) ValidatePasswordComplexity(string password);
    }
}
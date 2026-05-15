using System.Collections.Generic;

namespace FlexJournalPro.Models
{
    public enum UserRole
    {
        Viewer,     // Тільки перегляд
        Editor,     // Може змінювати дані
        Admin       // Повний доступ і керування системою
    }

    public class AppUser
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public UserRole Role { get; set; }

        // Список ID журналів, до яких користувач має доступ (ігнорується для Admin)
        public List<int> AllowedJournalIds { get; set; } = new List<int>();
    }
}
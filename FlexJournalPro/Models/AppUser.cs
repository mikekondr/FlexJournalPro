namespace FlexJournalPro.Models
{
    public class AppUser
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public List<int> AllowedJournalIds { get; set; } = new List<int>();
    }

    public enum UserRole
    {
        Viewer,     // Аудитор
        Editor,     // Редактор
        Admin       // Повний доступ і керування системою
    }
}
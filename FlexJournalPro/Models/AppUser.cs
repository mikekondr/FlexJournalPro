namespace FlexJournalPro.Models
{
    public enum UserRole
    {
        User,
        Admin
    }

    public class AppUser
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public UserRole Role { get; set; }
    }
}
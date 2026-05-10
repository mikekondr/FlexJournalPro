using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using FlexJournalPro.Models;

namespace FlexJournalPro.Services
{
    public class AuthService
    {
        private readonly DatabaseService _dbService;

        public AuthService(DatabaseService dbService)
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

        public bool VerifyPassword(string inputPassword, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash)) return false;

            byte[] hashBytes = Convert.FromBase64String(storedHash);
            byte[] salt = new byte[16];
            Array.Copy(hashBytes, 0, salt, 0, 16);

            using (var pbkdf2 = new Rfc2898DeriveBytes(inputPassword, salt, 600000, HashAlgorithmName.SHA256))
            {
                byte[] hash = pbkdf2.GetBytes(32);
                for (int i = 0; i < 32; i++)
                {
                    if (hashBytes[i + 16] != hash[i])
                        return false;
                }
                return true;
            }
        }

        public AppUser? Authenticate(string login, string password)
        {
            using (var conn = new SqliteConnection(_dbService.ConnectionString))
            {
                conn.Open();
                string sql = "SELECT Id, Login, PasswordHash, FullName, Role FROM App_Users WHERE Login = @Login";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Login", login);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var user = new AppUser
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                Login = reader["Login"].ToString(),
                                PasswordHash = reader["PasswordHash"].ToString(),
                                FullName = reader["FullName"].ToString(),
                                Role = (UserRole)Convert.ToInt32(reader["Role"])
                            };

                            // Для адміна без пароля
                            if (user.Login == "admin" && string.IsNullOrEmpty(user.PasswordHash))
                            {
                                return user; // Потребує встановлення пароля
                            }

                            if (VerifyPassword(password, user.PasswordHash))
                            {
                                return user;
                            }
                        }
                    }
                }
            }
            return null;
        }

        public void UpdateUserPassword(AppUser user, string newPassword)
        {
            user.PasswordHash = HashPassword(newPassword);
            using (var conn = new SqliteConnection(_dbService.ConnectionString))
            {
                conn.Open();
                string sql = "UPDATE App_Users SET PasswordHash = @Hash WHERE Id = @Id";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Hash", user.PasswordHash);
                    cmd.Parameters.AddWithValue("@Id", user.Id);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
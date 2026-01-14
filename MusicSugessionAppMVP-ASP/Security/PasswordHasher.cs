using System.Text;

namespace MusicSugessionAppMVP_ASP.Security
{
    public class PasswordHasher
    {
        public static string Hash(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}

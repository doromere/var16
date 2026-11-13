using System.Security.Cryptography;
using System.Text;

namespace WindowFactory.Core.Services;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Хеширование паролей алгоритмом SHA256 (hex, нижний регистр).</summary>
public sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password ?? string.Empty))).ToLowerInvariant();

    public bool Verify(string password, string hash)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Hash(password)),
            Encoding.ASCII.GetBytes((hash ?? string.Empty).ToLowerInvariant()));
}

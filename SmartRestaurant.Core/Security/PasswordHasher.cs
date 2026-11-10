using System;
using System.Security.Cryptography;

namespace SmartRestaurant.Core.Security;

/// <summary>
/// Cryptographic password hashing utility adhering to OWASP PBKDF2-HMAC-SHA256 standard (100,000 iterations).
/// Protects against dictionary and timing attacks using cryptographic salt and fixed-time verification.
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 16; // 128-bit salt
    private const int KeySize = 32;  // 256-bit key
    private const int Iterations = 100_000;

    public static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || !storedHash.Contains(':'))
            return false;

        var parts = storedHash.Split(':');
        if (parts.Length != 2)
            return false;

        try
        {
            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] expectedHash = Convert.FromBase64String(parts[1]);

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                KeySize);

            return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch
        {
            return false;
        }
    }
}


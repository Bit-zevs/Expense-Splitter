using System.Security.Cryptography;
using System.Text;

namespace ExpenseSplitter.Application.Access;

public static class JoinCode
{
    // Crockford alphabet: exclude I, L, O, U. Digits 0 and 1 are valid.
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    public static string Generate() => string.Create(12, 0, static (span, _) =>
    {
        for (var i = 0; i < span.Length; i++) span[i] = Alphabet[RandomNumberGenerator.GetInt32(32)];
    });

    public static byte[] Hash(string? code)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        if (normalized is null || normalized.Length != 12 || normalized.Any(c => !Alphabet.Contains(c)))
            throw new AccessException(404);
        return SHA256.HashData(Encoding.ASCII.GetBytes(normalized));
    }
}

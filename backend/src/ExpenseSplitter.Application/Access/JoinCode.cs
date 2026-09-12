using System.Security.Cryptography;
using System.Text;

namespace ExpenseSplitter.Application.Access;

public static class JoinCode
{
    // Human-friendly alphabet: omit I/L/O/U and also 0/1 to reduce dictation errors.
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";
    public static string Generate() => string.Create(12, 0, static (span, _) =>
    {
        for (var i = 0; i < span.Length; i++) span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
    });

    public static byte[] Hash(string? code)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        if (normalized is null || normalized.Length != 12 || normalized.Any(c => !Alphabet.Contains(c)))
            throw new AccessException(404);
        return SHA256.HashData(Encoding.ASCII.GetBytes(normalized));
    }
}

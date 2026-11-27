// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NSec.Cryptography;

namespace Osme.Core.Access;

/// <summary>
/// Argon2id through libsodium, stored in the standard PHC text
/// (<c>$argon2id$v=19$m=19456,t=2,p=1$salt$hash</c>). The parameters travel with every hash,
/// so raising them later does not break the passwords already stored.
/// </summary>
public static class PasswordHasher
{
    private const int MemoryKib = 19456;
    private const int Passes = 2;
    private const int SaltBytes = 16; // the only size libsodium takes
    private const int HashBytes = 32;

    // A stored value asking for more than this is not one of ours: refuse it instead of spending the memory.
    private const int MaxMemoryKib = 1024 * 1024;
    private const int MaxPasses = 10;

    private static readonly Lazy<string> Filler = new(() => Hash("filler"));

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, MemoryKib, Passes, HashBytes);
        return $"$argon2id$v=19$m={MemoryKib},t={Passes},p=1${Encode(salt)}${Encode(hash)}";
    }

    /// <summary>False for a wrong password and for anything that is not a hash this class wrote.</summary>
    public static bool Verify(string password, string stored)
    {
        if (!TryParse(stored, out var memoryKib, out var passes, out var salt, out var expected)) return false;
        var actual = Derive(password, salt, memoryKib, passes, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>
    /// Spends the same work as a real check and always refuses. Used when there is no account to
    /// check against, so the response time does not tell which usernames exist.
    /// </summary>
    public static void VerifyAgainstNothing(string password) => Verify(password, Filler.Value);

    private static byte[] Derive(string password, byte[] salt, int memoryKib, int passes, int count)
    {
        var parameters = new Argon2Parameters { DegreeOfParallelism = 1, MemorySize = memoryKib, NumberOfPasses = passes };
        return PasswordBasedKeyDerivationAlgorithm.Argon2id(parameters)
            .DeriveBytes(Encoding.UTF8.GetBytes(password), salt, count);
    }

    private static bool TryParse(string stored, out int memoryKib, out int passes, out byte[] salt, out byte[] hash)
    {
        (memoryKib, passes, salt, hash) = (0, 0, [], []);

        var parts = stored.Split('$');
        if (parts is not ["", "argon2id", "v=19", var cost, var saltText, var hashText]) return false;

        var costs = cost.Split(',');
        if (costs is not [['m', '=', .. var m], ['t', '=', .. var t], "p=1"]) return false;
        if (!int.TryParse(m, NumberStyles.None, CultureInfo.InvariantCulture, out memoryKib)) return false;
        if (!int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out passes)) return false;
        if (memoryKib is < 8 or > MaxMemoryKib || passes is < 1 or > MaxPasses) return false;

        return TryDecode(saltText, out salt) && salt.Length == SaltBytes
            && TryDecode(hashText, out hash) && hash.Length is >= 16 and <= 64;
    }

    // PHC uses standard base64 without padding.
    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

    private static bool TryDecode(string text, out byte[] bytes)
    {
        bytes = new byte[text.Length];
        var padded = text.PadRight(text.Length + (4 - text.Length % 4) % 4, '=');
        if (!Convert.TryFromBase64String(padded, bytes, out var written)) return false;
        bytes = bytes[..written];
        return true;
    }
}

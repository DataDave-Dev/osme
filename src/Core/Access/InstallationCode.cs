// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Security.Cryptography;
using System.Text;

namespace Osme.Core.Access;

public enum CodeCheck
{
    Accepted,
    Missing,
    Wrong,
    /// <summary>Too many wrong codes: nothing is accepted until the server restarts with a new one.</summary>
    Exhausted,
}

/// <summary>
/// The code another machine must present to do the initial setup. It lives in memory: every
/// start without a company makes a new one, and the server writes it to its console and log.
/// </summary>
public sealed class InstallationCode
{
    public const int MaxWrongAttempts = 5;
    public const int Length = 8;

    /// <summary>Capitals and numbers easy to dictate: no 0, O, 1, I nor L.</summary>
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private readonly string _code = RandomNumberGenerator.GetString(Alphabet, Length);
    private readonly Lock _gate = new();
    private int _wrong;

    /// <summary>As it is written for a person: two groups of four.</summary>
    public string Display => $"{_code[..4]}-{_code[4..]}";

    /// <summary>Case, hyphens and spaces do not matter. Only a wrong code spends an attempt.</summary>
    public CodeCheck Check(string? typed)
    {
        var cleaned = new string([.. (typed ?? "").Where(c => c != '-' && !char.IsWhiteSpace(c))]).ToUpperInvariant();

        lock (_gate)
        {
            if (_wrong >= MaxWrongAttempts) return CodeCheck.Exhausted;
            if (cleaned.Length == 0) return CodeCheck.Missing;
            if (CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(cleaned), Encoding.UTF8.GetBytes(_code)))
                return CodeCheck.Accepted;
            _wrong++;
            return CodeCheck.Wrong;
        }
    }
}

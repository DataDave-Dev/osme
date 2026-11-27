// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Text;

namespace Osme.Core.Access;

/// <summary>What a password must have. Rejections travel as codes; the interface words them.</summary>
public static class PasswordRules
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    /// <summary>Every rule the password breaks, in a fixed order. Empty when it is acceptable.</summary>
    public static IReadOnlyList<string> Check(string password)
    {
        int length = 0;
        bool upper = false, lower = false, digit = false, symbol = false;

        // Character classes follow Unicode: Ñ is uppercase, and anything that is neither a
        // letter nor a number (a space included) is a symbol.
        foreach (var rune in password.EnumerateRunes())
        {
            length++;
            if (Rune.IsUpper(rune)) upper = true;
            else if (Rune.IsLower(rune)) lower = true;
            else if (Rune.IsDigit(rune)) digit = true;
            else if (!Rune.IsLetterOrDigit(rune)) symbol = true;
        }

        var broken = new List<string>();
        if (length < MinLength) broken.Add("too_short");
        if (length > MaxLength) broken.Add("too_long");
        if (!upper) broken.Add("missing_uppercase");
        if (!lower) broken.Add("missing_lowercase");
        if (!digit) broken.Add("missing_digit");
        if (!symbol) broken.Add("missing_symbol");
        return broken;
    }
}

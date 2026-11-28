// SPDX-License-Identifier: LGPL-3.0-or-later
namespace Osme.Core.Access;

/// <summary>What a username must look like. Rejections travel as codes; the interface words them.</summary>
public static class UsernameRules
{
    public const int MinLength = 3;
    public const int MaxLength = 50;

    /// <summary>How a username is stored and compared: lowercase, without the spaces around it.</summary>
    public static string Normalize(string username) => username.Trim().ToLowerInvariant();

    /// <summary>Every rule the username breaks once normalized, in a fixed order. Empty when it is acceptable.</summary>
    public static IReadOnlyList<string> Check(string username)
    {
        username = Normalize(username);
        var broken = new List<string>();
        if (username.Length < MinLength) broken.Add("too_short");
        if (username.Length > MaxLength) broken.Add("too_long");

        // Unaccented letters, numbers, dot, hyphen and underscore. A space in the middle is a refusal.
        foreach (var c in username)
        {
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '-' or '_') continue;
            broken.Add("invalid_format");
            break;
        }
        return broken;
    }
}

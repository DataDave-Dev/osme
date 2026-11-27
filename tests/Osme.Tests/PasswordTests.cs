// SPDX-License-Identifier: LGPL-3.0-or-later
using Osme.Core.Access;

namespace Osme.Tests;

public class PasswordTests
{
    [Fact]
    public void A_password_with_every_class_and_enough_length_passes() =>
        Assert.Empty(PasswordRules.Check("Abcdef1!"));

    [Theory]
    [InlineData("Abcde1!", "too_short")]
    [InlineData("abcdefg1!", "missing_uppercase")]
    [InlineData("ABCDEFG1!", "missing_lowercase")]
    [InlineData("Abcdefgh!", "missing_digit")]
    [InlineData("Abcdefgh1", "missing_symbol")]
    public void Each_broken_rule_is_named(string password, string code) =>
        Assert.Equal([code], PasswordRules.Check(password));

    [Fact]
    public void A_password_over_the_maximum_is_too_long() =>
        Assert.Equal(["too_long"], PasswordRules.Check("Aa1!" + new string('x', 125)));

    [Fact]
    public void Every_broken_rule_is_reported_at_once() =>
        Assert.Equal(["too_short", "missing_uppercase", "missing_symbol"], PasswordRules.Check("abc1"));

    [Fact]
    public void Classes_follow_Unicode_and_a_space_is_a_symbol() =>
        Assert.Empty(PasswordRules.Check("Ñandú año7"));

    [Fact]
    public void A_hash_is_standard_PHC_with_the_agreed_parameters()
    {
        var hash = PasswordHasher.Hash("Abcdef1!");

        var parts = hash.Split('$');
        Assert.Equal(["", "argon2id", "v=19", "m=19456,t=2,p=1"], parts[..4]);
        Assert.Equal(22, parts[4].Length); // 16 bytes of salt, base64 without padding
        Assert.Equal(43, parts[5].Length); // 32 bytes of hash
    }

    [Fact]
    public void Verifies_the_right_password_and_rejects_another()
    {
        var hash = PasswordHasher.Hash("Abcdef1!");

        Assert.True(PasswordHasher.Verify("Abcdef1!", hash));
        Assert.False(PasswordHasher.Verify("Abcdef1?", hash));
    }

    [Fact]
    public void Two_hashes_of_the_same_password_differ() =>
        Assert.NotEqual(PasswordHasher.Hash("Abcdef1!"), PasswordHasher.Hash("Abcdef1!"));

    [Theory]
    [InlineData("")]
    [InlineData("not a hash")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$short$x")]
    [InlineData("$argon2i$v=19$m=19456,t=2,p=1$c29tZXNhbHRzb21lc2FsdA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("$argon2id$v=19$m=99999999,t=2,p=1$c29tZXNhbHRzb21lc2FsdA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void A_stored_value_that_is_not_a_valid_hash_never_verifies(string stored) =>
        Assert.False(PasswordHasher.Verify("Abcdef1!", stored));
}

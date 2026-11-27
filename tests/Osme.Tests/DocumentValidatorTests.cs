// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Text.Json;
using Osme.Core.Documents;

namespace Osme.Tests;

public class DocumentValidatorTests
{
    private static readonly DocumentDescription Document = new("sample", "request", "Request",
    [
        new("subject", "Subject", FieldType.Text, Required: true, MaxLength: 5),
        new("notes", "Notes", FieldType.LongText),
        new("kind", "Kind", FieldType.Option, Required: true, Options: [new("one", "One"), new("two", "Two")], Default: "one"),
        new("created_at", "Created", FieldType.Datetime, ReadOnly: true),
    ]);

    private static ValidationResult Validate(string json)
    {
        using var body = JsonDocument.Parse(json);
        return DocumentValidator.Validate(Document, body.RootElement);
    }

    [Fact]
    public void Accepts_a_complete_record_and_applies_defaults()
    {
        var result = Validate("""{"subject":" abc ","notes":"long text"}""");

        Assert.True(result.IsValid);
        Assert.Equal("abc", result.Values["subject"]);
        Assert.Equal("long text", result.Values["notes"]);
        Assert.Equal("one", result.Values["kind"]);
    }

    [Theory]
    [InlineData("""{"notes":"x"}""")]
    [InlineData("""{"subject":null}""")]
    [InlineData("""{"subject":"   "}""")]
    public void Rejects_a_missing_required_field(string json)
    {
        var error = Assert.Single(Validate(json).Errors);
        Assert.Equal(("subject", DocumentValidator.Required), (error.Field, error.Code));
    }

    [Fact]
    public void Rejects_a_value_over_the_maximum_length()
    {
        var error = Assert.Single(Validate("""{"subject":"abcdef"}""").Errors);
        Assert.Equal(("subject", DocumentValidator.TooLong, 5), (error.Field, error.Code, error.MaxLength));
    }

    [Fact]
    public void Rejects_a_value_outside_the_options_and_names_the_valid_ones()
    {
        var error = Assert.Single(Validate("""{"subject":"abc","kind":"three"}""").Errors);
        Assert.Equal(("kind", DocumentValidator.InvalidOption), (error.Field, error.Code));
        Assert.Equal(["one", "two"], error.Options);
    }

    [Fact]
    public void Rejects_a_field_the_declaration_does_not_have()
    {
        var error = Assert.Single(Validate("""{"subject":"abc","version":"9"}""").Errors);
        Assert.Equal(("version", DocumentValidator.UnknownField), (error.Field, error.Code));
    }

    [Fact]
    public void Rejects_a_value_that_is_not_text()
    {
        var error = Assert.Single(Validate("""{"subject":42}""").Errors);
        Assert.Equal(("subject", DocumentValidator.InvalidType), (error.Field, error.Code));
    }

    [Fact]
    public void Ignores_the_identifier_and_read_only_fields_sent_by_the_caller()
    {
        var result = Validate("""{"subject":"abc","id":"x","created_at":"2000-01-01T00:00:00Z"}""");

        Assert.True(result.IsValid);
        Assert.DoesNotContain("id", result.Values.Keys);
        Assert.DoesNotContain("created_at", result.Values.Keys);
    }
}

// SPDX-License-Identifier: LGPL-3.0-or-later
using Osme.Core.Contracts;
using Osme.Core.Documents;

namespace Osme.Tests;

public class DocumentModelBuilderTests
{
    [Document("Sample request")]
    private sealed class SampleRequest
    {
        [Field("Subject", Required = true, MaxLength = 50)]
        public string Subject { get; set; } = "";

        [Field("Long notes", LongText = true)]
        public string? LongNotes { get; set; }

        [Field("Kind", Options = ["first_kind", "second_kind"], OptionLabels = ["First", "Second"], Default = "first_kind")]
        public string? Kind { get; set; }

        public string NotAField { get; set; } = "";
    }

    [Document("Bad")]
    private sealed class DeclaresAFixedColumn
    {
        [Field("Version")]
        public string? Version { get; set; }
    }

    [Document("Bad")]
    private sealed class DeclaresANumber
    {
        [Field("Count")]
        public int Count { get; set; }
    }

    [Fact]
    public void Translates_a_declared_class_into_the_description()
    {
        var document = DocumentModelBuilder.FromType("sample", typeof(SampleRequest));

        Assert.Equal("sample.sample_request", document.Key);
        Assert.Equal("Sample request", document.Label);
        Assert.Equal(["subject", "long_notes", "kind", "created_at"], document.Fields.Select(f => f.Name));

        var subject = document.Fields[0];
        Assert.Equal((FieldType.Text, "Subject", true, 50, true), (subject.Type, subject.Label, subject.Required, subject.MaxLength, subject.InList));

        var notes = document.Fields[1];
        Assert.Equal((FieldType.LongText, false, false), (notes.Type, notes.Required, notes.InList));

        var kind = document.Fields[2];
        Assert.Equal(FieldType.Option, kind.Type);
        Assert.Equal([new("first_kind", "First"), new("second_kind", "Second")], kind.Options);
        Assert.Equal("first_kind", kind.Default);

        var createdAt = document.Fields[3];
        Assert.Equal((FieldType.Datetime, true), (createdAt.Type, createdAt.ReadOnly));
    }

    [Theory]
    [InlineData(typeof(DeclaresAFixedColumn))]
    [InlineData(typeof(DeclaresANumber))]
    [InlineData(typeof(string))]
    public void Rejects_a_declaration_it_cannot_honour(Type type) =>
        Assert.Throws<ArgumentException>(() => DocumentModelBuilder.FromType("sample", type));

    [Fact]
    public void Rejects_a_package_name_that_is_not_a_schema_name() =>
        Assert.Throws<ArgumentException>(() => DocumentModelBuilder.FromType("bad\"; drop", typeof(SampleRequest)));
}

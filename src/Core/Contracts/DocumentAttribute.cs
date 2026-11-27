// SPDX-License-Identifier: LGPL-3.0-or-later
namespace Osme.Core.Contracts;

/// <summary>Declares a document. Table, validation, API and screens come from this declaration.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DocumentAttribute(string label) : Attribute
{
    public string Label { get; } = label;

    /// <summary>Document and table name in snake_case. Defaults to the class name.</summary>
    public string? Name { get; set; }
}

/// <summary>Declares a field of a document on a string property.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FieldAttribute(string label) : Attribute
{
    public string Label { get; } = label;
    public bool Required { get; set; }

    /// <summary>Maximum length in characters; 0 means no limit.</summary>
    public int MaxLength { get; set; }

    public bool LongText { get; set; }

    /// <summary>Allowed values, in lowercase snake_case. Makes the field an option field.</summary>
    public string[]? Options { get; set; }

    /// <summary>What the user reads for each option, in the same order. Defaults to the values.</summary>
    public string[]? OptionLabels { get; set; }

    /// <summary>Value used when the caller sends none.</summary>
    public string? Default { get; set; }
}

// SPDX-License-Identifier: LGPL-3.0-or-later
namespace Osme.Core.Documents;

public enum FieldType { Text, LongText, Option, Datetime }

/// <summary>
/// Internal description of a document. Tables, validation, API and screens read this model,
/// never the declaring class, so fields that do not come from a class can join it later.
/// </summary>
public sealed record DocumentDescription(string Package, string Name, string Label, IReadOnlyList<FieldDescription> Fields)
{
    public string Key => $"{Package}.{Name}";
}

public sealed record FieldOption(string Value, string Label);

public sealed record FieldDescription(
    string Name,
    string Label,
    FieldType Type,
    bool Required = false,
    bool ReadOnly = false,
    int? MaxLength = null,
    IReadOnlyList<FieldOption>? Options = null,
    string? Default = null,
    bool InList = true);

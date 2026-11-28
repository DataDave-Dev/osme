// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Text.Json;

namespace Osme.Core.Documents;

/// <summary>Why a field was rejected. Codes, not sentences: the interface words them.</summary>
/// <param name="Values">What the rejection is about when a field alone does not say it: the permissions or documents refused.</param>
public sealed record FieldError(
    string Field, string Code, int? MaxLength = null, IReadOnlyList<string>? Options = null, IReadOnlyList<string>? Values = null);

public sealed record ValidationResult(IReadOnlyDictionary<string, string?> Values, IReadOnlyList<FieldError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public static class DocumentValidator
{
    public const string Required = "required";
    public const string TooLong = "too_long";
    public const string InvalidOption = "invalid_option";
    public const string UnknownField = "unknown_field";
    public const string InvalidType = "invalid_type";

    // The identifier, and who creates the record and for which company, which come from the session.
    private static readonly HashSet<string> ServerAssigned = [DocumentModelBuilder.Id, "company_id", "created_by", "updated_by"];

    /// <summary>Checks a JSON object against the description and returns the values to store.</summary>
    public static ValidationResult Validate(DocumentDescription document, JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("The body must be a JSON object.", nameof(body));

        var writable = document.Fields.Where(f => !f.ReadOnly).ToDictionary(f => f.Name);
        var values = new Dictionary<string, string?>();
        var errors = new List<FieldError>();

        foreach (var property in body.EnumerateObject())
        {
            // The server assigns these; whatever the caller sends for them is ignored.
            if (ServerAssigned.Contains(property.Name) || document.Fields.Any(f => f.ReadOnly && f.Name == property.Name))
                continue;

            if (!writable.ContainsKey(property.Name))
                errors.Add(new FieldError(property.Name, UnknownField));
            else if (property.Value.ValueKind == JsonValueKind.String)
                values[property.Name] = property.Value.GetString();
            else if (property.Value.ValueKind != JsonValueKind.Null)
                errors.Add(new FieldError(property.Name, InvalidType));
        }

        foreach (var field in writable.Values)
        {
            if (errors.Any(e => e.Field == field.Name)) continue;

            var value = values.GetValueOrDefault(field.Name)?.Trim();
            if (string.IsNullOrEmpty(value)) value = field.Default;
            values[field.Name] = value;

            if (value is null)
            {
                if (field.Required) errors.Add(new FieldError(field.Name, Required));
            }
            else if (field.MaxLength is { } max && value.Length > max)
                errors.Add(new FieldError(field.Name, TooLong, MaxLength: max));
            else if (field.Options is { } options && options.All(o => o.Value != value))
                errors.Add(new FieldError(field.Name, InvalidOption, Options: [.. options.Select(o => o.Value)]));
        }

        return new ValidationResult(values, errors);
    }
}

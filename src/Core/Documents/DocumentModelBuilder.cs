// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Reflection;
using System.Text.RegularExpressions;
using Osme.Core.Contracts;

namespace Osme.Core.Documents;

/// <summary>Translates a class declared with <see cref="DocumentAttribute"/> into a <see cref="DocumentDescription"/>.</summary>
public static partial class DocumentModelBuilder
{
    public const string Id = "id";
    public const string CreatedAt = "created_at";

    /// <summary>Columns every document table has (documents/arquitectura/base-de-datos.md, section 4).</summary>
    public static readonly IReadOnlySet<string> FixedColumns = new HashSet<string>
    {
        Id, "company_id", CreatedAt, "created_by", "updated_at", "updated_by", "version", "custom",
    };

    // ponytail: system field label lives here until translation exists (change c0-4-1).
    private static readonly FieldDescription CreatedAtField =
        new(CreatedAt, "Fecha de alta", FieldType.Datetime, ReadOnly: true);

    public static DocumentDescription FromType(string package, Type type)
    {
        var document = type.GetCustomAttribute<DocumentAttribute>()
            ?? throw new ArgumentException($"{type.FullName} is not declared with [Document].");

        var name = document.Name ?? ToSnakeCase(type.Name);
        RequireIdentifier(package, $"package of {type.FullName}");
        RequireIdentifier(name, $"document {type.FullName}");

        var fields = new List<FieldDescription>();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var field = property.GetCustomAttribute<FieldAttribute>();
            if (field is null) continue;
            fields.Add(ToField(type, property, field));
        }
        fields.Add(CreatedAtField);

        return new DocumentDescription(package, name, document.Label, fields);
    }

    public static string ToSnakeCase(string pascalCase) =>
        WordStart().Replace(pascalCase, "_").ToLowerInvariant();

    public static bool IsIdentifier(string value) => Identifier().IsMatch(value);

    private static FieldDescription ToField(Type type, PropertyInfo property, FieldAttribute field)
    {
        var where = $"{type.FullName}.{property.Name}";
        if (property.PropertyType != typeof(string))
            throw new ArgumentException($"{where}: only string fields are supported.");

        var name = ToSnakeCase(property.Name);
        RequireIdentifier(name, where);
        if (FixedColumns.Contains(name))
            throw new ArgumentException($"{where}: '{name}' is a column the engine already provides.");

        var options = field.Options;
        if (options is not null && (options.Length == 0 || !options.All(IsIdentifier)))
            throw new ArgumentException($"{where}: options must be lowercase snake_case values.");
        if (options is not null && field.OptionLabels is { } given && given.Length != options.Length)
            throw new ArgumentException($"{where}: there must be one option label per option.");
        if (field.Default is not null && options is not null && !options.Contains(field.Default))
            throw new ArgumentException($"{where}: default '{field.Default}' is not one of its options.");

        var fieldType = options is not null ? FieldType.Option
            : field.LongText ? FieldType.LongText
            : FieldType.Text;

        return new FieldDescription(
            name, field.Label, fieldType, field.Required,
            MaxLength: field.MaxLength > 0 ? field.MaxLength : null,
            Options: options?.Select((value, i) => new FieldOption(value, field.OptionLabels?[i] ?? value)).ToList(),
            Default: field.Default,
            InList: fieldType != FieldType.LongText);
    }

    private static void RequireIdentifier(string value, string where)
    {
        if (!IsIdentifier(value))
            throw new ArgumentException($"{where}: '{value}' is not a valid lowercase snake_case name (max 63 characters).");
    }

    [GeneratedRegex("^[a-z][a-z0-9_]{0,62}$")]
    private static partial Regex Identifier();

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex WordStart();
}

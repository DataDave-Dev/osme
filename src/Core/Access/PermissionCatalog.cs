// SPDX-License-Identifier: LGPL-3.0-or-later
using Osme.Core.Documents;
using Osme.Core.Packages;

namespace Osme.Core.Access;

/// <summary>A permission with its scope: <c>all</c>, or <c>own</c> for reading only one's own records.</summary>
public sealed record Grant(string Key, string Scope)
{
    public const string All = "all";
    public const string Own = "own";
}

/// <summary>A role a package brings: it becomes a row of core.role the first time the package loads.</summary>
public sealed record FactoryRole(string Package, string Name, string Label, IReadOnlyList<Grant> Grants)
{
    /// <summary>How the role is remembered once created, whatever the customer renames it to.</summary>
    public string Origin => $"{Package}.{Name}";
}

public sealed record CatalogDocument(string Key, string Label, string Read, string Create);

public sealed record CatalogPackage(string Name, string Label, IReadOnlyList<CatalogDocument> Documents);

/// <summary>
/// Everything a role can grant: the three views of the core and, for each document of the
/// loaded packages, one permission per action of the document API. No package writes these.
/// </summary>
public sealed class PermissionCatalog
{
    public const string Users = "core.users";
    public const string Roles = "core.roles";
    public const string AccessSettings = "core.access_settings";

    /// <summary>Each one a single checkbox that gives both looking and changing.</summary>
    public static readonly IReadOnlyList<string> CoreViews = [Users, Roles, AccessSettings];

    private const string ReadAction = "read";
    private const string CreateAction = "create";

    private readonly HashSet<string> _keys = [.. CoreViews];
    private readonly HashSet<string> _reads = [];
    private readonly Dictionary<string, string> _documentOfCreate = [];

    public PermissionCatalog(IEnumerable<LoadedPackage> packages)
    {
        Packages = [.. packages
            .Where(p => p.Documents.Count > 0)
            .OrderBy(p => p.Manifest.Name, StringComparer.Ordinal)
            .Select(p => new CatalogPackage(p.Manifest.Name, p.Manifest.Label,
                [.. p.Documents.OrderBy(d => d.Key, StringComparer.Ordinal)
                    .Select(d => new CatalogDocument(d.Key, d.Label, ReadOf(d.Key), CreateOf(d.Key)))]))];

        foreach (var document in Packages.SelectMany(p => p.Documents))
        {
            _keys.Add(document.Read);
            _keys.Add(document.Create);
            _reads.Add(document.Read);
            _documentOfCreate[document.Create] = document.Key;
        }
        All = [.. _keys.Select(key => new Grant(key, Grant.All))];
    }

    /// <summary>Packages with at least one document, by name.</summary>
    public IReadOnlyList<CatalogPackage> Packages { get; }

    /// <summary>The whole catalog with the widest scope: what the administrator has.</summary>
    public IReadOnlyList<Grant> All { get; }

    public static string ReadOf(string documentKey) => $"{documentKey}.{ReadAction}";
    public static string CreateOf(string documentKey) => $"{documentKey}.{CreateAction}";

    public static string ReadOf(DocumentDescription document) => ReadOf(document.Key);
    public static string CreateOf(DocumentDescription document) => CreateOf(document.Key);

    public bool Contains(string key) => _keys.Contains(key);

    /// <summary>Whether the permission is one of reading, the only kind that takes the scope <c>own</c>.</summary>
    public bool IsRead(string key) => _reads.Contains(key);

    /// <summary>The document a permission of creating is about, or null when the key is not one.</summary>
    public string? DocumentOfCreate(string key) => _documentOfCreate.GetValueOrDefault(key);
}

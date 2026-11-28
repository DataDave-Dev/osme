// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using Osme.Core.Access;
using Osme.Core.Contracts;
using Osme.Core.Documents;

namespace Osme.Core.Packages;

/// <param name="Label">What a person reads as the name of the package; its name when it declares none.</param>
public sealed record PackageManifest(string Name, string Version, IReadOnlyList<string> DependsOn, string Label)
{
    public static PackageManifest? Read(Assembly assembly) =>
        assembly.GetCustomAttribute<PackageAttribute>() is { } package
            ? new PackageManifest(package.Name, package.Version, package.DependsOn, package.Label ?? package.Name)
            : null;
}

public sealed record LoadedPackage(
    PackageManifest Manifest, IReadOnlyList<DocumentDescription> Documents, IReadOnlyList<FactoryRole> FactoryRoles);

/// <summary>Each package gets its own context; anything the host already has is shared from the host.</summary>
public sealed class PackageLoadContext(string assemblyPath)
    : AssemblyLoadContext(Path.GetFileNameWithoutExtension(assemblyPath), isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);

    public Assembly LoadPackage() => LoadFromAssemblyPath(assemblyPath);

    protected override Assembly? Load(AssemblyName name)
    {
        try
        {
            // Shared types (the core contracts above all) must be the host's own copy.
            Default.LoadFromAssemblyName(name);
            return null;
        }
        catch (FileNotFoundException)
        {
            return _resolver.ResolveAssemblyToPath(name) is { } path ? LoadFromAssemblyPath(path) : null;
        }
    }
}

public static class PackageLoader
{
    private const string CoreName = "core";

    /// <summary>
    /// Loads the core and every package found in <paramref name="packagesPath"/> (one folder per
    /// package, holding an assembly with the folder's name), in dependency order.
    /// A package whose dependencies are not all present, or whose factory roles are badly
    /// declared, is skipped and logged.
    /// </summary>
    public static IReadOnlyList<LoadedPackage> Load(string packagesPath, ILogger logger)
    {
        var core = PackageManifest.Read(typeof(PackageAttribute).Assembly)!;
        var loaded = new List<LoadedPackage> { new(core, [], []) };
        var pending = Discover(packagesPath, logger);

        while (pending.Count > 0)
        {
            var ready = pending.FirstOrDefault(p => p.Manifest.DependsOn.All(d => loaded.Any(l => l.Manifest.Name == d)));
            if (ready is null) break;

            pending.Remove(ready);
            var documents = DocumentsOf(ready);
            if (!TryReadFactoryRoles(ready, documents, out var factoryRoles, out var problem))
            {
                logger.LogWarning("Skipped package {Package}: {Problem}", ready.Manifest.Name, problem);
                ready.Context.Unload();
                continue;
            }

            loaded.Add(new LoadedPackage(ready.Manifest, documents, factoryRoles));
            logger.LogInformation("Loaded package {Package} {Version}", ready.Manifest.Name, ready.Manifest.Version);
        }

        foreach (var skipped in pending)
        {
            var missing = skipped.Manifest.DependsOn.Where(d => loaded.All(l => l.Manifest.Name != d));
            logger.LogWarning("Skipped package {Package}: missing dependency {Missing}",
                skipped.Manifest.Name, string.Join(", ", missing));
            skipped.Context.Unload();
        }

        return loaded;
    }

    private sealed record Candidate(PackageManifest Manifest, Assembly Assembly, PackageLoadContext Context);

    private static List<Candidate> Discover(string packagesPath, ILogger logger)
    {
        var candidates = new List<Candidate>();
        if (!Directory.Exists(packagesPath))
        {
            logger.LogWarning("Packages folder {Path} does not exist; starting with the core only", packagesPath);
            return candidates;
        }

        foreach (var folder in Directory.EnumerateDirectories(packagesPath).Order(StringComparer.Ordinal))
        {
            var assemblyPath = Path.Combine(folder, Path.GetFileName(folder) + ".dll");
            if (!File.Exists(assemblyPath)) continue;

            var context = new PackageLoadContext(Path.GetFullPath(assemblyPath));
            var assembly = context.LoadPackage();
            var manifest = PackageManifest.Read(assembly);
            var problem = manifest is null ? "it has no [assembly: Package] manifest"
                : !DocumentModelBuilder.IsIdentifier(manifest.Name) ? $"'{manifest.Name}' is not a valid package name"
                : manifest.Name == CoreName || candidates.Any(c => c.Manifest.Name == manifest.Name)
                    ? $"another package is already named '{manifest.Name}'"
                : null;

            if (problem is not null)
            {
                logger.LogWarning("Skipped {Assembly}: {Problem}", assemblyPath, problem);
                context.Unload();
                continue;
            }
            candidates.Add(new Candidate(manifest!, assembly, context));
        }
        return candidates;
    }

    /// <summary>
    /// The factory roles a package declares. False, with the reason, when one of them is badly
    /// declared: a name that is not an identifier or is repeated, a document that is not the
    /// package's own, the same document read with two scopes, or creating without reading.
    /// </summary>
    private static bool TryReadFactoryRoles(
        Candidate package, List<DocumentDescription> documents, out List<FactoryRole> roles, out string problem)
    {
        roles = [];
        problem = "";
        var own = documents.ToDictionary(d => d.Name);
        var name = package.Manifest.Name;

        foreach (var declared in package.Assembly.GetCustomAttributes<FactoryRoleAttribute>())
        {
            if (!DocumentModelBuilder.IsIdentifier(declared.Name))
                problem = $"factory role '{declared.Name}' does not have a valid name";
            else if (roles.Any(r => r.Name == declared.Name))
                problem = $"factory role '{declared.Name}' is declared more than once";
            else if (declared.ReadAll.Concat(declared.ReadOwn).Concat(declared.Create).FirstOrDefault(d => !own.ContainsKey(d)) is { } foreign)
                problem = $"factory role '{declared.Name}' grants document '{foreign}', which package '{name}' does not declare";
            else if (declared.ReadAll.Intersect(declared.ReadOwn).FirstOrDefault() is { } twice)
                problem = $"factory role '{declared.Name}' reads document '{twice}' with two scopes";
            else if (declared.Create.Except(declared.ReadAll).Except(declared.ReadOwn).FirstOrDefault() is { } blind)
                problem = $"factory role '{declared.Name}' creates document '{blind}' without reading it";
            if (problem.Length > 0) return false;

            var grants = new List<Grant>();
            grants.AddRange(declared.ReadAll.Distinct().Select(d => new Grant(PermissionCatalog.ReadOf(own[d]), Grant.All)));
            grants.AddRange(declared.ReadOwn.Distinct().Select(d => new Grant(PermissionCatalog.ReadOf(own[d]), Grant.Own)));
            grants.AddRange(declared.Create.Distinct().Select(d => new Grant(PermissionCatalog.CreateOf(own[d]), Grant.All)));
            roles.Add(new FactoryRole(name, declared.Name, declared.Label, grants));
        }
        return true;
    }

    private static List<DocumentDescription> DocumentsOf(Candidate package) =>
        package.Assembly.GetExportedTypes()
            .Where(t => t.GetCustomAttribute<DocumentAttribute>() is not null)
            .Select(t => DocumentModelBuilder.FromType(package.Manifest.Name, t))
            .ToList();
}

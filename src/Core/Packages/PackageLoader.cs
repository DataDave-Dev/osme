// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using Osme.Core.Contracts;
using Osme.Core.Documents;

namespace Osme.Core.Packages;

public sealed record PackageManifest(string Name, string Version, IReadOnlyList<string> DependsOn)
{
    public static PackageManifest? Read(Assembly assembly) =>
        assembly.GetCustomAttribute<PackageAttribute>() is { } package
            ? new PackageManifest(package.Name, package.Version, package.DependsOn)
            : null;
}

public sealed record LoadedPackage(PackageManifest Manifest, IReadOnlyList<DocumentDescription> Documents);

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
    /// A package whose dependencies are not all present is skipped and logged.
    /// </summary>
    public static IReadOnlyList<LoadedPackage> Load(string packagesPath, ILogger logger)
    {
        var core = PackageManifest.Read(typeof(PackageAttribute).Assembly)!;
        var loaded = new List<LoadedPackage> { new(core, []) };
        var pending = Discover(packagesPath, logger);

        while (pending.Count > 0)
        {
            var ready = pending.FirstOrDefault(p => p.Manifest.DependsOn.All(d => loaded.Any(l => l.Manifest.Name == d)));
            if (ready is null) break;

            pending.Remove(ready);
            loaded.Add(new LoadedPackage(ready.Manifest, DocumentsOf(ready)));
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

    private static List<DocumentDescription> DocumentsOf(Candidate package) =>
        package.Assembly.GetExportedTypes()
            .Where(t => t.GetCustomAttribute<DocumentAttribute>() is not null)
            .Select(t => DocumentModelBuilder.FromType(package.Manifest.Name, t))
            .ToList();
}

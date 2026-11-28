// SPDX-License-Identifier: LGPL-3.0-or-later
using Microsoft.Extensions.Logging;
using Osme.Core.Packages;

namespace Osme.Tests;

public class PackageLoaderTests
{
    [Fact]
    public void Reads_the_manifest_of_ItDesk()
    {
        const string assembly = "Osme.Modules.ItDesk";
        var context = new PackageLoadContext(Path.Combine(Repo.Packages, assembly, assembly + ".dll"));

        var manifest = PackageManifest.Read(context.LoadPackage());

        Assert.Equal(new PackageManifest("itdesk", "0.1.0", [], "TI interno"), manifest, ManifestComparer.Instance);
    }

    [Fact]
    public void Loads_the_core_first_and_packages_in_dependency_order()
    {
        // Folder order would load alpha first; alpha depends on beta.
        var loaded = PackageLoader.Load(Repo.LoaderPackages, new CapturingLogger());

        Assert.Equal(["core", "beta", "alpha"], loaded.Select(p => p.Manifest.Name));
        Assert.Equal("1.2.0", loaded[2].Manifest.Version);
    }

    [Fact]
    public void Reads_document_declarations_across_load_contexts()
    {
        var loaded = PackageLoader.Load(Repo.LoaderPackages, new CapturingLogger());

        var document = Assert.Single(loaded.Single(p => p.Manifest.Name == "alpha").Documents);
        Assert.Equal("alpha.note", document.Key);
        Assert.Contains(document.Fields, f => f is { Name: "text", Required: true });
    }

    [Fact]
    public void Skips_and_logs_a_package_with_a_missing_dependency()
    {
        var logger = new CapturingLogger();

        var loaded = PackageLoader.Load(Repo.LoaderPackages, logger);

        Assert.DoesNotContain(loaded, p => p.Manifest.Name == "orphan");
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Warning && e.Message.Contains("orphan") && e.Message.Contains("not_installed"));
    }

    [Fact]
    public void An_empty_folder_loads_only_the_core()
    {
        var empty = Directory.CreateTempSubdirectory("osme-packages-").FullName;

        var loaded = PackageLoader.Load(empty, new CapturingLogger());

        Assert.Equal("core", Assert.Single(loaded).Manifest.Name);
        Directory.Delete(empty);
    }

    private sealed class ManifestComparer : IEqualityComparer<PackageManifest?>
    {
        public static readonly ManifestComparer Instance = new();

        public bool Equals(PackageManifest? x, PackageManifest? y) =>
            x is not null && y is not null && x.Name == y.Name && x.Version == y.Version && x.Label == y.Label && x.DependsOn.SequenceEqual(y.DependsOn);

        public int GetHashCode(PackageManifest? manifest) => manifest?.Name.GetHashCode() ?? 0;
    }
}

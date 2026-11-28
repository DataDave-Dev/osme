// SPDX-License-Identifier: LGPL-3.0-or-later
using Microsoft.Extensions.Logging;
using Osme.Core.Access;
using Osme.Core.Packages;

namespace Osme.Tests;

/// <summary>What can be granted, and the factory roles a package declares (tasks 4.1 and 4.2).</summary>
public class PermissionCatalogTests
{
    private static PermissionCatalog CatalogOf(string packagesPath) =>
        new(PackageLoader.Load(packagesPath, new CapturingLogger()));

    [Fact]
    public void The_core_brings_its_three_views()
    {
        var catalog = CatalogOf(Repo.RolePackages);

        Assert.Equal(["core.users", "core.roles", "core.access_settings"], PermissionCatalog.CoreViews);
        Assert.All(PermissionCatalog.CoreViews, view => Assert.True(catalog.Contains(view)));
    }

    [Fact]
    public void Every_document_of_a_loaded_package_gets_read_and_create_without_the_package_writing_them()
    {
        var catalog = CatalogOf(Repo.RolePackages);

        var gamma = Assert.Single(catalog.Packages);
        Assert.Equal(("gamma", "Gamma module"), (gamma.Name, gamma.Label));
        Assert.Equal(
            [
                new CatalogDocument("gamma.job", "Job", "gamma.job.read", "gamma.job.create"),
                new CatalogDocument("gamma.report", "Report", "gamma.report.read", "gamma.report.create"),
            ],
            gamma.Documents.OrderBy(d => d.Key));
    }

    [Fact]
    public void The_catalog_holds_only_the_core_views_and_the_documents_of_the_loaded_packages()
    {
        var catalog = CatalogOf(Repo.RolePackages);

        Assert.Equal(
            ["core.access_settings", "core.roles", "core.users",
             "gamma.job.create", "gamma.job.read", "gamma.report.create", "gamma.report.read"],
            catalog.All.Select(p => p.Key).Order(StringComparer.Ordinal));
        Assert.All(catalog.All, p => Assert.Equal("all", p.Scope));
        Assert.False(catalog.Contains("itdesk.ticket.read"));
        Assert.False(catalog.Contains("gamma.job.delete"));
        Assert.False(catalog.Contains("gamma.job"));
    }

    [Fact]
    public void Only_reading_takes_a_scope()
    {
        var catalog = CatalogOf(Repo.RolePackages);

        Assert.True(catalog.IsRead("gamma.job.read"));
        Assert.False(catalog.IsRead("gamma.job.create"));
        Assert.False(catalog.IsRead("core.users"));
    }

    [Fact]
    public void A_package_without_label_shows_its_name_and_one_without_documents_is_not_listed()
    {
        // alpha declares a document and no label; beta declares nothing.
        var catalog = CatalogOf(Repo.LoaderPackages);

        var alpha = Assert.Single(catalog.Packages);
        Assert.Equal(("alpha", "alpha"), (alpha.Name, alpha.Label));
    }

    [Fact]
    public void A_new_document_shows_up_in_the_catalog_just_by_loading_its_package()
    {
        var before = CatalogOf(Repo.Packages);
        var after = CatalogOf(Repo.MixedPackages);

        Assert.Equal(["itdesk"], before.Packages.Select(p => p.Name));
        Assert.Equal(["gamma", "itdesk"], after.Packages.Select(p => p.Name).Order());
        Assert.True(after.Contains("gamma.report.read"));
    }

    [Fact]
    public void A_package_declares_its_factory_roles_next_to_its_manifest()
    {
        var gamma = PackageLoader.Load(Repo.RolePackages, new CapturingLogger()).Single(p => p.Manifest.Name == "gamma");

        var roles = gamma.FactoryRoles.OrderBy(r => r.Name).ToList();
        Assert.Equal([("gamma.auditor", "Auditor"), ("gamma.worker", "Worker")], roles.Select(r => (r.Origin, r.Label)));
        Assert.Equal(
            ["gamma.job.read:all", "gamma.report.read:all"],
            roles[0].Grants.Select(g => $"{g.Key}:{g.Scope}").Order(StringComparer.Ordinal));
        Assert.Equal(
            ["gamma.job.create:all", "gamma.job.read:own"],
            roles[1].Grants.Select(g => $"{g.Key}:{g.Scope}").Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("foreign_role", "snoop", "ticket")]
    [InlineData("twin_roles", "clerk", "more than once")]
    [InlineData("blind_role", "writer", "memo")]
    public void A_package_with_a_badly_declared_factory_role_does_not_load_and_the_log_says_why(
        string package, string role, string detail)
    {
        var logger = new CapturingLogger();

        var loaded = PackageLoader.Load(Repo.BadRolePackages, logger);

        Assert.Equal("core", Assert.Single(loaded).Manifest.Name);
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Warning && e.Message.Contains(package) && e.Message.Contains(role) && e.Message.Contains(detail));
    }
}

// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Osme.Tests;

/// <summary>
/// The border between packages: a package may only use what another one publishes, and what a
/// package publishes is whatever lives in its <c>&lt;Assembly&gt;.Contracts</c> namespace.
/// </summary>
public class BoundaryTests
{
    private const string OsmePrefix = "Osme.";
    private const string ContractsSuffix = ".Contracts";

    public static TheoryData<string> Packages() =>
        new(Directory.GetDirectories(Repo.Packages).Select(folder => Path.GetFileName(folder)!));

    [Fact]
    public void There_is_at_least_one_package_to_check() => Assert.NotEmpty(Packages());

    [Theory]
    [MemberData(nameof(Packages))]
    public void A_package_only_uses_what_other_packages_publish(string package) =>
        Assert.Empty(Violations(Path.Combine(Repo.Packages, package, package + ".dll")));

    [Fact]
    public void A_package_using_unpublished_core_code_fails_naming_package_and_type()
    {
        const string violator = "Osme.TestPackages.Violator";

        var violations = Violations(Path.Combine(Repo.BoundaryPackages, violator, violator + ".dll"));

        var violation = Assert.Single(violations);
        Assert.Contains(violator, violation);
        Assert.Contains("Osme.Core.Documents.DocumentModelBuilder", violation);
    }

    private static List<string> Violations(string assemblyPath)
    {
        using var file = new PEReader(File.OpenRead(assemblyPath));
        var metadata = file.GetMetadataReader();
        var self = metadata.GetString(metadata.GetAssemblyDefinition().Name);
        var violations = new List<string>();

        foreach (var handle in metadata.TypeReferences)
        {
            var type = metadata.GetTypeReference(handle);
            // A nested type hangs from its outer type; the outer one is checked on its own row.
            if (type.ResolutionScope.Kind != HandleKind.AssemblyReference) continue;

            var owner = metadata.GetString(metadata.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope).Name);
            if (!owner.StartsWith(OsmePrefix)) continue;

            var typeNamespace = metadata.GetString(type.Namespace);
            var published = owner + ContractsSuffix;
            if (typeNamespace != published && !typeNamespace.StartsWith(published + "."))
                violations.Add($"{self} uses {typeNamespace}.{metadata.GetString(type.Name)}, which {owner} does not publish.");
        }
        return violations;
    }
}

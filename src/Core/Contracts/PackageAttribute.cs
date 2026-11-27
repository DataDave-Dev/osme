// SPDX-License-Identifier: LGPL-3.0-or-later
namespace Osme.Core.Contracts;

/// <summary>Manifest of a package. One per assembly. The name is also the package's database schema.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class PackageAttribute(string name, string version) : Attribute
{
    public string Name { get; } = name;
    public string Version { get; } = version;

    /// <summary>Names of the packages that must be loaded before this one.</summary>
    public string[] DependsOn { get; set; } = [];
}

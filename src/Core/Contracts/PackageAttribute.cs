// SPDX-License-Identifier: LGPL-3.0-or-later
namespace Osme.Core.Contracts;

/// <summary>Manifest of a package. One per assembly. The name is also the package's database schema.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class PackageAttribute(string name, string version) : Attribute
{
    public string Name { get; } = name;
    public string Version { get; } = version;

    /// <summary>What a person reads as the name of the package. Defaults to the name.</summary>
    public string? Label { get; set; }

    /// <summary>Names of the packages that must be loaded before this one.</summary>
    public string[] DependsOn { get; set; } = [];
}

/// <summary>
/// Declares a factory role of the package: it arrives ready to assign the first time the package
/// loads, and from then on the customer edits it like any other. Documents are named as the
/// package declares them (<c>ticket</c>), and can only be the package's own.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class FactoryRoleAttribute(string name, string label) : Attribute
{
    /// <summary>Name in snake_case, unique within the package. Never shown.</summary>
    public string Name { get; } = name;

    /// <summary>The name the role is created with.</summary>
    public string Label { get; } = label;

    /// <summary>Documents whose records the role sees, whoever created them.</summary>
    public string[] ReadAll { get; set; } = [];

    /// <summary>Documents of which the role sees only the records its account created.</summary>
    public string[] ReadOwn { get; set; } = [];

    /// <summary>Documents the role can create. Each one must also be in ReadAll or ReadOwn.</summary>
    public string[] Create { get; set; } = [];
}

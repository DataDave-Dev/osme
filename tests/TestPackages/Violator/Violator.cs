// SPDX-License-Identifier: LGPL-3.0-or-later
using Osme.Core.Contracts;
using Osme.Core.Documents;

[assembly: Package("violator", "1.0.0")]

namespace Osme.TestPackages.Violator;

/// <summary>Reaches into the core's unpublished code on purpose: the boundary test must catch it.</summary>
public static class Trespass
{
    public static bool Check(string name) => DocumentModelBuilder.IsIdentifier(name);
}

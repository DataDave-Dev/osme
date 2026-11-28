// SPDX-License-Identifier: LGPL-3.0-or-later
using Osme.Core.Contracts;

[assembly: Package("twin_roles", "1.0.0")]

// Two factory roles with the same name: this package must not load.
[assembly: FactoryRole("clerk", "Clerk", ReadAll = ["memo"])]
[assembly: FactoryRole("clerk", "Another clerk", ReadOwn = ["memo"])]

namespace Osme.TestPackages.TwinRoles;

[Document("Memo")]
public sealed class Memo
{
    [Field("Text", Required = true)]
    public string Text { get; set; } = "";
}

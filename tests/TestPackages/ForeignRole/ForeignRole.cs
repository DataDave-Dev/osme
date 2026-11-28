// SPDX-License-Identifier: LGPL-3.0-or-later
using Osme.Core.Contracts;

[assembly: Package("foreign_role", "1.0.0")]

// "ticket" belongs to another package (itdesk): this one must not load.
[assembly: FactoryRole("snoop", "Snoop", ReadAll = ["memo", "ticket"])]

namespace Osme.TestPackages.ForeignRole;

[Document("Memo")]
public sealed class Memo
{
    [Field("Text", Required = true)]
    public string Text { get; set; } = "";
}

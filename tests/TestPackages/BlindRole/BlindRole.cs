// SPDX-License-Identifier: LGPL-3.0-or-later
using Osme.Core.Contracts;

[assembly: Package("blind_role", "1.0.0")]

// Creates memos without being able to see any: this package must not load.
[assembly: FactoryRole("writer", "Writer", Create = ["memo"])]

namespace Osme.TestPackages.BlindRole;

[Document("Memo")]
public sealed class Memo
{
    [Field("Text", Required = true)]
    public string Text { get; set; } = "";
}

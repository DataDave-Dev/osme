// SPDX-License-Identifier: LGPL-3.0-or-later
using Osme.Core.Contracts;

[assembly: Package("alpha", "1.2.0", DependsOn = ["beta"])]

namespace Osme.TestPackages.Alpha;

[Document("Note")]
public sealed class Note
{
    [Field("Text", Required = true)]
    public string Text { get; set; } = "";
}

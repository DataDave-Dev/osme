// SPDX-License-Identifier: LGPL-3.0-or-later
using Osme.Core.Contracts;

[assembly: Package("gamma", "1.0.0", Label = "Gamma module")]

[assembly: FactoryRole("worker", "Worker", ReadOwn = ["job"], Create = ["job"])]
[assembly: FactoryRole("auditor", "Auditor", ReadAll = ["job", "report"])]

namespace Osme.TestPackages.Gamma;

[Document("Job")]
public sealed class Job
{
    [Field("Title", Required = true)]
    public string Title { get; set; } = "";
}

[Document("Report")]
public sealed class Report
{
    [Field("Title", Required = true)]
    public string Title { get; set; } = "";
}

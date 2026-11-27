// SPDX-License-Identifier: LGPL-3.0-or-later
using Osme.Core.Contracts;

[assembly: Package("itdesk", "0.1.0")]

namespace Osme.Modules.ItDesk;

[Document("Ticket")]
public sealed class Ticket
{
    public const int TitleMaxLength = 200;

    [Field("Título", Required = true, MaxLength = TitleMaxLength)]
    public string Title { get; set; } = "";

    [Field("Descripción", LongText = true)]
    public string? Description { get; set; }

    [Field("Prioridad", Required = true, Options = ["low", "normal", "high"],
        OptionLabels = ["Baja", "Normal", "Alta"], Default = "normal")]
    public string Priority { get; set; } = "normal";
}

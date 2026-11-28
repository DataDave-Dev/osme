// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Text.Json;
using Osme.Core.Access;
using Osme.Core.Documents;

namespace Osme.Host;

/// <summary>
/// The uniform API of every declared document. Who may do what is decided here, once, the same
/// way for every document of every package: no package writes an access check
/// (change 0-2-1, decisions 7 and 8).
/// </summary>
public sealed class DocumentsApi(
    IReadOnlyDictionary<string, DocumentDescription> documents, DocumentStore store, InitialSetup setup)
{
    // There is one company per installation in this change, and it never goes away.
    private Guid? _companyId;

    public void MapRoutes(WebApplication app)
    {
        // The interface does not know any document: this is how it learns which ones to offer.
        app.MapGet("/api/meta", (HttpContext context) =>
            Api.Ok(documents.Values
                .Where(d => AccessApi.CallerOf(context).Account.Can(PermissionCatalog.ReadOf(d)))
                .Select(d => new { d.Key, d.Label })));

        app.MapGet("/api/meta/{key}", (string key, HttpContext context) =>
            Readable(context, key) is { } access ? Api.Ok(access.Document) : Api.NotFound());

        app.MapGet("/api/docs/{key}", async (string key, HttpContext context, CancellationToken cancel) =>
            Readable(context, key) is { } access
                ? Api.Ok(await store.ListAsync(access.Document, access.OnlyCreatedBy, cancel))
                : Api.NotFound());

        app.MapGet("/api/docs/{key}/{id}", async (string key, string id, HttpContext context, CancellationToken cancel) =>
            Readable(context, key) is { } access
            && Guid.TryParse(id, out var guid)
            && await store.GetAsync(access.Document, guid, access.OnlyCreatedBy, cancel) is { } record
                ? Api.Ok(record)
                : Api.NotFound());

        app.MapPost("/api/docs/{key}", async (string key, HttpContext context, CancellationToken cancel) =>
        {
            if (Readable(context, key) is not { } access) return Api.NotFound();

            var account = AccessApi.CallerOf(context).Account;
            if (!account.Can(PermissionCatalog.CreateOf(access.Document)))
                return Api.Fail(StatusCodes.Status403Forbidden, "forbidden");

            if (await AccessApi.ReadObjectAsync(context.Request, cancel) is not { } body) return AccessApi.InvalidBody();
            using (body)
            {
                var result = DocumentValidator.Validate(access.Document, body.RootElement);
                if (!result.IsValid)
                    return Api.Fail(StatusCodes.Status422UnprocessableEntity, "validation", result.Errors);

                var author = new Author(account.Id, await CompanyIdAsync(cancel));
                return Api.Ok(await store.InsertAsync(access.Document, result.Values, author, cancel), StatusCodes.Status201Created);
            }
        });
    }

    /// <summary>A document the caller may read, and whether only the records the caller created.</summary>
    private sealed record Access(DocumentDescription Document, Guid? OnlyCreatedBy);

    /// <summary>
    /// The one place that decides access to a document. Null both for a document nobody declares
    /// and for one the caller cannot read: the answer must not tell them apart.
    /// </summary>
    private Access? Readable(HttpContext context, string key)
    {
        if (!documents.TryGetValue(key, out var document)) return null;
        var account = AccessApi.CallerOf(context).Account;
        return account.ScopeOf(PermissionCatalog.ReadOf(document)) switch
        {
            Grant.All => new Access(document, null),
            Grant.Own => new Access(document, account.Id),
            _ => null,
        };
    }

    private async ValueTask<Guid> CompanyIdAsync(CancellationToken cancel) =>
        _companyId ??= await setup.CompanyIdAsync(cancel)
            ?? throw new InvalidOperationException("A signed-in request implies a company.");
}

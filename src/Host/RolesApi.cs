// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Text.Json;
using Osme.Core.Access;

namespace Osme.Host;

/// <summary>
/// The catalog of what can be granted and the roles that grant it, for whoever has the Roles
/// view (change 0-2-1, decisions 6 and 14).
/// </summary>
public sealed class RolesApi(Roles roles, PermissionCatalog catalog)
{
    public void MapRoutes(WebApplication app)
    {
        // The role editor is drawn from this: a new document shows up without touching the interface.
        app.MapGet("/api/permissions", (HttpContext context) =>
            Refuse(context) ?? Api.Ok(new { Core = PermissionCatalog.CoreViews, catalog.Packages }));

        app.MapGet("/api/roles", async (HttpContext context, CancellationToken cancel) =>
            Refuse(context) ?? Api.Ok(await roles.ListAsync(cancel)));

        app.MapPost("/api/roles", async (HttpContext context, CancellationToken cancel) =>
        {
            if (Refuse(context) is { } refusal) return refusal;
            if (await AccessApi.ReadObjectAsync(context.Request, cancel) is not { } document) return AccessApi.InvalidBody();
            using (document)
            {
                var body = document.RootElement;
                if (!TryText(body, "name", out var name) || !TryGrants(body, out var grants)) return AccessApi.InvalidBody();
                return Answer(
                    await roles.CreateAsync(name ?? "", grants ?? [], AccessApi.CallerOf(context).Account.Id, cancel),
                    StatusCodes.Status201Created);
            }
        });

        app.MapPatch("/api/roles/{id}", async (string id, HttpContext context, CancellationToken cancel) =>
        {
            if (Refuse(context) is { } refusal) return refusal;
            if (!Guid.TryParse(id, out var roleId)) return Api.NotFound();
            if (await AccessApi.ReadObjectAsync(context.Request, cancel) is not { } document) return AccessApi.InvalidBody();
            using (document)
            {
                var body = document.RootElement;
                if (!TryText(body, "name", out var name) || !TryGrants(body, out var grants) || !TryFlag(body, "is_active", out var isActive))
                    return AccessApi.InvalidBody();
                return Answer(
                    await roles.UpdateAsync(roleId, name, grants, isActive, AccessApi.CallerOf(context).Account.Id, cancel),
                    StatusCodes.Status200OK);
            }
        });
    }

    private static IResult? Refuse(HttpContext context) => AccessApi.RefuseWithout(context, PermissionCatalog.Roles);

    private static IResult Answer(RoleChange change, int done) => change.Status switch
    {
        RoleChangeStatus.Done => Api.Ok(change.Role!, done),
        RoleChangeStatus.Invalid => Api.Fail(StatusCodes.Status422UnprocessableEntity, "validation", change.Errors),
        RoleChangeStatus.SystemRole => Api.Fail(StatusCodes.Status409Conflict, "system_role"),
        _ => Api.NotFound(),
    };

    // What is absent comes out as null; what is present with another type makes the body invalid.

    private static bool TryText(JsonElement body, string name, out string? value)
    {
        value = null;
        if (!body.TryGetProperty(name, out var found)) return true;
        if (found.ValueKind != JsonValueKind.String) return false;
        value = found.GetString();
        return true;
    }

    private static bool TryFlag(JsonElement body, string name, out bool? value)
    {
        value = null;
        if (!body.TryGetProperty(name, out var found)) return true;
        if (found.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
        value = found.GetBoolean();
        return true;
    }

    private static bool TryGrants(JsonElement body, out List<Grant>? grants)
    {
        grants = null;
        if (!body.TryGetProperty("permissions", out var found)) return true;
        if (found.ValueKind != JsonValueKind.Array) return false;

        var read = new List<Grant>();
        foreach (var item in found.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !TryText(item, "key", out var key) || key is null) return false;
            // A scope that is missing or is not a text is refused later as an invalid scope, naming the permission.
            var scope = item.TryGetProperty("scope", out var given) && given.ValueKind == JsonValueKind.String ? given.GetString()! : "";
            read.Add(new Grant(key, scope));
        }
        grants = read;
        return true;
    }
}

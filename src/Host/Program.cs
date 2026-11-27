// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Npgsql;
using Osme.Core.Documents;
using Osme.Core.Packages;
using Osme.Host;

var builder = WebApplication.CreateBuilder(args);

// There is no sign-in yet, so the server only answers its own machine (change c0-2-1 lifts this).
var port = builder.Configuration.GetValue("Osme:Port", 5080);
builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenLocalhost(port));

builder.Services.ConfigureHttpJsonOptions(json =>
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));

var connectionString = builder.Configuration["OSME_DB"];
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Set the OSME_DB environment variable to the PostgreSQL connection string. See .env.example.");

var app = builder.Build();

var packagesPath = Path.GetFullPath(
    builder.Configuration["Osme:PackagesPath"] ?? "packages",
    builder.Environment.ContentRootPath);
var packages = PackageLoader.Load(packagesPath, app.Logger);
var documents = packages.SelectMany(p => p.Documents).ToDictionary(d => d.Key);

await using var dataSource = NpgsqlDataSource.Create(connectionString);
var store = new DocumentStore(dataSource);
foreach (var document in documents.Values)
    await store.EnsureStructureAsync(document);

app.UseExceptionHandler(errors => errors.Run(async context =>
{
    var failure = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    app.Logger.LogError(failure, "Unhandled error on {Path}", context.Request.Path);
    await Api.Fail(StatusCodes.Status500InternalServerError, "internal").ExecuteAsync(context);
}));

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/packages", () =>
    Api.Ok(packages.Select(p => new { p.Manifest.Name, p.Manifest.Version })));

// The interface does not know any document: this is how it learns which ones to offer.
app.MapGet("/api/meta", () =>
    Api.Ok(documents.Values.Select(d => new { d.Key, d.Label })));

app.MapGet("/api/meta/{key}", (string key) =>
    documents.TryGetValue(key, out var document) ? Api.Ok(document) : Api.NotFound());

app.MapGet("/api/docs/{key}", async (string key, CancellationToken cancel) =>
    documents.TryGetValue(key, out var document)
        ? Api.Ok(await store.ListAsync(document, cancel))
        : Api.NotFound());

app.MapGet("/api/docs/{key}/{id}", async (string key, string id, CancellationToken cancel) =>
    documents.TryGetValue(key, out var document)
    && Guid.TryParse(id, out var guid)
    && await store.GetAsync(document, guid, cancel) is { } record
        ? Api.Ok(record)
        : Api.NotFound());

app.MapPost("/api/docs/{key}", async (string key, HttpRequest request, CancellationToken cancel) =>
{
    if (!documents.TryGetValue(key, out var document)) return Api.NotFound();

    JsonDocument body;
    try
    {
        body = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancel);
    }
    catch (JsonException)
    {
        return Api.Fail(StatusCodes.Status400BadRequest, "invalid_body");
    }

    using (body)
    {
        if (body.RootElement.ValueKind != JsonValueKind.Object)
            return Api.Fail(StatusCodes.Status400BadRequest, "invalid_body");

        var result = DocumentValidator.Validate(document, body.RootElement);
        if (!result.IsValid)
            return Api.Fail(StatusCodes.Status422UnprocessableEntity, "validation", result.Errors);

        return Api.Ok(await store.InsertAsync(document, result.Values, cancel), StatusCodes.Status201Created);
    }
});

// Unknown /api routes answer in the same shape as everything else.
app.Map("/api/{**rest}", () => Api.NotFound());

app.Run();

public partial class Program;

// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Npgsql;
using Osme.Core.Access;
using Osme.Core.Documents;
using Osme.Core.Packages;
using Osme.Host;

var builder = WebApplication.CreateBuilder(args);

// Still only its own machine: the server opens to the network when it serves HTTPS (task 8.2 of change 0-2-1).
var port = builder.Configuration.GetValue("Osme:Port", 5080);
builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenLocalhost(port));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.ConfigureHttpJsonOptions(json =>
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));

var connectionString = builder.Configuration["OSME_DB"];
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Set the OSME_DB environment variable to the PostgreSQL connection string. See .env.example.");

var app = builder.Build();

// The core's own tables come first: packages point at them.
await using var dataSource = NpgsqlDataSource.Create(connectionString);
await CoreSchema.EnsureAsync(dataSource);

var packagesPath = Path.GetFullPath(
    builder.Configuration["Osme:PackagesPath"] ?? "packages",
    builder.Environment.ContentRootPath);
var packages = PackageLoader.Load(packagesPath, app.Logger);
var documents = packages.SelectMany(p => p.Documents).ToDictionary(d => d.Key);

var store = new DocumentStore(dataSource);
foreach (var document in documents.Values)
    await store.EnsureStructureAsync(document);

var clock = app.Services.GetRequiredService<TimeProvider>();
var sessions = new Sessions(dataSource, clock);
var setup = new InitialSetup(dataSource, clock);

// Without a company, every start makes a new installation code and writes it where whoever
// runs the server can read it: it is what another machine must present to do the initial setup.
InstallationCode? installationCode = null;
if (!await setup.IsDoneAsync())
{
    installationCode = new InstallationCode();
    app.Logger.LogWarning(
        "This installation has no company yet. Installation code for the initial setup from another machine: {InstallationCode}",
        installationCode.Display);
}

// What can be granted comes from the loaded packages; their factory roles are created once.
var catalog = new PermissionCatalog(packages);
var roles = new Roles(dataSource, catalog, clock);
await roles.SeedAsync(packages.SelectMany(p => p.FactoryRoles), app.Logger);

var access = new AccessApi(
    setup, sessions, new SignIn(dataSource, sessions, clock), new Accounts(dataSource, sessions, catalog, clock), installationCode);

app.UseExceptionHandler(errors => errors.Run(async context =>
{
    var failure = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    app.Logger.LogError(failure, "Unhandled error on {Path}", context.Request.Path);
    await Api.Fail(StatusCodes.Status500InternalServerError, "internal").ExecuteAsync(context);
}));

app.UseDefaultFiles();
app.UseStaticFiles();

access.UseGate(app);
access.MapRoutes(app);
new RolesApi(roles, catalog).MapRoutes(app);

// Needs a session and nothing else: it says what is installed, not what it holds.
app.MapGet("/api/packages", () =>
    Api.Ok(packages.Select(p => new { p.Manifest.Name, p.Manifest.Version })));

new DocumentsApi(documents, store, setup).MapRoutes(app);

// Unknown /api routes answer in the same shape as everything else.
app.Map("/api/{**rest}", () => Api.NotFound());

app.Run();

public partial class Program;

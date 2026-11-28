// SPDX-License-Identifier: LGPL-3.0-or-later
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Osme.Tests;

public static class Repo
{
    public static readonly string Root = FindRoot();
    public static readonly string Packages = Path.Combine(Root, "artifacts", "packages");
    public static readonly string LoaderPackages = Path.Combine(Root, "artifacts", "test-packages", "loader");
    public static readonly string BoundaryPackages = Path.Combine(Root, "artifacts", "test-packages", "boundary");

    /// <summary>One package (gamma) with two documents and two factory roles.</summary>
    public static readonly string RolePackages = Path.Combine(Root, "artifacts", "test-packages", "roles");

    /// <summary>Packages whose factory roles are badly declared: none of them may load.</summary>
    public static readonly string BadRolePackages = Path.Combine(Root, "artifacts", "test-packages", "bad-roles");

    private static readonly Lazy<string> Mixed = new(() =>
    {
        var folder = Directory.CreateTempSubdirectory("osme-mixed-packages-").FullName;
        foreach (var source in new[] { Packages, RolePackages })
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(folder, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
        return folder;
    });

    /// <summary>The real packages (itdesk) together with gamma, for what needs two packages.</summary>
    public static string MixedPackages => Mixed.Value;

    /// <summary>OSME_DB from the environment, or from the .env file at the repository root.</summary>
    public static string ConnectionString()
    {
        const string name = "OSME_DB";
        var value = Environment.GetEnvironmentVariable(name);
        var envFile = Path.Combine(Root, ".env");
        if (string.IsNullOrWhiteSpace(value) && File.Exists(envFile))
            value = File.ReadLines(envFile)
                .Where(line => line.StartsWith(name + "="))
                .Select(line => line[(name.Length + 1)..].Trim('"'))
                .FirstOrDefault();
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException("Tests need PostgreSQL: set OSME_DB or copy .env.example to .env.");
    }

    private static string FindRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "global.json")))
            folder = folder.Parent;
        return folder?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}

/// <summary>A real, empty PostgreSQL database that lives for one test.</summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _name = "osme_test_" + Guid.NewGuid().ToString("N");

    public string ConnectionString { get; private set; } = "";
    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public static async Task<TestDatabase> CreateAsync()
    {
        var database = new TestDatabase();
        await AdminAsync($"CREATE DATABASE {database._name}");
        database.ConnectionString = new NpgsqlConnectionStringBuilder(Repo.ConnectionString()) { Database = database._name }.ToString();
        database.DataSource = NpgsqlDataSource.Create(database.ConnectionString);
        return database;
    }

    public async Task<T?> ScalarAsync<T>(string sql)
    {
        await using var command = DataSource.CreateCommand(sql);
        return (T?)await command.ExecuteScalarAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await AdminAsync($"DROP DATABASE IF EXISTS {_name} WITH (FORCE)");
    }

    private static async Task AdminAsync(string sql)
    {
        await using var admin = NpgsqlDataSource.Create(Repo.ConnectionString());
        await using var command = admin.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>The real server, in memory, against a given database and packages folder.</summary>
public sealed class OsmeApp(string connectionString, string packagesPath) : WebApplicationFactory<Program>
{
    public const string Origin = "https://localhost";
    public const string AdministratorUsername = "admin";
    public const string AdministratorName = "Ana Admin";
    public const string Password = AccessDatabase.Password;

    /// <summary>An address of the local network, for the requests that must not come from the server's machine.</summary>
    public const string OtherMachine = "192.168.1.50";

    // The server in memory has no connection, so no caller address: this header gives the request one.
    private const string RemoteAddressHeader = "X-Test-Remote-Address";

    /// <summary>Everything the server wrote to its log.</summary>
    public CapturingLogger Log { get; } = new();

    /// <summary>The clock of the server, moved by hand.</summary>
    public TestClock Clock { get; } = new() { Now = DateTimeOffset.UtcNow };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("OSME_DB", connectionString);
        builder.UseSetting("Osme:PackagesPath", packagesPath);
        builder.ConfigureLogging(logging => logging.AddProvider(Log));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<IStartupFilter>(new RemoteAddressFilter());
        });
    }

    /// <summary>
    /// A browser of its own (own cookies) on the server's machine, or on <paramref name="from"/>.
    /// It sends the Origin of the server, as a page of the server would.
    /// </summary>
    public HttpClient Browser(string? from = null, string? origin = Origin)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(Origin) });
        if (origin is not null) client.DefaultRequestHeaders.Add("Origin", origin);
        if (from is not null) client.DefaultRequestHeaders.Add(RemoteAddressHeader, from);
        return client;
    }

    /// <summary>A browser signed in as the administrator, doing the initial setup when it is missing.</summary>
    public async Task<HttpClient> AdministratorAsync()
    {
        var client = Browser();
        var setup = await ApiTests.Send(client, HttpMethod.Post, "/api/setup", new
        {
            company_name = "Acme", name = AdministratorName, username = AdministratorUsername, password = Password,
        });
        if (setup.Status == System.Net.HttpStatusCode.Created) return client;

        var entered = await SignInAsync(client, AdministratorUsername);
        return entered.Status == System.Net.HttpStatusCode.OK
            ? client
            : throw new InvalidOperationException($"The test administrator could not get in: {entered.Body}");
    }

    public static Task<(System.Net.HttpStatusCode Status, System.Text.Json.JsonElement Body)> SignInAsync(
        HttpClient client, string username, string password = Password) =>
        ApiTests.Send(client, HttpMethod.Post, "/api/session", new { username, password });

    /// <summary>The installation code as the server wrote it to its log.</summary>
    public string InstallationCode()
    {
        _ = Server; // starts the server
        const string mark = "another machine: ";
        var line = Log.Entries.Single(e => e.Message.Contains(mark)).Message;
        return line[(line.IndexOf(mark, StringComparison.Ordinal) + mark.Length)..];
    }

    private sealed class RemoteAddressFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, following) =>
            {
                context.Connection.RemoteIpAddress = context.Request.Headers.TryGetValue(RemoteAddressHeader, out var address)
                    ? System.Net.IPAddress.Parse(address.ToString())
                    : System.Net.IPAddress.Loopback;
                context.Request.Headers.Remove(RemoteAddressHeader);
                await following(context);
            });
            next(app);
        };
    }
}

public sealed class CapturingLogger : ILogger, ILoggerProvider
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

    public IReadOnlyCollection<(LogLevel Level, string Message)> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => this;
    public void Dispose() { }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        _entries.Enqueue((logLevel, formatter(state, exception)));
}

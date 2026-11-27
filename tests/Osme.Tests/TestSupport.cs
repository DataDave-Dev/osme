// SPDX-License-Identifier: LGPL-3.0-or-later
using Microsoft.AspNetCore.Hosting;
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
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("OSME_DB", connectionString);
        builder.UseSetting("Osme:PackagesPath", packagesPath);
    }
}

public sealed class CapturingLogger : ILogger
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}

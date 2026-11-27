// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Osme.Tests;

/// <summary>The scenarios of specs/motor-de-documentos and specs/carga-de-paquetes, through the real server.</summary>
public class ApiTests
{
    private const string Tickets = "/api/docs/itdesk.ticket";

    internal static async Task<(HttpStatusCode Status, JsonElement Body)> Send(HttpClient client, HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) };
        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    private static void AssertNotFound((HttpStatusCode Status, JsonElement Body) response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Equal("""{"ok":false,"data":null,"error":{"code":"not_found","fields":null}}""", response.Body.GetRawText());
    }

    private static async Task<int> CountAsync(HttpClient client) =>
        (await Send(client, HttpMethod.Get, Tickets)).Body.GetProperty("data").GetArrayLength();

    [Fact]
    public async Task Lists_the_core_and_the_packages_present()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var app = new OsmeApp(database.ConnectionString, Repo.Packages);

        var (status, body) = await Send(app.CreateClient(), HttpMethod.Get, "/api/packages");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(
            """[{"name":"core","version":"0.1.0"},{"name":"itdesk","version":"0.1.0"}]""",
            body.GetProperty("data").GetRawText());
    }

    [Fact]
    public async Task Without_packages_the_server_runs_with_the_core_and_offers_no_document()
    {
        await using var database = await TestDatabase.CreateAsync();
        var empty = Directory.CreateTempSubdirectory("osme-packages-").FullName;
        await using var app = new OsmeApp(database.ConnectionString, empty);
        var client = app.CreateClient();

        var packages = await Send(client, HttpMethod.Get, "/api/packages");
        var documents = await Send(client, HttpMethod.Get, "/api/meta");

        Assert.Equal("""[{"name":"core","version":"0.1.0"}]""", packages.Body.GetProperty("data").GetRawText());
        Assert.Equal("[]", documents.Body.GetProperty("data").GetRawText());
        AssertNotFound(await Send(client, HttpMethod.Get, Tickets));
        Directory.Delete(empty);
    }

    [Fact]
    public async Task Publishes_the_description_of_a_document()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var app = new OsmeApp(database.ConnectionString, Repo.Packages);
        var client = app.CreateClient();

        var (status, body) = await Send(client, HttpMethod.Get, "/api/meta/itdesk.ticket");

        Assert.Equal(HttpStatusCode.OK, status);
        var data = body.GetProperty("data");
        Assert.Equal(("itdesk", "ticket", "itdesk.ticket"),
            (data.GetProperty("package").GetString(), data.GetProperty("name").GetString(), data.GetProperty("key").GetString()));
        Assert.False(string.IsNullOrEmpty(data.GetProperty("label").GetString()));
        var fields = data.GetProperty("fields").EnumerateArray().ToDictionary(f => f.GetProperty("name").GetString()!);
        Assert.Equal(["title", "description", "priority", "created_at"], fields.Keys);
        Assert.Equal(("text", true, false), Shape(fields["title"]));
        Assert.Equal(("long_text", false, false), Shape(fields["description"]));
        Assert.Equal(("option", true, false), Shape(fields["priority"]));
        Assert.Equal(("datetime", false, true), Shape(fields["created_at"]));
        Assert.Equal(["low", "normal", "high"], fields["priority"].GetProperty("options").EnumerateArray().Select(o => o.GetProperty("value").GetString()));
        Assert.All(fields["priority"].GetProperty("options").EnumerateArray(), o => Assert.False(string.IsNullOrEmpty(o.GetProperty("label").GetString())));
        Assert.All(fields.Values, f => Assert.False(string.IsNullOrEmpty(f.GetProperty("label").GetString())));

        var listed = await Send(client, HttpMethod.Get, "/api/meta");
        Assert.Equal("itdesk.ticket", listed.Body.GetProperty("data")[0].GetProperty("key").GetString());

        static (string?, bool, bool) Shape(JsonElement field) =>
            (field.GetProperty("type").GetString(), field.GetProperty("required").GetBoolean(), field.GetProperty("readOnly").GetBoolean());
    }

    [Theory]
    [InlineData("/api/meta/itdesk.nothing")]
    [InlineData("/api/meta/nothing")]
    [InlineData("/api/docs/sales.invoice")]
    [InlineData("/api/docs/itdesk.ticket/019a0000-0000-7000-8000-000000000000")]
    [InlineData("/api/docs/itdesk.ticket/not-an-id")]
    [InlineData("/api/anything/else")]
    public async Task What_does_not_exist_is_not_found_and_reveals_nothing(string url)
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var app = new OsmeApp(database.ConnectionString, Repo.Packages);

        AssertNotFound(await Send(app.CreateClient(), HttpMethod.Get, url));
    }

    [Fact]
    public async Task A_valid_record_is_stored_and_shows_up_in_the_list_and_by_identifier()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var app = new OsmeApp(database.ConnectionString, Repo.Packages);
        var client = app.CreateClient();

        var (status, body) = await Send(client, HttpMethod.Post, Tickets, new { title = "Printer is down", priority = "high" });

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.True(body.GetProperty("ok").GetBoolean());
        var created = body.GetProperty("data");
        Assert.Equal(7, created.GetProperty("id").GetGuid().Version);
        Assert.InRange(created.GetProperty("created_at").GetDateTimeOffset(), DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));

        var list = (await Send(client, HttpMethod.Get, Tickets)).Body.GetProperty("data");
        Assert.Equal(created.GetRawText(), list[0].GetRawText());
        var one = await Send(client, HttpMethod.Get, $"{Tickets}/{created.GetProperty("id").GetString()}");
        Assert.Equal(created.GetRawText(), one.Body.GetProperty("data").GetRawText());
    }

    [Fact]
    public async Task The_server_assigns_identifier_and_creation_date_whatever_the_caller_sends()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var app = new OsmeApp(database.ConnectionString, Repo.Packages);
        const string sentId = "11111111-1111-4111-8111-111111111111";

        var (status, body) = await Send(app.CreateClient(), HttpMethod.Post, Tickets,
            new { title = "Mine", id = sentId, created_at = "2001-01-01T00:00:00Z" });

        Assert.Equal(HttpStatusCode.Created, status);
        var data = body.GetProperty("data");
        Assert.NotEqual(sentId, data.GetProperty("id").GetString());
        Assert.True(data.GetProperty("created_at").GetDateTimeOffset().Year > 2001);
    }

    public static TheoryData<string, string, string> Rejections() => new()
    {
        { """{"description":"no title"}""", "title", "required" },
        { """{"title":"x","priority":"urgent"}""", "priority", "invalid_option" },
        { """{"title":"x","status":"open"}""", "status", "unknown_field" },
        { $$"""{"title":"{{new string('x', 201)}}"}""", "title", "too_long" },
    };

    [Theory]
    [MemberData(nameof(Rejections))]
    public async Task What_breaks_the_declaration_is_rejected_naming_the_field_and_nothing_is_stored(string json, string field, string code)
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var app = new OsmeApp(database.ConnectionString, Repo.Packages);
        var client = app.CreateClient();

        using var response = await client.PostAsync(Tickets, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.False(body.GetProperty("ok").GetBoolean());
        Assert.Equal("validation", body.GetProperty("error").GetProperty("code").GetString());
        var error = Assert.Single(body.GetProperty("error").GetProperty("fields").EnumerateArray());
        Assert.Equal((field, code), (error.GetProperty("field").GetString(), error.GetProperty("code").GetString()));
        if (code == "invalid_option")
            Assert.Equal("""["low","normal","high"]""", error.GetProperty("options").GetRawText());
        Assert.Equal(0, await CountAsync(client));
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("[]")]
    [InlineData("")]
    public async Task A_body_that_is_not_a_json_object_is_rejected(string json)
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var app = new OsmeApp(database.ConnectionString, Repo.Packages);

        using var response = await app.CreateClient().PostAsync(Tickets, new StringContent(json));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_body", body.GetProperty("error").GetProperty("code").GetString());
    }
}

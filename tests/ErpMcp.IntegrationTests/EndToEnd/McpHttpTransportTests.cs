using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ErpMcp.Application.Auditing;
using ErpMcp.Application.Orders;
using ErpMcp.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ErpMcp.IntegrationTests.EndToEnd;

/// <summary>
/// Runs the real server with <c>erp-mcp http</c> and connects with the SDK's HTTP client, using
/// tokens signed with the development key. Server ceiling: <c>read orders:draft</c>.
/// </summary>
public sealed class McpHttpTransportTests(PostgresFixture db) : IAsyncLifetime
{
    private const string SigningKey = "integration-test-signing-key-32-bytes-min!!";
    private const string Audience = "erp-mcp-tests";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly HttpClient _http = new();
    private Process? _server;
    private string _baseUrl = "";

    public async ValueTask InitializeAsync()
    {
        _baseUrl = $"http://127.0.0.1:{FreePort()}";
        var start = new ProcessStartInfo("dotnet", [Path.Combine(AppContext.BaseDirectory, "erp-mcp.dll"), "http"])
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        start.Environment["Database__ConnectionString"] = db.ConnectionString;
        start.Environment["Database__MigrateOnStartup"] = "false";
        start.Environment["Security__Scopes"] = "read orders:draft";
        start.Environment["Http__Url"] = _baseUrl;
        start.Environment["Auth__Audience"] = Audience;
        start.Environment["Auth__DevSigningKey"] = SigningKey;
        _server = Process.Start(start)!;
        _server.BeginErrorReadLine();
        _server.BeginOutputReadLine();

        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                if ((await _http.GetAsync($"{_baseUrl}/healthz", Ct)).IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // not listening yet
            }

            await Task.Delay(200, Ct);
        }

        throw new TimeoutException("HTTP server did not start.");
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        if (_server is { HasExited: false })
        {
            _server.Kill(entireProcessTree: true);
            await _server.WaitForExitAsync();
        }

        _server?.Dispose();
    }

    [Fact]
    public async Task Unauthenticated_requests_are_challenged_with_resource_metadata()
    {
        var response = await _http.PostAsync($"{_baseUrl}/mcp", new StringContent("{}", Encoding.UTF8, "application/json"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().ShouldContain("resource_metadata=");
    }

    [Fact]
    public async Task Protected_resource_metadata_advertises_scopes()
    {
        var metadata = await _http.GetFromJsonAsync<JsonElement>($"{_baseUrl}/.well-known/oauth-protected-resource/mcp", Ct);

        metadata.GetProperty("resource").GetString().ShouldBe($"{_baseUrl}/mcp");
        metadata.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString()).ShouldContain("orders:draft");
    }

    [Theory]
    [InlineData("wrong-signature")]
    [InlineData("wrong-audience")]
    [InlineData("expired")]
    public async Task Invalid_tokens_are_rejected(string problem)
    {
        var token = problem switch
        {
            "wrong-signature" => Token("read", key: "a-completely-different-signing-key-of-32+bytes"),
            "wrong-audience" => Token("read", audience: "someone-else"),
            _ => Token("read", expires: DateTime.UtcNow.AddMinutes(-5)),
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/mcp") { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new("Bearer", token);

        (await _http.SendAsync(request, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_scopes_narrow_the_tool_list()
    {
        await using var client = await ConnectAsync(Token("catalog:read openid"));

        var tools = await client.ListToolsAsync(cancellationToken: Ct);

        tools.Select(t => t.Name).ShouldBe(["get_product", "get_server_info", "list_product_categories", "search_products"], ignoreOrder: true);
    }

    [Fact]
    public async Task Drafts_record_the_person_and_that_person_cannot_approve()
    {
        await using var client = await ConnectAsync(Token("read orders:draft", email: "alice@example.com"));

        var result = await client.CallToolAsync("create_draft_order", new Dictionary<string, object?>
        {
            ["customerCode"] = "CUST-0020",
            ["lines"] = new[] { new { sku = "BEV-0004", quantity = 3 } },
        }, cancellationToken: Ct);

        result.IsError.ShouldNotBe(true, result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text);
        var order = result.StructuredContent!.Value.GetProperty("order");
        var number = order.GetProperty("orderNumber").GetString()!;
        order.GetProperty("onBehalfOf").GetString().ShouldBe("alice@example.com");
        order.GetProperty("createdBy").GetString()!.ShouldStartWith("agent:");

        using var scope = db.Services.CreateScope();
        var approvals = scope.ServiceProvider.GetRequiredService<OrderApprovalService>();
        (await Should.ThrowAsync<DomainRuleViolationException>(() => approvals.ApproveAsync(number, "alice@example.com", Ct)))
            .Message.ShouldContain("requested order");
        (await approvals.ApproveAsync(number, "bob@example.com", Ct)).DecidedBy.ShouldBe("bob@example.com");

        var audit = await scope.ServiceProvider.GetRequiredService<IAuditLog>()
            .ListRecentAsync(new AuditQuery(200, "create_draft_order", Actor: "alice@example.com"), Ct);
        var entry = audit.Select(r => r.Entry).First(e => e.EntityRef == number);
        entry.OnBehalfOf.ShouldBe("alice@example.com");
        entry.SessionId.ShouldNotBeNullOrEmpty();
    }

    private async Task<McpClient> ConnectAsync(string token) =>
        await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri($"{_baseUrl}/mcp"),
            Name = "http-tests",
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        }), cancellationToken: Ct);

    private static string Token(
        string scope, string email = "tester@example.com", string audience = Audience, string key = SigningKey, DateTime? expires = null)
    {
        var expiry = expires ?? DateTime.UtcNow.AddMinutes(5);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "erp-mcp-dev",
            Audience = audience,
            Subject = new ClaimsIdentity([new Claim("sub", "user-1"), new Claim("email", email), new Claim("scope", scope)]),
            NotBefore = expiry.AddMinutes(-10),
            IssuedAt = expiry.AddMinutes(-10),
            Expires = expiry,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256),
        });
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

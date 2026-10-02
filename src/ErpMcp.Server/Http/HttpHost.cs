using System.Text;
using ErpMcp.Application;
using ErpMcp.Application.Security;
using ErpMcp.Infrastructure;
using ErpMcp.Server.Cli;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;

namespace ErpMcp.Server.Http;

/// <summary>
/// Streamable HTTP transport for remote MCP clients. The server acts as an OAuth 2.1 resource
/// server, as the MCP authorization spec describes:
/// <list type="bullet">
/// <item>every <c>/mcp</c> request needs a bearer token from the configured authorization server;</item>
/// <item>unauthenticated requests get <c>401</c> with a <c>WWW-Authenticate</c> header pointing at</item>
/// <item><c>/.well-known/oauth-protected-resource</c>, which tells clients where to get a token and which scopes exist;</item>
/// <item>the token's <c>scope</c> claim narrows the server's configured grant per request (<see cref="Security.ScopeResolver"/>).</item>
/// </list>
/// </summary>
internal static partial class HttpHost
{
    public static async Task<int> RunAsync(CommandLine cli)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = cli.ConfigurationArgs,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

        var http = builder.Configuration.GetSection(HttpOptions.SectionName).Get<HttpOptions>() ?? new HttpOptions();
        var auth = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        if (Validate(auth) is { } problem)
        {
            await Console.Error.WriteLineAsync($"error: {problem}");
            return 1;
        }

        var resource = $"{(http.PublicUrl ?? http.Url).TrimEnd('/')}/mcp";

        builder.Services
            .AddApplication()
            .AddInfrastructure(builder.Configuration)
            .AddErpMcpServer(builder.Configuration, McpTransport.Http);

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options => ConfigureJwt(options, auth))
            .AddMcp(options => options.ResourceMetadata = new ProtectedResourceMetadata
            {
                Resource = resource,
                ResourceName = "ERP MCP Server",
                AuthorizationServers = [auth.Authority ?? auth.DevIssuer],
                ScopesSupported = [.. Scopes.All.Order(StringComparer.Ordinal)],
            });
        builder.Services.AddAuthorization();

        var app = builder.Build();
        if (auth.DevSigningKey is not null)
        {
            LogDevKey(app.Logger);
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
        app.MapMcp("/mcp").RequireAuthorization();

        app.Urls.Add(http.Url);
        await app.RunAsync();
        return 0;
    }

    private static string? Validate(AuthOptions auth)
    {
        if (string.IsNullOrWhiteSpace(auth.Audience))
        {
            return "Auth:Audience is required for the HTTP transport.";
        }

        if (string.IsNullOrWhiteSpace(auth.Authority) == (auth.DevSigningKey is null))
        {
            return "Configure exactly one of Auth:Authority (real authorization server) or Auth:DevSigningKey (local development).";
        }

        if (auth.DevSigningKey is { } key && Encoding.UTF8.GetByteCount(key) < 32)
        {
            return "Auth:DevSigningKey must be at least 32 bytes.";
        }

        return null;
    }

    private static void ConfigureJwt(JwtBearerOptions options, AuthOptions auth)
    {
        // Keep claim names as issued ("scope", "email", "sub") rather than mapping to WS-* URIs.
        options.MapInboundClaims = false;

        if (auth.DevSigningKey is { } key)
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = auth.DevIssuer,
                ValidAudience = auth.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.FromSeconds(30),
            };
            return;
        }

        options.Authority = auth.Authority;
        options.Audience = auth.Audience;
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Auth:DevSigningKey is set: accepting locally signed development tokens. Never use this in production.")]
    private static partial void LogDevKey(ILogger logger);
}

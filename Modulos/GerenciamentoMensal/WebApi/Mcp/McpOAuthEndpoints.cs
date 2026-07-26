using System.Text.Json.Serialization;
using Application.Mcp.Configuration;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WebApi.Mcp;

public static class McpOAuthEndpoints
{
    public static void MapMcpOAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/oauth/register", async (
            McpOAuthRegistrationRequest request,
            IOpenIddictApplicationManager applications,
            IOptions<McpFeatureOptions> featureOptions,
            CancellationToken cancellationToken) =>
        {
            var redirectUris = request.RedirectUris ?? [];
            var scopes = request.Scope?
                .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                ?? ["mcp:read"];

            if (string.IsNullOrWhiteSpace(request.ClientName) ||
                redirectUris.Count == 0 ||
                redirectUris.Any(uri => !McpOAuthClientRegistrationValidator.IsAllowedRedirectUri(uri)) ||
                !McpOAuthClientRegistrationValidator.AreAllowedScopes(scopes) ||
                request.TokenEndpointAuthMethod is not (null or "none"))
            {
                return Results.BadRequest(new
                {
                    error = "invalid_client_metadata",
                    error_description = "Metadados de cliente MCP inválidos."
                });
            }

            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = $"mcp_{Guid.NewGuid():N}",
                DisplayName = request.ClientName.Trim(),
                ClientType = ClientTypes.Public,
                ConsentType = ConsentTypes.Explicit
            };
            foreach (var redirectUri in redirectUris)
                descriptor.RedirectUris.Add(new Uri(redirectUri));
            descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(Permissions.Endpoints.Token);
            descriptor.Permissions.Add(Permissions.Endpoints.Revocation);
            descriptor.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
            descriptor.Permissions.Add(Permissions.ResponseTypes.Code);
            descriptor.Permissions.Add(
                Permissions.Prefixes.Resource +
                $"{featureOptions.Value.PublicBaseUrl.TrimEnd('/')}/mcp");
            foreach (var scope in scopes)
                descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);

            await applications.CreateAsync(descriptor, cancellationToken);
            return Results.Ok(new
            {
                client_id = descriptor.ClientId,
                client_name = descriptor.DisplayName,
                redirect_uris = redirectUris,
                token_endpoint_auth_method = "none",
                grant_types = new[] { "authorization_code", "refresh_token" },
                response_types = new[] { "code" },
                scope = string.Join(' ', scopes),
                client_id_issued_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        })
        .AllowAnonymous()
        .RequireRateLimiting("mcp-dcr");
    }
}

public sealed record McpOAuthRegistrationRequest(
    [property: JsonPropertyName("client_name")]
    string ClientName,
    [property: JsonPropertyName("redirect_uris")]
    IReadOnlyList<string>? RedirectUris,
    [property: JsonPropertyName("token_endpoint_auth_method")]
    string? TokenEndpointAuthMethod,
    [property: JsonPropertyName("scope")]
    string? Scope);

using System.Security.Claims;
using Application.Mcp.Configuration;
using Application.Mcp.Services;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using Domain.Mcp.Repositories;
using Microsoft.AspNetCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WebApi.Mcp;

public static class McpOAuthAuthorizeEndpoint
{
    public static void MapMcpOAuthAuthorizeEndpoint(this WebApplication app)
    {
        app.MapMethods("/oauth/authorize", ["GET", "POST"], async (
            HttpContext httpContext,
            IMcpAuthorizationInteractionRepository interactions,
            McpConnectionService connections,
            IOpenIddictApplicationManager applications,
            IOptions<McpFeatureOptions> featureOptions,
            CancellationToken cancellationToken) =>
        {
            var request = httpContext.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("Requisição OAuth não reconhecida.");
            var interactionId = httpContext.Request.Query["interaction_id"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(interactionId))
            {
                var resumedInteraction = await interactions.GetAsync(interactionId, cancellationToken);
                if (resumedInteraction is null ||
                    resumedInteraction.Status != McpAuthorizationInteractionStatus.Approved ||
                    string.IsNullOrWhiteSpace(resumedInteraction.UserId) ||
                    string.IsNullOrWhiteSpace(resumedInteraction.ConnectionId) ||
                    !resumedInteraction.MatchesResumeRequest(
                        request.ClientId ?? string.Empty,
                        request.RedirectUri ?? string.Empty,
                        request.State ?? string.Empty,
                        request.CodeChallenge ?? string.Empty,
                        request.CodeChallengeMethod ?? string.Empty,
                        request.GetResources().FirstOrDefault() ?? string.Empty,
                        request.GetScopes().ToArray()))
                {
                    return Results.BadRequest(new
                    {
                        error = "access_denied",
                        error_description = "Consentimento MCP ausente, expirado ou inválido."
                    });
                }

                var connection = await connections.GetAsync(
                    resumedInteraction.ConnectionId, resumedInteraction.UserId, cancellationToken);
                if (connection is null || !connection.IsUsable(DateTime.UtcNow))
                    return Results.BadRequest(new { error = "access_denied" });

                var identity = new ClaimsIdentity(
                    TokenValidationParameters.DefaultAuthenticationType,
                    Claims.Name,
                    Claims.Role);
                identity.SetClaim(Claims.Subject, connection.UserId)
                    .SetClaim(Claims.Name, connection.ClientName)
                    .SetClaim(McpClaimNames.ConnectionId, connection.Id)
                    .SetClaim(McpClaimNames.AuthorizationId, connection.AuthorizationId)
                    .SetAuthorizationId(connection.AuthorizationId)
                    .SetScopes(connection.Scopes)
                    .SetResources(string.IsNullOrWhiteSpace(resumedInteraction.Resource)
                        ? [$"{featureOptions.Value.PublicBaseUrl.TrimEnd('/')}/mcp"]
                        : [resumedInteraction.Resource]);

                foreach (var claim in identity.Claims)
                    claim.SetDestinations(Destinations.AccessToken);

                return Results.SignIn(
                    new ClaimsPrincipal(identity),
                    authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var client = await applications.FindByClientIdAsync(
                request.ClientId!, cancellationToken);
            if (client is null)
                return Results.BadRequest(new { error = "invalid_client" });

            var clientName = await applications.GetDisplayNameAsync(client, cancellationToken)
                ?? request.ClientId!;
            var pendingInteraction = new McpAuthorizationInteraction(
                Guid.NewGuid().ToString("N")[..24],
                request.ClientId!,
                clientName,
                request.GetScopes().ToArray(),
                DateTime.UtcNow.AddMinutes(10),
                request.RedirectUri ?? string.Empty,
                request.State ?? string.Empty,
                request.CodeChallenge ?? string.Empty,
                request.CodeChallengeMethod ?? string.Empty,
                request.GetResources().FirstOrDefault() ?? string.Empty);
            await interactions.AddAsync(pendingInteraction, cancellationToken);

            var consentUrl = featureOptions.Value.ConsentUrl;
            if (string.IsNullOrWhiteSpace(consentUrl))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "MCP_CONSENT_URL não configurada.");
            }

            var separator = consentUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
            return Results.Redirect(
                $"{consentUrl}{separator}mcpAuthorizationInteraction={Uri.EscapeDataString(pendingInteraction.Id)}");
        }).AllowAnonymous();
    }

    public static string BuildContinuationUrl(
        McpFeatureOptions options,
        McpConnection connection,
        McpAuthorizationInteraction interaction)
    {
        var baseUrl = options.PublicBaseUrl.TrimEnd('/');
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = connection.ClientId,
            ["redirect_uri"] = interaction.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = string.Join(' ', connection.Scopes),
            ["state"] = interaction.State,
            ["code_challenge"] = interaction.CodeChallenge,
            ["code_challenge_method"] = interaction.CodeChallengeMethod,
            ["resource"] = interaction.Resource,
            ["interaction_id"] = interaction.Id
        };
        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
            $"{baseUrl}/oauth/authorize", query);
    }

    public static string BuildDeniedContinuationUrl(McpAuthorizationInteraction interaction)
    {
        var query = new Dictionary<string, string?>
        {
            ["error"] = "access_denied",
            ["state"] = interaction.State
        };
        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
            interaction.RedirectUri, query);
    }
}

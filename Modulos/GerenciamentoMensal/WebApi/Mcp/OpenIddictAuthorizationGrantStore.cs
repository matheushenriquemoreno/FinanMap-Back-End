using System.Collections.Immutable;
using System.Security.Claims;
using Application.Mcp.Interfaces;
using Domain.Mcp.Entities;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WebApi.Mcp;

public sealed class OpenIddictAuthorizationGrantStore(
    IOpenIddictApplicationManager applications,
    IOpenIddictAuthorizationManager authorizations,
    IOpenIddictTokenManager tokens) : IMcpAuthorizationGrantStore
{
    public async Task<string> CreateAsync(
        string userId,
        McpAuthorizationInteraction interaction,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default)
    {
        var application = await applications.FindByClientIdAsync(
            interaction.ClientId, cancellationToken)
            ?? throw new InvalidOperationException("Cliente OAuth MCP não encontrado.");
        var applicationId = await applications.GetIdAsync(application, cancellationToken)
            ?? throw new InvalidOperationException("Cliente OAuth MCP sem identificador.");
        var identity = new ClaimsIdentity();
        identity.SetClaim(Claims.Subject, userId).SetScopes(scopes);
        var authorization = await authorizations.CreateAsync(
            new ClaimsPrincipal(identity),
            userId,
            applicationId,
            AuthorizationTypes.Permanent,
            scopes.ToImmutableArray(),
            cancellationToken);

        return await authorizations.GetIdAsync(authorization, cancellationToken)
            ?? throw new InvalidOperationException("Autorização OAuth MCP sem identificador.");
    }

    public async Task RevokeAsync(
        string authorizationId,
        CancellationToken cancellationToken = default)
    {
        var authorization = await authorizations.FindByIdAsync(
            authorizationId, cancellationToken);
        if (authorization is null ||
            !await authorizations.TryRevokeAsync(authorization, cancellationToken))
        {
            throw new InvalidOperationException("Autorização MCP ativa não encontrada.");
        }

        await tokens.RevokeByAuthorizationIdAsync(authorizationId, cancellationToken);
    }

    public async Task<bool> IsActiveAsync(
        string authorizationId,
        CancellationToken cancellationToken = default)
    {
        var authorization = await authorizations.FindByIdAsync(
            authorizationId, cancellationToken);
        return authorization is not null &&
            await authorizations.HasStatusAsync(
                authorization, Statuses.Valid, cancellationToken);
    }
}

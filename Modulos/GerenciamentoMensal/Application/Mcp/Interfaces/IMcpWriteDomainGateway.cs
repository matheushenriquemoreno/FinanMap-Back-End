#nullable enable

using Application.Mcp.Models;
using Domain.Mcp.Entities;

namespace Application.Mcp.Interfaces;

public interface IMcpWriteDomainGateway
{
    Task<McpDomainPreparation> PrepareAsync(
        string userId,
        McpWriteCommand command,
        CancellationToken cancellationToken = default);

    Task<McpDomainEffect> ExecuteAsync(
        string userId,
        McpWriteCommand command,
        string operationId,
        IReadOnlyList<McpSnapshotHash> snapshots,
        CancellationToken cancellationToken = default);

    Task<McpDomainEffect?> FindEffectAsync(
        string userId,
        McpWriteCommand command,
        string operationId,
        CancellationToken cancellationToken = default);
}

public sealed record McpWriteStoredRecord(
    string Id,
    McpWriteEntity Entity,
    IReadOnlyDictionary<string, object?> Values,
    string? McpOperationId,
    string? LastMcpOperationId,
    string? LastMcpResultHash);

public interface IMcpWriteEffectStore
{
    Task<McpWriteStoredRecord?> LoadOwnedAsync(
        McpWriteEntity entity,
        string id,
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> CategoryHasLinksAsync(
        string id,
        string userId,
        CancellationToken cancellationToken = default);

    Task<McpWriteStoredRecord?> FindEffectAsync(
        McpWriteEntity entity,
        string userId,
        string operationId,
        CancellationToken cancellationToken = default);
}

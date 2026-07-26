using Domain.Mcp.Entities;
using Domain.Mcp.Repositories;
using Infra.Configure.Env;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mcp;

public sealed class McpConnectionRepository(IMongoClient mongoClient)
    : RepositoryMongoBase<McpConnection>(mongoClient), IMcpConnectionRepository
{
    public override string GetCollectionName() => "McpConnections";

    public Task AddAsync(McpConnection connection, CancellationToken cancellationToken = default) =>
        _entityCollection.InsertOneAsync(connection, cancellationToken: cancellationToken);

    public Task<McpConnection?> GetOwnedAsync(
        string id, string userId, CancellationToken cancellationToken = default) =>
        _entityCollection
            .Find(item => item.Id == id && item.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<McpConnection>> ListOwnedAsync(
        string userId, CancellationToken cancellationToken = default) =>
        await _entityCollection
            .Find(item => item.UserId == userId)
            .SortByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task UpdateAsync(McpConnection connection, CancellationToken cancellationToken = default) =>
        _entityCollection.ReplaceOneAsync(
            item => item.Id == connection.Id && item.UserId == connection.UserId,
            connection,
            cancellationToken: cancellationToken);
}

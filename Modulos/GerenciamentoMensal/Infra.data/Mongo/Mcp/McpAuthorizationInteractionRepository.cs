using Domain.Mcp.Entities;
using Domain.Mcp.Repositories;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mcp;

public sealed class McpAuthorizationInteractionRepository(IMongoClient mongoClient)
    : RepositoryMongoBase<McpAuthorizationInteraction>(mongoClient), IMcpAuthorizationInteractionRepository
{
    public override string GetCollectionName() => "McpAuthorizationInteractions";

    public Task AddAsync(
        McpAuthorizationInteraction interaction, CancellationToken cancellationToken = default) =>
        _entityCollection.InsertOneAsync(interaction, cancellationToken: cancellationToken);

    public Task<McpAuthorizationInteraction?> GetAsync(
        string id, CancellationToken cancellationToken = default) =>
        _entityCollection.Find(item => item.Id == id).FirstOrDefaultAsync(cancellationToken);

    public Task UpdateAsync(
        McpAuthorizationInteraction interaction, CancellationToken cancellationToken = default) =>
        _entityCollection.ReplaceOneAsync(
            item => item.Id == interaction.Id, interaction, cancellationToken: cancellationToken);
}

using Testcontainers.MongoDb;
using Xunit;

namespace Tests;

[CollectionDefinition(Name)]
public sealed class McpMongoCollection : ICollectionFixture<McpMongoFixture>
{
    public const string Name = "Mcp Mongo standalone";
}

public sealed class McpMongoFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder(
        "mongo@sha256:e0ce8c35124d4a9f9785532d1f268f39e9728ffa1cb38f46fa482436424c4bd3")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

using Domain.Entity;
using Domain.Enum;
using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using Infra.Data.Mongo.Mcp;
using MongoDB.Driver;
using Xunit;

namespace Tests;

[Collection(McpMongoCollection.Name)]
public sealed class McpMongoAuditIntegrationTests(McpMongoFixture mongo)
{
    [Fact]
    public async Task Standalone_mongo_preserves_owned_audit_and_isolates_history_a_b_after_record_deletion()
    {
        var client = new MongoClient(mongo.ConnectionString);
        var repository = new McpOperationJournalRepository(client);
        var suffix = Guid.NewGuid().ToString("N");
        var ownerA = $"owner-a-{suffix}";
        var ownerB = $"owner-b-{suffix}";
        var category = new Categoria("Fixture descartável", TipoCategoria.Despesa, ownerA)
        {
            Id = $"category-{suffix}"
        };
        var categoryCollection = client
            .GetDatabase("FinanMap")
            .GetCollection<Categoria>("Categoria");
        var journalCollection = client
            .GetDatabase("FinanMap")
            .GetCollection<McpOperationJournal>("McpOperationJournal");
        var journalA = McpOperationJournal.Start(
            ownerA,
            $"connection-a-{suffix}",
            $"correlation-a-{suffix}",
            "finanmap_categories_list",
            McpOperationClass.Read,
            new Dictionary<string, object?> { ["recordId"] = category.Id });
        journalA.Complete(new Dictionary<string, object?>
        {
            ["status"] = "success",
            ["count"] = 1
        });
        var journalB = McpOperationJournal.Start(
            ownerB,
            $"connection-b-{suffix}",
            $"correlation-b-{suffix}",
            "finanmap_categories_list",
            McpOperationClass.Read);
        journalB.Complete(new Dictionary<string, object?> { ["status"] = "empty" });

        try
        {
            await categoryCollection.InsertOneAsync(category);
            await repository.AddAsync(journalA);
            await repository.CompleteAsync(journalA, journalA.ResultSummary);
            await repository.AddAsync(journalB);
            await repository.CompleteAsync(journalB, journalB.ResultSummary);

            await categoryCollection.DeleteOneAsync(item => item.Id == category.Id);

            var pageA = await repository.ListOwnedAsync(
                ownerA, null, null, null, null, null, 50);
            var preserved = Assert.Single(pageA.Items);
            Assert.Equal(journalA.Id, preserved.Id);
            Assert.Equal(ownerA, preserved.UserId);
            Assert.Equal("finanmap_categories_list", preserved.ToolName);
            Assert.Equal(category.Id, preserved.SanitizedParameters["recordId"]);
            Assert.Equal("success", preserved.ResultSummary["status"]);
            Assert.NotEqual(default, preserved.StartedAtUtc);
            Assert.NotNull(preserved.FinishedAtUtc);
            Assert.Null(await repository.GetOwnedAsync(journalA.Id, ownerB));

            var pageB = await repository.ListOwnedAsync(
                ownerB, null, null, null, null, null, 50);
            Assert.Equal(journalB.Id, Assert.Single(pageB.Items).Id);
        }
        finally
        {
            await categoryCollection.DeleteOneAsync(item => item.Id == category.Id);
            await journalCollection.DeleteManyAsync(item =>
                item.Id == journalA.Id || item.Id == journalB.Id);
        }
    }
}

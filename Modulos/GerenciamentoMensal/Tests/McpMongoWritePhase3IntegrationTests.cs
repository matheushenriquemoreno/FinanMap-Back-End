using Domain.Mcp.Entities;
using Domain.Mcp.Enums;
using Application.Implementacoes;
using Application.Service;
using Application.Mcp.Interfaces;
using Application.Mcp.Models;
using Application.Mcp.Services;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Login.Interfaces;
using Domain.Relatorios.AcumuladoMensal;
using Infra.Data.Mongo.Config.Interface;
using Infra.Data.Mongo.Mappings;
using Infra.Data.Mongo.Mcp;
using Infra.Data.Mongo.Repositorys;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Tests;

[Collection(McpMongoCollection.Name)]
public sealed class McpMongoWritePhase3IntegrationTests(McpMongoFixture mongo)
{
    [Fact]
    public async Task Six_confirmed_category_and_income_flows_use_real_services_and_mongo_end_to_end()
    {
        var client = new MongoClient(mongo.ConnectionString);
        RegisterFinancialMappings(client);
        var database = client.GetDatabase("FinanMap");
        var categoriesCollection = database.GetCollection<Categoria>("Categoria");
        var incomesCollection = database.GetCollection<Rendimento>("Rendimento");
        var suffix = Guid.NewGuid().ToString("N");
        var ownerId = ObjectId.GenerateNewId().ToString();
        var user = new Usuario(
            "Integração MCP",
            $"mcp-{suffix}@example.com")
        {
            Id = ownerId
        };
        var loggedUser = new LoggedUser(user);
        var categoryRepository = new CategoriaRepository(client);
        var incomeRepository = new RendimentoRepository(
            client,
            categoryRepository);
        var categoryService = new CategoriaService(
            categoryRepository,
            loggedUser);
        var incomeService = new RendimentoService(
            incomeRepository,
            categoryRepository,
            new AcumuladoMensalRepository(client),
            loggedUser);
        var previews = new McpPreviewRepository(client);
        var journals = new McpOperationJournalRepository(client);
        var writeService = new McpWriteService(
            new McpWriteDomainGateway(
                categoryService,
                incomeService,
                new McpWriteEffectStore(client)),
            new ConnectionValidator(),
            previews,
            journals,
            journals,
            new McpPreviewPayloadProtector(
                Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()),
            TimeProvider.System,
            new McpAuditSanitizer());
        var context = new McpCallContext(
            ownerId,
            $"connection-{suffix}",
            $"correlation-{suffix}",
            ClientId: "integration-client");
        var incomeCategory = await categoryRepository.Add(
            new Categoria(
                $"Receitas-{suffix}",
                Domain.Enum.TipoCategoria.Rendimento,
                ownerId)
            {
                Id = ObjectId.GenerateNewId().ToString()
            });

        try
        {
            var categoryCreatePreview = await writeService.PrepareCategoryCreateAsync(
                context,
                new McpCategoryCreatePreviewInput(
                    $"category-create-{suffix}",
                    $"Temporária-{suffix}",
                    Domain.Enum.TipoCategoria.Despesa));
            Assert.Equal(
                1,
                await categoriesCollection.CountDocumentsAsync(item =>
                    item.UsuarioId == ownerId));
            var categoryCreate = await ConfirmAndReplayAsync(
                writeService,
                context,
                categoryCreatePreview,
                "APPLY_CHANGES");
            Assert.Equal("category", categoryCreate.EntityType);
            var categoryId = categoryCreate.EntityId!;
            var createdCategory = await categoryRepository.GetById(categoryId);
            Assert.Equal(categoryCreate.OperationId, createdCategory.McpOperationId);

            var categoryUpdatePreview = await writeService.PrepareCategoryUpdateAsync(
                context,
                new McpCategoryUpdatePreviewInput(
                    $"category-update-{suffix}",
                    categoryId,
                    $"Alterada-{suffix}"));
            Assert.Equal(
                $"Temporária-{suffix}",
                (await categoryRepository.GetById(categoryId)).Nome);
            var categoryUpdate = await ConfirmAndReplayAsync(
                writeService,
                context,
                categoryUpdatePreview,
                "APPLY_CHANGES");
            var updatedCategory = await categoryRepository.GetById(categoryId);
            Assert.Equal($"Alterada-{suffix}", updatedCategory.Nome);
            Assert.Equal(categoryUpdate.OperationId, updatedCategory.LastMcpOperationId);
            Assert.False(string.IsNullOrWhiteSpace(updatedCategory.LastMcpResultHash));

            var categoryDeletePreview = await writeService.PrepareCategoryDeleteAsync(
                context,
                new McpCategoryDeletePreviewInput(
                    $"category-delete-{suffix}",
                    categoryId));
            Assert.NotNull(await categoryRepository.GetById(categoryId));
            var categoryDelete = await ConfirmAndReplayAsync(
                writeService,
                context,
                categoryDeletePreview,
                "DELETE_PERMANENTLY");
            Assert.Equal("category", categoryDelete.EntityType);
            Assert.Equal(categoryId, categoryDelete.EntityId);
            Assert.Null(await categoryRepository.GetById(categoryId));
            await AssertDeleteReceiptAsync(
                journals,
                ownerId,
                categoryDelete.OperationId,
                "category",
                categoryId);

            var incomeCreatePreview = await writeService.PrepareIncomeCreateAsync(
                context,
                new McpIncomeCreatePreviewInput(
                    $"income-create-{suffix}",
                    DateTime.UtcNow.Year,
                    7,
                    $"Salário-{suffix}",
                    "1250.50",
                    incomeCategory.Id));
            Assert.Equal(
                0,
                await incomesCollection.CountDocumentsAsync(item =>
                    item.UsuarioId == ownerId));
            var incomeCreate = await ConfirmAndReplayAsync(
                writeService,
                context,
                incomeCreatePreview,
                "APPLY_CHANGES");
            Assert.Equal("income", incomeCreate.EntityType);
            var incomeId = incomeCreate.EntityId!;
            var createdIncome = await incomeRepository.GetById(incomeId);
            Assert.Equal(incomeCreate.OperationId, createdIncome.McpOperationId);
            Assert.Equal(1250.50m, createdIncome.Valor);

            var incomeUpdatePreview = await writeService.PrepareIncomeUpdateAsync(
                context,
                new McpIncomeUpdatePreviewInput(
                    $"income-update-{suffix}",
                    incomeId,
                    $"Salário ajustado-{suffix}",
                    "1400.75",
                    incomeCategory.Id));
            Assert.Equal(
                1250.50m,
                (await incomeRepository.GetById(incomeId)).Valor);
            var incomeUpdate = await ConfirmAndReplayAsync(
                writeService,
                context,
                incomeUpdatePreview,
                "APPLY_CHANGES");
            var updatedIncome = await incomeRepository.GetById(incomeId);
            Assert.Equal($"Salário ajustado-{suffix}", updatedIncome.Descricao);
            Assert.Equal(1400.75m, updatedIncome.Valor);
            Assert.Equal(incomeUpdate.OperationId, updatedIncome.LastMcpOperationId);
            Assert.False(string.IsNullOrWhiteSpace(updatedIncome.LastMcpResultHash));

            var incomeDeletePreview = await writeService.PrepareIncomeDeleteAsync(
                context,
                new McpIncomeDeletePreviewInput(
                    $"income-delete-{suffix}",
                    incomeId));
            Assert.NotNull(await incomeRepository.GetById(incomeId));
            var incomeDelete = await ConfirmAndReplayAsync(
                writeService,
                context,
                incomeDeletePreview,
                "DELETE_PERMANENTLY");
            Assert.Equal("income", incomeDelete.EntityType);
            Assert.Equal(incomeId, incomeDelete.EntityId);
            Assert.Null(await incomeRepository.GetById(incomeId));
            await AssertDeleteReceiptAsync(
                journals,
                ownerId,
                incomeDelete.OperationId,
                "income",
                incomeId);
        }
        finally
        {
            await incomesCollection.DeleteManyAsync(item =>
                item.UsuarioId == ownerId);
            await categoriesCollection.DeleteManyAsync(item =>
                item.UsuarioId == ownerId);
            await database.GetCollection<McpPreview>("McpPreviews")
                .DeleteManyAsync(item => item.UserId == ownerId);
            await database.GetCollection<McpOperationJournal>("McpOperationJournal")
                .DeleteManyAsync(item => item.UserId == ownerId);
        }
    }

    [Fact]
    public async Task Prepare_replay_with_real_mongo_reuses_journal_and_preview_or_rejects_divergent_hash()
    {
        var client = new MongoClient(mongo.ConnectionString);
        new McpMapping().RegisterMap(client);
        var previews = new McpPreviewRepository(client);
        var journals = new McpOperationJournalRepository(client);
        var suffix = Guid.NewGuid().ToString("N");
        var context = new McpCallContext(
            $"owner-{suffix}",
            $"connection-{suffix}",
            $"correlation-{suffix}",
            ClientId: "integration-client");
        var gateway = new ReplayGateway();
        var service = new McpWriteService(
            gateway,
            new ConnectionValidator(),
            previews,
            journals,
            journals,
            new McpPreviewPayloadProtector(
                Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()),
            TimeProvider.System,
            new McpAuditSanitizer());

        try
        {
            var first = await service.PrepareCategoryCreateAsync(
                context,
                new McpCategoryCreatePreviewInput(
                    $"request-{suffix}",
                    "Trabalho",
                    Domain.Enum.TipoCategoria.Despesa));
            var replay = await service.PrepareCategoryCreateAsync(
                context with { CorrelationId = $"replay-{suffix}" },
                new McpCategoryCreatePreviewInput(
                    $"request-{suffix}",
                    "Trabalho",
                    Domain.Enum.TipoCategoria.Despesa));
            var conflict = await service.PrepareCategoryCreateAsync(
                context with { CorrelationId = $"conflict-{suffix}" },
                new McpCategoryCreatePreviewInput(
                    $"request-{suffix}",
                    "Outro nome",
                    Domain.Enum.TipoCategoria.Despesa));

            Assert.Equal("requires_confirmation", first.Status);
            Assert.Equal("requires_confirmation", replay.Status);
            Assert.Equal(first.Data!.PreviewId, replay.Data!.PreviewId);
            Assert.Equal(first.Data.PayloadHash, replay.Data.PayloadHash);
            Assert.Equal(1, gateway.PrepareCount);
            Assert.Equal("rejected", conflict.Status);
            Assert.Equal("IDEMPOTENCY_CONFLICT", Assert.Single(conflict.Errors).Code);
        }
        finally
        {
            await client.GetDatabase("FinanMap")
                .GetCollection<McpPreview>("McpPreviews")
                .DeleteManyAsync(item => item.UserId == context.UserId);
            await client.GetDatabase("FinanMap")
                .GetCollection<McpOperationJournal>("McpOperationJournal")
                .DeleteManyAsync(item => item.UserId == context.UserId);
        }
    }

    [Fact]
    public async Task Journal_index_migrates_legacy_null_previews_and_preserves_real_preview_uniqueness()
    {
        var client = new MongoClient(mongo.ConnectionString);
        var database = client.GetDatabase("FinanMap");
        var rawJournals = database.GetCollection<BsonDocument>("McpOperationJournal");
        var suffix = Guid.NewGuid().ToString("N");
        var legacyIds = new[]
        {
            ObjectId.GenerateNewId(),
            ObjectId.GenerateNewId()
        };
        var indexDocuments = await rawJournals.Indexes.ListAsync();
        var previewIndex = (await indexDocuments.ToListAsync())
            .FirstOrDefault(item =>
                item.GetValue("name", "").AsString == "PreviewId_1");
        if (previewIndex is not null)
            await rawJournals.Indexes.DropOneAsync("PreviewId_1");

        await rawJournals.InsertManyAsync(legacyIds.Select(id =>
            new BsonDocument
            {
                ["_id"] = id,
                ["PreviewId"] = BsonNull.Value,
                ["UserId"] = $"legacy-owner-{suffix}"
            }));

        try
        {
            new McpMapping().RegisterMap(client);
            var indexes = await (await rawJournals.Indexes.ListAsync()).ToListAsync();
            var migrated = Assert.Single(indexes, item =>
                item.GetValue("name", "").AsString == "PreviewId_1");
            Assert.True(migrated.GetValue("unique", false).ToBoolean());
            Assert.True(migrated.Contains("partialFilterExpression"));

            var repository = new McpOperationJournalRepository(client);
            var previewId = $"preview-{suffix}";
            var first = McpOperationJournal.Start(
                $"owner-{suffix}",
                $"connection-{suffix}",
                $"correlation-a-{suffix}",
                "finanmap_operation_confirm",
                McpOperationClass.Confirm,
                idempotencyKey: previewId,
                requestHash: "hash-a",
                previewId: previewId);
            var duplicate = McpOperationJournal.Start(
                first.UserId,
                first.ConnectionId,
                $"correlation-b-{suffix}",
                first.ToolName,
                McpOperationClass.Confirm,
                idempotencyKey: previewId,
                requestHash: "hash-a",
                previewId: previewId);

            Assert.True((await repository.CreateOrGetAsync(first)).Created);
            var replay = await repository.CreateOrGetAsync(duplicate);
            Assert.False(replay.Created);
            Assert.Equal(first.Id, replay.Journal.Id);
        }
        finally
        {
            await rawJournals.DeleteManyAsync(
                Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.In("_id", legacyIds),
                    Builders<BsonDocument>.Filter.Eq(
                        "UserId",
                        $"owner-{suffix}")));
        }
    }

    [Fact]
    public async Task Standalone_preview_and_journal_enforce_unique_intent_and_single_cas_winner()
    {
        var client = new MongoClient(mongo.ConnectionString);
        new McpMapping().RegisterMap(client);
        var previews = new McpPreviewRepository(client);
        var journals = new McpOperationJournalRepository(client);
        var suffix = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var preview = McpPreview.Prepare(
            $"owner-{suffix}",
            $"connection-{suffix}",
            "finanmap_category_create_preview",
            McpPreviewAction.Create,
            $"request-{suffix}",
            [1, 2, 3],
            $"hash-{suffix}",
            [],
            new Dictionary<string, object?> { ["entityType"] = "category" },
            McpRequiredDecision.ApplyChanges,
            now,
            TimeSpan.FromMinutes(15),
            TimeSpan.FromHours(24));

        try
        {
            var created = await previews.CreateOrGetAsync(preview);
            var replay = await previews.CreateOrGetAsync(McpPreview.Prepare(
                preview.UserId,
                preview.ConnectionId,
                preview.ToolName,
                preview.Action,
                preview.RequestId,
                [9],
                preview.PayloadHash,
                [],
                preview.SafeSummary,
                preview.RequiredDecision,
                now,
                TimeSpan.FromMinutes(15),
                TimeSpan.FromHours(24)));

            Assert.True(created.Created);
            Assert.False(replay.Created);
            Assert.False(replay.PayloadConflict);
            Assert.Equal(preview.Id, replay.Preview.Id);

            var journal = McpOperationJournal.Start(
                preview.UserId,
                preview.ConnectionId,
                $"correlation-{suffix}",
                "finanmap_operation_confirm",
                McpOperationClass.Confirm,
                idempotencyKey: preview.Id,
                requestHash: preview.PayloadHash,
                previewId: preview.Id,
                steps:
                [
                    new McpOperationStep(
                        "apply",
                        McpOperationStepState.Pending,
                        null,
                        null,
                        null)
                ]);
            var persistedJournal = await journals.CreateOrGetAsync(journal);
            Assert.True(persistedJournal.Created);

            var reservations = await Task.WhenAll(
                previews.TryReserveAsync(
                    preview.Id,
                    preview.UserId,
                    preview.ConnectionId,
                    preview.PayloadHash,
                    McpRequiredDecision.ApplyChanges,
                    journal.Id,
                    now.AddSeconds(1)),
                previews.TryReserveAsync(
                    preview.Id,
                    preview.UserId,
                    preview.ConnectionId,
                    preview.PayloadHash,
                    McpRequiredDecision.ApplyChanges,
                    "other-operation",
                    now.AddSeconds(1)));
            Assert.Single(reservations, item => item is not null);

            var leases = await Task.WhenAll(
                journals.TryAcquireLeaseAsync(
                    journal.Id, "worker-a", now.AddSeconds(1), TimeSpan.FromMinutes(1)),
                journals.TryAcquireLeaseAsync(
                    journal.Id, "worker-b", now.AddSeconds(1), TimeSpan.FromMinutes(1)));
            Assert.Single(leases, item => item is not null);
        }
        finally
        {
            await client.GetDatabase("FinanMap")
                .GetCollection<McpPreview>("McpPreviews")
                .DeleteManyAsync(item => item.UserId == preview.UserId);
            await client.GetDatabase("FinanMap")
                .GetCollection<McpOperationJournal>("McpOperationJournal")
                .DeleteManyAsync(item => item.UserId == preview.UserId);
        }
    }

    [Fact]
    public async Task Effect_store_applies_owner_snapshot_and_marker_in_one_document_cas()
    {
        var client = new MongoClient(mongo.ConnectionString);
        new McpMapping().RegisterMap(client);
        var store = new McpWriteEffectStore(client);
        var database = client.GetDatabase("FinanMap");
        var categories = database.GetCollection<BsonDocument>("Categoria");
        var incomes = database.GetCollection<BsonDocument>("Rendimento");
        var fixedCosts = database.GetCollection<BsonDocument>("CustoFixo");
        var ownerA = ObjectId.GenerateNewId();
        var ownerB = ObjectId.GenerateNewId();
        var categoryId = ObjectId.GenerateNewId();
        var incomeId = ObjectId.GenerateNewId();
        var fixedCostId = ObjectId.GenerateNewId();
        var category = new BsonDocument
        {
            ["_id"] = categoryId,
            ["Nome"] = "Antes",
            ["UsuarioId"] = ownerA,
            ["Tipo"] = (int)Domain.Enum.TipoCategoria.Rendimento
        };
        var income = new BsonDocument
        {
            ["_id"] = incomeId,
            ["Ano"] = 2026,
            ["Mes"] = 7,
            ["Descricao"] = "Salário",
            ["Valor"] = new Decimal128(100m),
            ["CategoriaId"] = categoryId,
            ["UsuarioId"] = ownerA
        };
        var fixedCost = new BsonDocument
        {
            ["_id"] = fixedCostId,
            ["Nome"] = "Aluguel",
            ["DiaVencimento"] = 10,
            ["CategoriaId"] = categoryId,
            ["UsuarioId"] = ownerA,
            ["Ativo"] = true
        };

        try
        {
            await categories.InsertOneAsync(category);
            await incomes.InsertOneAsync(income);
            await fixedCosts.InsertOneAsync(fixedCost);
            Assert.True(await store.CategoryHasLinksAsync(
                categoryId.ToString(),
                ownerA.ToString()));
            var loaded = await store.LoadOwnedAsync(
                McpWriteEntity.Category,
                categoryId.ToString(),
                ownerA.ToString());
            Assert.Equal("Antes", loaded!.Values["name"]);
            Assert.Null(await store.LoadOwnedAsync(
                McpWriteEntity.Category,
                categoryId.ToString(),
                ownerB.ToString()));

            await categories.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", categoryId),
                Builders<BsonDocument>.Update
                    .Set("Nome", "Depois")
                    .Set("LastMcpOperationId", "operation-category")
                    .Set("LastMcpResultHash", "result-category"));
            Assert.NotNull(await store.FindEffectAsync(
                McpWriteEntity.Category,
                ownerA.ToString(),
                "operation-category"));

            var loadedIncome = await store.LoadOwnedAsync(
                McpWriteEntity.Income,
                incomeId.ToString(),
                ownerA.ToString());
            Assert.Equal("100.00", loadedIncome!.Values["amount"]);
        }
        finally
        {
            await categories.DeleteOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", categoryId));
            await incomes.DeleteOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", incomeId));
            await fixedCosts.DeleteOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", fixedCostId));
        }
    }

    [Fact]
    public async Task Financial_creation_marker_is_unique_per_owner_and_indexed_for_reconciliation()
    {
        var client = new MongoClient(mongo.ConnectionString);
        new McpMapping().RegisterMap(client);
        var categories = client.GetDatabase("FinanMap")
            .GetCollection<BsonDocument>("Categoria");
        var ownerA = ObjectId.GenerateNewId();
        var ownerB = ObjectId.GenerateNewId();
        var operationId = $"operation-{Guid.NewGuid():N}";
        var documents = new[]
        {
            Category(ObjectId.GenerateNewId(), ownerA, operationId),
            Category(ObjectId.GenerateNewId(), ownerA, operationId),
            Category(ObjectId.GenerateNewId(), ownerB, operationId)
        };

        try
        {
            await categories.InsertOneAsync(documents[0]);
            await Assert.ThrowsAsync<MongoWriteException>(() =>
                categories.InsertOneAsync(documents[1]));
            await categories.InsertOneAsync(documents[2]);
        }
        finally
        {
            await categories.DeleteManyAsync(
                Builders<BsonDocument>.Filter.Eq("McpOperationId", operationId));
        }
    }

    private static BsonDocument Category(
        ObjectId id,
        ObjectId ownerId,
        string operationId) =>
        new()
        {
            ["_id"] = id,
            ["Nome"] = "Marcada",
            ["UsuarioId"] = ownerId,
            ["Tipo"] = (int)Domain.Enum.TipoCategoria.Despesa,
            ["McpOperationId"] = operationId
        };

    private static async Task<McpOperationData> ConfirmAndReplayAsync(
        McpWriteService service,
        McpCallContext context,
        McpToolEnvelope<McpPreviewData> preview,
        string decision)
    {
        Assert.Equal("requires_confirmation", preview.Status);
        var input = new McpOperationConfirmInput(
            preview.Data!.PreviewId,
            preview.Data.PayloadHash,
            decision);
        var confirmed = await service.ConfirmAsync(context, input);
        var replay = await service.ConfirmAsync(
            context with { CorrelationId = $"{context.CorrelationId}-replay" },
            input);

        Assert.Equal("success", confirmed.Status);
        Assert.Equal("success", replay.Status);
        Assert.Equal(confirmed.Data!.OperationId, replay.Data!.OperationId);
        Assert.Equal(confirmed.Data.EntityType, replay.Data.EntityType);
        Assert.Equal(confirmed.Data.EntityId, replay.Data.EntityId);
        return confirmed.Data;
    }

    private static async Task AssertDeleteReceiptAsync(
        McpOperationJournalRepository journals,
        string ownerId,
        string operationId,
        string entityType,
        string entityId)
    {
        var receipt = await journals.GetOwnedAsync(operationId, ownerId);
        Assert.NotNull(receipt);
        Assert.Equal(McpOperationState.Completed, receipt.State);
        Assert.Equal("delete", receipt.ResultSummary["action"]);
        var target = Assert.Single(receipt.TargetRefs);
        Assert.Equal(entityType, target.EntityType);
        Assert.Equal(entityId, target.EntityId);
        Assert.Equal("completed", receipt.Steps.Single().State.ToString().ToLowerInvariant());
    }

    private static void RegisterFinancialMappings(IMongoClient client)
    {
        new EntityBaseMapping().RegisterMap(client);
        var assembly = typeof(McpMapping).Assembly;
        foreach (var mappingName in new[]
                 {
                     "TransacaoMapping",
                     "RendimentoMapping"
                 })
        {
            var mappingType = assembly.GetTypes().Single(type =>
                type.Name == mappingName);
            var mapping = Activator.CreateInstance(
                mappingType,
                nonPublic: true);
            if (mapping is IMongoMappingClassBase classBaseMapping)
                classBaseMapping.RegisterMap(client);
            else
                ((IMongoMapping)mapping!).RegisterMap(client);
        }
        new CategoriaMapping().RegisterMap(client);
        new McpMapping().RegisterMap(client);
    }

    private sealed class LoggedUser(Usuario user) : IUsuarioLogado
    {
        public string Id => user.Id;
        public Usuario Usuario => user;
        public string IdContextoDados => user.Id;
        public Usuario UsuarioContextoDados => user;
        public bool EmModoCompartilhado => false;
        public NivelPermissao? PermissaoAtual => null;
    }

    private sealed class ReplayGateway : IMcpWriteDomainGateway
    {
        public int PrepareCount { get; private set; }

        public Task<McpDomainPreparation> PrepareAsync(
            string userId,
            McpWriteCommand command,
            CancellationToken cancellationToken = default)
        {
            PrepareCount++;
            return Task.FromResult(McpDomainPreparation.Ready(
                command,
                null,
                command.Values,
                []));
        }

        public Task<McpDomainEffect> ExecuteAsync(
            string userId,
            McpWriteCommand command,
            string operationId,
            IReadOnlyList<McpSnapshotHash> snapshots,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpDomainEffect?> FindEffectAsync(
            string userId,
            McpWriteCommand command,
            string operationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<McpDomainEffect?>(null);
    }

    private sealed class ConnectionValidator : IMcpConnectionValidator
    {
        public Task<McpConnection> ValidateActiveAsync(
            string connectionId,
            string userId,
            string requiredScope,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(McpConnection.CreateActive(
                userId,
                "authorization",
                "integration-client",
                "Integration Client",
                [requiredScope]));
    }
}

using Domain.Entity;
using Domain.Repository;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Repositorys
{
    public class RendimentoRepository : RepositoryTransacaoBase<Rendimento>, IRendimentoRepository
    {
        public RendimentoRepository(IMongoClient mongoClient, ICategoriaRepository categoriaRepository) : base(mongoClient, categoriaRepository)
        {
        }

        public override string GetCollectionName()
        {
            return nameof(Rendimento);
        }

        public Task<Rendimento?> TryUpdateMcpAsync(
            string id,
            string userId,
            int expectedYear,
            int expectedMonth,
            string expectedDescription,
            decimal expectedAmount,
            string expectedCategoryId,
            string proposedDescription,
            decimal proposedAmount,
            string proposedCategoryId,
            string operationId,
            string resultHash,
            CancellationToken cancellationToken = default)
        {
            var filter = Builders<Rendimento>.Filter.Where(item =>
                item.Id == id &&
                item.UsuarioId == userId &&
                item.Ano == expectedYear &&
                item.Mes == expectedMonth &&
                item.Descricao == expectedDescription &&
                item.Valor == expectedAmount &&
                item.CategoriaId == expectedCategoryId);
            var update = Builders<Rendimento>.Update
                .Set(item => item.Descricao, proposedDescription)
                .Set(item => item.Valor, proposedAmount)
                .Set(item => item.CategoriaId, proposedCategoryId)
                .Set(item => item.LastMcpOperationId, operationId)
                .Set(item => item.LastMcpResultHash, resultHash);
            return _entityCollection.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<Rendimento>
                {
                    ReturnDocument = ReturnDocument.After
                },
                cancellationToken);
        }

        public async Task<bool> TryDeleteMcpAsync(
            string id,
            string userId,
            int expectedYear,
            int expectedMonth,
            string expectedDescription,
            decimal expectedAmount,
            string expectedCategoryId,
            CancellationToken cancellationToken = default)
        {
            var result = await _entityCollection.DeleteOneAsync(
                item =>
                    item.Id == id &&
                    item.UsuarioId == userId &&
                    item.Ano == expectedYear &&
                    item.Mes == expectedMonth &&
                    item.Descricao == expectedDescription &&
                    item.Valor == expectedAmount &&
                    item.CategoriaId == expectedCategoryId,
                cancellationToken);
            return result.DeletedCount == 1;
        }
    }
}

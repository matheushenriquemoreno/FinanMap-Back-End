using Domain.Entity;
using Domain.Repository;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Repositorys;

public class CompraPlanejadaRepository : RepositoryMongoBase<CompraPlanejada>, ICompraPlanejadaRepository
{
    public CompraPlanejadaRepository(IMongoClient mongoClient) : base(mongoClient)
    {
    }

    public override string GetCollectionName()
        => "ComprasPlanejadas";

    public async Task<CompraPlanejada> GetById(string id, string usuarioId)
    {
        var filtro = Builders<CompraPlanejada>.Filter.And(
            Builders<CompraPlanejada>.Filter.Eq(compra => compra.Id, id),
            Builders<CompraPlanejada>.Filter.Eq(compra => compra.UsuarioId, usuarioId));

        return await _entityCollection.Find(filtro).FirstOrDefaultAsync();
    }

    public Task<List<CompraPlanejada>> GetPendentes(string usuarioId)
        => ListarPorEstado(usuarioId, EstadoCompraPlanejada.Pendente);

    public Task<List<CompraPlanejada>> GetComprados(string usuarioId)
        => ListarPorEstado(usuarioId, EstadoCompraPlanejada.Comprado);

    public async Task<bool> AtualizarSePendente(CompraPlanejada entity)
        => await AtualizarSeEstado(entity, EstadoCompraPlanejada.Pendente);

    public async Task<bool> AtualizarSeEstado(
        CompraPlanejada entity,
        EstadoCompraPlanejada estadoEsperado)
    {
        var filtro = Builders<CompraPlanejada>.Filter.And(
            Builders<CompraPlanejada>.Filter.Eq(compra => compra.Id, entity.Id),
            Builders<CompraPlanejada>.Filter.Eq(compra => compra.UsuarioId, entity.UsuarioId),
            Builders<CompraPlanejada>.Filter.Eq(compra => compra.Estado, estadoEsperado));

        var resultado = await _entityCollection.ReplaceOneAsync(filtro, entity);
        return resultado.MatchedCount == 1;
    }

    private async Task<List<CompraPlanejada>> ListarPorEstado(
        string usuarioId,
        EstadoCompraPlanejada estado)
    {
        var filtro = Builders<CompraPlanejada>.Filter.And(
            Builders<CompraPlanejada>.Filter.Eq(compra => compra.UsuarioId, usuarioId),
            Builders<CompraPlanejada>.Filter.Eq(compra => compra.Estado, estado));

        return await _entityCollection
            .Find(filtro)
            .SortByDescending(compra => compra.Prioridade)
            .ThenByDescending(compra => compra.DataCriacao)
            .ToListAsync();
    }
}

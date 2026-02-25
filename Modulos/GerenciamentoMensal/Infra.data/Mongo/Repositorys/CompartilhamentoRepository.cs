using Domain.Compartilhamento.Entity;
using Domain.Compartilhamento.Repository;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Repositorys;

public class CompartilhamentoRepository : RepositoryMongoBase<Compartilhamento>, ICompartilhamentoRepository
{
    public CompartilhamentoRepository(IMongoClient mongoClient) : base(mongoClient)
    {
    }

    public override string GetCollectionName() => "Compartilhamento";

    public async Task<List<Compartilhamento>> ObterPorProprietarioId(string proprietarioId)
    {
        return await _entityCollection
            .Find(x => x.ProprietarioId == proprietarioId)
            .ToListAsync();
    }

    public async Task<List<Compartilhamento>> ObterPorConvidadoId(string convidadoId)
    {
        return await _entityCollection
            .Find(x => x.ConvidadoId == convidadoId)
            .ToListAsync();
    }

    public async Task<Compartilhamento?> ObterPorProprietarioEConvidado(string proprietarioId, string convidadoId)
    {
        return await _entityCollection
            .Find(x => x.ProprietarioId == proprietarioId && x.ConvidadoId == convidadoId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Compartilhamento>> ObterConvitesPendentesPorEmail(string email)
    {
        return await _entityCollection
            .Find(x => x.ConvidadoEmail == email.ToLower() && x.Status == StatusConvite.Pendente)
            .ToListAsync();
    }
}

using Domain;
using Domain.Entity;
using Domain.Repository;
using MongoDB.Driver;

namespace Infra.Data.Mongo.RepositoryBase;

public abstract class RepositoryTransacaoBase<T> : RepositoryMongoBase<T>, IRepositoryTransacaoBase<T> where T : Transacao
{
    protected readonly ICategoriaRepository _categoryRepository;

    public RepositoryTransacaoBase(IMongoClient mongoClient, ICategoriaRepository categoryRepository) : base(mongoClient)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<IEnumerable<T>> ObterPeloMes(int mes, int ano, string usuarioId)
    {
        return await ObterPeloMesFilter(mes, ano, usuarioId);
    }

    public async Task<IEnumerable<T>> ObterPeloMesFilter(int mes, int ano, string usuarioId, List<FilterDefinition<T>> filtros = null)
    {
        var filterDefinition = Builders<T>.Filter;
        var filter = filterDefinition.And(
            filterDefinition.Eq(x => x.Mes, mes),
            filterDefinition.Eq(x => x.Ano, ano),
            filterDefinition.Eq(x => x.UsuarioId, usuarioId)
        );

        if (filtros != null && filtros.Any())
        {
            foreach (var filtro in filtros)
            {
                filter = filterDefinition.And(filter, filtro);
            }
        }

        var transacoes = await _entityCollection.Find(filter)
            .SortByDescending(x => x.Valor)
            .ToListAsync();

        foreach (var despesa in transacoes)
        {
            await IncluirDependencias(despesa);
        }

        return transacoes;
    }

    public override async Task<T> GetById(string id)
    {
        var transacao = await _entityCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

        if (transacao is not null)
            await IncluirDependencias(transacao);

        return transacao;
    }

    protected virtual async Task IncluirDependencias(T transacao)
    {
        var categoria = await _categoryRepository.GetById(transacao.CategoriaId);

        transacao.Categoria = categoria;
    }
}

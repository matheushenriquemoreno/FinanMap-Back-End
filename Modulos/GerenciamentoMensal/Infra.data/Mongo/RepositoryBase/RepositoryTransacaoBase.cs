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

    public async Task<IEnumerable<T>> ObterPorPeriodo(
        DateOnly from,
        DateOnly to,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        var filters = Builders<T>.Filter;
        var lowerBound = filters.Gt(item => item.Ano, from.Year) |
                         (filters.Eq(item => item.Ano, from.Year) &
                          filters.Gte(item => item.Mes, from.Month));
        var upperBound = filters.Lt(item => item.Ano, to.Year) |
                         (filters.Eq(item => item.Ano, to.Year) &
                          filters.Lte(item => item.Mes, to.Month));
        var transactions = await _entityCollection.Find(
                filters.Eq(item => item.UsuarioId, usuarioId) &
                lowerBound &
                upperBound)
            .SortByDescending(item => item.Ano)
            .ThenByDescending(item => item.Mes)
            .ThenByDescending(item => item.Valor)
            .ToListAsync(cancellationToken);
        await IncluirDependenciasEmLote(transactions);
        return transactions;
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

        await IncluirDependenciasEmLote(transacoes);

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

    private async Task IncluirDependenciasEmLote(IReadOnlyCollection<T> transactions)
    {
        var categoryIds = transactions
            .Select(item => item.CategoriaId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (categoryIds.Count == 0)
            return;

        var categories = await _categoryRepository.GetByIds(categoryIds);
        var byId = categories.ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var transaction in transactions)
        {
            transaction.Categoria = byId.GetValueOrDefault(
                transaction.CategoriaId);
        }
    }
}

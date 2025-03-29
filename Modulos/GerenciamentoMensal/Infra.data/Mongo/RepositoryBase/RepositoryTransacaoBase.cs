using Domain.Entity;
using Domain.Repository;
using MongoDB.Driver;

namespace Infra.Data.Mongo.RepositoryBase;

public abstract class RepositoryTransacaoBase<T> : RepositoryMongoBase<T> where T : Transacao
{
    protected readonly ICategoriaRepository _categoryRepository;

    public RepositoryTransacaoBase(IMongoClient mongoClient, ICategoriaRepository categoryRepository) : base(mongoClient)
    {
        _categoryRepository = categoryRepository;
    }

    public override async Task<T> GetByID(string id)
    {
        var transacao = await _entityCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

        if (transacao is not null)
            await IncluirDependencias(transacao);

        return transacao;
    }

    protected virtual async Task IncluirDependencias(Transacao transacao)
    {
        var categoria = await _categoryRepository.GetByID(transacao.CategoriaId);

        transacao.Categoria = categoria;
    }
}

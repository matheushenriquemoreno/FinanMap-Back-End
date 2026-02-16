using Domain.Login.Entity;
using Domain.Login.Repository;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Repositorys;

public class RefreshTokenRepository : RepositoryMongoBase<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(IMongoClient mongoClient) : base(mongoClient)
    {
    }

    public override string GetCollectionName()
    {
        return nameof(RefreshToken);
    }

    public async Task<RefreshToken> GetByToken(string token)
    {
        return await _entityCollection
            .Find(x => x.Token == token)
            .FirstOrDefaultAsync();
    }

    public async Task DeleteByUsuarioId(string usuarioId)
    {
        await _entityCollection.DeleteManyAsync(x => x.UsuarioId == usuarioId);
    }
}

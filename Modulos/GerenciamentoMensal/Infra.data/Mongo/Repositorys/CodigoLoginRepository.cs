using Domain.Login.Entity;
using Domain.Login.Repository;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Repositorys
{
    public class CodigoLoginRepository : RepositoryMongoBase<CodigoLogin>, ICodigoLoginRepository
    {
        public CodigoLoginRepository(IMongoClient mongoClient) : base(mongoClient)
        {
        }

        public override string GetCollectionName()
        {
            return nameof(CodigoLogin);
        }

        public async Task<CodigoLogin> GetByEmail(string Email)
        {
            return await _entityCollection
                .Find(x => x.Email == Email.ToLower())
                .FirstOrDefaultAsync();
        }

        public async Task<CodigoLogin> GetByCodigo(string codigo)
        {
            return await _entityCollection
                .Find(x => x.Codigo == codigo)
                .FirstOrDefaultAsync();
        }

        public async Task DeleteExpirados(string email)
        {
            await _entityCollection.DeleteManyAsync(x => x.Email.Equals(email) && x.DataExpiracao < DateTime.UtcNow);
        }
    }
}

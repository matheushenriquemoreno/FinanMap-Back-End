using Domain.Entity;
using Domain.Repository;
using Infra.Data.Mongo.RepositoryBase;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Repositorys
{
    public class UsuarioRepository : RepositoryMongoBase<Usuario>, IUsuarioRepository
    {
        public UsuarioRepository(IMongoClient mongoClient) : base(mongoClient)
        {
        }

        public async Task<Usuario> GetByEmail(string email)
        {
            return await _entityCollection.Find(x => x.Email == email.ToLower()).FirstOrDefaultAsync();
        }

        public override string GetCollectionName()
        {
            return nameof(Usuario);
        }
    }
}

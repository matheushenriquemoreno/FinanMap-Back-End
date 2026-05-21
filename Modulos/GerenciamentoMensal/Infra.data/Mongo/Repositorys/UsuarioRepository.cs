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

        public async Task<List<string>> FiltrarUsuariosComNotificacaoAtiva(List<string> usuarioIds)
        {
            var filter = Builders<Usuario>.Filter.And(
                Builders<Usuario>.Filter.In(x => x.Id, usuarioIds),
                Builders<Usuario>.Filter.Eq(x => x.ReceberNotificacoesCustosFixos, true)
            );

            return await _entityCollection.Find(filter).Project(x => x.Id).ToListAsync();
        }
    }
}

using System.Linq.Expressions;
using Domain;
using Infra.Configure.Env;
using MongoDB.Driver;
using SharedDomain.Entity;

namespace Infra.Data.Mongo.RepositoryBase;

public abstract class RepositoryMongoBase<T> : IRepositoryBase<T> where T : IEntityBase
{
    protected readonly IMongoCollection<T> _entityCollection;
    protected readonly IMongoClient _mongoClient;
    protected readonly string _database = MongoDBSettings.DataBaseName;

    public RepositoryMongoBase(IMongoClient mongoClient)
    {
        _entityCollection = mongoClient.GetCollection<T>(_database, this.GetCollectionName());
        _mongoClient = mongoClient;
    }

    public abstract string GetCollectionName();

    public virtual async Task<T> Add(T entity)
    {
        await _entityCollection.InsertOneAsync(entity);
        return entity;
    }

    public virtual async Task Delete(T entity) =>
        await _entityCollection.DeleteOneAsync(x => x.Id == entity.Id);

    public virtual async Task<T> GetById(string id) =>
        await _entityCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public virtual async Task<T> Update(T entity)
    {
        await _entityCollection.ReplaceOneAsync(x => x.Id == entity.Id, entity);
        return entity;
    }

    public virtual async Task<IEnumerable<T>> GetWhere(Expression<Func<T, bool>> filtro)
    {
        return await _entityCollection.Find(filtro).ToListAsync();
    }

    public async Task<List<T>> GetByIds(List<string> ids)
    {
        var filter = Builders<T>.Filter.In(x => x.Id, ids);

        return await _entityCollection.Find(filter).ToListAsync();
    }

    public async Task<List<T>> Add(List<T> entitys)
    {
        await _entityCollection.InsertManyAsync(entitys);
        return entitys;
    }
}

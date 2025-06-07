using System.Collections.Concurrent;
using System.Linq.Expressions;
using Domain.Entity;
using Domain.Enum;
using Domain.Repository;
using Microsoft.Extensions.Caching.Memory;

namespace Infra.Cache.Repository;

public class CachedCategoriaRepository : ICategoriaRepository
{
    private readonly ICategoriaRepository _repositoryDecorate;
    private readonly IMemoryCache _memoryCache;
    private readonly MemoryCacheEntryOptions _cacheOptions;
    private static readonly ConcurrentDictionary<string, string> _cacheKeysFilterAllCategorias = new();

    public CachedCategoriaRepository(ICategoriaRepository repositoryDecorate, IMemoryCache memoryCache)
    {
        _repositoryDecorate = repositoryDecorate;
        _memoryCache = memoryCache;

        // Configurar as opções do cache com o tempo de vida especificado
        _cacheOptions = new MemoryCacheEntryOptions()
          .SetSlidingExpiration(TimeSpan.FromMinutes(3)) // configura o tempo de acesso do cache, caso não for acessado em 3 minutos e removido.
          .SetAbsoluteExpiration(TimeSpan.FromMinutes(15)); // Configura o tempo que o item vai ser removido do cache
    }

    public async Task<Categoria> Add(Categoria entity)
    {
        InvalidarCache(usuarioCategoria: entity.UsuarioId);

        var categoria = await _repositoryDecorate.Add(entity);
        return categoria;
    }

    public bool CategoriaJaExiste(string nome, string idUsuario, TipoCategoria tipo)
    {
        return _repositoryDecorate.CategoriaJaExiste(nome, idUsuario, tipo);
    }

    public async Task Delete(Categoria entity)
    {
        InvalidarCache(entity.Id, entity.UsuarioId);
        await _repositoryDecorate.Delete(entity);
    }

    public async Task<Categoria> GetById(string id)
    {
        var key = id;

        return await _memoryCache.GetOrCreateAsync(key, item =>
         {
             return _repositoryDecorate.GetById(id);
         },
         _cacheOptions);
    }

    public IQueryable<Categoria> GetCategorias()
    {
        return _repositoryDecorate.GetCategorias();
    }

    public async Task<List<Categoria>> GetCategorias(TipoCategoria tipoCategoria, string nome, string idUsuario)
    {
        if (!string.IsNullOrEmpty(nome))
            return await _repositoryDecorate.GetCategorias(tipoCategoria, nome, idUsuario);

        var key = $"{idUsuario}-{tipoCategoria}";

        _cacheKeysFilterAllCategorias.TryAdd(idUsuario, key);

        return await _memoryCache.GetOrCreateAsync(key, item =>
        {
            return _repositoryDecorate.GetCategorias(tipoCategoria, nome, idUsuario);
        }, _cacheOptions);
    }

    public async Task<IEnumerable<Categoria>> GetWhere(Expression<Func<Categoria, bool>> filtro)
    {
        return await _repositoryDecorate.GetWhere(filtro);
    }

    public async Task<Categoria> Update(Categoria entity)
    {
        InvalidarCache(entity.Id, entity.UsuarioId);
        var categoria = await _repositoryDecorate.Update(entity);
        return categoria;
    }

    private void InvalidarCache(string key = null, string usuarioCategoria = null)
    {
        try
        {
            if (key is not null)
                _memoryCache.Remove(key);

            if (usuarioCategoria is not null)
            {
                var keysUser = _cacheKeysFilterAllCategorias
                    .Where(x => x.Key == usuarioCategoria)
                    .ToList();

                foreach (var item in keysUser)
                {
                    _memoryCache.Remove(item.Value);
                    _cacheKeysFilterAllCategorias.TryRemove(item);
                }
            }

            Console.WriteLine("Cache invalidados com sucesso!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao invalidar Cache!");
            Console.WriteLine(ex.Message);
        }
    }

    public async Task<bool> CategoriaPossuiVinculo(Categoria Categoria)
    {
        return await _repositoryDecorate.CategoriaPossuiVinculo(Categoria);
    }

    public async Task<List<Categoria>> GetByIds(List<string> ids)
    {
        return await _repositoryDecorate.GetByIds(ids);
    }

    public Task<List<Categoria>> Add(List<Categoria> entity)
    {
        return _repositoryDecorate.Add(entity);
    }
}

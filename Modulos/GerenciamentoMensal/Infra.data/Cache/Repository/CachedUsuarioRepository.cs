using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Domain.Entity;
using Domain.Repository;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infra.Cache.Repository;

public class CachedUsuarioRepository : IUsuarioRepository
{
    private readonly IUsuarioRepository _repositoryDecorate;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CachedUsuarioRepository> _logger;
    private readonly MemoryCacheEntryOptions _cacheOptions;

    public CachedUsuarioRepository(
        IUsuarioRepository repositoryDecorate,
        IMemoryCache memoryCache,
        ILogger<CachedUsuarioRepository> logger)
    {
        _repositoryDecorate = repositoryDecorate;
        _memoryCache = memoryCache;
        _logger = logger;

        // Cache de 1 dia
        _cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromDays(1));
    }

    public async Task<Usuario> Add(Usuario entity)
    {
        var result = await _repositoryDecorate.Add(entity);
        InvalidarCache(result.Id, result.Email);
        return result;
    }

    public async Task<List<Usuario>> Add(List<Usuario> entitys)
    {
        var result = await _repositoryDecorate.Add(entitys);
        foreach (var user in result)
        {
            InvalidarCache(user.Id, user.Email);
        }
        return result;
    }

    public async Task<Usuario> Update(Usuario entity)
    {
        InvalidarCache(entity.Id, entity.Email);
        var result = await _repositoryDecorate.Update(entity);
        return result;
    }

    public async Task Delete(Usuario entity)
    {
        InvalidarCache(entity.Id, entity.Email);
        await _repositoryDecorate.Delete(entity);
    }

    public async Task<Usuario> GetById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        var key = GetIdKey(id);

        return await _memoryCache.GetOrCreateAsync(key, async item =>
        {
            _logger.LogInformation("Cache miss para usuário ID: {Id}. Buscando no banco de dados.", id);
            var usuario = await _repositoryDecorate.GetById(id);
            if (usuario is null)
            {
                item.SetAbsoluteExpiration(TimeSpan.FromSeconds(30));
            }
            return usuario;
        });
    }

    public async Task<Usuario> GetByEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
            return null;

        var key = GetEmailKey(email);

        return await _memoryCache.GetOrCreateAsync(key, async item =>
        {
            _logger.LogInformation("Cache miss para usuário Email: {Email}. Buscando no banco de dados.", email);
            var usuario = await _repositoryDecorate.GetByEmail(email);
            if (usuario is null)
            {
                item.SetAbsoluteExpiration(TimeSpan.FromSeconds(30));
            }
            return usuario;
        });
    }

    public async Task<List<Usuario>> GetByIds(List<string> ids)
    {
        return await _repositoryDecorate.GetByIds(ids);
    }

    public async Task<IEnumerable<Usuario>> GetWhere(Expression<Func<Usuario, bool>> filtro)
    {
        return await _repositoryDecorate.GetWhere(filtro);
    }

    public async Task<List<string>> FiltrarUsuariosComNotificacaoAtiva(List<string> usuarioIds)
    {
        return await _repositoryDecorate.FiltrarUsuariosComNotificacaoAtiva(usuarioIds);
    }

    private void InvalidarCache(string id, string email)
    {
        try
        {
            if (!string.IsNullOrEmpty(id))
            {
                var idKey = GetIdKey(id);
                _memoryCache.Remove(idKey);
                _logger.LogInformation("Cache invalidado para chave: {Key}", idKey);
            }

            if (!string.IsNullOrEmpty(email))
            {
                var emailKey = GetEmailKey(email);
                _memoryCache.Remove(emailKey);
                _logger.LogInformation("Cache invalidado para chave: {Key}", emailKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao invalidar cache para o usuário {Id}", id);
        }
    }

    private static string GetIdKey(string id) => $"usuario-{id}";
    private static string GetEmailKey(string email) => $"usuario-email-{email.ToLower()}";
}

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Domain.Entity;
using Domain.Repository;
using Infra.Cache.Repository;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Tests;

public class CachedUsuarioRepositoryTests
{
    private readonly UsuarioRepositoryFake _repositoryRealFake;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CachedUsuarioRepository> _logger;
    private readonly CachedUsuarioRepository _cachedRepository;

    public CachedUsuarioRepositoryTests()
    {
        _repositoryRealFake = new UsuarioRepositoryFake();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<CachedUsuarioRepository>.Instance;

        _cachedRepository = new CachedUsuarioRepository(
            _repositoryRealFake,
            _memoryCache,
            _logger
        );
    }

    [Fact]
    public async Task GetById_CacheMissERecuperacaoPosteriorDeCacheHit()
    {
        // Arrange
        var userId = "user-123";
        var usuario = new Usuario("Maria", "maria@email.com") { Id = userId };
        _repositoryRealFake.UsuariosMap[userId] = usuario;

        // Act
        // Primeira chamada: Deve ser Cache Miss (chama repositório real)
        var result1 = await _cachedRepository.GetById(userId);
        
        // Segunda chamada: Deve ser Cache Hit (não chama repositório real)
        var result2 = await _cachedRepository.GetById(userId);

        // Assert
        Assert.NotNull(result1);
        Assert.Same(usuario, result1);
        Assert.Same(usuario, result2);
        Assert.Equal(1, _repositoryRealFake.GetByIdChamadas);
    }

    [Fact]
    public async Task GetByEmail_CacheMissERecuperacaoPosteriorDeCacheHit()
    {
        // Arrange
        var email = "maria@email.com";
        var usuario = new Usuario("Maria", email) { Id = "user-123" };
        _repositoryRealFake.UsuariosPorEmailMap[email] = usuario;

        // Act
        // Primeira chamada: Deve ser Cache Miss
        var result1 = await _cachedRepository.GetByEmail(email);
        
        // Segunda chamada: Deve ser Cache Hit
        var result2 = await _cachedRepository.GetByEmail(email);

        // Assert
        Assert.NotNull(result1);
        Assert.Same(usuario, result1);
        Assert.Same(usuario, result2);
        Assert.Equal(1, _repositoryRealFake.GetByEmailChamadas);
    }

    [Fact]
    public async Task Update_DeveInvalidarCacheDoUsuario()
    {
        // Arrange
        var userId = "user-123";
        var email = "maria@email.com";
        var usuario = new Usuario("Maria", email) { Id = userId };
        _repositoryRealFake.UsuariosMap[userId] = usuario;
        _repositoryRealFake.UsuariosPorEmailMap[email] = usuario;

        // Popula os caches de ID e Email
        await _cachedRepository.GetById(userId);
        await _cachedRepository.GetByEmail(email);

        // Act
        // Executa atualização (deve invalidar cache)
        usuario.AtualizarAvatar("avatar-05");
        await _cachedRepository.Update(usuario);

        // Chama novamente os métodos
        var getByIdResult = await _cachedRepository.GetById(userId);
        var getByEmailResult = await _cachedRepository.GetByEmail(email);

        // Assert
        Assert.Same(usuario, getByIdResult);
        Assert.Same(usuario, getByEmailResult);
        
        // Deve ter chamado o repositório real 2 vezes para cada método, já que o update limpou o cache
        Assert.Equal(2, _repositoryRealFake.GetByIdChamadas);
        Assert.Equal(2, _repositoryRealFake.GetByEmailChamadas);
    }

    [Fact]
    public async Task Delete_DeveInvalidarCacheDoUsuario()
    {
        // Arrange
        var userId = "user-123";
        var email = "maria@email.com";
        var usuario = new Usuario("Maria", email) { Id = userId };
        _repositoryRealFake.UsuariosMap[userId] = usuario;

        // Popula cache
        await _cachedRepository.GetById(userId);

        // Act
        await _cachedRepository.Delete(usuario);

        // Chama novamente
        await _cachedRepository.GetById(userId);

        // Assert
        Assert.Equal(2, _repositoryRealFake.GetByIdChamadas);
    }

    private sealed class UsuarioRepositoryFake : IUsuarioRepository
    {
        public Dictionary<string, Usuario> UsuariosMap { get; } = new();
        public Dictionary<string, Usuario> UsuariosPorEmailMap { get; } = new();
        
        public int GetByIdChamadas { get; private set; }
        public int GetByEmailChamadas { get; private set; }

        public Task<Usuario> GetById(string id)
        {
            GetByIdChamadas++;
            UsuariosMap.TryGetValue(id, out var user);
            return Task.FromResult(user!);
        }

        public Task<Usuario> GetByEmail(string email)
        {
            GetByEmailChamadas++;
            UsuariosPorEmailMap.TryGetValue(email, out var user);
            return Task.FromResult(user!);
        }

        public Task<Usuario> Update(Usuario entity)
        {
            return Task.FromResult(entity);
        }

        public Task<Usuario> Add(Usuario entity)
        {
            return Task.FromResult(entity);
        }

        public Task<List<Usuario>> Add(List<Usuario> entitys)
        {
            return Task.FromResult(entitys);
        }

        public Task Delete(Usuario entity)
        {
            return Task.CompletedTask;
        }

        public Task<List<Usuario>> GetByIds(List<string> ids) => throw new NotImplementedException();
        public Task<IEnumerable<Usuario>> GetWhere(Expression<Func<Usuario, bool>> filtro) => throw new NotImplementedException();
        public Task<List<string>> FiltrarUsuariosComNotificacaoAtiva(List<string> usuarioIds) => throw new NotImplementedException();
    }
}

#nullable enable

using System.Linq.Expressions;
using Application.Implementacoes;
using Application.Mcp.Models;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Enum;
using Domain.Login.Interfaces;
using Domain.Mcp.Enums;
using Domain.Repository;
using Xunit;

namespace Tests;

public sealed class McpApplicationMutationServicePhase3Tests
{
    [Fact]
    public async Task Category_delete_rechecks_links_and_returns_actionable_causal_rejection()
    {
        var category = new Categoria(
            "Moradia",
            TipoCategoria.Despesa,
            "owner-a")
        {
            Id = "category-a"
        };
        var repository = new CategoryRepositoryFake(category)
        {
            HasLinks = true
        };
        var service = new CategoriaService(
            repository,
            new LoggedUserFake("owner-a"));
        var command = new McpWriteCommand(
            McpWriteEntity.Category,
            McpPreviewAction.Delete,
            category.Id,
            new Dictionary<string, object?>(),
            new Dictionary<string, object?>
            {
                ["name"] = category.Nome,
                ["type"] = category.Tipo.ToString()
            });

        var result = await service.AplicarMutacaoMcpAsync(
            command,
            "operation-a",
            "result-hash-a");

        Assert.Equal(McpApplicationMutationState.Rejected, result.State);
        Assert.Equal("DELETE_BLOCKED_RELATIONSHIP", result.ErrorCode);
        Assert.Contains("Remova ou altere", result.Message);
        Assert.Equal(1, repository.LinkChecks);
        Assert.Equal(0, repository.DeleteAttempts);
        Assert.NotNull(await repository.GetById(category.Id));
    }

    private sealed class LoggedUserFake(string ownerId) : IUsuarioLogado
    {
        public string Id => ownerId;
        public Usuario Usuario => throw new NotSupportedException();
        public string IdContextoDados => ownerId;
        public Usuario UsuarioContextoDados => throw new NotSupportedException();
        public bool EmModoCompartilhado => false;
        public NivelPermissao? PermissaoAtual => null;
    }

    private sealed class CategoryRepositoryFake(Categoria category)
        : ICategoriaRepository
    {
        private Categoria? _category = category;

        public bool HasLinks { get; init; }
        public int LinkChecks { get; private set; }
        public int DeleteAttempts { get; private set; }

        public IQueryable<Categoria> GetCategorias() =>
            _category is null
                ? Array.Empty<Categoria>().AsQueryable()
                : new[] { _category }.AsQueryable();

        public bool CategoriaJaExiste(
            string nome,
            string idUsuario,
            TipoCategoria tipo) =>
            false;

        public Task<List<Categoria>> GetCategorias(
            TipoCategoria tipoCategoria,
            string nome,
            string idUsuario) =>
            Task.FromResult(GetCategorias().ToList());

        public Task<bool> CategoriaPossuiVinculo(Categoria categoria)
        {
            LinkChecks++;
            return Task.FromResult(HasLinks);
        }

        public Task<Categoria?> TryUpdateMcpAsync(
            string id,
            string userId,
            string expectedName,
            TipoCategoria expectedType,
            string proposedName,
            TipoCategoria proposedType,
            string operationId,
            string resultHash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Categoria?>(null);

        public Task<bool> TryDeleteMcpAsync(
            string id,
            string userId,
            string expectedName,
            TipoCategoria expectedType,
            CancellationToken cancellationToken = default)
        {
            DeleteAttempts++;
            _category = null;
            return Task.FromResult(true);
        }

        public Task<Categoria> Add(Categoria entity) =>
            Task.FromResult(_category = entity);

        public Task<List<Categoria>> Add(List<Categoria> entity) =>
            Task.FromResult(entity);

        public Task<Categoria> Update(Categoria entity) =>
            Task.FromResult(_category = entity);

        public Task Delete(Categoria entity)
        {
            _category = null;
            return Task.CompletedTask;
        }

        public Task<Categoria> GetById(string id) =>
            Task.FromResult(_category!);

        public Task<List<Categoria>> GetByIds(List<string> ids) =>
            Task.FromResult(GetCategorias().ToList());

        public Task<IEnumerable<Categoria>> GetWhere(
            Expression<Func<Categoria, bool>> filtro) =>
            Task.FromResult<IEnumerable<Categoria>>(
                GetCategorias().Where(filtro));
    }
}

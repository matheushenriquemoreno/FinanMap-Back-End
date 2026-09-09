using System.Linq.Expressions;
using Application.CompraPlanejada.DTOs;
using Application.CompraPlanejada.Service;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Login.Interfaces;
using Domain.Repository;
using Xunit;

namespace Tests;

public class CompraPlanejadaServiceTests
{
    [Fact]
    public async Task AdicionarEListar_MantemLinksETotalEstimado()
    {
        var usuario = new Usuario("Usuário Teste", "teste@finanmap.com") { Id = "usuario-1" };
        var repositorio = new CompraPlanejadaRepositoryFake();
        var service = new CompraPlanejadaService(repositorio, new UsuarioLogadoFake(usuario));

        var adicionar = await service.Adicionar(new CreateCompraPlanejadaDTO
        {
            Nome = "Notebook",
            ValorEstimado = 3599.90m,
            Prioridade = PrioridadeCompraPlanejada.Alta,
            Descricao = "Para trabalho",
            LinksLojas =
            [
                new() { Url = "https://loja-a.example/notebook", NomeLoja = "Loja A" },
                new() { Url = "https://loja-b.example/notebook", NomeLoja = "Loja B" }
            ]
        });

        var listar = await service.ListarPendentes();

        Assert.True(adicionar.IsSucess);
        Assert.True(listar.IsSucess);
        Assert.Single(listar.Value.Itens);
        Assert.Equal(3599.90m, listar.Value.TotalEstimado);
        Assert.Equal(2, listar.Value.Itens[0].LinksLojas.Count);
        Assert.Equal("Loja A", listar.Value.Itens[0].LinksLojas[0].NomeLoja);
    }

    [Fact]
    public async Task ListarPendentes_SomaValoresDecimaisSemPerda()
    {
        var usuario = new Usuario("Usuário Teste", "teste@finanmap.com") { Id = "usuario-1" };
        var repositorio = new CompraPlanejadaRepositoryFake();
        repositorio.Itens.AddRange(
        [
            new CompraPlanejada("usuario-1", "A", 0.10m, PrioridadeCompraPlanejada.Alta),
            new CompraPlanejada("usuario-1", "B", 0.20m, PrioridadeCompraPlanejada.Media)
        ]);
        var service = new CompraPlanejadaService(repositorio, new UsuarioLogadoFake(usuario));

        var resultado = await service.ListarPendentes();

        Assert.Equal(0.30m, resultado.Value.TotalEstimado);
    }

    [Fact]
    public async Task AdicionarComDadosInvalidos_NaoPersisteENaoConfirmaSucesso()
    {
        var usuario = new Usuario("Usuário Teste", "teste@finanmap.com") { Id = "usuario-1" };
        var repositorio = new CompraPlanejadaRepositoryFake();
        var service = new CompraPlanejadaService(repositorio, new UsuarioLogadoFake(usuario));

        var resultado = await service.Adicionar(new CreateCompraPlanejadaDTO
        {
            Nome = "Produto",
            ValorEstimado = 0,
            Prioridade = PrioridadeCompraPlanejada.Baixa
        });

        Assert.True(resultado.IsFailure);
        Assert.Empty(repositorio.Itens);
    }

    [Fact]
    public async Task AdicionarEmContextoSomenteVisualizacao_RetornaForbidden()
    {
        var usuario = new Usuario("Usuário Teste", "teste@finanmap.com") { Id = "usuario-2" };
        var repositorio = new CompraPlanejadaRepositoryFake();
        var service = new CompraPlanejadaService(
            repositorio,
            new UsuarioLogadoFake(usuario, "usuario-1", NivelPermissao.Visualizar));

        var resultado = await service.Adicionar(new CreateCompraPlanejadaDTO
        {
            Nome = "Produto",
            ValorEstimado = 10,
            Prioridade = PrioridadeCompraPlanejada.Baixa
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(TypeError.Forbidden, resultado.Error.GetType());
        Assert.Empty(repositorio.Itens);
    }

    private sealed class UsuarioLogadoFake(
        Usuario usuario,
        string? contexto = null,
        NivelPermissao? permissao = null) : IUsuarioLogado
    {
        public string Id => usuario.Id;
        public Usuario Usuario => usuario;
        public string IdContextoDados => contexto ?? usuario.Id;
        public Usuario UsuarioContextoDados => usuario;
        public bool EmModoCompartilhado => contexto is not null;
        public NivelPermissao? PermissaoAtual => permissao;
    }

    private sealed class CompraPlanejadaRepositoryFake : ICompraPlanejadaRepository
    {
        public List<CompraPlanejada> Itens { get; } = [];

        public Task<CompraPlanejada> Add(CompraPlanejada entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Id))
                entity.Id = Guid.NewGuid().ToString("N");

            Itens.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<List<CompraPlanejada>> Add(List<CompraPlanejada> entity)
        {
            Itens.AddRange(entity);
            return Task.FromResult(entity);
        }

        public Task Delete(CompraPlanejada entity)
        {
            Itens.RemoveAll(item => item.Id == entity.Id);
            return Task.CompletedTask;
        }

        public Task<CompraPlanejada> GetById(string id)
            => Task.FromResult(Itens.SingleOrDefault(item => item.Id == id)!);

        public Task<CompraPlanejada> GetById(string id, string usuarioId)
            => Task.FromResult(Itens.SingleOrDefault(item => item.Id == id && item.UsuarioId == usuarioId)!);

        public Task<List<CompraPlanejada>> GetByIds(List<string> ids)
            => Task.FromResult(Itens.Where(item => ids.Contains(item.Id)).ToList());

        public Task<IEnumerable<CompraPlanejada>> GetWhere(Expression<Func<CompraPlanejada, bool>> filtro)
            => Task.FromResult(Itens.AsQueryable().Where(filtro).AsEnumerable());

        public Task<CompraPlanejada> Update(CompraPlanejada entity)
            => Task.FromResult(entity);

        public Task<List<CompraPlanejada>> GetPendentes(string usuarioId)
            => Task.FromResult(Itens
                .Where(item => item.UsuarioId == usuarioId && item.Estado == EstadoCompraPlanejada.Pendente)
                .OrderByDescending(item => item.Prioridade)
                .ThenByDescending(item => item.DataCriacao)
                .ToList());

        public Task<List<CompraPlanejada>> GetComprados(string usuarioId)
            => Task.FromResult(Itens
                .Where(item => item.UsuarioId == usuarioId && item.Estado == EstadoCompraPlanejada.Comprado)
                .ToList());
    }
}

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

    [Fact]
    public async Task AtualizarPendente_AlteraCamposEPreservaIdentidadeEstadoEData()
    {
        var usuario = new Usuario("Usuário Teste", "teste@finanmap.com") { Id = "usuario-1" };
        var compra = new CompraPlanejada(
            "usuario-1", "Produto antigo", 10m, PrioridadeCompraPlanejada.Baixa, "Descrição antiga")
        {
            Id = "compra-1"
        };
        var dataCriacao = compra.DataCriacao;
        var repositorio = new CompraPlanejadaRepositoryFake(compra);
        var service = new CompraPlanejadaService(repositorio, new UsuarioLogadoFake(usuario));

        var resultado = await service.Atualizar(new UpdateCompraPlanejadaDTO
        {
            Id = "compra-1",
            Nome = "Produto novo",
            ValorEstimado = 25.50m,
            Prioridade = PrioridadeCompraPlanejada.Alta,
            Descricao = "Descrição nova",
            LinksLojas =
            [
                new() { Url = "https://loja.example/produto", NomeLoja = "Loja" }
            ]
        });

        Assert.True(resultado.IsSucess);
        Assert.Equal("compra-1", compra.Id);
        Assert.Equal(EstadoCompraPlanejada.Pendente, compra.Estado);
        Assert.Equal(dataCriacao, compra.DataCriacao);
        Assert.Equal("Produto novo", compra.Nome);
        Assert.Equal(25.50m, compra.ValorEstimado);
        Assert.Single(compra.LinksLojas);
    }

    [Fact]
    public async Task AtualizarComDadosInvalidos_NaoMudaOItem()
    {
        var usuario = new Usuario("Usuário Teste", "teste@finanmap.com") { Id = "usuario-1" };
        var compra = new CompraPlanejada(
            "usuario-1", "Produto", 10m, PrioridadeCompraPlanejada.Media)
        {
            Id = "compra-1"
        };
        var repositorio = new CompraPlanejadaRepositoryFake(compra);
        var service = new CompraPlanejadaService(repositorio, new UsuarioLogadoFake(usuario));

        var resultado = await service.Atualizar(new UpdateCompraPlanejadaDTO
        {
            Id = "compra-1",
            Nome = "",
            ValorEstimado = 99m,
            Prioridade = PrioridadeCompraPlanejada.Alta
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal("Produto", compra.Nome);
        Assert.Equal(10m, compra.ValorEstimado);
        Assert.Equal(PrioridadeCompraPlanejada.Media, compra.Prioridade);
    }

    [Fact]
    public async Task AtualizarComLinkInvalido_NaoMudaOItemNemConfirmaSucesso()
    {
        var usuario = new Usuario("Usuário Teste", "teste@finanmap.com") { Id = "usuario-1" };
        var compra = new CompraPlanejada(
            "usuario-1", "Produto", 10m, PrioridadeCompraPlanejada.Media)
        {
            Id = "compra-1"
        };
        var repositorio = new CompraPlanejadaRepositoryFake(compra);
        var service = new CompraPlanejadaService(repositorio, new UsuarioLogadoFake(usuario));

        var resultado = await service.Atualizar(new UpdateCompraPlanejadaDTO
        {
            Id = "compra-1",
            Nome = "Produto novo",
            ValorEstimado = 99m,
            Prioridade = PrioridadeCompraPlanejada.Alta,
            LinksLojas =
            [
                new() { Url = "ftp://loja.example/produto", NomeLoja = "Loja" }
            ]
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal("Produto", compra.Nome);
        Assert.Equal(10m, compra.ValorEstimado);
        Assert.Equal(PrioridadeCompraPlanejada.Media, compra.Prioridade);
    }

    [Fact]
    public async Task AtualizarItemDeOutroContexto_RetornaNotFound()
    {
        var usuario = new Usuario("Usuário Teste", "teste@finanmap.com") { Id = "usuario-1" };
        var repositorio = new CompraPlanejadaRepositoryFake(new CompraPlanejada(
            "usuario-2", "Produto", 10m, PrioridadeCompraPlanejada.Media)
        {
            Id = "compra-2"
        });
        var service = new CompraPlanejadaService(repositorio, new UsuarioLogadoFake(usuario));

        var resultado = await service.Atualizar(new UpdateCompraPlanejadaDTO
        {
            Id = "compra-2",
            Nome = "Alteração",
            ValorEstimado = 10m,
            Prioridade = PrioridadeCompraPlanejada.Media
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(TypeError.NotFound, resultado.Error.GetType());
    }

    [Fact]
    public async Task ExcluirPendente_RemoveSomenteOAlvoEAtualizaTotal()
    {
        var usuario = new Usuario("Usuário Teste", "teste@finanmap.com") { Id = "usuario-1" };
        var alvo = new CompraPlanejada("usuario-1", "Alvo", 10m, PrioridadeCompraPlanejada.Media)
        {
            Id = "compra-1"
        };
        var outro = new CompraPlanejada("usuario-1", "Outro", 20m, PrioridadeCompraPlanejada.Baixa)
        {
            Id = "compra-2"
        };
        var repositorio = new CompraPlanejadaRepositoryFake(alvo, outro);
        var service = new CompraPlanejadaService(repositorio, new UsuarioLogadoFake(usuario));

        var resultado = await service.Excluir("compra-1");
        var lista = await service.ListarPendentes();

        Assert.True(resultado.IsSucess);
        Assert.Single(lista.Value.Itens);
        Assert.Equal("compra-2", lista.Value.Itens[0].Id);
        Assert.Equal(20m, lista.Value.TotalEstimado);
    }

    [Fact]
    public async Task ExcluirPendenteInexistenteOuDeOutroContexto_RetornaNotFound()
    {
        var usuario = new Usuario("Usuário Teste", "teste@finanmap.com") { Id = "usuario-1" };
        var repositorio = new CompraPlanejadaRepositoryFake(new CompraPlanejada(
            "usuario-2", "Alheio", 10m, PrioridadeCompraPlanejada.Media)
        {
            Id = "compra-2"
        });
        var service = new CompraPlanejadaService(repositorio, new UsuarioLogadoFake(usuario));

        var resultado = await service.Excluir("compra-2");

        Assert.True(resultado.IsFailure);
        Assert.Equal(TypeError.NotFound, resultado.Error.GetType());
        Assert.Single(repositorio.Itens);
    }

    [Fact]
    public async Task ExcluirPendenteRepetido_RetornaNotFound()
    {
        var usuario = new Usuario("Usuário Teste", "teste@finanmap.com") { Id = "usuario-1" };
        var repositorio = new CompraPlanejadaRepositoryFake(new CompraPlanejada(
            "usuario-1", "Produto", 10m, PrioridadeCompraPlanejada.Media)
        {
            Id = "compra-1"
        });
        var service = new CompraPlanejadaService(repositorio, new UsuarioLogadoFake(usuario));

        await service.Excluir("compra-1");
        var resultado = await service.Excluir("compra-1");

        Assert.True(resultado.IsFailure);
        Assert.Equal(TypeError.NotFound, resultado.Error.GetType());
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

        public CompraPlanejadaRepositoryFake(params CompraPlanejada[] itens)
        {
            Itens.AddRange(itens);
        }

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

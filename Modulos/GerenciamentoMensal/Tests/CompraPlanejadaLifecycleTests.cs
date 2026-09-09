using System.Linq.Expressions;
using Application.CompraPlanejada.DTOs;
using Application.CompraPlanejada.Service;
using Application.DTOs;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Login.Interfaces;
using Domain.Repository;
using Xunit;

namespace Tests;

public class CompraPlanejadaLifecycleTests
{
    [Fact]
    public async Task ConcluirSemDespesa_MoveParaCompradosEPreservaComparativo()
    {
        var compra = CriarCompra("compra-1", 100m);
        var repositorio = new CompraPlanejadaLifecycleRepository(compra);
        var service = CriarService(repositorio);

        var resultado = await service.Concluir("compra-1", new ConcluirCompraPlanejadaDTO
        {
            ValorReal = 87.50m,
            DataCompra = DateTime.UtcNow.Date.AddDays(-1)
        });

        var pendentes = await service.ListarPendentes();
        var comprados = await service.ListarComprados();

        Assert.True(resultado.IsSucess);
        Assert.Empty(pendentes.Value.Itens);
        Assert.Single(comprados.Value.Itens);
        Assert.Equal(100m, comprados.Value.Itens[0].ValorEstimado);
        Assert.Equal(87.50m, comprados.Value.Itens[0].ValorReal);
        Assert.Equal(87.50m, comprados.Value.TotalReal);
        Assert.Null(comprados.Value.Itens[0].DespesaId);
    }

    [Fact]
    public async Task ConcluirComDadosInvalidos_NaoMudaItemNemCriaDespesa()
    {
        var compra = CriarCompra("compra-1", 100m);
        var repositorio = new CompraPlanejadaLifecycleRepository(compra);
        var despesas = new CompraPlanejadaDespesaGatewayFake();
        var service = CriarService(repositorio, despesas);

        var resultado = await service.Concluir("compra-1", new ConcluirCompraPlanejadaDTO
        {
            ValorReal = 0,
            DataCompra = DateTime.UtcNow.Date.AddDays(1),
            CriarDespesa = true,
            Ano = DateTime.UtcNow.Year,
            Mes = DateTime.UtcNow.Month,
            CategoriaId = "categoria-1"
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(TypeError.Validation, resultado.Error.GetType());
        Assert.Equal(EstadoCompraPlanejada.Pendente, compra.Estado);
        Assert.Null(compra.ValorReal);
        Assert.Empty(despesas.Criadas);
    }

    [Fact]
    public async Task ConcluirRepetido_RetornaValidationESemSegundoVinculo()
    {
        var compra = CriarCompra("compra-1", 100m);
        var repositorio = new CompraPlanejadaLifecycleRepository(compra);
        var service = CriarService(repositorio);

        Assert.True((await service.Concluir("compra-1", ConclusaoValida())).IsSucess);
        var repeticao = await service.Concluir("compra-1", ConclusaoValida());

        Assert.True(repeticao.IsFailure);
        Assert.Equal(TypeError.Validation, repeticao.Error.GetType());
        Assert.Single((await service.ListarComprados()).Value.Itens);
    }

    [Fact]
    public async Task ConcluirComDespesa_UsaValorRealEVinculaUmaUnicaDespesa()
    {
        var compra = CriarCompra("compra-1", 100m);
        var repositorio = new CompraPlanejadaLifecycleRepository(compra);
        var despesas = new CompraPlanejadaDespesaGatewayFake();
        var service = CriarService(repositorio, despesas);

        var resultado = await service.Concluir("compra-1", new ConcluirCompraPlanejadaDTO
        {
            ValorReal = 92.30m,
            DataCompra = DateTime.UtcNow.Date,
            CriarDespesa = true,
            Ano = 2026,
            Mes = 9,
            CategoriaId = "categoria-1"
        });

        Assert.True(resultado.IsSucess);
        Assert.Single(despesas.Criadas);
        Assert.Equal(92.30m, despesas.Criadas[0].Valor);
        Assert.Equal(9, despesas.Criadas[0].Mes);
        Assert.Equal(2026, despesas.Criadas[0].Ano);
        Assert.Equal("categoria-1", despesas.Criadas[0].CategoriaId);
        Assert.Equal("despesa-1", compra.DespesaId);
    }

    [Fact]
    public async Task FalhaAoPersistirVinculo_CompensaDespesaEDeixaPendente()
    {
        var compra = CriarCompra("compra-1", 100m);
        var repositorio = new CompraPlanejadaLifecycleRepository(compra) { FalharAtualizacao = true };
        var despesas = new CompraPlanejadaDespesaGatewayFake();
        var service = CriarService(repositorio, despesas);

        var resultado = await service.Concluir("compra-1", ConclusaoValida(criarDespesa: true));

        Assert.True(resultado.IsFailure);
        Assert.Equal(TypeError.NotFound, resultado.Error.GetType());
        Assert.Equal(EstadoCompraPlanejada.Pendente, compra.Estado);
        Assert.Null(compra.DespesaId);
        Assert.Equal(["despesa-1"], despesas.Excluidas);
    }

    [Fact]
    public async Task ListarComprados_IsolaContextoESomaValoresDecimais()
    {
        var primeiro = CriarCompra("compra-1", 10.10m);
        primeiro.MarcarComoComprado(9.90m, DateTime.UtcNow.Date);
        var segundo = CriarCompra("compra-2", 20.20m);
        segundo.MarcarComoComprado(18.80m, DateTime.UtcNow.Date);
        var alheio = CriarCompra("compra-3", 99m, "usuario-2");
        alheio.MarcarComoComprado(98m, DateTime.UtcNow.Date);
        var service = CriarService(new CompraPlanejadaLifecycleRepository(primeiro, segundo, alheio));

        var resultado = await service.ListarComprados();

        Assert.Equal(30.30m, resultado.Value.TotalEstimado);
        Assert.Equal(28.70m, resultado.Value.TotalReal);
        Assert.Equal(["compra-1", "compra-2"], resultado.Value.Itens.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task ReverterSemDespesa_DevolveParaPendentesELimpaDadosReais()
    {
        var compra = CriarCompra("compra-1", 100m);
        compra.MarcarComoComprado(87m, DateTime.UtcNow.Date);
        var service = CriarService(new CompraPlanejadaLifecycleRepository(compra));

        var resultado = await service.Reverter("compra-1", new ReverterCompraPlanejadaDTO());

        Assert.True(resultado.IsSucess);
        Assert.Equal(EstadoCompraPlanejada.Pendente, compra.Estado);
        Assert.Equal(100m, compra.ValorEstimado);
        Assert.Null(compra.ValorReal);
        Assert.Null(compra.DataCompra);
        Assert.Single((await service.ListarPendentes()).Value.Itens);
        Assert.Empty((await service.ListarComprados()).Value.Itens);
    }

    [Fact]
    public async Task ReverterComDespesaPreservada_RemoveSomenteOVinculo()
    {
        var compra = CriarCompra("compra-1", 100m);
        compra.MarcarComoComprado(87m, DateTime.UtcNow.Date);
        compra.VincularDespesa("despesa-1");
        var despesas = new CompraPlanejadaDespesaGatewayFake();
        var service = CriarService(new CompraPlanejadaLifecycleRepository(compra), despesas);

        var resultado = await service.Reverter("compra-1", new ReverterCompraPlanejadaDTO
        {
            ExcluirDespesa = false
        });

        Assert.True(resultado.IsSucess);
        Assert.Null(compra.DespesaId);
        Assert.Empty(despesas.Excluidas);
    }

    [Fact]
    public async Task ReverterComDespesaExcluida_ExcluiDespesaEVinculo()
    {
        var compra = CriarCompra("compra-1", 100m);
        compra.MarcarComoComprado(87m, DateTime.UtcNow.Date);
        compra.VincularDespesa("despesa-1");
        var despesas = new CompraPlanejadaDespesaGatewayFake();
        var service = CriarService(new CompraPlanejadaLifecycleRepository(compra), despesas);

        var resultado = await service.Reverter("compra-1", new ReverterCompraPlanejadaDTO
        {
            ExcluirDespesa = true
        });

        Assert.True(resultado.IsSucess);
        Assert.Equal(EstadoCompraPlanejada.Pendente, compra.Estado);
        Assert.Equal(["despesa-1"], despesas.Excluidas);
    }

    [Fact]
    public async Task FalhaAoExcluirDespesaNaReversao_RestauraCompraEVinculo()
    {
        var compra = CriarCompra("compra-1", 100m);
        compra.MarcarComoComprado(87m, DateTime.UtcNow.Date);
        compra.VincularDespesa("despesa-1");
        var despesas = new CompraPlanejadaDespesaGatewayFake
        {
            ResultadoExclusao = Result.Failure(Error.NotFound("Despesa não encontrada."))
        };
        var service = CriarService(new CompraPlanejadaLifecycleRepository(compra), despesas);

        var resultado = await service.Reverter("compra-1", new ReverterCompraPlanejadaDTO
        {
            ExcluirDespesa = true
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(EstadoCompraPlanejada.Comprado, compra.Estado);
        Assert.Equal(87m, compra.ValorReal);
        Assert.Equal("despesa-1", compra.DespesaId);
    }

    [Fact]
    public async Task ExcluirComprado_RemoveItemEMantemDespesaSemApagaLa()
    {
        var compra = CriarCompra("compra-1", 100m);
        compra.MarcarComoComprado(87m, DateTime.UtcNow.Date);
        compra.VincularDespesa("despesa-1");
        var repositorio = new CompraPlanejadaLifecycleRepository(compra);
        var despesas = new CompraPlanejadaDespesaGatewayFake();
        var service = CriarService(repositorio, despesas);

        var resultado = await service.Excluir("compra-1");

        Assert.True(resultado.IsSucess);
        Assert.Empty(repositorio.Itens);
        Assert.Empty(despesas.Excluidas);
    }

    [Fact]
    public async Task Visualizador_LeContextoMasNaoExecutaMutacoesDoCiclo()
    {
        var compra = CriarCompra("compra-1", 100m);
        compra.MarcarComoComprado(87m, DateTime.UtcNow.Date);
        var repositorio = new CompraPlanejadaLifecycleRepository(compra);
        var usuario = new Usuario("Visualizador", "visualizador@finanmap.com") { Id = "usuario-2" };
        var service = new CompraPlanejadaService(
            repositorio,
            new UsuarioLogadoFake(usuario, "usuario-1", NivelPermissao.Visualizar));

        var pendentes = await service.ListarPendentes();
        var comprados = await service.ListarComprados();
        var concluir = await service.Concluir("compra-1", ConclusaoValida());
        var reverter = await service.Reverter("compra-1", new ReverterCompraPlanejadaDTO());
        var excluir = await service.Excluir("compra-1");

        Assert.True(pendentes.IsSucess);
        Assert.Empty(pendentes.Value.Itens);
        Assert.True(comprados.IsSucess);
        Assert.Single(comprados.Value.Itens);
        Assert.Equal(TypeError.Forbidden, concluir.Error.GetType());
        Assert.Equal(TypeError.Forbidden, reverter.Error.GetType());
        Assert.Equal(TypeError.Forbidden, excluir.Error.GetType());
        Assert.Single(repositorio.Itens);
    }

    [Fact]
    public async Task CentenasDeItens_MantemSeparacaoEAgregadosExatos()
    {
        var itens = Enumerable.Range(1, 400)
            .Select(indice => CriarCompra(
                $"compra-{indice}",
                indice / 100m))
            .ToArray();

        foreach (var item in itens.Where((_, indice) => indice % 2 == 1))
            item.MarcarComoComprado(item.ValorEstimado - 0.01m, DateTime.UtcNow.Date);

        var service = CriarService(new CompraPlanejadaLifecycleRepository(itens));
        var pendentes = await service.ListarPendentes();
        var comprados = await service.ListarComprados();

        Assert.Equal(200, pendentes.Value.Itens.Count);
        Assert.Equal(200, comprados.Value.Itens.Count);
        Assert.Equal(
            itens.Where((_, indice) => indice % 2 == 0).Sum(item => item.ValorEstimado),
            pendentes.Value.TotalEstimado);
        Assert.Equal(
            itens.Where((_, indice) => indice % 2 == 1).Sum(item => item.ValorEstimado),
            comprados.Value.TotalEstimado);
        Assert.Equal(
            itens.Where((_, indice) => indice % 2 == 1).Sum(item => item.ValorEstimado - 0.01m),
            comprados.Value.TotalReal);
    }

    private static CompraPlanejadaService CriarService(
        CompraPlanejadaLifecycleRepository repositorio,
        ICompraPlanejadaDespesaGateway despesas = null)
    {
        var usuario = new Usuario("Usuário Teste", "teste@finanmap.com") { Id = "usuario-1" };
        return new CompraPlanejadaService(
            repositorio,
            new UsuarioLogadoFake(usuario),
            despesas);
    }

    private static CompraPlanejada CriarCompra(
        string id,
        decimal valorEstimado,
        string usuarioId = "usuario-1")
        => new(usuarioId, $"Compra {id}", valorEstimado, PrioridadeCompraPlanejada.Media)
        {
            Id = id
        };

    private static ConcluirCompraPlanejadaDTO ConclusaoValida(bool criarDespesa = false)
        => new()
        {
            ValorReal = 90m,
            DataCompra = DateTime.UtcNow.Date,
            CriarDespesa = criarDespesa,
            Ano = 2026,
            Mes = 9,
            CategoriaId = "categoria-1"
        };

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

    private sealed class CompraPlanejadaLifecycleRepository(params CompraPlanejada[] itens)
        : ICompraPlanejadaRepository
    {
        public List<CompraPlanejada> Itens { get; } = [.. itens];
        public bool FalharAtualizacao { get; set; }

        public Task<CompraPlanejada> Add(CompraPlanejada entity)
        {
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

        public Task<bool> AtualizarSePendente(CompraPlanejada entity)
            => AtualizarSeEstado(entity, EstadoCompraPlanejada.Pendente);

        public Task<bool> AtualizarSeEstado(CompraPlanejada entity, EstadoCompraPlanejada estadoEsperado)
            => Task.FromResult(!FalharAtualizacao && Itens.Any(item =>
                item.Id == entity.Id && item.UsuarioId == entity.UsuarioId));

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

    private sealed class CompraPlanejadaDespesaGatewayFake : ICompraPlanejadaDespesaGateway
    {
        public List<CreateDespesaDTO> Criadas { get; } = [];
        public List<string> Excluidas { get; } = [];
        public Result<string> ResultadoCriacao { get; set; } = Result.Success("despesa-1");
        public Result ResultadoExclusao { get; set; } = Result.Success();

        public Task<Result<string>> Criar(CreateDespesaDTO dto)
        {
            Criadas.Add(dto);
            return Task.FromResult(ResultadoCriacao);
        }

        public Task<Result> Excluir(string id)
        {
            Excluidas.Add(id);
            return Task.FromResult(ResultadoExclusao);
        }
    }
}

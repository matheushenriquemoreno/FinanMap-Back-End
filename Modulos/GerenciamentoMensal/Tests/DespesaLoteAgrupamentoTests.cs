using System.Linq.Expressions;
using Application.DTOs;
using Application.Services;
using Domain;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Enum;
using Domain.Enums;
using Domain.Login.Interfaces;
using Domain.Relatorios.AcumuladoMensal;
using Domain.Relatorios.Entity;
using Domain.Repository;
using Xunit;

namespace Tests;

public class DespesaLoteAgrupamentoTests
{
    [Fact]
    public async Task AtualizarDespesaEmLote_AgrupadoraComValorBase_SomaFilhaSemSobrescreverBase()
    {
        var contexto = CriarContextoParcelado();
        var agrupadora = contexto.CriarAgrupadora("cartao-1", 2026, 1, 3000);
        var service = contexto.CriarService();

        var resultado = await service.AtualizarDespesaEmLoteAsync("parcela-1", new AtualizarLoteDespesaDTO
        {
            NovoValor = 1000,
            NovaDescricao = "Compra",
            NovaCategoriaId = contexto.CategoriaDespesa.Id,
            IdDespesaAgrupadora = agrupadora.Id,
            Modificador = ModificadorLote.ApenasEsta
        });

        Assert.True(resultado.IsSucess);
        Assert.Equal(4000, contexto.Repository.Get(agrupadora.Id).Valor);
    }

    [Fact]
    public async Task AtualizarDespesaEmLote_ApenasEsta_AgrupaSomenteDespesaAlvo()
    {
        var contexto = CriarContextoParcelado();
        var agrupadora = contexto.CriarAgrupadora("cartao-1", 2026, 1, 3000);
        var service = contexto.CriarService();

        var resultado = await service.AtualizarDespesaEmLoteAsync("parcela-1", new AtualizarLoteDespesaDTO
        {
            NovoValor = 90,
            NovaDescricao = "Compra",
            NovaCategoriaId = contexto.CategoriaDespesa.Id,
            IdDespesaAgrupadora = agrupadora.Id,
            Modificador = ModificadorLote.ApenasEsta
        });

        Assert.True(resultado.IsSucess);
        Assert.Equal(agrupadora.Id, contexto.Repository.Get("parcela-1").IdDespesaAgrupadora);
        Assert.Null(contexto.Repository.Get("parcela-2").IdDespesaAgrupadora);
        Assert.Null(contexto.Repository.Get("parcela-3").IdDespesaAgrupadora);
        Assert.Equal(3090, contexto.Repository.Get(agrupadora.Id).Valor);
    }

    [Fact]
    public async Task AtualizarDespesaEmLote_EstaEProximas_AgrupaAlvoEParcelasFuturas()
    {
        var contexto = CriarContextoParcelado();
        var agrupadora = contexto.CriarAgrupadora("cartao-1", 2026, 1);
        var service = contexto.CriarService();

        var resultado = await service.AtualizarDespesaEmLoteAsync("parcela-2", new AtualizarLoteDespesaDTO
        {
            NovoValor = 70,
            NovaDescricao = "Compra",
            NovaCategoriaId = contexto.CategoriaDespesa.Id,
            IdDespesaAgrupadora = agrupadora.Id,
            Modificador = ModificadorLote.EstaEProximas
        });

        Assert.True(resultado.IsSucess);
        Assert.Null(contexto.Repository.Get("parcela-1").IdDespesaAgrupadora);
        Assert.NotNull(contexto.Repository.Get("parcela-2").IdDespesaAgrupadora);
        Assert.NotNull(contexto.Repository.Get("parcela-3").IdDespesaAgrupadora);
        Assert.Equal(70, contexto.Repository.Get("parcela-2").Agrupadora.Valor);
        Assert.Equal(70, contexto.Repository.Get("parcela-3").Agrupadora.Valor);
    }

    [Fact]
    public async Task AtualizarDespesaEmLote_TodasDoLote_AgrupaTodasAsParcelas()
    {
        var contexto = CriarContextoParcelado();
        var agrupadora = contexto.CriarAgrupadora("cartao-1", 2026, 1);
        var service = contexto.CriarService();

        var resultado = await service.AtualizarDespesaEmLoteAsync("parcela-2", new AtualizarLoteDespesaDTO
        {
            NovoValor = 50,
            NovaDescricao = "Compra",
            NovaCategoriaId = contexto.CategoriaDespesa.Id,
            IdDespesaAgrupadora = agrupadora.Id,
            Modificador = ModificadorLote.TodasDoLote
        });

        Assert.True(resultado.IsSucess);
        Assert.All(contexto.Parcelas, despesa => Assert.NotNull(despesa.IdDespesaAgrupadora));
        Assert.All(contexto.Parcelas, despesa => Assert.Equal(50, despesa.Agrupadora.Valor));
    }

    [Fact]
    public async Task AtualizarDespesaEmLote_TrocaAgrupadora_RecalculaAgrupadorasAntigaENova()
    {
        var contexto = CriarContextoParcelado();
        var agrupadoraAntiga = contexto.CriarAgrupadora("cartao-antigo", 2026, 1, 3000);
        var agrupadoraNova = contexto.CriarAgrupadora("cartao-novo", 2026, 1, 2000);
        contexto.Vincular(contexto.Repository.Get("parcela-1"), agrupadoraAntiga);
        contexto.Vincular(contexto.Repository.Get("parcela-2"), agrupadoraAntiga);
        contexto.Vincular(contexto.Repository.Get("parcela-3"), agrupadoraAntiga);
        var service = contexto.CriarService();

        var resultado = await service.AtualizarDespesaEmLoteAsync("parcela-1", new AtualizarLoteDespesaDTO
        {
            NovoValor = 40,
            NovaDescricao = "Compra",
            NovaCategoriaId = contexto.CategoriaDespesa.Id,
            IdDespesaAgrupadora = agrupadoraNova.Id,
            Modificador = ModificadorLote.TodasDoLote
        });

        Assert.True(resultado.IsSucess);
        Assert.All(contexto.Parcelas, despesa => Assert.NotEqual(agrupadoraAntiga.Id, despesa.IdDespesaAgrupadora));
        Assert.Equal(agrupadoraNova.Id, contexto.Repository.Get("parcela-1").IdDespesaAgrupadora);
        Assert.Equal(3000, contexto.Repository.Get(agrupadoraAntiga.Id).Valor);
        Assert.Equal(2040, contexto.Repository.Get("parcela-1").Agrupadora.Valor);
    }

    [Fact]
    public async Task AtualizarDespesaEmLote_RemoveAgrupamento_RecalculaAgrupadora()
    {
        var contexto = CriarContextoParcelado();
        var agrupadora = contexto.CriarAgrupadora("cartao-1", 2026, 1, 3000);
        contexto.Vincular(contexto.Repository.Get("parcela-1"), agrupadora);
        contexto.Vincular(contexto.Repository.Get("parcela-2"), agrupadora);
        contexto.Vincular(contexto.Repository.Get("parcela-3"), agrupadora);
        var service = contexto.CriarService();

        var resultado = await service.AtualizarDespesaEmLoteAsync("parcela-1", new AtualizarLoteDespesaDTO
        {
            NovoValor = 30,
            NovaDescricao = "Compra",
            NovaCategoriaId = contexto.CategoriaDespesa.Id,
            IdDespesaAgrupadora = string.Empty,
            Modificador = ModificadorLote.TodasDoLote
        });

        Assert.True(resultado.IsSucess);
        Assert.All(contexto.Parcelas, despesa => Assert.Null(despesa.IdDespesaAgrupadora));
        Assert.Equal(3000, contexto.Repository.Get(agrupadora.Id).Valor);
        Assert.False(contexto.Repository.Get(agrupadora.Id).EhAgrupadora());
    }

    [Fact]
    public async Task AtualizarDespesaEmLote_ValorAumentaOuDiminui_RecalculaAgrupadora()
    {
        var contexto = CriarContextoParcelado();
        var agrupadora = contexto.CriarAgrupadora("cartao-1", 2026, 1, 3000);
        contexto.Vincular(contexto.Repository.Get("parcela-1"), agrupadora);
        var service = contexto.CriarService();

        var resultadoAumentar = await service.AtualizarDespesaEmLoteAsync("parcela-1", new AtualizarLoteDespesaDTO
        {
            NovoValor = 120,
            NovaDescricao = "Compra",
            NovaCategoriaId = contexto.CategoriaDespesa.Id,
            IdDespesaAgrupadora = agrupadora.Id,
            Modificador = ModificadorLote.ApenasEsta
        });

        var resultadoDiminuir = await service.AtualizarDespesaEmLoteAsync("parcela-1", new AtualizarLoteDespesaDTO
        {
            NovoValor = 25,
            NovaDescricao = "Compra",
            NovaCategoriaId = contexto.CategoriaDespesa.Id,
            IdDespesaAgrupadora = agrupadora.Id,
            Modificador = ModificadorLote.ApenasEsta
        });

        Assert.True(resultadoAumentar.IsSucess);
        Assert.True(resultadoDiminuir.IsSucess);
        Assert.Equal(3025, contexto.Repository.Get(agrupadora.Id).Valor);
    }

    [Fact]
    public async Task ExcluirDespesaEmLote_ApenasEsta_RemoveFilhaEMantemValorBaseDaAgrupadora()
    {
        var contexto = CriarContextoParcelado();
        var agrupadora = contexto.CriarAgrupadora("cartao-1", 2026, 1, 3000);
        contexto.Vincular(contexto.Repository.Get("parcela-1"), agrupadora);
        var service = contexto.CriarService();

        var resultado = await service.ExcluirDespesaEmLoteAsync("parcela-1", ModificadorLote.ApenasEsta);

        Assert.True(resultado.IsSucess);
        Assert.Equal(3000, contexto.Repository.Get(agrupadora.Id).Valor);
        Assert.False(contexto.Repository.Get(agrupadora.Id).EhAgrupadora());
    }

    private static TestContext CriarContextoParcelado()
    {
        var usuario = new Usuario("Usuario Teste", "usuario@teste.com") { Id = "usuario-1" };
        var categoria = new Categoria("Compras", TipoCategoria.Despesa, usuario.Id) { Id = "categoria-despesa" };
        var repository = new DespesaRepositoryFake();

        for (var i = 1; i <= 3; i++)
        {
            repository.Despesas.Add(new Despesa(2026, i, "Compra", 100, categoria, usuario)
            {
                Id = $"parcela-{i}",
                DespesaOrigemId = "lote-1",
                IsParcelado = true,
                ParcelaAtual = i,
                TotalParcelas = 3
            });
        }

        return new TestContext(
            usuario,
            categoria,
            repository,
            new CategoriaRepositoryFake(categoria),
            new AcumuladoMensalReportRepositoryFake());
    }

    private sealed class TestContext(
        Usuario usuario,
        Categoria categoriaDespesa,
        DespesaRepositoryFake repository,
        CategoriaRepositoryFake categoriaRepository,
        AcumuladoMensalReportRepositoryFake reportRepository)
    {
        public Categoria CategoriaDespesa => categoriaDespesa;
        public DespesaRepositoryFake Repository => repository;
        public List<Despesa> Parcelas => repository.Despesas.Where(x => x.DespesaOrigemId == "lote-1").OrderBy(x => x.ParcelaAtual).ToList();

        public DespesaService CriarService() =>
            new(repository, categoriaRepository, reportRepository, new UsuarioLogadoFake(usuario));

        public Despesa CriarAgrupadora(string id, int ano, int mes, decimal valor = 0)
        {
            var agrupadora = new Despesa(ano, mes, "Cartao", valor, categoriaDespesa, usuario)
            {
                Id = id
            };
            repository.Despesas.Add(agrupadora);
            return agrupadora;
        }

        public void Vincular(Despesa despesa, Despesa agrupadora)
        {
            despesa.AdicionarDespesaAgrupadora(agrupadora);
            agrupadora.MarcarDespesaComoAgrupadora();
            agrupadora.AtualizarValor(agrupadora.Valor + despesa.Valor);
        }
    }

    private sealed class UsuarioLogadoFake(Usuario usuario) : IUsuarioLogado
    {
        public string Id => usuario.Id;
        public Usuario Usuario => usuario;
        public string IdContextoDados => usuario.Id;
        public Usuario UsuarioContextoDados => usuario;
        public bool EmModoCompartilhado => false;
        public NivelPermissao? PermissaoAtual => null;
    }

    private sealed class DespesaRepositoryFake : IDespesaRepository
    {
        public List<Despesa> Despesas { get; } = [];

        public Despesa Get(string id) => Despesas.Single(x => x.Id == id);

        public Task<Despesa> Add(Despesa entity)
        {
            if (string.IsNullOrEmpty(entity.Id))
                entity.Id = $"despesa-{Despesas.Count + 1}";

            Despesas.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<List<Despesa>> Add(List<Despesa> entity)
        {
            Despesas.AddRange(entity);
            return Task.FromResult(entity);
        }

        public Task<Despesa> Update(Despesa entity) => Task.FromResult(entity);

        public Task Delete(Despesa entity)
        {
            Despesas.Remove(entity);
            return Task.CompletedTask;
        }

        public Task<Despesa> GetById(string id)
        {
            var despesa = Despesas.FirstOrDefault(x => x.Id == id);
            if (despesa?.EstaAgrupada() == true)
                despesa.Agrupadora = Despesas.FirstOrDefault(x => x.Id == despesa.IdDespesaAgrupadora);

            return Task.FromResult(despesa!);
        }

        public Task<List<Despesa>> GetByIds(List<string> ids) =>
            Task.FromResult(Despesas.Where(x => ids.Contains(x.Id)).ToList());

        public Task<IEnumerable<Despesa>> GetWhere(Expression<Func<Despesa, bool>> filtro) =>
            Task.FromResult(Despesas.Where(filtro.Compile()).AsEnumerable());

        public Task<IEnumerable<Despesa>> ObterPeloMes(int mes, int ano, string usuarioId) =>
            Task.FromResult(Despesas.Where(x => x.Mes == mes && x.Ano == ano && x.UsuarioId == usuarioId).AsEnumerable());

        public Task<decimal> GetValorTotalDespesasDaAgrupadora(string idDespesaAgrupadora) =>
            Task.FromResult(Despesas.Where(x => x.IdDespesaAgrupadora == idDespesaAgrupadora).Sum(x => x.Valor));

        public Task<IEnumerable<Despesa>> GetPeloMes(int mes, int ano, string usuarioId, string descricao) =>
            Task.FromResult(Despesas.Where(x => x.Mes == mes && x.Ano == ano && x.UsuarioId == usuarioId).AsEnumerable());

        public Task<IEnumerable<Despesa>> GetDespesasDaAgrupadora(string idDespesaAgrupadora) =>
            Task.FromResult(Despesas.Where(x => x.IdDespesaAgrupadora == idDespesaAgrupadora).AsEnumerable());

        public Task<IEnumerable<Despesa>> GetDespesasDoLoteAsync(string despesaOrigemId) =>
            Task.FromResult(Despesas.Where(x => x.DespesaOrigemId == despesaOrigemId).AsEnumerable());

        public Task InsertManyAsync(IEnumerable<Despesa> despesas)
        {
            Despesas.AddRange(despesas);
            return Task.CompletedTask;
        }

        public Task UpdateManyAsync(IEnumerable<Despesa> despesas) => Task.CompletedTask;

        public Task DeleteManyAsync(IEnumerable<Despesa> despesas)
        {
            foreach (var despesa in despesas.ToList())
                Despesas.Remove(despesa);

            return Task.CompletedTask;
        }
    }

    private sealed class CategoriaRepositoryFake(Categoria categoria) : ICategoriaRepository
    {
        public Task<Categoria> GetById(string id) => Task.FromResult(id == categoria.Id ? categoria : null!);
        public IQueryable<Categoria> GetCategorias() => new List<Categoria> { categoria }.AsQueryable();
        public bool CategoriaJaExiste(string nome, string idUsuario, TipoCategoria tipo) => false;
        public Task<List<Categoria>> GetCategorias(TipoCategoria tipoCategoria, string nome, string idUsuario) => Task.FromResult(new List<Categoria> { categoria });
        public Task<bool> CategoriaPossuiVinculo(Categoria Categoria) => Task.FromResult(false);
        public Task<Categoria> Add(Categoria entity) => Task.FromResult(entity);
        public Task<List<Categoria>> Add(List<Categoria> entity) => Task.FromResult(entity);
        public Task<Categoria> Update(Categoria entity) => Task.FromResult(entity);
        public Task Delete(Categoria entity) => Task.CompletedTask;
        public Task<List<Categoria>> GetByIds(List<string> ids) => Task.FromResult(new List<Categoria> { categoria });
        public Task<IEnumerable<Categoria>> GetWhere(Expression<Func<Categoria, bool>> filtro) => Task.FromResult(new List<Categoria> { categoria }.Where(filtro.Compile()).AsEnumerable());
    }

    private sealed class AcumuladoMensalReportRepositoryFake : IAcumuladoMensalReportRepository
    {
        public Task<AcumuladoMensalReport> Obter(int mes, int ano, string idUsuario) =>
            Task.FromResult(new AcumuladoMensalReport(ano, mes, 0, 0, 0));
    }
}

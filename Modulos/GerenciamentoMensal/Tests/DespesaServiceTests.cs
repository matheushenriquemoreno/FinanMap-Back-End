using System.Linq.Expressions;
using Application.DTOs;
using Application.Services;
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

public class DespesaServiceTests
{
    [Fact]
    public async Task AtualizarDespesaEmLoteAsync_DespesaParcelada_MantemDescricaoSemSufixoDaParcela()
    {
        var fixture = CriarFixtureLoteParcelado();
        var service = fixture.CriarService();

        var resultado = await service.AtualizarDespesaEmLoteAsync(
            fixture.Despesas[0].Id,
            new AtualizarLoteDespesaDTO
            {
                NovaDescricao = "Manutenção Carro",
                NovoValor = 100,
                NovaCategoriaId = fixture.Categoria.Id,
                Modificador = ModificadorLote.TodasDoLote
            });

        Assert.True(resultado.IsSucess);
        Assert.All(fixture.Repositorio.DespesasAtualizadas, despesa =>
        {
            Assert.Equal("Manutenção Carro", despesa.Descricao);
            Assert.DoesNotContain($"({despesa.ParcelaAtual}/{despesa.TotalParcelas})", despesa.Descricao);
        });
    }

    [Fact]
    public async Task AtualizarDespesaEmLoteAsync_DescricaoJaContaminada_RemoveSufixoFinalDaParcela()
    {
        var fixture = CriarFixtureLoteParcelado();
        var service = fixture.CriarService();

        var resultado = await service.AtualizarDespesaEmLoteAsync(
            fixture.Despesas[0].Id,
            new AtualizarLoteDespesaDTO
            {
                NovaDescricao = "Manutenção Carro (1/3)",
                NovoValor = 100,
                NovaCategoriaId = fixture.Categoria.Id,
                Modificador = ModificadorLote.TodasDoLote
            });

        Assert.True(resultado.IsSucess);
        Assert.All(fixture.Repositorio.DespesasAtualizadas, despesa =>
            Assert.Equal("Manutenção Carro", despesa.Descricao));
    }

    [Fact]
    public async Task AtualizarDespesaEmLoteAsync_DespesaRecorrente_MantemDescricaoInformada()
    {
        var fixture = CriarFixtureLoteRecorrente();
        var service = fixture.CriarService();

        var resultado = await service.AtualizarDespesaEmLoteAsync(
            fixture.Despesas[0].Id,
            new AtualizarLoteDespesaDTO
            {
                NovaDescricao = "Academia Mensal",
                NovoValor = 90,
                NovaCategoriaId = fixture.Categoria.Id,
                Modificador = ModificadorLote.TodasDoLote
            });

        Assert.True(resultado.IsSucess);
        Assert.All(fixture.Repositorio.DespesasAtualizadas, despesa =>
            Assert.Equal("Academia Mensal", despesa.Descricao));
    }

    private static DespesaServiceFixture CriarFixtureLoteParcelado()
    {
        var usuario = new Usuario("Usuário Teste", "usuario@finanmap.com") { Id = "usuario-id" };
        var categoria = new Categoria("Transporte", TipoCategoria.Despesa, usuario.Id) { Id = "categoria-id" };
        var despesas = Enumerable.Range(1, 3)
            .Select(parcela => new Despesa(2026, parcela, "Manutenção Carro", 100, categoria, usuario)
            {
                Id = $"despesa-{parcela}",
                DespesaOrigemId = "lote-1",
                IsParcelado = true,
                ParcelaAtual = parcela,
                TotalParcelas = 3
            })
            .ToList();

        return new DespesaServiceFixture(usuario, categoria, despesas);
    }

    private static DespesaServiceFixture CriarFixtureLoteRecorrente()
    {
        var usuario = new Usuario("Usuário Teste", "usuario@finanmap.com") { Id = "usuario-id" };
        var categoria = new Categoria("Saúde", TipoCategoria.Despesa, usuario.Id) { Id = "categoria-id" };
        var despesas = Enumerable.Range(1, 3)
            .Select(mes => new Despesa(2026, mes, "Academia", 90, categoria, usuario)
            {
                Id = $"despesa-{mes}",
                DespesaOrigemId = "lote-1",
                IsRecorrente = true
            })
            .ToList();

        return new DespesaServiceFixture(usuario, categoria, despesas);
    }

    private sealed class DespesaServiceFixture(
        Usuario usuario,
        Categoria categoria,
        List<Despesa> despesas)
    {
        public Categoria Categoria { get; } = categoria;
        public List<Despesa> Despesas { get; } = despesas;
        public DespesaRepositoryFake Repositorio { get; } = new(despesas);

        public DespesaService CriarService()
        {
            return new DespesaService(
                Repositorio,
                new CategoriaRepositoryFake(Categoria),
                new AcumuladoMensalReportRepositoryFake(),
                new UsuarioLogadoFake(usuario));
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

    private sealed class DespesaRepositoryFake(List<Despesa> despesas) : IDespesaRepository
    {
        public List<Despesa> DespesasAtualizadas { get; private set; } = [];

        public Task<Despesa> Add(Despesa entity) => Task.FromResult(entity);
        public Task<List<Despesa>> Add(List<Despesa> entity) => Task.FromResult(entity);
        public Task Delete(Despesa entity) => Task.CompletedTask;
        public Task DeleteManyAsync(IEnumerable<Despesa> despesas) => Task.CompletedTask;
        public Task<IEnumerable<Despesa>> GetDespesasDaAgrupadora(string idDespesaAgrupadora) => Task.FromResult(Enumerable.Empty<Despesa>());
        public Task<IEnumerable<Despesa>> GetDespesasDoLoteAsync(string despesaOrigemId) => Task.FromResult(despesas.Where(d => d.DespesaOrigemId == despesaOrigemId));
        public Task<Despesa> GetById(string id) => Task.FromResult(despesas.SingleOrDefault(d => d.Id == id)!);
        public Task<List<Despesa>> GetByIds(List<string> ids) => Task.FromResult(despesas.Where(d => ids.Contains(d.Id)).ToList());
        public Task<IEnumerable<Despesa>> GetPeloMes(int mes, int ano, string usuarioId, string descricao) => Task.FromResult(Enumerable.Empty<Despesa>());
        public Task<decimal> GetValorTotalDespesasDaAgrupadora(string idDespesaAgrupadora) => Task.FromResult(0m);
        public Task<IEnumerable<Despesa>> GetWhere(Expression<Func<Despesa, bool>> filtro) => Task.FromResult(despesas.AsQueryable().Where(filtro).AsEnumerable());
        public Task InsertManyAsync(IEnumerable<Despesa> despesas) => Task.CompletedTask;
        public Task<IEnumerable<Despesa>> ObterPeloMes(int mes, int ano, string usuarioId) => Task.FromResult(Enumerable.Empty<Despesa>());
        public Task<Despesa> Update(Despesa entity) => Task.FromResult(entity);

        public Task UpdateManyAsync(IEnumerable<Despesa> despesas)
        {
            DespesasAtualizadas = despesas.ToList();
            return Task.CompletedTask;
        }
    }

    private sealed class CategoriaRepositoryFake(Categoria categoria) : ICategoriaRepository
    {
        public Task<Categoria> Add(Categoria entity) => Task.FromResult(entity);
        public Task<List<Categoria>> Add(List<Categoria> entity) => Task.FromResult(entity);
        public bool CategoriaJaExiste(string nome, string idUsuario, TipoCategoria tipo) => false;
        public Task<bool> CategoriaPossuiVinculo(Categoria Categoria) => Task.FromResult(false);
        public Task Delete(Categoria entity) => Task.CompletedTask;
        public Task<Categoria> GetById(string id) => Task.FromResult(id == categoria.Id ? categoria : null!);
        public Task<List<Categoria>> GetByIds(List<string> ids) => Task.FromResult(ids.Contains(categoria.Id) ? new List<Categoria> { categoria } : []);
        public IQueryable<Categoria> GetCategorias() => new[] { categoria }.AsQueryable();
        public Task<List<Categoria>> GetCategorias(TipoCategoria tipoCategoria, string nome, string idUsuario) => Task.FromResult(new List<Categoria> { categoria });
        public Task<IEnumerable<Categoria>> GetWhere(Expression<Func<Categoria, bool>> filtro) => Task.FromResult(new[] { categoria }.AsQueryable().Where(filtro).AsEnumerable());
        public Task<Categoria> Update(Categoria entity) => Task.FromResult(entity);
    }

    private sealed class AcumuladoMensalReportRepositoryFake : IAcumuladoMensalReportRepository
    {
        public Task<AcumuladoMensalReport> Obter(int mes, int ano, string idUsuario) => Task.FromResult<AcumuladoMensalReport>(null!);
    }
}

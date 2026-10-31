using System.Linq.Expressions;
using System.Reflection;
using Application.MetaFinanceira.DTOs;
using Application.MetaFinanceira.Service;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Login.Interfaces;
using Domain.MetaFinanceira.Repository;
using Domain.Repository;
using Infra.Data.Mongo.Repositorys;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;

namespace Tests;

public class MetaFinanceiraArquivamentoTests
{
    [Fact]
    public async Task ArquivarMetaEmAndamento_PersisteArquivada()
    {
        var meta = CriarMeta("meta-1", 100m);
        var repositorio = new MetaFinanceiraArquivamentoRepository(meta);
        var service = CriarService(repositorio);

        var resultado = await service.Arquivar("meta-1");

        Assert.True(resultado.IsSucess);
        Assert.True(meta.Arquivada);
        Assert.False(meta.Concluida);
    }

    [Fact]
    public async Task ArquivarMetaConcluida_PersisteArquivada()
    {
        var meta = CriarMeta("meta-1", 100m, valorContribuicao: 100m);
        var repositorio = new MetaFinanceiraArquivamentoRepository(meta);
        var service = CriarService(repositorio);

        var resultado = await service.Arquivar("meta-1");

        Assert.True(resultado.IsSucess);
        Assert.True(meta.Concluida);
        Assert.True(meta.Arquivada);
    }

    [Fact]
    public async Task ArquivarMetaInexistente_RetornaNotFound()
    {
        var meta = CriarMeta("meta-1", 100m);
        var repositorio = new MetaFinanceiraArquivamentoRepository(meta);
        var service = CriarService(repositorio);

        var resultado = await service.Arquivar("meta-inexistente");

        Assert.True(resultado.IsFailure);
        Assert.Equal(TypeError.NotFound, resultado.Error.GetType());
        Assert.False(meta.Arquivada);
    }

    [Fact]
    public async Task ArquivarDuasVezes_MantemEstadoArquivada()
    {
        var meta = CriarMeta("meta-1", 100m);
        var repositorio = new MetaFinanceiraArquivamentoRepository(meta);
        var service = CriarService(repositorio);

        Assert.True((await service.Arquivar("meta-1")).IsSucess);
        var repeticao = await service.Arquivar("meta-1");

        Assert.True(repeticao.IsSucess);
        Assert.True(meta.Arquivada);
    }

    [Fact]
    public async Task DesarquivarMetaArquivada_RetornaAoEstadoAtivo()
    {
        var meta = CriarMeta("meta-1", 100m);
        meta.Arquivar();
        var repositorio = new MetaFinanceiraArquivamentoRepository(meta);
        var service = CriarService(repositorio);

        var resultado = await service.Desarquivar("meta-1");

        Assert.True(resultado.IsSucess);
        Assert.False(meta.Arquivada);
    }

    [Fact]
    public async Task Visualizador_LeContextoMasNaoArquivaNemDesarquiva()
    {
        var meta = CriarMeta("meta-1", 100m);
        var repositorio = new MetaFinanceiraArquivamentoRepository(meta);
        var usuario = new Usuario("Visualizador", "visualizador@finanmap.com") { Id = "usuario-2" };
        var service = new MetaFinanceiraService(
            repositorio,
            new UsuarioLogadoFake(usuario, "usuario-1", NivelPermissao.Visualizar));

        var arquivar = await service.Arquivar("meta-1");
        var desarquivar = await service.Desarquivar("meta-1");

        Assert.Equal(TypeError.Forbidden, arquivar.Error.GetType());
        Assert.Equal(TypeError.Forbidden, desarquivar.Error.GetType());
        Assert.False(meta.Arquivada);
    }

    [Fact]
    public async Task ObterTodas_OrdenaAtivasAntesDasArquivadas()
    {
        var antiga = CriarMeta("meta-antiga", 100m);
        var recente = CriarMeta("meta-recente", 100m);
        var arquivadaRecente = CriarMeta("meta-arquivada-recente", 100m);
        arquivadaRecente.Arquivar();

        // DataCriacao é definida no construtor; ajustar datas distintas para testar a ordenação.
        DefinirDataCriacao(antiga, DateTime.Now.AddDays(-3));
        DefinirDataCriacao(recente, DateTime.Now.AddDays(-1));
        DefinirDataCriacao(arquivadaRecente, DateTime.Now);

        var repositorio = new MetaFinanceiraArquivamentoRepository(arquivadaRecente, recente, antiga);
        var service = CriarService(repositorio);

        // A ordenação atual é mantida: não concluídas primeiro, depois mais recentes.
        var todas = await service.ObterTodas();
        var soAtivas = await service.ObterTodas(arquivadas: false);
        var soArquivadas = await service.ObterTodas(arquivadas: true);

        Assert.Equal(["meta-arquivada-recente", "meta-recente", "meta-antiga"], todas.Select(m => m.Id).ToArray());
        Assert.Equal(["meta-recente", "meta-antiga"], soAtivas.Select(m => m.Id).ToArray());
        Assert.Equal(["meta-arquivada-recente"], soArquivadas.Select(m => m.Id).ToArray());
        Assert.All(soAtivas, m => Assert.False(m.Arquivada));
        Assert.All(soArquivadas, m => Assert.True(m.Arquivada));
    }

    [Fact]
    public async Task ObterResumo_UsaOMesmoFiltroDaListagem()
    {
        var ativaEmAndamento = CriarMeta("meta-1", 100m, valorContribuicao: 40m);
        var ativaConcluida = CriarMeta("meta-2", 200m, valorContribuicao: 200m);
        var arquivadaConcluida = CriarMeta("meta-3", 300m, valorContribuicao: 300m);
        arquivadaConcluida.Arquivar();

        var repositorio = new MetaFinanceiraArquivamentoRepository(ativaEmAndamento, ativaConcluida, arquivadaConcluida);
        var service = CriarService(repositorio);

        var todas = await service.ObterResumo();
        var soAtivas = await service.ObterResumo(arquivadas: false);
        var soArquivadas = await service.ObterResumo(arquivadas: true);

        // Todas
        Assert.Equal(600m, todas.TotalMetas);
        Assert.Equal(540m, todas.TotalInvestido);
        Assert.Equal(90m, todas.PercentualGeral);
        Assert.Equal(2, todas.MetasConcluidas);
        Assert.Equal(3, todas.TotalDeMetasAtivas);

        // Só ativas (exclui a arquivada)
        Assert.Equal(300m, soAtivas.TotalMetas);
        Assert.Equal(240m, soAtivas.TotalInvestido);
        Assert.Equal(80m, soAtivas.PercentualGeral);
        Assert.Equal(1, soAtivas.MetasConcluidas);
        Assert.Equal(2, soAtivas.TotalDeMetasAtivas);

        // Só arquivadas
        Assert.Equal(300m, soArquivadas.TotalMetas);
        Assert.Equal(300m, soArquivadas.TotalInvestido);
        Assert.Equal(100m, soArquivadas.PercentualGeral);
        Assert.Equal(1, soArquivadas.MetasConcluidas);
        Assert.Equal(1, soArquivadas.TotalDeMetasAtivas);
    }

    [Fact]
    public async Task AdicionarContribuicao_EmMetaArquivada_RetornaFalhaDeValidacao()
    {
        var meta = CriarMeta("meta-1", 100m);
        meta.Arquivar();
        var repositorio = new MetaFinanceiraArquivamentoRepository(meta);
        var service = CriarService(repositorio);

        var resultado = await service.AdicionarContribuicao("meta-1", new ContribuicaoDTO
        {
            Valor = 25m,
            Data = DateTime.Today,
            Descricao = "Aporte após arquivamento"
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(TypeError.Validation, resultado.Error.GetType());
        Assert.Equal("Não é possível registrar contribuição em uma meta arquivada.", resultado.Error.Message);
        Assert.Equal(0m, meta.ValorAtual);
        Assert.Empty(meta.Contribuicoes);
    }

    [Fact]
    public void FiltroSomentoAtivas_ToleraMetasAntigasSemCampoArquivada()
    {
        // O fake em memória não consegue representar um documento sem o campo Arquivada
        // (a entidade é bool não anulável). Por isso representamos o caso do deploy —
        // metas antigas sem o campo — validando o filtro Mongo renderizado:
        // o documento do filtro não pode usar Eq(false), que não casa campo ausente.
        var filtro = MetaFinanceiraRepository.ConstruirFiltroObterPorUsuario("usuario-1", arquivadas: false);
        var documento = RenderizarFiltro(filtro);

        Assert.Equal(BsonBoolean.True, documento["Arquivada"]["$ne"]);
        Assert.NotEqual(BsonBoolean.False, documento["Arquivada"]);
    }

    [Fact]
    public void FiltroSomentoArquivadas_ExigeArquivadaTrue()
    {
        // Documentos antigos nunca foram arquivados: Eq(true) os exclui corretamente.
        var filtro = MetaFinanceiraRepository.ConstruirFiltroObterPorUsuario("usuario-1", arquivadas: true);
        var documento = RenderizarFiltro(filtro);

        Assert.Equal(BsonBoolean.True, documento["Arquivada"]);
    }

    [Fact]
    public void FiltroSemParametroArquivadas_NaoRestringeArquivamento()
    {
        var filtro = MetaFinanceiraRepository.ConstruirFiltroObterPorUsuario("usuario-1", arquivadas: null);
        var documento = RenderizarFiltro(filtro);

        Assert.False(documento.Contains("Arquivada"));
    }

    [Fact]
    public void ParseArquivadas_ComValorInvalido_TratadoComoSemFiltro()
    {
        // Teste do parser de query da listagem via reflexão (método private no WebApi).
        var metodo = typeof(WebApi.Controllers.MetaFinanceira)
            .GetMethod("ParseArquivadas", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(metodo);

        foreach (var valor in new[] { "abc", "", null })
        {
            var resultado = metodo!.Invoke(null, [valor]);
            Assert.Null(resultado);
        }
    }

    [Fact]
    public void ParseArquivadas_ComValoresValidos_Booleanos()
    {
        var metodo = typeof(WebApi.Controllers.MetaFinanceira)
            .GetMethod("ParseArquivadas", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(metodo);

        Assert.True((bool?)metodo!.Invoke(null, ["true"]));
        Assert.False((bool?)metodo!.Invoke(null, ["false"]));
    }

    private static BsonDocument RenderizarFiltro(FilterDefinition<Domain.Entity.MetaFinanceira> filtro)
    {
        var serializadores = BsonSerializer.SerializerRegistry;
        var serializer = serializadores.GetSerializer<Domain.Entity.MetaFinanceira>();
        return filtro.Render(new RenderArgs<Domain.Entity.MetaFinanceira>(serializer, serializadores)).AsBsonDocument;
    }

    private static MetaFinanceiraService CriarService(
        MetaFinanceiraArquivamentoRepository repositorio)
    {
        var usuario = new Usuario("Usuário Teste", "teste@finanmap.com") { Id = "usuario-1" };
        return new MetaFinanceiraService(
            repositorio,
            new UsuarioLogadoFake(usuario));
    }

    private static MetaFinanceira CriarMeta(
        string id,
        decimal valorAlvo,
        decimal valorContribuicao = 0m,
        string usuarioId = "usuario-1")
    {
        var meta = new MetaFinanceira(
            $"Meta {id}",
            valorAlvo,
            DateTime.Today.AddDays(30),
            CategoriaIconeMeta.Moradia,
            new Usuario("Proprietário", "proprietario@finanmap.com") { Id = usuarioId })
        {
            Id = id
        };

        if (valorContribuicao > 0)
            meta.AdicionarContribuicao(valorContribuicao, DateTime.Today);

        return meta;
    }

    private static void DefinirDataCriacao(MetaFinanceira meta, DateTime data)
    {
        typeof(MetaFinanceira)
            .GetProperty(nameof(MetaFinanceira.DataCriacao))!
            .SetValue(meta, data);
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

    private sealed class MetaFinanceiraArquivamentoRepository(params MetaFinanceira[] itens)
        : IMetaFinanceiraRepository
    {
        public List<MetaFinanceira> Itens { get; } = [.. itens];

        public Task<MetaFinanceira> Add(MetaFinanceira entity)
        {
            Itens.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<List<MetaFinanceira>> Add(List<MetaFinanceira> entity)
        {
            Itens.AddRange(entity);
            return Task.FromResult(entity);
        }

        public Task<MetaFinanceira> Update(MetaFinanceira entity)
        {
            var indice = Itens.FindIndex(item => item.Id == entity.Id);
            if (indice >= 0)
                Itens[indice] = entity;
            return Task.FromResult(entity);
        }

        public Task Delete(MetaFinanceira entity)
        {
            Itens.RemoveAll(item => item.Id == entity.Id);
            return Task.CompletedTask;
        }

        public Task<MetaFinanceira> GetById(string id)
            => Task.FromResult(Itens.SingleOrDefault(item => item.Id == id)!);

        public Task<List<MetaFinanceira>> GetByIds(List<string> ids)
            => Task.FromResult(Itens.Where(item => ids.Contains(item.Id)).ToList());

        public Task<IEnumerable<MetaFinanceira>> GetWhere(Expression<Func<MetaFinanceira, bool>> filtro)
            => Task.FromResult(Itens.AsQueryable().Where(filtro).AsEnumerable());

        public Task<List<MetaFinanceira>> ObterPorUsuario(string usuarioId, bool? arquivadas = null)
        {
            // Mesma ordenação aplicada pelo serviço em ObterTodas.
            var resultado = Itens
                .Where(item => item.UsuarioId == usuarioId)
                .Where(item => !arquivadas.HasValue || item.Arquivada == arquivadas.Value)
                .OrderBy(item => item.Concluida)
                .ThenByDescending(item => item.DataCriacao);

            return Task.FromResult(resultado.ToList());
        }
    }
}

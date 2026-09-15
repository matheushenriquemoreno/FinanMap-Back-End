using Domain.Dashboard;
using Domain.Entity;
using Domain.Enum;
using Xunit;

namespace Tests;

public class DashboardDistribuicaoDespesaTests
{
    private static readonly Usuario Usuario = CriarUsuario();
    private static readonly Categoria CategoriaCartao = CriarCategoria("categoria-cartao", "Cartao");
    private static readonly Categoria CategoriaMercado = CriarCategoria("categoria-mercado", "Mercado");
    private static readonly Categoria CategoriaLazer = CriarCategoria("categoria-lazer", "Lazer");

    [Fact]
    public void Calcular_AgrupadoraComValorProprioEFilha_AtribuiValorProprioECategoriaDaFilha()
    {
        var agrupadora = CriarDespesa("agrupadora-1", 500, CategoriaCartao);

        var despesas = new List<Despesa>
        {
            agrupadora,
            CriarDespesa("filha-1", 200, CategoriaMercado, agrupadora.Id)
        };

        var contribuicoes = DistribuicaoDespesaCategorias.Calcular(despesas);

        Assert.Equal(2, contribuicoes.Count);
        Assert.Equal(300, contribuicoes[CategoriaCartao.Id]);
        Assert.Equal(200, contribuicoes[CategoriaMercado.Id]);
    }

    [Fact]
    public void Calcular_DespesaComum_AtribuiValorIntegralASuaCategoria()
    {
        var despesas = new List<Despesa>
        {
            CriarDespesa("despesa-1", 150, CategoriaLazer)
        };

        var contribuicoes = DistribuicaoDespesaCategorias.Calcular(despesas);

        Assert.Equal(150, contribuicoes[CategoriaLazer.Id]);
    }

    [Fact]
    public void Calcular_FilhaSemAgrupadoraNoConjunto_AtribuiValorIntegralACategoriaDaFilha()
    {
        var despesas = new List<Despesa>
        {
            CriarDespesa("filha-orfa", 90, CategoriaMercado, "agrupadora-ausente")
        };

        var contribuicoes = DistribuicaoDespesaCategorias.Calcular(despesas);

        Assert.Equal(90, contribuicoes[CategoriaMercado.Id]);
    }

    [Fact]
    public void Calcular_MultiplasAgrupadoras_TrataCadaUmaIndependentemente()
    {
        var agrupadoraCartao = CriarDespesa("agrupadora-cartao", 500, CategoriaCartao);
        var agrupadoraLazer = CriarDespesa("agrupadora-lazer", 300, CategoriaLazer);

        var despesas = new List<Despesa>
        {
            agrupadoraCartao,
            CriarDespesa("filha-cartao", 200, CategoriaMercado, agrupadoraCartao.Id),
            agrupadoraLazer,
            CriarDespesa("filha-lazer", 100, CategoriaMercado, agrupadoraLazer.Id)
        };

        var contribuicoes = DistribuicaoDespesaCategorias.Calcular(despesas);

        Assert.Equal(300, contribuicoes[CategoriaCartao.Id]);
        Assert.Equal(200, contribuicoes[CategoriaLazer.Id]);
        Assert.Equal(300, contribuicoes[CategoriaMercado.Id]);
    }

    [Fact]
    public void Calcular_SomaDasContribuicoes_IgualaSomaDasAgrupadorasCheiasMaisDespesasComuns()
    {
        var agrupadora = CriarDespesa("agrupadora-1", 500, CategoriaCartao);

        var despesas = new List<Despesa>
        {
            agrupadora,
            CriarDespesa("filha-1", 200, CategoriaMercado, agrupadora.Id),
            CriarDespesa("comum-1", 100, CategoriaLazer)
        };

        var contribuicoes = DistribuicaoDespesaCategorias.Calcular(despesas);

        Assert.Equal(600, contribuicoes.Values.Sum());
    }

    [Fact]
    public void Calcular_CategoriaVazia_NaoGeraContribuicao()
    {
        var categoriaSemId = new Categoria("Sem Id", TipoCategoria.Despesa, Usuario.Id);
        var despesaSemCategoria = new Despesa(2026, 1, "Despesa", 80, categoriaSemId, Usuario) { Id = "sem-categoria" };

        var contribuicoes = DistribuicaoDespesaCategorias.Calcular(new List<Despesa> { despesaSemCategoria });

        Assert.Empty(contribuicoes);
    }

    private static Usuario CriarUsuario() =>
        new("Usuario Teste", "usuario@teste.com") { Id = "usuario-1" };

    private static Categoria CriarCategoria(string id, string nome) =>
        new(nome, TipoCategoria.Despesa, Usuario.Id) { Id = id };

    private static Despesa CriarDespesa(string id, decimal valor, Categoria categoria, string idDespesaAgrupadora = null) =>
        new(2026, 1, "Despesa", valor, categoria, Usuario)
        {
            Id = id,
            IdDespesaAgrupadora = idDespesaAgrupadora
        };
}

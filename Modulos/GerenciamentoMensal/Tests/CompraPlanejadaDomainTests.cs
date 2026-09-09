using Domain.Entity;
using Domain.Exceptions;
using Xunit;

namespace Tests;

public class CompraPlanejadaDomainTests
{
    [Fact]
    public void CriarComDadosValidos_MantemDadosEPermanecePendente()
    {
        var links = new[]
        {
            new LinkLojaCompraPlanejada("https://loja-a.example/produto", "Loja A"),
            new LinkLojaCompraPlanejada("https://loja-b.example/produto", "Loja B")
        };

        var compra = new CompraPlanejada(
            "usuario-1",
            "Notebook",
            3599.90m,
            PrioridadeCompraPlanejada.Alta,
            "Para trabalhar",
            links);

        Assert.Equal("usuario-1", compra.UsuarioId);
        Assert.Equal("Notebook", compra.Nome);
        Assert.Equal(3599.90m, compra.ValorEstimado);
        Assert.Equal(PrioridadeCompraPlanejada.Alta, compra.Prioridade);
        Assert.Equal("Para trabalhar", compra.Descricao);
        Assert.Equal(2, compra.LinksLojas.Count);
        Assert.Equal(EstadoCompraPlanejada.Pendente, compra.Estado);
        Assert.Null(compra.ValorReal);
        Assert.Null(compra.DataCompra);
    }

    [Fact]
    public void CriarComNomeNulo_LancaErro()
    {
        var exception = Assert.Throws<DomainValidatorException>(() => new CompraPlanejada(
            "usuario-1", null, 10m, PrioridadeCompraPlanejada.Media));

        Assert.Contains("Nome da compra é obrigatório.", exception.Errors);
    }

    [Fact]
    public void CriarComNomeVazio_LancaErro()
    {
        var exception = Assert.Throws<DomainValidatorException>(() => new CompraPlanejada(
            "usuario-1", " ", 10m, PrioridadeCompraPlanejada.Media));

        Assert.Contains("Nome da compra é obrigatório.", exception.Errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CriarComValorEstimadoNaoPositivo_LancaErro(decimal valor)
    {
        var exception = Assert.Throws<DomainValidatorException>(() => new CompraPlanejada(
            "usuario-1", "Produto", valor, PrioridadeCompraPlanejada.Media));

        Assert.Contains("Valor estimado deve ser maior que zero.", exception.Errors);
    }

    [Fact]
    public void CriarComPrioridadeInvalida_LancaErro()
    {
        var exception = Assert.Throws<DomainValidatorException>(() => new CompraPlanejada(
            "usuario-1", "Produto", 10m, (PrioridadeCompraPlanejada)99));

        Assert.Contains("Prioridade da compra é inválida.", exception.Errors);
    }

    [Theory]
    [InlineData("ftp://loja.example/produto", "Loja")]
    [InlineData("https://loja.example/produto", "")]
    public void CriarComLinkInvalido_LancaErro(string url, string nomeLoja)
    {
        Assert.Throws<DomainValidatorException>(() => new LinkLojaCompraPlanejada(url, nomeLoja));
    }

    [Fact]
    public void CriarComEstimativaDecimal_PreservaPrecisao()
    {
        var compra = new CompraPlanejada(
            "usuario-1", "Produto", 0.01m, PrioridadeCompraPlanejada.Baixa);

        Assert.Equal(0.01m, compra.ValorEstimado);
    }

    [Fact]
    public void VincularDespesaDuasVezes_LancaErroENaoSubstituiVinculo()
    {
        var compra = new CompraPlanejada(
            "usuario-1", "Produto", 10m, PrioridadeCompraPlanejada.Media);
        compra.MarcarComoComprado(9m, DateTime.UtcNow.Date);
        compra.VincularDespesa("despesa-1");

        var exception = Assert.Throws<DomainValidatorException>(() => compra.VincularDespesa("despesa-2"));

        Assert.Contains("Item já possui uma despesa vinculada.", exception.Errors);
        Assert.Equal("despesa-1", compra.DespesaId);
    }
}

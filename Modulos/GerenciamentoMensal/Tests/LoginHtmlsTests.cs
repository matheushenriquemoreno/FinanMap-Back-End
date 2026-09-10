using Application.Email.Htmls;
using Xunit;

namespace Tests;

public class LoginHtmlsTests
{
    [Fact]
    public void ObterHtmlLogin_InsereCodigoEValidadeNoTemplate()
    {
        var html = LoginHtmls.ObterHtmlLogin("UVE96W", 15);

        Assert.Contains("UVE96W", html);
        Assert.Contains("Válido por 15 minutos", html);
        Assert.DoesNotContain("{{codigo}}", html);
        Assert.DoesNotContain("{{expiracao}}", html);
    }

    [Fact]
    public void ObterHtmlLogin_CodificaCodigoAntesDeInseriLoNoHtml()
    {
        var html = LoginHtmls.ObterHtmlLogin("<script>", 15);

        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
    }
}

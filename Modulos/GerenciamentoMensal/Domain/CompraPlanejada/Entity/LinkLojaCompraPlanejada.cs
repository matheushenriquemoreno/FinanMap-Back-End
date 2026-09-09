using Domain.Validator;

namespace Domain.Entity;

public class LinkLojaCompraPlanejada
{
    public string Url { get; private set; }
    public string NomeLoja { get; private set; }

    protected LinkLojaCompraPlanejada()
    {
    }

    public LinkLojaCompraPlanejada(string url, string nomeLoja)
    {
        Url = url?.Trim();
        NomeLoja = nomeLoja?.Trim();

        ValidarDados();
    }

    private void ValidarDados()
    {
        var validator = DomainValidator.Create();

        validator.Validar(
            () => !Uri.TryCreate(Url, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https"),
            "URL da loja inválida.");
        validator.Validar(() => string.IsNullOrWhiteSpace(NomeLoja), "Nome da loja é obrigatório.");

        validator.LancarExceptionSePossuiErro();
    }
}

namespace WebApi.Configs.Models;

public class MultiStatusResponse : ApiResultError
{
    public int QuantidadeSucesso { get; set; }
    public int QuantidadeErros { get; set; }

    public MultiStatusResponse()
    {
        
    }
    protected MultiStatusResponse(string message) : base(message)
    {
    }

    protected MultiStatusResponse(List<string> message) : base(message)
    {
    }
    protected MultiStatusResponse(List<string> message, int quantidadeSucesso, int quantidadeErros) : base(message)
    {
        QuantidadeSucesso = quantidadeSucesso;
        QuantidadeErros = quantidadeErros;
    }

    public static MultiStatusResponse Create(Error error)
        => new MultiStatusResponse(error.Message);

    public static MultiStatusResponse Create(List<string> messages, int sucessos, int quantidadeErros)
    => new(messages, sucessos, quantidadeErros);
}



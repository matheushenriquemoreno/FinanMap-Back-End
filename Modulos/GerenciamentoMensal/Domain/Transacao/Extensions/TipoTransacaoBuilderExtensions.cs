namespace Domain;

public static class TipoTransacaoBuilderExtensions
{
    public static TipoTransacaoActionBuilder CriarBuilder(this TipoTransacao tipo)
    {
        return new TipoTransacaoActionBuilder(tipo);
    }
}


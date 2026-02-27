namespace Domain.Entity;

public enum TipoNotificacaoMeta
{
    MetadeCaminho,   // 50%
    QuaseLa,         // 80%
    MetaAlcancada    // 100%
}

public class NotificacaoMeta
{
    public TipoNotificacaoMeta Tipo { get; set; }
    public string NomeMeta { get; set; }
    public decimal ValorAlvo { get; set; }
    public decimal ValorAtual { get; set; }
    public string Mensagem { get; set; }

    public NotificacaoMeta(TipoNotificacaoMeta tipo, string nomeMeta, decimal valorAlvo, decimal valorAtual)
    {
        Tipo = tipo;
        NomeMeta = nomeMeta;
        ValorAlvo = valorAlvo;
        ValorAtual = valorAtual;
        Mensagem = tipo switch
        {
            TipoNotificacaoMeta.MetadeCaminho => $"Metade do caminho! 💪 Você já juntou R$ {valorAtual:N2} para \"{nomeMeta}\".",
            TipoNotificacaoMeta.QuaseLa => $"Quase lá! 🔥 Faltam apenas R$ {(valorAlvo - valorAtual):N2} para \"{nomeMeta}\".",
            TipoNotificacaoMeta.MetaAlcancada => $"Parabéns! 🎉 Você atingiu a meta \"{nomeMeta}\"!",
            _ => string.Empty
        };
    }
}

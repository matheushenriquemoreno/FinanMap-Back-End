using Domain.Enum;

namespace Domain.Entity;

public class CustoFixoLembreteHistorico : EntityBase
{
    public string UsuarioId { get; set; }
    public DateTime DataReferencia { get; set; }
    public TipoLembrete TipoLembrete { get; set; }
    public DateTime CreatedAt { get; set; }

    protected CustoFixoLembreteHistorico()
    {
    }

    public CustoFixoLembreteHistorico(string usuarioId, DateTime dataReferencia, TipoLembrete tipoLembrete)
    {
        UsuarioId = usuarioId;
        DataReferencia = dataReferencia.Date; // Salva apenas a parte da data
        TipoLembrete = tipoLembrete;
        CreatedAt = DateTime.UtcNow;
    }
}

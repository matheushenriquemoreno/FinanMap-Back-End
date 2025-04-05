using Domain;

namespace Application.ReplicarTransacao.DTOs;

public class ReplicarTransacoesPeriodoDTO
{
    public DateTime PeriodoInicial {  get; set; }
    public DateTime PeriodoFinal { get; set; }
    public TipoTransacao TipoTransacao { get; set; }
    public List<string> IdRegistros { get; set; }
}


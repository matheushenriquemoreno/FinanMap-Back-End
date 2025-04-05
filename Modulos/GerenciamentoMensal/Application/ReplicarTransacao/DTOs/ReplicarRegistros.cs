namespace Application.ReplicarTransacao.DTOs;

public class ReplicarRegistros
{
    public DateTime PeriodoInicial { get; set; }
    public DateTime PeriodoFinal { get; set; }
    public List<string> IdRegistros { get; set; }
}

namespace Domain.Relatorios.Entity;

public class AcumuladoMensalReport
{
    public int Ano { get; set; }
    public int Mes { get; set; }
    public decimal ValorRendimento { get; set; }
    public decimal ValorInvestimentos { get; set; }
    public decimal ValorDespesas { get; set; }
    public decimal ValorFinal
    {
        get
        {
            return ValorRendimento - ValorDespesas - ValorInvestimentos;
        }
    }

    public AcumuladoMensalReport(int ano, int mes, decimal valorRendimento, decimal valorInvestimentos, decimal valorDespesas)
    {
        Ano = ano;
        Mes = mes;
        ValorRendimento = valorRendimento;
        ValorInvestimentos = valorInvestimentos;
        ValorDespesas = valorDespesas;
    }
}

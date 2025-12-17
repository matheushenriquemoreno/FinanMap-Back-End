namespace Domain.Dashboard.Models;

public record EvolucaoPeriodoModel(
    string Label,
    decimal Rendimento,
    decimal Despesa,
    decimal Investimento
);

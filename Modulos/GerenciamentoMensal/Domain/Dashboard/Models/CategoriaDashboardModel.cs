namespace Domain.Dashboard.Models;

public record CategoriaDashboardModel(
    string Categoria,
    decimal Valor,
    string Tipo,
    decimal? Percentual
);

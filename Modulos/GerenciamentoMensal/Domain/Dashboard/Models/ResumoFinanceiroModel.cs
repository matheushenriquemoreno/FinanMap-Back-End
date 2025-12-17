namespace Domain.Dashboard.Models;

public record ResumoFinanceiroModel(
    ResumoItemModel Rendimento,
    ResumoItemModel Despesa,
    ResumoItemModel Investimento
);

public record ResumoItemModel(
    decimal Total,
    List<decimal> Tendencia
);

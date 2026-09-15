using Domain.Entity;

namespace Domain.Dashboard;

/// <summary>
/// Regra pura de distribuição de despesas por categoria. A agrupadora contribui pelo
/// valor próprio (valor total menos a soma das filhas) e cada filha contribui pela
/// sua própria categoria, sem duplicar valores.
/// </summary>
public static class DistribuicaoDespesaCategorias
{
    public static Dictionary<string, decimal> Calcular(IEnumerable<Despesa> despesas)
    {
        var contribuicoes = new Dictionary<string, decimal>();

        if (despesas is null)
            return contribuicoes;

        var lista = despesas.ToList();

        var somaFilhasPorAgrupadora = lista
            .Where(despesa => despesa.EstaAgrupada())
            .GroupBy(despesa => despesa.IdDespesaAgrupadora)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Sum(despesa => despesa.Valor));

        foreach (var despesa in lista)
        {
            var valor = despesa.Valor;

            if (!despesa.EstaAgrupada())
            {
                somaFilhasPorAgrupadora.TryGetValue(despesa.Id, out var somaFilhas);
                valor -= somaFilhas;
            }

            if (string.IsNullOrEmpty(despesa.CategoriaId))
                continue;

            contribuicoes[despesa.CategoriaId] = contribuicoes.TryGetValue(despesa.CategoriaId, out var atual)
                ? atual + valor
                : valor;
        }

        return contribuicoes;
    }
}

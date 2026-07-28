using Domain.Entity;

namespace Domain;

public interface IRepositoryTransacaoBase<T> : IRepositoryBase<T> where T : Transacao
{
    public Task<IEnumerable<T>> ObterPeloMes(int mes, int ano, string usuarioId);

    public async Task<IEnumerable<T>> ObterPorPeriodo(
        DateOnly from,
        DateOnly to,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        var result = new List<T>();
        var current = new DateOnly(from.Year, from.Month, 1);
        var last = new DateOnly(to.Year, to.Month, 1);
        while (current <= last)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.AddRange(await ObterPeloMes(
                current.Month,
                current.Year,
                usuarioId));
            current = current.AddMonths(1);
        }
        return result;
    }
}


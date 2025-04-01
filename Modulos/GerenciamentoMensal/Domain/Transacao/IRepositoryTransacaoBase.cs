using Domain.Entity;

namespace Domain;

public interface IRepositoryTransacaoBase<T> : IRepositoryBase<T> where T : Transacao
{
    public Task<IEnumerable<T>> ObterPeloMes(int mes, int ano, string usuarioId);
}


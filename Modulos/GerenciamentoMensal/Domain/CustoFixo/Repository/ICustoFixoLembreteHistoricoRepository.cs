using Domain.Entity;
using Domain.Enum;

namespace Domain.Repository;

public interface ICustoFixoLembreteHistoricoRepository : IRepositoryBase<CustoFixoLembreteHistorico>
{
    Task<bool> ExisteRegistroAsync(string usuarioId, DateTime dataReferencia, TipoLembrete tipo);
    Task RegistrarEnvioAsync(CustoFixoLembreteHistorico historico);
}

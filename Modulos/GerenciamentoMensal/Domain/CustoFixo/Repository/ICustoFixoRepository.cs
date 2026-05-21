using Domain.Entity;

namespace Domain.Repository;

public interface ICustoFixoRepository : IRepositoryBase<CustoFixo>
{
    Task<List<CustoFixo>> GetByUsuarioId(string usuarioId);
    Task<bool> ExisteAtivoDuplicado(string usuarioId, string nome, int diaVencimento, string ignorarId = null);
    Task<List<CustoFixo>> GetCustosFixosAtivosPorDiaVencimento(int diaVencimento);
    Task<List<string>> GetUsuarioIdsPorDiaVencimento(int diaVencimento);
    Task<List<CustoFixo>> GetCustosFixosPorUsuariosEDiaVencimento(List<string> usuarioIds, int diaVencimento);
}

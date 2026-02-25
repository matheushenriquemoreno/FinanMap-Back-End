using Domain.Compartilhamento.Entity;

namespace Domain.Compartilhamento.Repository;

public interface ICompartilhamentoRepository : IRepositoryBase<Entity.Compartilhamento>
{
    /// <summary>
    /// Busca todos os compartilhamentos onde o usuário é o PROPRIETÁRIO (quem compartilhou)
    /// </summary>
    Task<List<Entity.Compartilhamento>> ObterPorProprietarioId(string proprietarioId);

    /// <summary>
    /// Busca todos os compartilhamentos onde o usuário é o CONVIDADO (quem recebeu acesso)
    /// </summary>
    Task<List<Entity.Compartilhamento>> ObterPorConvidadoId(string convidadoId);

    /// <summary>
    /// Busca um compartilhamento específico entre proprietário e convidado
    /// </summary>
    Task<Entity.Compartilhamento?> ObterPorProprietarioEConvidado(string proprietarioId, string convidadoId);

    /// <summary>
    /// Busca compartilhamentos pendentes para um e-mail (convidado ainda não tem conta ou não aceitou)
    /// </summary>
    Task<List<Entity.Compartilhamento>> ObterConvitesPendentesPorEmail(string email);
}

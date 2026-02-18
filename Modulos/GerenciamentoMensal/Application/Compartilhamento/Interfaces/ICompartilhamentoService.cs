using Application.Compartilhamento.DTOs;

namespace Application.Compartilhamento.Interfaces;

public interface ICompartilhamentoService
{
    /// <summary>
    /// Cria um novo convite de compartilhamento
    /// </summary>
    Task<Result<ResultCompartilhamentoDTO>> Convidar(CriarCompartilhamentoDTO dto);

    /// <summary>
    /// Lista todos os compartilhamentos feitos PELO usuário logado (ele é o dono)
    /// </summary>
    Task<List<ResultCompartilhamentoDTO>> ObterMeusCompartilhamentos();

    /// <summary>
    /// Lista todos os convites recebidos PELO usuário logado
    /// </summary>
    Task<List<ResultCompartilhamentoDTO>> ObterConvitesRecebidos();

    /// <summary>
    /// Aceita ou recusa um convite recebido
    /// </summary>
    Task<Result> ResponderConvite(ResponderConviteDTO dto);

    /// <summary>
    /// Atualiza o nível de permissão de um compartilhamento existente
    /// </summary>
    Task<Result> AtualizarPermissao(AtualizarPermissaoDTO dto);

    /// <summary>
    /// Revoga (exclui) um compartilhamento — somente o proprietário pode fazer
    /// </summary>
    Task<Result> Revogar(string compartilhamentoId);
}

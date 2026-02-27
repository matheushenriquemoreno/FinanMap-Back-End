using Application.MetaFinanceira.DTOs;

namespace Application.MetaFinanceira.Interface;

public interface IMetaFinanceiraService
{
    Task<Result<ResultMetaFinanceiraDTO>> Adicionar(CreateMetaFinanceiraDTO dto);
    Task<Result<ResultMetaFinanceiraDTO>> Atualizar(UpdateMetaFinanceiraDTO dto);
    Task<Result> Excluir(string id);
    Task<Result<ResultMetaFinanceiraDTO>> ObterPeloID(string id);
    Task<List<ResultMetaFinanceiraDTO>> ObterTodas();
    Task<ResumoMetasDTO> ObterResumo();
    Task<Result<ResultContribuicaoDTO>> AdicionarContribuicao(string metaId, ContribuicaoDTO dto);
    Task<Result> RemoverContribuicao(string metaId, string contribuicaoId);
}

public class ResultContribuicaoDTO
{
    public ResultMetaFinanceiraDTO MetaAtualizada { get; set; }
    public NotificacaoMetaDTO? Notificacao { get; set; }
}

public class NotificacaoMetaDTO
{
    public string Tipo { get; set; }
    public string Mensagem { get; set; }
}

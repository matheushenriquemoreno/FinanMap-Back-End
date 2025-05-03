using Application.DTOs;
using Application.Shared.Interfaces.Service;
using Application.Shared.Transacao.DTOs;
using Domain.Entity;

namespace Application.Interfaces;

public interface IDespesaService : IServiceBase<Despesa, CreateDespesaDTO, UpdateDespesaDTO, ResultDespesaDTO>
{
    Task<Result<ResultDespesaDTO>> AtualizarValor(UpdateValorTransacaoDTO updateValorTransacaoDTO);
    Task<List<ResultDespesaDTO>> ObterMesAno(int mes, int ano, string descricao = null);
    Task<List<ResultDespesaDTO>> ObterDespesasDaAgrupadora(string idDespesa);
}

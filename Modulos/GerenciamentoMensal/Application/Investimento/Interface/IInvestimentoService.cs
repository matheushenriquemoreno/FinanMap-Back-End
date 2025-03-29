using Application.DTOs;
using Application.Shared.Interfaces.Service;
using Application.Shared.Transacao.DTOs;
using Domain.Entity;

namespace Application.Interface;

public interface IInvestimentoService : IServiceBase<Investimento, CreateInvestimentoDTO, UpdateInvestimentoDTO, ResultInvestimentoDTO>
{
    Task<Result<ResultInvestimentoDTO>> AtualizarValor(UpdateValorTransacaoDTO updateValorTransacaoDTO);
    Task<List<ResultInvestimentoDTO>> ObterMesAno(int mes, int ano);
}

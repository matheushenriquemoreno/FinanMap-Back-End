using Application.DTOs;
using Application.Shared.Interfaces.Service;
using Application.Shared.Transacao.DTOs;
using Domain.Entity;

namespace Application.Interface;

public interface IRendimentoService : IServiceBase<Rendimento, CreateRendimentoDTO, UpdateRendimentoDTO, ResultRendimentoDTO>
{
    Task<Result<ResultRendimentoDTO>> AtualizarValor(UpdateValorTransacaoDTO updateValorTransacaoDTO);
    Task<List<ResultRendimentoDTO>> ObterRendimentoMes(int mes, int ano);
}

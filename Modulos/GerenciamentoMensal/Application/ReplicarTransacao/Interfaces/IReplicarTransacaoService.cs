using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application.ReplicarTransacao.DTOs;

namespace Application.ReplicarTransacao.Interfaces
{
    public interface IReplicarTransacaoService
    {
        Task<Result> ReplicarTransacaoPeriodo(ReplicarTransacoesPeriodoDTO periodo);
    }
}

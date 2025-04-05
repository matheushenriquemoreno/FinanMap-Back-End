using Application.ReplicarTransacao.DTOs;
using Application.ReplicarTransacao.Interfaces;
using Domain;
using Domain.Entity;
using Domain.Repository;
using SharedDomain.Entity;

namespace Application.ReplicarTransacao.Implementacoes
{
    public class ReplicarTransacaoService : IReplicarTransacaoService
    {
        private readonly IRendimentoRepository rendimentoRepository;
        private readonly IDespesaRepository despesaRepository;
        private readonly IInvestimentoRepository investimentoRepository;

        public ReplicarTransacaoService(IRendimentoRepository rendimentoRepository, IDespesaRepository despesaRepository, IInvestimentoRepository investimentoRepository)
        {
            this.rendimentoRepository = rendimentoRepository;
            this.despesaRepository = despesaRepository;
            this.investimentoRepository = investimentoRepository;
        }

        public async Task<Result> ReplicarTransacaoPeriodo(ReplicarTransacoesPeriodoDTO periodo)
        {
            try
            {
                if (periodo.PeriodoInicial > periodo.PeriodoFinal)
                    return Result.Failure(Error.Validation("Perido inicial não pode ser maior que o final!"));

                if (periodo.IdRegistros is null || periodo.IdRegistros.Count == 0)
                    return Result.Failure(Error.Validation("Ids precisam ser informados!"));

                var replica = new ReplicarRegistros()
                {
                    PeriodoInicial = periodo.PeriodoInicial,
                    PeriodoFinal = periodo.PeriodoFinal,
                    IdRegistros = periodo.IdRegistros,
                };

                await periodo.TipoTransacao
                    .CriarBuilder()
                    .QuandoRendimento(() => ReplicarTranscaoRendimento(replica))
                    .QuandoDespesa(() => ReplicarTranscaoDespesa(replica))
                    .QuandoInvestimento(() => ReplicarTranscaoInvestimento(replica))
                    .ExecutarAsync();

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(Error.Exception($"Ocorreu um erro ao tentar replicar o {TipoTransacao.Rendimento}.", ex));
            }

        }

        public async Task ReplicarTranscaoRendimento(ReplicarRegistros periodo)
        {
            await ReplicarBase(rendimentoRepository, periodo);
        }

        public async Task ReplicarTranscaoDespesa(ReplicarRegistros periodo)
        {
            await ReplicarBase(despesaRepository, periodo);
        }

        public async Task ReplicarTranscaoInvestimento(ReplicarRegistros periodo)
        {
            await ReplicarBase(investimentoRepository, periodo);
        }

        private async Task ReplicarBase<T>(IRepositoryBase<T> repository, ReplicarRegistros periodo) where T : Transacao, IClone<T>
        {
            List<T> registros = await repository.GetByID(periodo.IdRegistros);

            if (registros.Count == 0)
                throw new Exception("Não foi encontrados registros");

            var periodoInicial = new DateTime(periodo.PeriodoInicial.Year, periodo.PeriodoInicial.Month, 1);
            var periodoFinal = new DateTime(periodo.PeriodoFinal.Year, periodo.PeriodoFinal.Month, 1);

            while (periodoInicial <= periodoFinal)
            {
                var novosRegistros = new List<T>();

                foreach (var registro in registros)
                {
                    var clone = registro.Clone();

                    clone.Ano = periodoInicial.Year;
                    clone.Mes = periodoInicial.Month;

                    var registrosIguais = await repository
                                    .GetWhere(x => x.Ano == clone.Ano
                                        && x.Mes == clone.Mes
                                        && x.Descricao == clone.Descricao);

                    if (registrosIguais.Count() > 0)
                        continue;

                    novosRegistros.Add(clone);
                }

                if (novosRegistros.Count > 0)
                    await repository.Add(novosRegistros);

                periodoInicial = periodoInicial.AddMonths(1);
            }
        }
    }
}

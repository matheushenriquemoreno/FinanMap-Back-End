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
            await ReplicarDespesa(despesaRepository, periodo);
        }

        public async Task ReplicarTranscaoInvestimento(ReplicarRegistros periodo)
        {
            await ReplicarBase(investimentoRepository, periodo);
        }

        private async Task ReplicarBase<T>(IRepositoryBase<T> repository, ReplicarRegistros periodo) where T : Transacao, IClone<T>
        {
            List<T> registros = await repository.GetByIds(periodo.IdRegistros);

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

                    var registroJaCadastrado = await ObterRegistroJaCadastrado(repository, clone);

                    if (registroJaCadastrado is not null)
                    {
                        registroJaCadastrado.Valor = clone.Valor;

                        await repository.Update(registroJaCadastrado);
                        continue;
                    }

                    novosRegistros.Add(clone);
                }

                if (novosRegistros.Count > 0)
                    await repository.Add(novosRegistros);

                periodoInicial = periodoInicial.AddMonths(1);
            }
        }

        private async Task ReplicarDespesa(IDespesaRepository repository, ReplicarRegistros periodo)
        {
            List<Despesa> despesas = await repository.GetByIds(periodo.IdRegistros);

            if (despesas.Count == 0)
                throw new Exception("Não foi encontrados registros");

            var periodoInicial = new DateTime(periodo.PeriodoInicial.Year, periodo.PeriodoInicial.Month, 1);
            var periodoFinal = new DateTime(periodo.PeriodoFinal.Year, periodo.PeriodoFinal.Month, 1);

            while (periodoInicial <= periodoFinal)
            {
                var novosRegistros = new List<Despesa>();

                foreach (var despesa in despesas)
                {
                    var clone = despesa.Clone();

                    clone.Ano = periodoInicial.Year;
                    clone.Mes = periodoInicial.Month;

                    Despesa registroCadastrado = await ObterRegistroJaCadastrado(repository, clone);

                    var ExisteRegistroCadastrado = registroCadastrado is not null;
                    if (ExisteRegistroCadastrado)
                    {
                        registroCadastrado.Valor = clone.Valor;
                        await repository.Update(registroCadastrado);
                    }
                    else
                    {
                        clone = await repository.Add(clone);
                    }

                    if (despesa.DespesaAgrupadora.HasValue && despesa.DespesaAgrupadora.Value)
                    {
                        await ClonarDesesasAgrupadas(repository, periodoInicial, despesa.Id, ExisteRegistroCadastrado ? registroCadastrado : clone);
                    }
                }

                periodoInicial = periodoInicial.AddMonths(1);
            }
        }

        private static async Task ClonarDesesasAgrupadas(IDespesaRepository repository, DateTime periodoInicial, string idDespesa, Despesa despesaCriadaOuExistente)
        {
            var despesasAgrupadas = await repository.GetDespesasDaAgrupadora(idDespesa);

            var clones = new List<Despesa>();

            foreach (var despesaFilha in despesasAgrupadas)
            {
                var cloneFilha = despesaFilha.Clone();
                cloneFilha.Ano = periodoInicial.Year;
                cloneFilha.Mes = periodoInicial.Month;
                cloneFilha.IdDespesaAgrupadora = despesaCriadaOuExistente.Id;

                var registroJaCadastrado = await ObterRegistroJaCadastrado(repository, cloneFilha);

                if (registroJaCadastrado is not null)
                {
                    registroJaCadastrado.Valor = cloneFilha.Valor;
                    await repository.Update(registroJaCadastrado);
                    continue;
                }

                clones.Add(cloneFilha);
            }

            if (clones.Count > 0)
                await repository.Add(clones);
        }

        private static async Task<T> ObterRegistroJaCadastrado<T>(IRepositoryBase<T> repository, T clone) where T : Transacao, IClone<T>
        {
            var registroIgualNoMes = await repository
                            .GetWhere(x => x.Ano == clone.Ano
                                && x.Mes == clone.Mes
                                && x.Descricao == clone.Descricao
                                && x.CategoriaId == clone.CategoriaId);

            var registroCadastrado = registroIgualNoMes.FirstOrDefault();
            return registroCadastrado;
        }
    }
}

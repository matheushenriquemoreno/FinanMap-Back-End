using Domain.Entity;
using Domain.Relatorios.AcumuladoMensal;
using Domain.Relatorios.Entity;
using Infra.Configure.Env;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Infra.Data.Mongo.Repositorys
{
    public class AcumuladoMensalRepository : IAcumuladoMensalReportRepository
    {
        protected readonly IMongoCollection<Rendimento> _rendimentoCollection;
        protected readonly IMongoCollection<Despesa> _despesaCollection;
        protected readonly IMongoCollection<Investimento> _investimentoCollection;

        public AcumuladoMensalRepository(IMongoClient mongoClient)
        {
            var mongoDatabase = mongoClient.GetDatabase(MongoDBSettings.DataBaseName);
            _rendimentoCollection = mongoDatabase.GetCollection<Rendimento>(nameof(Rendimento));
            _despesaCollection = mongoDatabase.GetCollection<Despesa>(nameof(Despesa));
            _investimentoCollection = mongoDatabase.GetCollection<Investimento>(nameof(Investimento));
        }

        public async Task<AcumuladoMensalReport> Obter(int mes, int ano, string idUsuario)
        {
            var totalrendimento = ObterValorMes(mes, ano, idUsuario, _rendimentoCollection);

            var filtroDespesa = new List<FilterDefinition<Despesa>>();
            filtroDespesa.Add(Builders<Despesa>.Filter.Eq(x => x.IdDespesaAgrupadora, null));
            var totalDespesa = ObterValorMes(mes, ano, idUsuario, _despesaCollection, filtroDespesa);
            
            var totalInvestimento = ObterValorMes(mes, ano, idUsuario, _investimentoCollection);

            await Task.WhenAll(totalrendimento, totalDespesa, totalInvestimento);

            return new AcumuladoMensalReport(ano, mes, await totalrendimento, await totalInvestimento, await totalDespesa);
        }

        private async Task<decimal> ObterValorMes<T>(int mes, int ano, string idUsuario, IMongoCollection<T> mongoCollection, List<FilterDefinition<T>> filtrosAdicionais = null) where T : Transacao
        {
            FilterDefinition<T> filter = FiltrosMesAno<T>(mes, ano, idUsuario);

            if (filtrosAdicionais != null && filtrosAdicionais.Any())
            {
                foreach (var filtro in filtrosAdicionais)
                {
                    filter = Builders<T>.Filter.And(filter, filtro);
                }
            }

            var pipelineExecutionMongo = new EmptyPipelineDefinition<T>()
                        .Match(filter)
                        .Group(r => mes + ano,
                            g => new
                            {
                                Total = g.Sum(x => x.Valor)
                            }
                        );

            var result = await mongoCollection
                .Aggregate(pipelineExecutionMongo)
                .FirstOrDefaultAsync();

            return result?.Total ?? 0;
        }
        
        private static FilterDefinition<T> FiltrosMesAno<T>(int mes, int ano, string idUsuario) where T : Transacao
        {
            return Builders<T>.Filter.And(
                            Builders<T>.Filter.Eq(x => x.Ano, ano),
                            Builders<T>.Filter.Eq(x => x.Mes, mes),
                            Builders<T>.Filter.Eq(x => x.UsuarioId, idUsuario)
                        );
        }
    }
}

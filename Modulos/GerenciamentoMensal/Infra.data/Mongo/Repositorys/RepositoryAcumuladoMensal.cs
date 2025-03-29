using Domain.Entity;
using Domain.Relatorios.AcumuladoMensal;
using Domain.Relatorios.Entity;
using Infra.Configure.Env;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Infra.Data.Mongo.Repositorys
{
    public class RepositoryAcumuladoMensal : IAcumuladoMensalReportRepository
    {
        protected readonly IMongoCollection<Rendimento> _rendimentoCollection;
        protected readonly IMongoCollection<Despesa> _despesaCollection;
        protected readonly IMongoCollection<Investimento> _investimentoCollection;

        public RepositoryAcumuladoMensal(IMongoClient mongoClient)
        {
            var mongoDatabase = mongoClient.GetDatabase(MongoDBSettings.DataBaseName);
            _rendimentoCollection = mongoDatabase.GetCollection<Rendimento>(nameof(Rendimento));
            _despesaCollection = mongoDatabase.GetCollection<Despesa>(nameof(Despesa));
            _investimentoCollection = mongoDatabase.GetCollection<Investimento>(nameof(Investimento));
        }

        public async Task<AcumuladoMensalReport> Obter(int mes, int ano, string idUsuario)
        {
            var totalrendimento = ObterValorMes(mes, ano, idUsuario, _rendimentoCollection);
            var totalDespesa = ObterValorMes(mes, ano, idUsuario, _despesaCollection);
            var totalInvestimento = ObterValorMes(mes, ano, idUsuario, _investimentoCollection);

            await Task.WhenAll(totalrendimento, totalDespesa, totalInvestimento);

            return new AcumuladoMensalReport(ano, mes, await totalrendimento, await totalInvestimento, await totalDespesa);
        }

        private async Task<decimal> ObterValorMes<T>(int mes, int ano, string idUsuario, IMongoCollection<T> mongoCollection) where T : Transacao
        {
            FilterDefinition<T> filter = FiltrosMesAno<T>(mes, ano, idUsuario);

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

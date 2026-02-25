#nullable enable
using Domain.Dashboard.Models;
using Domain.Entity;
using Infra.Configure.Env;
using Infra.Mongo.Repositorys;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Infra.Data.Mongo.Repositorys
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly IMongoCollection<Rendimento> _rendimentoCollection;
        private readonly IMongoCollection<Despesa> _despesaCollection;
        private readonly IMongoCollection<Investimento> _investimentoCollection;

        public DashboardRepository(IMongoClient mongoClient)
        {
            var database = mongoClient.GetDatabase(MongoDBSettings.DataBaseName);
            _rendimentoCollection = database.GetCollection<Rendimento>(nameof(Rendimento));
            _despesaCollection = database.GetCollection<Despesa>(nameof(Despesa));
            _investimentoCollection = database.GetCollection<Investimento>(nameof(Investimento));
        }

        public async Task<ResumoFinanceiroModel> ObterResumoFinanceiro(string usuarioId, int mesInicial, int anoInicial, int mesFinal, int anoFinal)
        {
            var rendimentoTotal = await ObterTotalPeriodo(_rendimentoCollection, usuarioId, mesInicial, anoInicial, mesFinal, anoFinal);
            var despesaTotal = await ObterTotalPeriodoDespesa(usuarioId, mesInicial, anoInicial, mesFinal, anoFinal);
            var investimentoTotal = await ObterTotalPeriodo(_investimentoCollection, usuarioId, mesInicial, anoInicial, mesFinal, anoFinal);

            var isMesUnico = (mesInicial == mesFinal && anoInicial == anoFinal);
            var rendimentoTendencia = await ObterTendencia(_rendimentoCollection, usuarioId, mesInicial, anoInicial, mesFinal, anoFinal, isMesUnico);
            var despesaTendencia = await ObterTendenciaDespesa(usuarioId, mesInicial, anoInicial, mesFinal, anoFinal, isMesUnico);
            var investimentoTendencia = await ObterTendencia(_investimentoCollection, usuarioId, mesInicial, anoInicial, mesFinal, anoFinal, isMesUnico);

            return new ResumoFinanceiroModel(
                new ResumoItemModel(rendimentoTotal, rendimentoTendencia),
                new ResumoItemModel(despesaTotal, despesaTendencia),
                new ResumoItemModel(investimentoTotal, investimentoTendencia)
            );
        }

        public async Task<List<EvolucaoPeriodoModel>> ObterEvolucaoPeriodo(string usuarioId, int mesInicial, int anoInicial, int mesFinal, int anoFinal)
        {
            var isMesUnico = (mesInicial == mesFinal && anoInicial == anoFinal);
            var resultado = new List<EvolucaoPeriodoModel>();

            if (isMesUnico)
            {
                var semanas = ObterSemanasMes(anoInicial, mesInicial);
                foreach (var (numeroSemana, diaInicio, diaFim) in semanas)
                {
                    var rendimento = await ObterTotalPeriodoComDias(_rendimentoCollection, usuarioId, mesInicial, anoInicial, 1, diaFim);
                    var despesa = await ObterTotalPeriodoComDiasDespesa(usuarioId, mesInicial, anoInicial, 1, diaFim);
                    var investimento = await ObterTotalPeriodoComDias(_investimentoCollection, usuarioId, mesInicial, anoInicial, 1, diaFim);

                    resultado.Add(new EvolucaoPeriodoModel($"Semana {numeroSemana}", rendimento, despesa, investimento));
                }
            }
            else
            {
                var nomesMeses = new[] { "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez" };
                var periodos = GerarPeriodosMensal(mesInicial, anoInicial, mesFinal, anoFinal);

                foreach (var (mes, ano) in periodos)
                {
                    var rendimento = await ObterTotalPeriodo(_rendimentoCollection, usuarioId, mes, ano, mes, ano);
                    var despesa = await ObterTotalPeriodoDespesa(usuarioId, mes, ano, mes, ano);
                    var investimento = await ObterTotalPeriodo(_investimentoCollection, usuarioId, mes, ano, mes, ano);

                    resultado.Add(new EvolucaoPeriodoModel(nomesMeses[mes - 1], rendimento, despesa, investimento));
                }
            }

            return resultado;
        }

        public async Task<List<CategoriaDashboardModel>> ObterDistribuicaoCategorias(string usuarioId, int mesInicial, int anoInicial, int mesFinal, int anoFinal, string? tipo)
        {
            var resultado = new List<CategoriaDashboardModel>();

            if (string.IsNullOrEmpty(tipo) || tipo == "Rendimento")
                resultado.AddRange(await ObterDistribuicaoPorCollection(_rendimentoCollection, "Rendimento", usuarioId, mesInicial, anoInicial, mesFinal, anoFinal));

            if (string.IsNullOrEmpty(tipo) || tipo == "Despesa")
                resultado.AddRange(await ObterDistribuicaoPorCollectionDespesa(usuarioId, mesInicial, anoInicial, mesFinal, anoFinal));

            if (string.IsNullOrEmpty(tipo) || tipo == "Investimento")
                resultado.AddRange(await ObterDistribuicaoPorCollection(_investimentoCollection, "Investimento", usuarioId, mesInicial, anoInicial, mesFinal, anoFinal));

            return resultado;
        }

        // --- Helper Methods ---

        private FilterDefinition<T> CriarFiltroPeriodo<T>(string usuarioId, int mesInicial, int anoInicial, int mesFinal, int anoFinal) where T : Transacao
        {
            if (anoInicial == anoFinal)
            {
                return Builders<T>.Filter.And(
                    Builders<T>.Filter.Eq("UsuarioId", ObjectId.Parse(usuarioId)),
                    Builders<T>.Filter.Eq(x => x.Ano, anoInicial),
                    Builders<T>.Filter.Gte(x => x.Mes, mesInicial),
                    Builders<T>.Filter.Lte(x => x.Mes, mesFinal)
                );
            }

            return Builders<T>.Filter.And(
                Builders<T>.Filter.Eq("UsuarioId", ObjectId.Parse(usuarioId)),
                Builders<T>.Filter.Or(
                    Builders<T>.Filter.And(
                        Builders<T>.Filter.Eq(x => x.Ano, anoInicial),
                        Builders<T>.Filter.Gte(x => x.Mes, mesInicial)
                    ),
                    Builders<T>.Filter.And(
                        Builders<T>.Filter.Gt(x => x.Ano, anoInicial),
                        Builders<T>.Filter.Lt(x => x.Ano, anoFinal)
                    ),
                    Builders<T>.Filter.And(
                        Builders<T>.Filter.Eq(x => x.Ano, anoFinal),
                        Builders<T>.Filter.Lte(x => x.Mes, mesFinal)
                    )
                )
            );
        }

        private async Task<decimal> ObterTotalPeriodo<T>(IMongoCollection<T> collection, string usuarioId, int mesInicial, int anoInicial, int mesFinal, int anoFinal) where T : Transacao
        {
            var filter = CriarFiltroPeriodo<T>(usuarioId, mesInicial, anoInicial, mesFinal, anoFinal);

            return await collection.Aggregate()
                .Match(filter)
                .Group(x => 1, g => new { Total = g.Sum(x => x.Valor) })
                .Project(x => x.Total)
                .FirstOrDefaultAsync();
        }

        private async Task<decimal> ObterTotalPeriodoDespesa(string usuarioId, int mesInicial, int anoInicial, int mesFinal, int anoFinal)
        {
            var filterBase = CriarFiltroPeriodo<Despesa>(usuarioId, mesInicial, anoInicial, mesFinal, anoFinal);
            var filter = Builders<Despesa>.Filter.And(
                filterBase,
                Builders<Despesa>.Filter.Eq(x => x.IdDespesaAgrupadora, null) // Filtro específico de despesa
            );

            return await _despesaCollection.Aggregate()
                .Match(filter)
                .Group(x => 1, g => new { Total = g.Sum(x => x.Valor) })
                .Project(x => x.Total)
                .FirstOrDefaultAsync();
        }

        private async Task<decimal> ObterTotalPeriodoComDias<T>(IMongoCollection<T> collection, string usuarioId, int mes, int ano, int diaInicio, int diaFim) where T : Transacao
        {
            var dataFimDt = new DateTime(ano, mes, diaFim, 23, 59, 59);

            var filter = Builders<T>.Filter.And(
                Builders<T>.Filter.Eq("UsuarioId", ObjectId.Parse(usuarioId)),
                Builders<T>.Filter.Eq(x => x.Ano, ano),
                Builders<T>.Filter.Eq(x => x.Mes, mes)
            );

            if (diaFim < DateTime.DaysInMonth(ano, mes))
            {
                filter &= Builders<T>.Filter.Lte(x => x.DataCriacao, dataFimDt);
            }

            return await collection.Aggregate()
                .Match(filter)
                .Group(x => 1, g => new { Total = g.Sum(x => x.Valor) })
                .Project(x => x.Total)
                .FirstOrDefaultAsync();
        }

        private async Task<decimal> ObterTotalPeriodoComDiasDespesa(string usuarioId, int mes, int ano, int diaInicio, int diaFim)
        {
            var dataFimDt = new DateTime(ano, mes, diaFim, 23, 59, 59);

            var filter = Builders<Despesa>.Filter.And(
                Builders<Despesa>.Filter.Eq("UsuarioId", ObjectId.Parse(usuarioId)),
                Builders<Despesa>.Filter.Eq(x => x.Ano, ano),
                Builders<Despesa>.Filter.Eq(x => x.Mes, mes),
                Builders<Despesa>.Filter.Eq(x => x.IdDespesaAgrupadora, null) // Filtro específico
            );

            if (diaFim < DateTime.DaysInMonth(ano, mes))
            {
                filter &= Builders<Despesa>.Filter.Lte(x => x.DataCriacao, dataFimDt);
            }

            return await _despesaCollection.Aggregate()
                .Match(filter)
                .Group(x => 1, g => new { Total = g.Sum(x => x.Valor) })
                .Project(x => x.Total)
                .FirstOrDefaultAsync();
        }

        private async Task<List<decimal>> ObterTendencia<T>(IMongoCollection<T> collection, string usuarioId, int mesInicial, int anoInicial, int mesFinal, int anoFinal, bool porSemana) where T : Transacao
        {
            var tendencia = new List<decimal>();

            if (porSemana)
            {
                var semanas = ObterSemanasMes(anoInicial, mesInicial);
                foreach (var (_, diaInicio, diaFim) in semanas)
                {
                    tendencia.Add(await ObterTotalPeriodoComDias(collection, usuarioId, mesInicial, anoInicial, 1, diaFim));
                }
            }
            else
            {
                var periodos = GerarPeriodosMensal(mesInicial, anoInicial, mesFinal, anoFinal);
                foreach (var (mes, ano) in periodos)
                {
                    tendencia.Add(await ObterTotalPeriodo(collection, usuarioId, mesInicial, anoInicial, mes, ano));
                }
            }
            return tendencia;
        }

        private async Task<List<decimal>> ObterTendenciaDespesa(string usuarioId, int mesInicial, int anoInicial, int mesFinal, int anoFinal, bool porSemana)
        {
            var tendencia = new List<decimal>();

            if (porSemana)
            {
                var semanas = ObterSemanasMes(anoInicial, mesInicial);
                foreach (var (_, diaInicio, diaFim) in semanas)
                {
                    tendencia.Add(await ObterTotalPeriodoComDiasDespesa(usuarioId, mesInicial, anoInicial, 1, diaFim));
                }
            }
            else
            {
                var periodos = GerarPeriodosMensal(mesInicial, anoInicial, mesFinal, anoFinal);
                foreach (var (mes, ano) in periodos)
                {
                    tendencia.Add(await ObterTotalPeriodoDespesa(usuarioId, mesInicial, anoInicial, mes, ano));
                }
            }
            return tendencia;
        }

        private async Task<List<CategoriaDashboardModel>> ObterDistribuicaoPorCollection<T>(IMongoCollection<T> collection, string tipo, string usuarioId, int mesInicial, int anoInicial, int mesFinal, int anoFinal) where T : Transacao
        {
            var filter = CriarFiltroPeriodo<T>(usuarioId, mesInicial, anoInicial, mesFinal, anoFinal);

            // Using BsonDocument pipeline to avoid mapping issues with Categoria property
            var categoriaCollection = collection.Database.GetCollection<BsonDocument>("Categoria");

            var pipeline = new EmptyPipelineDefinition<T>()
                .Match(filter)
                .Group(
                    x => x.CategoriaId,
                    g => new BsonDocument
                    {
                        { "CategoriaId", g.Key },
                        { "Total", g.Sum(x => x.Valor) }
                    }
                )
                .Lookup<T, BsonDocument, BsonDocument, BsonDocument>(categoriaCollection, "CategoriaId", "_id", "CategoriaInfo")
                .Unwind("CategoriaInfo")
                .Project(new BsonDocument {
                    { "Categoria", "$CategoriaInfo.Nome" },
                    { "Total", "$Total" }
                });

            var groupResult = await collection.Aggregate(pipeline).ToListAsync();

            var resultModel = groupResult.Select(x => new
            {
                Categoria = x.GetValue("Categoria").AsString,
                Total = x.GetValue("Total").AsDecimal
            }).ToList();

            var totalGeral = resultModel.Sum(x => x.Total);

            return resultModel.Select(x => new CategoriaDashboardModel(
                x.Categoria ?? "Sem Categoria",
                x.Total,
                tipo,
                totalGeral > 0 ? Math.Round((x.Total / totalGeral) * 100, 2) : 0
            )).ToList();
        }

        private async Task<List<CategoriaDashboardModel>> ObterDistribuicaoPorCollectionDespesa(string usuarioId, int mesInicial, int anoInicial, int mesFinal, int anoFinal)
        {
            var filterBase = CriarFiltroPeriodo<Despesa>(usuarioId, mesInicial, anoInicial, mesFinal, anoFinal);
            var filter = Builders<Despesa>.Filter.And(
                filterBase,
                Builders<Despesa>.Filter.Eq(x => x.IdDespesaAgrupadora, null)
            );

            // Using BsonDocument pipeline to avoid mapping issues with Categoria property
            var categoriaCollection = _despesaCollection.Database.GetCollection<BsonDocument>("Categoria");

            var pipeline = new EmptyPipelineDefinition<Despesa>()
                .Match(filter)
                .Group(
                    x => x.CategoriaId,
                    g => new BsonDocument
                    {
                        { "CategoriaId", g.Key },
                        { "Total", g.Sum(x => x.Valor) }
                    }
                )
                .Lookup<Despesa, BsonDocument, BsonDocument, BsonDocument>(categoriaCollection, "CategoriaId", "_id", "CategoriaInfo")
                .Unwind("CategoriaInfo")
                .Project(new BsonDocument {
                    { "Categoria", "$CategoriaInfo.Nome" },
                    { "Total", "$Total" }
                });

            var groupResult = await _despesaCollection.Aggregate(pipeline).ToListAsync();

            var resultModel = groupResult.Select(x => new
            {
                Categoria = x.GetValue("Categoria").AsString,
                Total = x.GetValue("Total").AsDecimal
            }).ToList();

            var totalGeral = resultModel.Sum(x => x.Total);

            return resultModel.Select(x => new CategoriaDashboardModel(
                x.Categoria ?? "Sem Categoria",
                x.Total,
                "Despesa",
                totalGeral > 0 ? Math.Round((x.Total / totalGeral) * 100, 2) : 0
            )).ToList();
        }

        private List<(int NumeroSemana, int DiaInicio, int DiaFim)> ObterSemanasMes(int ano, int mes)
        {
            var semanas = new List<(int, int, int)>();
            var ultimoDia = DateTime.DaysInMonth(ano, mes);

            var diaAtual = 1;
            var numeroSemana = 1;

            while (diaAtual <= ultimoDia)
            {
                var diaInicio = diaAtual;
                var diaFim = Math.Min(diaAtual + 6, ultimoDia);

                semanas.Add((numeroSemana, diaInicio, diaFim));

                diaAtual = diaFim + 1;
                numeroSemana++;
            }

            return semanas;
        }

        private List<(int Mes, int Ano)> GerarPeriodosMensal(int mesInicial, int anoInicial, int mesFinal, int anoFinal)
        {
            var periodos = new List<(int, int)>();
            var ano = anoInicial;
            var mes = mesInicial;
            while (ano < anoFinal || (ano == anoFinal && mes <= mesFinal))
            {
                periodos.Add((mes, ano));
                mes++;
                if (mes > 12)
                {
                    mes = 1;
                    ano++;
                }
            }
            return periodos;
        }
    }
}

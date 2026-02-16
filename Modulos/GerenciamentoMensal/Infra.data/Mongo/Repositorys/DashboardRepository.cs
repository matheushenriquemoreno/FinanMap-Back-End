using Domain.Dashboard.Models;
using Infra.Data.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infra.Mongo.Repositorys;

public class DashboardRepository : IDashboardRepository
{
    private readonly IMongoDatabase _database;

    public DashboardRepository(IMongoClient mongoClient)
    {
        _database = mongoClient.GetDatabase();
    }

    public async Task<ResumoFinanceiroModel> ObterResumoFinanceiro(string usuarioId, int mesInicial, int mesFinal, int ano)
    {
        var dataInicio = new DateTime(ano, mesInicial, 1);
        var dataFim = new DateTime(ano, mesFinal, DateTime.DaysInMonth(ano, mesFinal), 23, 59, 59);

        // Obter totais
        var rendimentoTotal = await ObterTotalPorTipo("Rendimento", usuarioId, dataInicio, dataFim);
        var despesaTotal = await ObterTotalPorTipo("Despesa", usuarioId, dataInicio, dataFim);
        var investimentoTotal = await ObterTotalPorTipo("Investimento", usuarioId, dataInicio, dataFim);

        // Obter tendências (valores semanais ou mensais)
        var rendimentoTendencia = await ObterTendencia("Rendimento", usuarioId, dataInicio, dataFim, mesInicial == mesFinal);
        var despesaTendencia = await ObterTendencia("Despesa", usuarioId, dataInicio, dataFim, mesInicial == mesFinal);
        var investimentoTendencia = await ObterTendencia("Investimento", usuarioId, dataInicio, dataFim, mesInicial == mesFinal);

        return new ResumoFinanceiroModel(
            new ResumoItemModel(rendimentoTotal, rendimentoTendencia),
            new ResumoItemModel(despesaTotal, despesaTendencia),
            new ResumoItemModel(investimentoTotal, investimentoTendencia)
        );
    }

    public async Task<List<EvolucaoPeriodoModel>> ObterEvolucaoPeriodo(string usuarioId, int mesInicial, int mesFinal, int ano)
    {
        var dataInicio = new DateTime(ano, mesInicial, 1);
        var dataFim = new DateTime(ano, mesFinal, DateTime.DaysInMonth(ano, mesFinal), 23, 59, 59);
        var isMesUnico = mesInicial == mesFinal;

        var resultado = new List<EvolucaoPeriodoModel>();

        if (isMesUnico)
        {
            // Agrupar por semanas
            var semanas = ObterSemanasMes(ano, mesInicial);

            foreach (var (numeroSemana, inicioSemana, fimSemana) in semanas)
            {
                var rendimento = await ObterTotalPorTipo("Rendimento", usuarioId, inicioSemana, fimSemana);
                var despesa = await ObterTotalPorTipo("Despesa", usuarioId, inicioSemana, fimSemana);
                var investimento = await ObterTotalPorTipo("Investimento", usuarioId, inicioSemana, fimSemana);

                resultado.Add(new EvolucaoPeriodoModel(
                    $"Semana {numeroSemana}",
                    rendimento,
                    despesa,
                    investimento
                ));
            }
        }
        else
        {
            // Agrupar por meses
            for (int mes = mesInicial; mes <= mesFinal; mes++)
            {
                var inicioMes = new DateTime(ano, mes, 1);
                var fimMes = new DateTime(ano, mes, DateTime.DaysInMonth(ano, mes), 23, 59, 59);

                var rendimento = await ObterTotalPorTipo("Rendimento", usuarioId, inicioMes, fimMes);
                var despesa = await ObterTotalPorTipo("Despesa", usuarioId, inicioMes, fimMes);
                var investimento = await ObterTotalPorTipo("Investimento", usuarioId, inicioMes, fimMes);

                var nomesMeses = new[] { "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez" };

                resultado.Add(new EvolucaoPeriodoModel(
                    nomesMeses[mes - 1],
                    rendimento,
                    despesa,
                    investimento
                ));
            }
        }

        return resultado;
    }

    public async Task<List<CategoriaDashboardModel>> ObterDistribuicaoCategorias(string usuarioId, int mesInicial, int mesFinal, int ano, string? tipo)
    {
        var dataInicio = new DateTime(ano, mesInicial, 1);
        var dataFim = new DateTime(ano, mesFinal, DateTime.DaysInMonth(ano, mesFinal), 23, 59, 59);

        var tipos = string.IsNullOrEmpty(tipo)
            ? new[] { "Rendimento", "Despesa", "Investimento" }
            : new[] { tipo };

        var resultado = new List<CategoriaDashboardModel>();

        foreach (var tipoTransacao in tipos)
        {
            var collection = _database.GetCollection<BsonDocument>(tipoTransacao);

            var pipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument
                {
                    { "UsuarioId", usuarioId },
                    { "Data", new BsonDocument
                        {
                            { "$gte", dataInicio },
                            { "$lte", dataFim }
                        }
                    }
                }),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$CategoriaId" },
                    { "total", new BsonDocument("$sum", "$Valor") }
                }),
                new BsonDocument("$lookup", new BsonDocument
                {
                    { "from", "Categoria" },
                    { "localField", "_id" },
                    { "foreignField", "_id" },
                    { "as", "categoria" }
                }),
                new BsonDocument("$unwind", "$categoria"),
                new BsonDocument("$project", new BsonDocument
                {
                    { "categoria", "$categoria.Nome" },
                    { "valor", "$total" }
                })
            };

            var agregacao = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
            var totalTipo = agregacao.Sum(x => x["valor"].AsDecimal);

            foreach (var item in agregacao)
            {
                var valor = item["valor"].AsDecimal;
                var percentual = totalTipo > 0 ? (valor / totalTipo) * 100 : 0;

                resultado.Add(new CategoriaDashboardModel(
                    item["categoria"].AsString,
                    valor,
                    tipoTransacao,
                    Math.Round(percentual, 2)
                ));
            }
        }

        return resultado;
    }

    private async Task<decimal> ObterTotalPorTipo(string tipo, string usuarioId, DateTime dataInicio, DateTime dataFim)
    {
        var collection = _database.GetCollection<BsonDocument>(tipo);

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("UsuarioId", usuarioId),
            Builders<BsonDocument>.Filter.Gte("Data", dataInicio),
            Builders<BsonDocument>.Filter.Lte("Data", dataFim)
        );

        var pipeline = new[]
        {
            new BsonDocument("$match", filter.Render(new RenderArgs<BsonDocument>(collection.DocumentSerializer, collection.Settings.SerializerRegistry))),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", BsonNull.Value },
                { "total", new BsonDocument("$sum", "$Valor") }
            })
        };

        var resultado = await collection.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();

        return resultado?["total"].AsDecimal ?? 0;
    }

    private async Task<List<decimal>> ObterTendencia(string tipo, string usuarioId, DateTime dataInicio, DateTime dataFim, bool porSemana)
    {
        var tendencia = new List<decimal>();

        if (porSemana)
        {
            var semanas = ObterSemanasMes(dataInicio.Year, dataInicio.Month);
            foreach (var (_, inicioSemana, fimSemana) in semanas)
            {
                var total = await ObterTotalPorTipo(tipo, usuarioId, inicioSemana, fimSemana);
                tendencia.Add(total);
            }
        }
        else
        {
            for (int mes = dataInicio.Month; mes <= dataFim.Month; mes++)
            {
                var inicioMes = new DateTime(dataInicio.Year, mes, 1);
                var fimMes = new DateTime(dataInicio.Year, mes, DateTime.DaysInMonth(dataInicio.Year, mes), 23, 59, 59);

                var total = await ObterTotalPorTipo(tipo, usuarioId, inicioMes, fimMes);
                tendencia.Add(total);
            }
        }

        return tendencia;
    }

    private List<(int NumeroSemana, DateTime Inicio, DateTime Fim)> ObterSemanasMes(int ano, int mes)
    {
        var semanas = new List<(int, DateTime, DateTime)>();
        var primeiroDia = new DateTime(ano, mes, 1);
        var ultimoDia = new DateTime(ano, mes, DateTime.DaysInMonth(ano, mes));

        var diaAtual = primeiroDia;
        var numeroSemana = 1;

        while (diaAtual <= ultimoDia)
        {
            var inicioSemana = diaAtual;
            var fimSemana = diaAtual.AddDays(6);

            if (fimSemana > ultimoDia)
                fimSemana = ultimoDia;

            semanas.Add((numeroSemana, inicioSemana, new DateTime(fimSemana.Year, fimSemana.Month, fimSemana.Day, 23, 59, 59)));

            diaAtual = fimSemana.AddDays(1);
            numeroSemana++;
        }

        return semanas;
    }
}

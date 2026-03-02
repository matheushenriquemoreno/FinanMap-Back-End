using Domain.Entity;
using Infra.Data.Mongo.Config.Interface;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Infra.Data.Mongo.Mappings;

public class MetaFinanceiraMapping : IMongoMapping
{
    public void RegisterMap(IMongoClient mongoClient)
    {
        BsonClassMap.TryRegisterClassMap<Domain.Entity.MetaFinanceira>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapMember(c => c.Nome);
            cm.MapMember(c => c.ValorAlvo);
            cm.MapMember(c => c.DataLimite);
            cm.MapMember(c => c.Categoria);
            cm.MapMember(c => c.UsuarioId);
            cm.MapMember(c => c.DataCriacao);
            cm.MapMember(c => c.Contribuicoes);
        });

        BsonClassMap.TryRegisterClassMap<Contribuicao>(cm =>
        {
            cm.AutoMap();
            cm.MapMember(c => c.Id);
            cm.MapMember(c => c.Valor);
            cm.MapMember(c => c.Data);
            cm.MapMember(c => c.InvestimentoId);
            cm.MapMember(c => c.NomeInvestimento);
            cm.MapMember(c => c.Origem).SetSerializer(new MongoDB.Bson.Serialization.Serializers.EnumSerializer<OrigemContribuicao>(MongoDB.Bson.BsonType.String));
        });
    }
}

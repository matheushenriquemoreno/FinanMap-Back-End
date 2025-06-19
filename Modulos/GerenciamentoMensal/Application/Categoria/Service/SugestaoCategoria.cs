using Application.IA;
using Application.Interfaces;
using Domain.Enum;

namespace Application.Service
{
    public class SugestaoCategoria : ISugestaoCategoria
    {
        private readonly IAGenerativaChat _iaChat;

        public SugestaoCategoria(IAGenerativaChat iaChat)
        {
            _iaChat = iaChat;
        }

        private static string instrucao = @"
Você vai servir de um apoio de cadastro de categorias em um site que gerencia as finaças pessoais, vou sempre te passar o tipo da categoria, e o nome do item a ser cadastrado, com isso e vai me dar dicas,
visando ficar mais fácil o cadastro para o usuário final.
O resultado e somente o nome da categoria, e vou sempre te mandar assim: Tipo: Despesa, Nome: Aliança Casamento ou Tipo: Investimento, Nome: CDB de 110% do CDI ou Tipo: Rendimento, Nome: Servicos como freelancer.
Ai com base nessas informações você vai me mandar uma lista (a Lista deve conter no minimo 7 itens) de categorias separadas por vírgula que poderia se encaixar no tipo e no nome do item.".Trim();

        public Result<List<string>> ObterSurgestoesDeCategoriaBaseadoNoItemACadastrar(TipoCategoria tipo, string nomeItem)
        {
            var response = _iaChat.GetReponse(instrucao, $"Tipo: {tipo}, Nome: {nomeItem}");

            if (string.IsNullOrEmpty(response))
                return Result.Failure<List<string>>(Error.NotFound("Não houve foi possivel obter sugestões de categorias"));

            var categorias = response.Split(",");

            if (categorias.Length == 0)
                return Result.Failure<List<string>>(Error.NotFound("Não houve foi possivel obter sugestões de categorias"));

            return Result.Success(categorias
                .Select(x => x.Trim())
                .ToList());
        }
    }
}

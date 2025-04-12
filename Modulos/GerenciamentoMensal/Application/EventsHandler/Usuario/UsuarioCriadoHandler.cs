using Domain.Entity;
using Domain.Enum;
using Domain.Event;
using Domain.Repository;
using MediatR;

namespace Application.EventsHandler.Usuario
{
    public class UsuarioCriadoHandler : INotificationHandler<UsuarioCriadoEvent>
    {
        private readonly ICategoriaRepository _categoriaRepository;

        public UsuarioCriadoHandler(ICategoriaRepository categoriaRepository)
        {
            _categoriaRepository = categoriaRepository;
        }

        public async Task Handle(UsuarioCriadoEvent notification, CancellationToken cancellationToken)
        {
            var idUsuario = notification.Usuario.Id;

            // Despesas
            var categoriasPadraoDespesa = new List<Categoria>
            {
                new Categoria("Moradia", TipoCategoria.Despesa, idUsuario),
                new Categoria("Alimentação", TipoCategoria.Despesa, idUsuario),
                new Categoria("Transporte", TipoCategoria.Despesa, idUsuario),
                new Categoria("Educação", TipoCategoria.Despesa, idUsuario),
                new Categoria("Saúde", TipoCategoria.Despesa, idUsuario),
                new Categoria("Contas e Serviços", TipoCategoria.Despesa, idUsuario),
                new Categoria("Dívidas e Financiamentos", TipoCategoria.Despesa, idUsuario),
                new Categoria("Doações e Presentes", TipoCategoria.Despesa, idUsuario),
                new Categoria("Cartão de Credito", TipoCategoria.Despesa, idUsuario),
                new Categoria("Festas e Eventos", TipoCategoria.Despesa, idUsuario),
                new Categoria("Lazer e Entretenimento", TipoCategoria.Despesa, idUsuario),
                new Categoria("Serviços de streaming", TipoCategoria.Despesa, idUsuario),
            };

            // Rendimentos
            var categoriasPadraoRendimento = new List<Categoria>
            {
                new Categoria("Salário", TipoCategoria.Rendimento, idUsuario),
                new Categoria("Decimo Terceiro", TipoCategoria.Rendimento, idUsuario),
                new Categoria("Renda Extra", TipoCategoria.Rendimento, idUsuario),
                new Categoria("Dividendos", TipoCategoria.Rendimento, idUsuario),
                new Categoria("Reembolsos", TipoCategoria.Rendimento, idUsuario),
                new Categoria("Benefícios", TipoCategoria.Rendimento, idUsuario),
            };

            // Investimentos
            var categoriasPadraoInvestimento = new List<Categoria>
            {
                new Categoria("Ações", TipoCategoria.Investimento, idUsuario),
                new Categoria("Fundos Imobiliários", TipoCategoria.Investimento, idUsuario),
                new Categoria("Criptomoedas", TipoCategoria.Investimento, idUsuario),
                new Categoria("Renda Fixa", TipoCategoria.Investimento, idUsuario),
                new Categoria("Fundos de Investimento", TipoCategoria.Investimento, idUsuario),
                new Categoria("Previdência Privada", TipoCategoria.Investimento, idUsuario),
                new Categoria("Outros Investimentos", TipoCategoria.Investimento, idUsuario),
                new Categoria("Reserva de Emergencia", TipoCategoria.Investimento, idUsuario)
            };

            var categorias = new List<Categoria>();
            categorias.AddRange(categoriasPadraoDespesa);
            categorias.AddRange(categoriasPadraoRendimento);
            categorias.AddRange(categoriasPadraoInvestimento);

            await Parallel.ForEachAsync(categorias, async (categoria, token) =>
            {
                await _categoriaRepository.Add(categoria);
            });
        }

    }
}

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
                    new Categoria("Cartão de Crédito", TipoCategoria.Despesa, idUsuario),
                    new Categoria("Supermercado", TipoCategoria.Despesa, idUsuario),
                    new Categoria("Financiamento", TipoCategoria.Despesa, idUsuario),
                };

            // Rendimentos
            var categoriasPadraoRendimento = new List<Categoria>
                {
                    new Categoria("Salario", TipoCategoria.Rendimento, idUsuario),
                    new Categoria("Renda Extra", TipoCategoria.Rendimento, idUsuario),
                    new Categoria("Cashback", TipoCategoria.Rendimento, idUsuario)
                };

            // Investimentos
            var categoriasPadraoInvestimento = new List<Categoria>
                {
                    new Categoria("Ações", TipoCategoria.Investimento, idUsuario),
                    new Categoria("Renda fixa", TipoCategoria.Investimento, idUsuario),
                    new Categoria("Reserva de Emergencia", TipoCategoria.Investimento, idUsuario),
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

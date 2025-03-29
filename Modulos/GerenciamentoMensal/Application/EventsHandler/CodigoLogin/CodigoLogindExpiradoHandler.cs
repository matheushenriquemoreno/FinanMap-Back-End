using Domain.Login.Events;
using Domain.Login.Repository;
using MediatR;

namespace Application.EventsHandler.CodigoLogin
{
    public class CodigoLogindExpiradoHandler : INotificationHandler<CodigoLoginExpiradoEvent>
    {
        private readonly ICodigoLoginRepository _codigoLoginRepository;

        public CodigoLogindExpiradoHandler(ICodigoLoginRepository codigoLoginRepository)
        {
            _codigoLoginRepository = codigoLoginRepository;
        }

        public async Task Handle(CodigoLoginExpiradoEvent notification, CancellationToken cancellationToken)
        {
            await _codigoLoginRepository.DeleteExpirados(notification.CodigoLogin.Email);
        }
    }
}

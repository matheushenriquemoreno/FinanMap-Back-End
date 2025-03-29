using Domain.Login.Entity;
using MediatR;

namespace Domain.Login.Events
{
    public class CodigoLoginExpiradoEvent : INotification
    {
        public CodigoLogin CodigoLogin { get; private set; }

        public CodigoLoginExpiradoEvent(CodigoLogin codigoLogin)
        {
            CodigoLogin = codigoLogin;
        }
    }
}

using Domain.Entity;
using MediatR;

namespace Domain.Event;

public class UsuarioCriadoEvent : INotification
{
    public Usuario Usuario { get; set; }

    public UsuarioCriadoEvent(Usuario usuario)
    {
        Usuario = usuario;
    }
}

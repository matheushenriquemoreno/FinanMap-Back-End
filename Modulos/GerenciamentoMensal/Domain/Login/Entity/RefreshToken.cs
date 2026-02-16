using Domain.Entity;

namespace Domain.Login.Entity;

public class RefreshToken : EntityBase
{
    public string Token { get; private set; }
    public string UsuarioId { get; private set; }
    public DateTime DataCriacao { get; private set; }
    public DateTime DataExpiracao { get; private set; }

    private const int DiasExpiracao = 45;

    private RefreshToken(string usuarioId)
    {
        Token = Guid.NewGuid().ToString("N");
        UsuarioId = usuarioId;
        DataCriacao = DateTime.UtcNow;
        DataExpiracao = DataCriacao.AddDays(DiasExpiracao);
    }

    public static RefreshToken Create(string usuarioId) => new(usuarioId);

    public bool EstaExpirado() => DateTime.UtcNow > DataExpiracao;
}

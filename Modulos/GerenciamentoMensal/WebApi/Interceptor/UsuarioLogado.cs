using Domain.Entity;
using Domain.Login.Interfaces;
using Domain.Repository;
using SharedDomain.Exceptions;

namespace WebApi.Interceptor
{
    public class UsuarioLogado : IUsuarioLogado
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUsuarioRepository usuarioRepository;

        public UsuarioLogado(IHttpContextAccessor httpContextAccessor, IUsuarioRepository usuarioRepository)
        {
            _httpContextAccessor = httpContextAccessor;
            this.usuarioRepository = usuarioRepository;
        }

        public Usuario Usuario
        {
            get
            {
                return usuarioRepository.GetByID(this.Id).Result;
            }
        }

        public string Id
        {
            get
            {
                try
                {
                    var idUsuarioLogado = _httpContextAccessor
                        .HttpContext?
                        .User?
                        .Claims?
                        .Where(x => x.Type == nameof(Usuario.Id))
                        .FirstOrDefault()
                        ?.Value;

                    return idUsuarioLogado ?? throw new AutenticacaoNecessariaException("Para acessar essa funcionalidade e necessario autenticação!");
                }
                catch (Exception ex)
                {
                    throw;
                }
            }
        }
    }
}

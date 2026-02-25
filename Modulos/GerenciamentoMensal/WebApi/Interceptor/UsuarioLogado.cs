using Domain.Compartilhamento.Entity;
using Domain.Compartilhamento.Repository;
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
        private readonly ICompartilhamentoRepository _compartilhamentoRepository;

        public UsuarioLogado(
            IHttpContextAccessor httpContextAccessor,
            IUsuarioRepository usuarioRepository,
            ICompartilhamentoRepository compartilhamentoRepository)
        {
            _httpContextAccessor = httpContextAccessor;
            this.usuarioRepository = usuarioRepository;
            _compartilhamentoRepository = compartilhamentoRepository;
        }

        public Usuario Usuario
        {
            get
            {
                var usuario = usuarioRepository.GetById(this.Id).Result;

                return usuario ?? throw new AutenticacaoNecessariaException("Para acessar essa funcionalidade e necessario autenticação!"); ;
            }
        }

        public string Id
        {
            get
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
        }

        public string IdContextoDados
        {
            get
            {
                // 1. Verifica se existe o header X-Proprietario-Id na requisição
                var proprietarioId = _httpContextAccessor.HttpContext?
                    .Request.Headers["X-Proprietario-Id"]
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(proprietarioId))
                {
                    // 2. Se existe, valida que o usuário logado TEM permissão (compartilhamento aceito)
                    var compartilhamento = _compartilhamentoRepository
                        .ObterPorProprietarioEConvidado(proprietarioId, this.Id).Result;

                    if (compartilhamento != null && compartilhamento.Status == StatusConvite.Aceito)
                        return proprietarioId; // ← Retorna o ID do PROPRIETÁRIO (quem compartilhou)

                    throw new AutenticacaoNecessariaException(
                        "Você não tem permissão para acessar os dados deste usuário!");
                }

                return this.Id; // ← Sem header = retorna o ID do próprio usuário logado
            }
        }

        public Usuario UsuarioContextoDados
        {
            get
            {
                var usuario = usuarioRepository.GetById(this.IdContextoDados).Result;

                return usuario ?? throw new AutenticacaoNecessariaException("Para acessar essa funcionalidade e necessario autenticação!");
            }
        }

        public bool EmModoCompartilhado =>
            IdContextoDados != this.Id;

        public NivelPermissao? PermissaoAtual
        {
            get
            {
                if (!EmModoCompartilhado)
                    return null;

                var proprietarioId = _httpContextAccessor.HttpContext?
                    .Request.Headers["X-Proprietario-Id"]
                    .FirstOrDefault();

                var compartilhamento = _compartilhamentoRepository
                    .ObterPorProprietarioEConvidado(proprietarioId!, this.Id).Result;

                return compartilhamento?.Permissao;
            }
        }
    }
}

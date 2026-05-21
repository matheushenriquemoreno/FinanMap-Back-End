using System.Threading.Tasks;
using Application.DTOs;

namespace Application.Interface;

public interface IServiceUsuario
{
    UsuarioDTO ObterUsuarioLogado();
    Task<CustoFixoConfiguracaoDTO> ObterConfiguracaoCustoFixoAsync();
    Task<Result> AtualizarConfiguracaoCustoFixoAsync(CustoFixoConfiguracaoDTO configuracao);
}

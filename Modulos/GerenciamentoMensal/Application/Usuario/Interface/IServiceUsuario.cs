using System.Threading.Tasks;
using Application.DTOs;

namespace Application.Interface;

public interface IServiceUsuario
{
    UsuarioDTO ObterUsuarioLogado();
    Task<Result<AtualizarAvatarDTO>> AtualizarAvatarAsync(AtualizarAvatarDTO dto);
    Task<CustoFixoConfiguracaoDTO> ObterConfiguracaoCustoFixoAsync();
    Task<Result> AtualizarConfiguracaoCustoFixoAsync(CustoFixoConfiguracaoDTO configuracao);
}

using Application.Login.DTOs;

namespace Application.Login.Interfaces;

public interface ILoginService
{
    Task<Result> Login(LoginDTO login);
    Task<Result<ResultLoginDTO>> VerificarCodigoEmailValido(CodigoLoginDTO codigoLoginDTO);
    Task<Result> CriarUsuario(CreateUsuarioDTO login);
}

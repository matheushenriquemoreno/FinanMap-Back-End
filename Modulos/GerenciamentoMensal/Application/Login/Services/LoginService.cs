using Application.Email.Interfaces;
using Application.Login.DTOs;
using Application.Login.Interfaces;
using Domain.Entity;
using Domain.Event;
using Domain.Exceptions;
using Domain.Login.Entity;
using Domain.Login.Events;
using Domain.Login.Repository;
using Domain.Repository;
using MediatR;
using SharedDomain.Validator;

namespace Application.Login.Services;

public class LoginService : ILoginService
{
    private readonly ICodigoLoginRepository _codigoLoginRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IUsuarioEmailService _emailService;
    private readonly IServiceJWT _serviceJWT;
    private readonly IMediator _mediator;
    private const string MessageCodigoExpirado = "Codigo informado invalido, ou expirado.";

    public LoginService(ICodigoLoginRepository codigoLoginRepository, IUsuarioEmailService emailService, IUsuarioRepository usuarioRepository, IServiceJWT serviceJWT, IMediator mediator)
    {
        _codigoLoginRepository = codigoLoginRepository;
        _emailService = emailService;
        _usuarioRepository = usuarioRepository;
        _serviceJWT = serviceJWT;
        _mediator = mediator;
    }

    public async Task<Result> Login(LoginDTO login)
    {
        try
        {
            if (!EmailValidator.IsValidEmail(login.Email))
                return Result.Failure(Error.NotFound("E-mail invalido!"));

            Usuario usuario = await _usuarioRepository.GetByEmail(login.Email);

            if (usuario is null)
                return Result.Failure(Error.NotFound("Certifique de ter realizado o cadastro!"));

            var codigo = CodigoLogin.Create(usuario.Email);

            await _codigoLoginRepository.Add(codigo);

            return await EnviarCodigoLogin(false, usuario, codigo);
        }
        catch (Exception ex) when (ex is not DomainValidatorException)
        {
            return Result.Failure(Error.Exception(ex));
        }
    }

    public async Task<Result<ResultLoginDTO>> VerificarCodigoEmailValido(CodigoLoginDTO codigoLoginDTO)
    {
        try
        {
            CodigoLogin codigoLogin = await _codigoLoginRepository.GetByCodigo(codigoLoginDTO.Codigo);

            if (codigoLogin == null)
                return Result.Failure<ResultLoginDTO>(Error.NotFound(MessageCodigoExpirado));

            if (codigoLogin.EstaExpirado())
            {
                await _mediator.Publish(new CodigoLoginExpiradoEvent(codigoLogin));
                return Result.Failure<ResultLoginDTO>(Error.NotFound(MessageCodigoExpirado));
            }

            if (codigoLogin.CodigoValido(codigoLoginDTO.Email, codigoLoginDTO.Codigo))
            {
                var usuario = await _usuarioRepository.GetByEmail(codigoLogin.Email);
                var tokenAcess = _serviceJWT.CriarToken(usuario);
                var result = new ResultLoginDTO(tokenAcess, usuario.Nome);
                await _codigoLoginRepository.Delete(codigoLogin);
                return Result.Success(result);
            }

            return Result.Failure<ResultLoginDTO>(Error.NotFound(MessageCodigoExpirado));
        }
        catch (Exception ex) when (ex is not DomainValidatorException)
        {
            return Result.Failure<ResultLoginDTO>(Error.Exception(ex));
        }
    }

    public async Task<Result> CriarUsuario(CreateUsuarioDTO usuarioDTO)
    {
        try
        {
            Usuario usuario = await _usuarioRepository.GetByEmail(usuarioDTO.Email);

            if (usuario is not null)
                return Result.Failure(Error.Validation("E-mail invalido para cadastro!"));

            usuario = new Usuario(usuarioDTO.Nome, usuarioDTO.Email);
            await _usuarioRepository.Add(usuario);
            await _mediator.Publish(new UsuarioCriadoEvent(usuario));

            var codigo = CodigoLogin.Create(usuario.Email);
            await _codigoLoginRepository.Add(codigo);

            return await EnviarCodigoLogin(true, usuario, codigo);
        }
        catch (Exception ex) when (ex is not DomainValidatorException)
        {
            return Result.Failure(Error.Exception(ex));
        }
    }

    private async Task<Result> EnviarCodigoLogin(bool primeiroLogin, Usuario usuario, CodigoLogin codigo)
    {
        Result resultEmailEnviado = await _emailService.EnviarEmailParaLogin(primeiroLogin, usuario.Email, codigo);

        if (resultEmailEnviado.IsSucess)
        {
            return Result.Success();
        }

        await _codigoLoginRepository.Delete(codigo);
        return Result.Failure();
    }

}

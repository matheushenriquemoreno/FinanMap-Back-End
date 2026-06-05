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
using Microsoft.Extensions.Logging;
using SharedDomain.Validator;

namespace Application.Login.Services;

public class LoginService : ILoginService
{
    private readonly ICodigoLoginRepository _codigoLoginRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IUsuarioEmailService _emailService;
    private readonly IServiceJWT _serviceJWT;
    private readonly IMediator _mediator;
    private readonly ILogger<LoginService> _logger;
    private const string MessageCodigoExpirado = "Codigo informado invalido, ou expirado.";
    private const string MessageRefreshTokenInvalido = "Refresh token invalido ou expirado.";

    public LoginService(
        ICodigoLoginRepository codigoLoginRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUsuarioEmailService emailService,
        IUsuarioRepository usuarioRepository,
        IServiceJWT serviceJWT,
        IMediator mediator,
        ILogger<LoginService> logger)
    {
        _codigoLoginRepository = codigoLoginRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _emailService = emailService;
        _usuarioRepository = usuarioRepository;
        _serviceJWT = serviceJWT;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result> Login(LoginDTO login)
    {
        try
        {
            if (!EmailValidator.IsValidEmail(login.Email))
            {
                _logger.LogWarning("Tentativa de login com e-mail invalido: {Email}", login.Email);
                return Result.Failure(Error.NotFound("E-mail invalido!"));
            }

            Usuario usuario = await _usuarioRepository.GetByEmail(login.Email);

            if (usuario is null)
            {
                _logger.LogWarning("Tentativa de login para e-mail nao cadastrado: {Email}", login.Email);
                return Result.Failure(Error.NotFound("Certifique de ter realizado o cadastro!"));
            }

            var codigo = CodigoLogin.Create(usuario.Email);

            await _codigoLoginRepository.Add(codigo);
            _logger.LogInformation("Codigo de login solicitado para o e-mail {Email}", usuario.Email);

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
            {
                _logger.LogWarning("Tentativa de validar codigo de login invalido ou expirado para o e-mail {Email}", codigoLoginDTO.Email);
                return Result.Failure<ResultLoginDTO>(Error.NotFound(MessageCodigoExpirado));
            }

            if (codigoLogin.EstaExpirado())
            {
                await _mediator.Publish(new CodigoLoginExpiradoEvent(codigoLogin));
                _logger.LogWarning("Tentativa de validar codigo de login expirado para o e-mail {Email}", codigoLogin.Email);
                return Result.Failure<ResultLoginDTO>(Error.NotFound(MessageCodigoExpirado));
            }

            if (codigoLogin.CodigoValido(codigoLoginDTO.Email, codigoLoginDTO.Codigo))
            {
                var usuario = await _usuarioRepository.GetByEmail(codigoLogin.Email);
                var result = await GerarTokensParaUsuario(usuario);
                await _codigoLoginRepository.Delete(codigoLogin);
                _logger.LogInformation("Login realizado com sucesso para o e-mail {Email}", codigoLogin.Email);
                return Result.Success(result);
            }

            _logger.LogWarning("Tentativa de validar codigo de login invalido para o e-mail {Email}", codigoLoginDTO.Email);
            return Result.Failure<ResultLoginDTO>(Error.NotFound(MessageCodigoExpirado));
        }
        catch (Exception ex) when (ex is not DomainValidatorException)
        {
            return Result.Failure<ResultLoginDTO>(Error.Exception(ex));
        }
    }

    public async Task<Result<ResultLoginDTO>> RefreshToken(RefreshTokenRequestDTO refreshTokenDTO)
    {
        try
        {
            var refreshToken = await _refreshTokenRepository.GetByToken(refreshTokenDTO.RefreshToken);

            if (refreshToken == null)
                return Result.Failure<ResultLoginDTO>(Error.NotFound(MessageRefreshTokenInvalido));

            if (refreshToken.EstaExpirado())
            {
                // Fire-and-forget: deleta token expirado sem bloquear a resposta
                _ = _refreshTokenRepository.Delete(refreshToken);
                return Result.Failure<ResultLoginDTO>(Error.NotFound(MessageRefreshTokenInvalido));
            }

            // Paraleliza: deleta o token usado (rotation) + busca o usuário simultaneamente
            var deleteTask = _refreshTokenRepository.Delete(refreshToken);
            var usuarioTask = _usuarioRepository.GetById(refreshToken.UsuarioId);

            await Task.WhenAll(deleteTask, usuarioTask);

            var usuario = usuarioTask.Result;

            if (usuario is null)
                return Result.Failure<ResultLoginDTO>(Error.NotFound(MessageRefreshTokenInvalido));

            var result = await GerarTokensParaUsuario(usuario);
            return Result.Success(result);
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
            {
                _logger.LogWarning("Tentativa de cadastro com e-mail ja existente: {Email}", usuarioDTO.Email);
                return Result.Failure(Error.Validation("E-mail invalido para cadastro!"));
            }

            usuario = new Usuario(usuarioDTO.Nome, usuarioDTO.Email);
            await _usuarioRepository.Add(usuario);
            await _mediator.Publish(new UsuarioCriadoEvent(usuario));
            _logger.LogInformation("Novo cadastro realizado para o e-mail {Email}", usuario.Email);

            var codigo = CodigoLogin.Create(usuario.Email);
            await _codigoLoginRepository.Add(codigo);

            return await EnviarCodigoLogin(true, usuario, codigo);
        }
        catch (Exception ex) when (ex is not DomainValidatorException)
        {
            return Result.Failure(Error.Exception(ex));
        }
    }

    private async Task<ResultLoginDTO> GerarTokensParaUsuario(Usuario usuario)
    {
        // Gera JWT (CPU-bound, sem I/O) antes de ir ao banco
        var tokenAcess = _serviceJWT.CriarToken(usuario);
        var novoRefreshToken = Domain.Login.Entity.RefreshToken.Create(usuario.Id);

        // Persiste o novo refresh token
        await _refreshTokenRepository.Add(novoRefreshToken);

        return new ResultLoginDTO(tokenAcess, novoRefreshToken.Token, usuario.Nome);
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

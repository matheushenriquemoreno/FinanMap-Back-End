namespace Application.Login.DTOs;

public record ResultLoginDTO(string Token, string RefreshToken, string NomeUsuario);

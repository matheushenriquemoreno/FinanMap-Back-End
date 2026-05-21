using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Email.DTOs;
using Domain.Enum;

namespace Application.Email.Interfaces;

public interface ICustoFixoEmailService
{
    Task<Result> EnviarLembreteAsync(string email, string nomeUsuario, List<CustoFixoLembreteItem> itens, TipoLembrete tipo);
}

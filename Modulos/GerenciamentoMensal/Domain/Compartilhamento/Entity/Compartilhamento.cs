using Domain.Entity;
using Domain.Validator;
using SharedDomain.Validator;

namespace Domain.Compartilhamento.Entity;

public class Compartilhamento : EntityBase
{
    public string ProprietarioId { get; set; }   // ID do usuário que compartilha (dono dos dados)
    public string ConvidadoId { get; set; }       // ID do usuário que recebe acesso
    public string ConvidadoEmail { get; set; }    // E-mail do convidado (para exibição)
    public string ProprietarioEmail { get; set; } // E-mail do proprietário (para exibição)
    public string ProprietarioNome { get; set; }  // Nome do proprietário (para exibição)
    public NivelPermissao Permissao { get; set; } // Enum: Visualizar ou Editar
    public StatusConvite Status { get; set; }     // Enum: Pendente, Aceito, Recusado
    public DateTime DataCriacao { get; set; }
    public DateTime? DataAtualizacao { get; set; }

    public Compartilhamento(
        string proprietarioId,
        string proprietarioEmail,
        string proprietarioNome,
        string convidadoId,
        string convidadoEmail,
        NivelPermissao permissao)
    {
        var validator = DomainValidator.Create();

        validator.Validar(() => string.IsNullOrEmpty(proprietarioId), "Necessário informar o ID do proprietário!");
        validator.Validar(() => string.IsNullOrEmpty(convidadoId), "Necessário informar o ID do convidado!");
        validator.Validar(() => EmailValidator.IsValidEmail(convidadoEmail) == false, "E-mail do convidado inválido!");
        validator.Validar(() => proprietarioId == convidadoId, "Não é possível compartilhar consigo mesmo!");
        validator.Validar(() => !System.Enum.IsDefined(typeof(NivelPermissao), permissao), "Nível de permissão inválido!");

        validator.LancarExceptionSePossuiErro();

        ProprietarioId = proprietarioId;
        ProprietarioEmail = proprietarioEmail?.ToLower() ?? string.Empty;
        ProprietarioNome = proprietarioNome ?? string.Empty;
        ConvidadoId = convidadoId;
        ConvidadoEmail = convidadoEmail.ToLower();
        Permissao = permissao;
        Status = StatusConvite.Pendente;
        DataCriacao = DateTime.UtcNow;
    }

    public void AtualizarPermissao(NivelPermissao novaPermissao)
    {
        var validator = DomainValidator.Create();
        validator.Validar(() => !System.Enum.IsDefined(typeof(NivelPermissao), novaPermissao), "Nível de permissão inválido!");
        validator.LancarExceptionSePossuiErro();

        Permissao = novaPermissao;
        DataAtualizacao = DateTime.UtcNow;
    }

    public void Aceitar()
    {
        Status = StatusConvite.Aceito;
        DataAtualizacao = DateTime.UtcNow;
    }

    public void Recusar()
    {
        Status = StatusConvite.Recusado;
        DataAtualizacao = DateTime.UtcNow;
    }
}

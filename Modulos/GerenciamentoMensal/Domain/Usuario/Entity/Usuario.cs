using Domain.Validator;
using Domain.Exceptions;
using SharedDomain.Validator;

namespace Domain.Entity
{
    public class Usuario : EntityBase
    {
        public const string AvatarPadrao = "avatar-01";
        private static readonly HashSet<string> AvataresPermitidos =
        [
            AvatarPadrao,
            "avatar-02",
            "avatar-03",
            "avatar-04",
            "avatar-05",
            "avatar-06",
            "avatar-07",
            "avatar-08"
        ];

        public string Nome { get; set; }
        public string Email { get; set; }
        public string AvatarId { get; private set; } = AvatarPadrao;
        public bool ReceberNotificacoesCustosFixos { get; set; } = true;

        public Usuario(string nome, string email)
        {
            var validator = DomainValidator.Create();

            validator.Validar(() => EmailValidator.IsValidEmail(email) == false, "E-mail informado invalido!");
            validator.Validar(() => string.IsNullOrEmpty(nome), "Necessario informar o Nome!");

            validator.LancarExceptionSePossuiErro();

            Nome = nome;
            Email = email.ToLower();
        }

        public void AtualizarAvatar(string avatarId)
        {
            if (string.IsNullOrWhiteSpace(avatarId) || !AvataresPermitidos.Contains(avatarId))
            {
                throw new DomainValidatorException("Avatar informado inválido.");
            }

            AvatarId = avatarId;
        }
    }
}

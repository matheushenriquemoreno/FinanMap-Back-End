using Domain.Validator;
using SharedDomain.Validator;

namespace Domain.Entity
{
    public class Usuario : EntityBase
    {
        public string Nome { get; set; }
        public string Email { get; set; }

        public Usuario(string nome, string email)
        {
            var validator = DomainValidator.Create();

            validator.Validar(() => EmailValidator.IsValidEmail(email) == false, "E-mail informado invalido!");
            validator.Validar(() => string.IsNullOrEmpty(nome), "Necessario informar o Nome!");

            validator.LancarExceptionSePossuiErro();

            Nome = nome;
            Email = email.ToLower();
        }
    }
}

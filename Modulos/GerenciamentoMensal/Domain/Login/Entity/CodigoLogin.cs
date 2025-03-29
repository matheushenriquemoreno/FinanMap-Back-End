using Domain.Entity;
using SharedDomain.Validator;

namespace Domain.Login.Entity
{
    public class CodigoLogin : EntityBase
    {
        private static readonly Random random = new();
        public string Codigo { get; set; }
        public string Email { get; set; }
        public DateTime DataCriacao { get; protected set; }
        public DateTime DataExpiracao { get; protected set; }
        public int MinutosExpiracao { get; private set; } = 15;

        private CodigoLogin(string email)
        {
            if (EmailValidator.IsValidEmail(email))

                Codigo = GerarCodigoAleatorio();
            DataCriacao = DateTime.UtcNow;
            DataExpiracao = DataCriacao.AddMinutes(MinutosExpiracao);
            Email = email.ToLower();
        }

        public static CodigoLogin Create(string email) => new(email);

        private string GerarCodigoAleatorio()
        {
            const string caracteres = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            int tamanhoCodigo = 6;

            return new string(Enumerable.Range(0, tamanhoCodigo)
                .Select(_ => caracteres[random.Next(caracteres.Length)]).ToArray());
        }

        /// <summary>
        /// Valida se o codigo informado e valido para o email
        /// </summary>
        /// <param name="email">Email do usuario</param>
        /// <param name="codigo">Codigo enviado no e-mail</param>
        /// <returns>true caso o codigo e o email esteja igual, e false caso o contrario</returns>
        public bool CodigoValido(string email, string codigo)
        {
            return this.Email.Equals(email.ToLower()) && this.Codigo.Equals(codigo);
        }

        public bool EstaExpirado()
        {
            if (DateTime.UtcNow > DataExpiracao)
                return true;

            return false;
        }
    }
}

using Domain.Enum;
using Domain.Validator;

namespace Domain.Entity;

public class Categoria : EntityBase
{
    public string Nome { get; set; }
    public string UsuarioId { get; set; }
    public TipoCategoria Tipo { get; set; }
    public string McpOperationId { get; private set; }
    public string LastMcpOperationId { get; private set; }
    public string LastMcpResultHash { get; private set; }

    protected Categoria()
    {
    }

    public Categoria(string nome, TipoCategoria tipo, string idUsuario)
    {
        Nome = nome;
        Tipo = tipo;
        UsuarioId = idUsuario;
        ValidarDados();
    }

    private void ValidarDados()
    {
        var validator = DomainValidator.Create();

        validator.Validar(() => string.IsNullOrEmpty(this.Nome), "Nome categoria obrigatorio!");
        validator.Validar(() => string.IsNullOrEmpty(this.UsuarioId), "Id Usuario vinculado a categoria obrigatorio!");
        validator.Validar(() => !System.Enum.IsDefined(typeof(TipoCategoria), this.Tipo), "Categoria informada invalida!");

        validator.LancarExceptionSePossuiErro();
    }

    public void AtualizarNome(string nome)
    {
        this.Nome = nome;
        this.ValidarDados();
    }

    public void MarcarCriacaoMcp(string operationId)
    {
        if (string.IsNullOrWhiteSpace(Id))
            Id = Guid.NewGuid().ToString("N")[..24];
        if (string.IsNullOrWhiteSpace(McpOperationId))
            McpOperationId = operationId;
    }

    public void MarcarAlteracaoMcp(string operationId, string resultHash)
    {
        LastMcpOperationId = operationId;
        LastMcpResultHash = resultHash;
    }
}

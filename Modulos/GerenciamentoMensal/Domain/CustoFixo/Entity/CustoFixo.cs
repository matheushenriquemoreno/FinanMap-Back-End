using Domain.Validator;

namespace Domain.Entity;

public class CustoFixo : EntityBase
{
    public string Nome { get; set; }
    public int DiaVencimento { get; set; }
    public string UsuarioId { get; set; }
    public string CategoriaId { get; set; }
    public bool Ativo { get; set; } = true;

    protected CustoFixo()
    {
    }

    public CustoFixo(string nome, int diaVencimento, string usuarioId, string categoriaId = null)
    {
        Nome = nome;
        DiaVencimento = diaVencimento;
        UsuarioId = usuarioId;
        CategoriaId = categoriaId;
        Ativo = true;

        ValidarDados();
    }

    public void Atualizar(string nome, int diaVencimento, string categoriaId, bool ativo)
    {
        Nome = nome;
        DiaVencimento = diaVencimento;
        CategoriaId = categoriaId;
        Ativo = ativo;

        ValidarDados();
    }

    private void ValidarDados()
    {
        var validator = DomainValidator.Create();

        validator.Validar(() => string.IsNullOrWhiteSpace(Nome), "Nome do custo fixo obrigatorio!");
        validator.Validar(() => string.IsNullOrWhiteSpace(UsuarioId), "Id Usuario vinculado ao custo fixo obrigatorio!");
        validator.Validar(() => DiaVencimento < 1 || DiaVencimento > 31, "Dia de vencimento deve estar entre 1 e 31!");

        validator.LancarExceptionSePossuiErro();
    }
}

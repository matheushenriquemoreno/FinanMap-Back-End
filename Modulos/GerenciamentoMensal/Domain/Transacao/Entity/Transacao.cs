using Domain.Exceptions;
using Domain.Validator;

namespace Domain.Entity;

public abstract class Transacao : EntityBase
{
    public int Ano { get; set; }
    public int Mes { get; set; }
    public string Descricao { get; set; }
    public decimal Valor { get; set; }
    public string CategoriaId { get; protected set; }
    public Categoria Categoria { get; set; }
    public string UsuarioId { get; protected set; }
    public Usuario Usuario { get; protected set; }
    public DateTime DataCriacao { get; set; }

    protected Transacao() { }

    protected Transacao(int ano, int mes, string descricao, decimal valor, Categoria categoria, Usuario usuario)
    {
        Ano = ano;
        Mes = mes;
        Descricao = string.IsNullOrEmpty(descricao) ? categoria.Nome : descricao;
        Valor = valor;
        UsuarioId = usuario.Id;
        Usuario = usuario;
        DataCriacao = DateTime.Now;
        this.PreencherCategoria(categoria);
        this.ValidarDados();
    }

    public void Atualizar(string descricao, decimal valor, Categoria categoria)
    {
        Descricao = descricao;
        Valor = valor;
        this.PreencherCategoria(categoria);
        this.ValidarDados();
    }

    public void Atualizar(decimal valor)
    {
        Valor = valor;
        this.ValidarDados();
    }

    public void PreencherCategoria(Categoria categoria)
    {
        if (CategoriaEhValida(categoria))
        {
            CategoriaId = categoria.Id;
            Categoria = categoria;
        }
        else
            throw new DomainValidatorException("Categoria informada invalida para vinculo.");
    }

    protected abstract bool CategoriaEhValida(Categoria categoria);

    public virtual void ValidarDados()
    {
        var validator = DomainValidator.Create();

        validator.Validar(() => this.Valor < 0, "Não se pode adicionar uma transação negativa.");
        validator.Validar(() => this.Ano < DateTime.Now.Year - 5, "Ano informado invalido.");
        validator.Validar(() => this.Mes < 1 || this.Mes > 12, "Mês informado invalido!");

        validator.LancarExceptionSePossuiErro();
    }
}

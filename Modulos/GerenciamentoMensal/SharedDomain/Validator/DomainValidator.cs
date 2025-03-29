using Domain.Exceptions;

namespace Domain.Validator;

public class DomainValidator
{
    private readonly List<string> _erros;

    public DomainValidator()
    {
        _erros = new List<string>();
    }

    public static DomainValidator Create()
     => new DomainValidator();


    /// <summary>
    /// função para validar registros, função espera que retorne true em caso de uma validação de erro e false para sucesso.
    /// </summary>
    /// <param name="validar">função que espera um retorno true ou false, true caso possui erro, e false caso não</param>
    /// <param name="mensagemErro"></param>
    public void Validar(Func<bool> validar, string mensagemErro)
    {
        if (validar())
            _erros.Add(mensagemErro);
    }

    public bool PossuiErro()
    {
        return _erros.Count > 0;
    }

    public void LancarExceptionSePossuiErro()
    {
        if (PossuiErro())
            throw new DomainValidatorException(this._erros);
    }
}


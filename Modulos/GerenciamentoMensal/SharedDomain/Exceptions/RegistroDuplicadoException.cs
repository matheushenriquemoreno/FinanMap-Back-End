namespace SharedDomain.Exceptions;

public class RegistroDuplicadoException : Exception
{
    public RegistroDuplicadoException() { }
    public RegistroDuplicadoException(string mensage) : base(mensage) { }

}

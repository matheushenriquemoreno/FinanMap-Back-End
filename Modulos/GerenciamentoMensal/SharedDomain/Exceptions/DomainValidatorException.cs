namespace Domain.Exceptions;

public class DomainValidatorException : Exception
{
    public List<string> Errors { get; set; }

    public DomainValidatorException(List<string> errors)
    {
        Errors = errors;
    }

    public DomainValidatorException(string errors)
    {
        Errors = new List<string>() { errors };
    }
}


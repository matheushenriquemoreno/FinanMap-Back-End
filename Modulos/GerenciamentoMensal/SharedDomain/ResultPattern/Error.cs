namespace System;

public class Error
{
    public string Message { get; set; }

    protected TypeError Type { get; set; }

    private Error(string message, TypeError typeError)
    {
        Message = message;
        Type = typeError;
    }

    public TypeError GetType()
    {
        return Type;
    }

    public static Error Validation(string message) =>
        new Error(message, TypeError.Validation);

    public static Error NotFound(string message) =>
        new Error(message, TypeError.NotFound);

    public static Error Exception(Exception ex) =>
            new Error($"{ex}, {ex.InnerException}", TypeError.Exception);
}

public enum TypeError
{
    // Erros Voltados a validação 
    Validation = 1,

    // Erros voltados a não existencia de um registro
    NotFound = 2,

    // Erro para quando uma Exception for lancada
    Exception = 3,
}

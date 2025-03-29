namespace System;

public class Result
{
    public bool IsSucess { get; private set; }

    public bool IsFailure => !IsSucess;

    public Error Error { get; private set; }

    protected Result(bool isSucess, Error error)
    {
        IsSucess = isSucess;
        Error = error;
    }

    protected Result(bool isSucess)
    {
        IsSucess = isSucess;
    }

    public static Result Success() => new Result(true);
    public static Result<TValue> Success<TValue>(TValue value) => new Result<TValue>(value, true);

    public static Result Failure(Error erro) => new Result(false, erro);
    public static Result Failure() => new Result(false, default);
    public static Result<TValue> Failure<TValue>(Error error) => new Result<TValue>(default, false, error);
}

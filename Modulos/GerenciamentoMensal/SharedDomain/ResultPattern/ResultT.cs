namespace System;

public class Result<TValue> : Result
{
    private TValue value;

    public TValue Value
    {
        get
        {
            if (this.IsFailure)
                throw new InvalidOperationException("Não e possivel acessar o resultado da operação, devido a uma falha encontrada.");

            return this.value;
        }

        protected set
        {
            this.value = value;
        }
    }

    protected internal Result(TValue value, bool isSucess, Error error)
     : base(isSucess, error)
    {
        Value = value;
    }

    protected internal Result(TValue value, bool isSucess)
        : base(isSucess)
    {
        Value = value;
    }
}

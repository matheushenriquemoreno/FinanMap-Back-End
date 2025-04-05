namespace SharedDomain.Entity;

public interface IClone<T> where T : class
{
    public T Clone();
}

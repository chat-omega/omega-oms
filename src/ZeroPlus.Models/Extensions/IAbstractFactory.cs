namespace ZeroPlus.Models.Extensions
{
    public interface IAbstractFactory<T>
    {
        T Create();
    }
}
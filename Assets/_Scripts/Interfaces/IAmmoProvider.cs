namespace _Scripts.Interfaces
{
    public interface IAmmoProvider
    {
        int Reserve { get; }
        int ConsumeFromReserve(int amount);
        bool AddToReserve(int amount);
    }
}
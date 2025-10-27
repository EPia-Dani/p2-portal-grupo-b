namespace _Scripts.Interfaces
{
    public interface IDoor
    {
        bool NeedsKey { get; }
        void Open();
        void Close();
        void Unlock();
    }
}
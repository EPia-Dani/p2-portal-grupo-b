namespace _Scripts.Interfaces
{
    public interface IPortalable
    {
        void SetIsInPortal(Portal inPortal, Portal outPortal);
        void ExitPortal();
        void Warp();
        bool HasCrossedPlane(Portal portal);
    }
}
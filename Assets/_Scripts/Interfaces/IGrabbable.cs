using UnityEngine;

public interface IGrabbable
{
    void OnGrab(GravityGun handTransform);
    void OnRelease();
}

using UnityEngine;

public interface IGrabbable
{
    void OnGrab(GravityGun gravityGun);
    void OnRelease();
    void OnThrow(GravityGun gravityGun);
}

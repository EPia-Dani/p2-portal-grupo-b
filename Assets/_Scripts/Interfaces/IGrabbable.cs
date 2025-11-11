using UnityEngine;

public interface IGrabbable
{
    void OnGrab(GravityGun gravityGun);
    void OnRelease();
    void OnThrow(GravityGun gravityGun);
    void SetTargetPose(Vector3 pos, Quaternion rot);
}

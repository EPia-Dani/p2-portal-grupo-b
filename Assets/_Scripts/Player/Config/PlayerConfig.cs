using UnityEngine;


namespace _Scripts.Player.Config
{
    [CreateAssetMenu(menuName = "FPS/Player Config")]
    public class PlayerConfig : ScriptableObject
    {
        [Header("Camera")]
        public float sensitivity = 0.3f;
        
        [Header("Smoothing")]
        public float smoothSpeed = 10f;
        public float deadZone = 0.05f;

        [Header("Movement")]
        public float crouchSpeed = 5f;
        public float walkSpeed = 7f;
        public float runSpeed = 15f;
        public float jumpSpeed = 6f;
        public AudioClip footstepAClip;
        public AudioClip footstepBClip;
        public AudioClip jumpClip;
        
        [Header("Heights")]
        public float walkHeight = 2f;
        public float crouchHeight = 1.5f;

        [Header("Look Limits")]
        [Range(-90, 0)] public float minPitch = -45f;
        [Range(0, 90)] public float maxPitch = 45f;
    }
}

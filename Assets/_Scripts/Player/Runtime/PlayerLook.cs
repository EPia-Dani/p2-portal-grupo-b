using System;
using _Scripts.Player.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.Player.Runtime
{
    [DisallowMultipleComponent]
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] private Transform pitchController;
        [SerializeField] private PlayerConfig config;
        
        private float _yaw;
        private float _pitch;
        private float _sensMultiplier = 1f;
        
        private bool _isDead = false;
        
        private void Awake()
        {
            _yaw = transform.eulerAngles.y;
            _pitch = pitchController.localEulerAngles.x;
            Cursor.lockState = CursorLockMode.Locked;
        }

        void Update()
        {
            if (_isDead) return;
            Apply();
        }
        
        public void OnLook(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            AddLookDelta(ctx.ReadValue<Vector2>());
        }
        
        public void AddLookDelta(Vector2 delta)
        {
            float effectiveSensitivity = config.sensitivity * _sensMultiplier;
            _yaw   += delta.x * effectiveSensitivity;
            _pitch -= delta.y * effectiveSensitivity;
            _pitch  = Mathf.Clamp(_pitch, config.minPitch, config.maxPitch);
        }
        
        public void Apply()
        {
            transform.rotation            = Quaternion.Euler(0f, _yaw, 0f);
            pitchController.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
        
        public void ResetToCurrentForward()
        {
            _yaw = transform.eulerAngles.y;   
            float px = pitchController.localEulerAngles.x;
            if (px > 180f) px -= 360f;                // ← normaliza
            _pitch = Mathf.Clamp(px, config.minPitch, config.maxPitch);
            _pitch = pitchController.localEulerAngles.x;    // re-sync pitch
        }
        // En PlayerLook
        public void SetYawPitchAbsolute(float yawDeg, float pitchDeg)
        {
            _yaw = yawDeg;
            _pitch = Mathf.Clamp(pitchDeg, config.minPitch, config.maxPitch);
            Apply();
        }
    }
}

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
        
        // Recoil (procedural)
        private float _recoilPitch = 0f;
        private float _recoilYaw = 0f;
        [SerializeField] private float recoilRecoverySpeed = 8f; // degrees per second recovered
        [SerializeField] private float recoilYawRandom = 0.5f;   // max yaw jitter per shot
        
        private void Awake()
        {
            _yaw = transform.eulerAngles.y;
            _pitch = pitchController.localEulerAngles.x;
            Cursor.lockState = CursorLockMode.Locked;
        }

        void Update()
        {
            if (_isDead) return;
            // smooth recoil recovery
            if (_recoilPitch != 0f)
            {
                _recoilPitch = Mathf.MoveTowards(_recoilPitch, 0f, recoilRecoverySpeed * Time.deltaTime);
            }
            if (_recoilYaw != 0f)
            {
                _recoilYaw = Mathf.MoveTowards(_recoilYaw, 0f, recoilRecoverySpeed * Time.deltaTime);
            }

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
            // Apply procedural recoil as additive offsets to current look
            transform.rotation            = Quaternion.Euler(0f, _yaw + _recoilYaw, 0f);
            pitchController.localRotation = Quaternion.Euler(_pitch + _recoilPitch, 0f, 0f);
        }
        
        public void SetSensitivityMultiplier(float mul)
        {
            _sensMultiplier = mul;
        }
        
        public void ApplyRecoil(float recoil)
        {
            // Add recoil as an instantaneous kick; yaw gets a small random horizontal component
            _recoilPitch -= recoil;
            _recoilYaw += UnityEngine.Random.Range(-recoilYawRandom, recoilYawRandom);

            // Clamp pitch so recoil doesn't push beyond limits
            float clampedPitch = Mathf.Clamp(_pitch + _recoilPitch, config.minPitch, config.maxPitch);
            // Adjust recoilPitch so that the applied pitch stays within clamp
            _recoilPitch = clampedPitch - _pitch;
        }
    }
}

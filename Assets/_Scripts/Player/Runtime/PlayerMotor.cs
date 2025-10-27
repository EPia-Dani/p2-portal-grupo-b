using _Scripts.Player.Config;
using UnityEngine;
using UnityEngine.InputSystem;
namespace _Scripts.Player.Runtime
{
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private PlayerConfig config;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private Animator animator;
        
        private CharacterController _cc;
        private Vector2 _moveInput;   // x: sides, y: forward
        private float _verticalVel;
        private bool _canRun = true;
        private bool _running;
        private bool _crouching;
        private bool _grounded;

        public Vector3 Velocity => _cc.velocity;
        public float NormalizedSpeed => new Vector2(_cc.velocity.x, _cc.velocity.z).magnitude / config.runSpeed;
        public bool IsGrounded => _grounded;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        void Update()
        {
                Tick(Time.deltaTime);
        }

        void LateUpdate()
        {
                LateTick();
        }
        
        public void OnMove(InputAction.CallbackContext ctx)
        {
            SetMoveInput(ctx.ReadValue<Vector2>());
        }

        public void OnSprint(InputAction.CallbackContext ctx)
        {
            SetRunning(ctx.ReadValueAsButton());
        }

        public void OnJump(InputAction.CallbackContext ctx)
        {
            if (ctx.started)  JumpPressed();
            if (ctx.canceled) JumpReleased();
        }

        public void OnCrouch(InputAction.CallbackContext ctx)
        {
            SetCrouching(ctx.ReadValueAsButton());
        }

        public void SetMoveInput(Vector2 input)   => _moveInput = input.sqrMagnitude > 1f ? input.normalized : input;
        public void SetCanRun(bool canRun) => _canRun = canRun;
        public void SetRunning(bool running) => _running = running;
        public void SetCrouching(bool crouching)  => _crouching = crouching;

        public void JumpPressed()
        {
            if (_grounded)
            {
                _verticalVel = config.jumpSpeed;
                _grounded = false;
                if(audioSource.isPlaying)
                    audioSource.Stop();
                audioSource?.PlayOneShot(config.jumpClip);
            }
        }

        public void JumpReleased()
        {
            if (_verticalVel > 0f) _verticalVel = 0f; // variable jump height
        }

        public void Tick(float dt)
        {
            // look-aligned ground motion
            Vector3 wish = new Vector3(_moveInput.x, 0f, _moveInput.y);
            Vector3 worldWish = transform.TransformDirection(wish);

            float speed = _crouching ? config.crouchSpeed : _running && _canRun ? config.runSpeed : config.walkSpeed;

            // gravity
            float g = Physics.gravity.y;
            _verticalVel += g * dt;

            Vector3 displacement = new Vector3(worldWish.x * speed * dt,
                                               _verticalVel * dt,
                                               worldWish.z * speed * dt);

            CollisionFlags flags = _cc.Move(displacement);

            if ((flags & CollisionFlags.Below) != 0)
            {
                _grounded = true;
                if (_verticalVel < 0f) _verticalVel = -2f; // stick to ground
            }
            else
            {
                _grounded = false;
            }
        }
        
        public void TeleportTo(Vector3 position)
        {
            _cc.enabled = false;
            transform.position = position;
            _cc.enabled = true;
        }

        public void LateTick()
        {
            animator?.SetFloat("Speed",NormalizedSpeed);
            _cc.height = _crouching ? config.crouchHeight : config.walkHeight;
        }

        private bool _playA;
        public void PlayFootstepSound()
        {
            audioSource?.PlayOneShot(_playA? config.footstepAClip : config.footstepBClip);
            _playA = !_playA;
        }
    }
}


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
        private float MAX_FALL_SPEED = -30f;
        private bool _canRun = false; // In Portal you can only run in certain areas
        private bool _running;
        private bool _crouching;
        private bool _grounded;

        [SerializeField] private float externalFriction = 10f; 
        private Vector3 _externalVelocity;       
        public Vector3 Velocity => _cc.velocity;
        public Vector2 MovementDirection => new (_cc.velocity.x, _cc.velocity.z);
        public bool IsGrounded => _grounded;
        
        [SerializeField] private LayerMask portalMask = ~0;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _cc.detectCollisions = true;
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
                animator.SetTrigger("Jump");
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
            _verticalVel = Mathf.Max(_verticalVel, MAX_FALL_SPEED);

            Vector3 totalVel = new Vector3(worldWish.x * speed, _verticalVel, worldWish.z * speed) + _externalVelocity;
            Vector3 displacement = totalVel * dt;

            CollisionFlags flags = _cc.Move(displacement);
            
            if ((flags & CollisionFlags.Sides) != 0)
            {
                _externalVelocity.x = 0f;
                _externalVelocity.z = 0f;
            }

            if ((flags & CollisionFlags.Below) != 0)
            {
                bool realGround = !IsGroundedOnPortal();
                if (realGround)
                {
                    _grounded = true;
                    if (_verticalVel < 0f) _verticalVel = -2f;
                    if (_externalVelocity.magnitude > 0f) _externalVelocity = Vector3.zero;
                }
                else
                {
                    _grounded = false;
                }
            }
            else _grounded = false;
            
            if ((flags & CollisionFlags.Above) != 0 && _externalVelocity.y > 0f)
            {
                _externalVelocity.y = 0f;
                _verticalVel = 0f;
            }


            // decay injected momentum
            if (_externalVelocity != Vector3.zero)
                _externalVelocity = Vector3.MoveTowards(_externalVelocity, Vector3.zero, externalFriction * dt);
        }
        
        private bool IsGroundedOnPortal()
        {
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            float dist = 5f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, dist, portalMask, QueryTriggerInteraction.Collide))
            {
                if (hit.collider.CompareTag("Portal")) return true;
            }
            return false;
        }
        
        public void TeleportTo(Vector3 position)
        {
            _cc.enabled = false;
            transform.position = position;
            _cc.enabled = true;
        }

        public void LateTick()
        {   
            animator.SetFloat("MovementX", MovementDirection.x);
            animator?.SetFloat("MovementZ",MovementDirection.y);
            _cc.height = _crouching ? config.crouchHeight : config.walkHeight;
        }

        public void InjectExternalVelocity(Vector3 worldVel) => _externalVelocity = worldVel;
        public void SetVerticalVelocity(float v) => _verticalVel = v;
        
        private bool _playA;
        public void PlayFootstepSound()
        {
            audioSource?.PlayOneShot(_playA? config.footstepAClip : config.footstepBClip);
            _playA = !_playA;
        }
    }
}


using _Scripts.Interfaces;
using UnityEngine;

[RequireComponent(typeof(LineRenderer), typeof(AudioSource))]
public class Turret : MonoBehaviour, IDamageable
{
    private enum TurretState
    {
        Idle,
        Scanning,
        Firing,
        PickedUpFiring,
        PanicDisabled
    }

    [Header("References")]
    [SerializeField] private Transform eye;       // Where the vision ray starts
    [SerializeField] private Transform firePoint; // Where bullets/rays come from

    [Header("Vision")]
    [SerializeField] private float viewDistance = 20f;
    [SerializeField] private LayerMask visionMask; // Walls + Player layer

    [Header("Shooting")]
    [SerializeField] private float fireRate = 10f; // shots per second

    [Header("Tipping")]
    [SerializeField] private float tippedAngle = 60f; // degrees from upright to be "tipped"

    [Header("Sounds (per state)")]
    [SerializeField] private AudioClip idleStateSfx;
    [SerializeField] private AudioClip scanningStateSfx;
    [SerializeField] private AudioClip firingStateSfx;
    [SerializeField] private AudioClip pickedUpStateSfx;
    [SerializeField] private AudioClip panicSfx;          // used for PanicDisabled state
    [SerializeField] private AudioClip damageSfx;

    [Header("Sounds (per action)")]
    [SerializeField] private AudioClip shootSfx;          // per shot, not per state

    private LineRenderer _line;
    private AudioSource _audio;
    private float _fireTimer;
    private bool _isPickedUp;
    private bool _isDisabled;
    private bool _shotThisFrame;

    private GrabbableBase _grabbable;
    private TurretState _state = TurretState.Idle;

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _audio = GetComponent<AudioSource>();
        _grabbable = GetComponent<GrabbableBase>();

        _line.positionCount = 2;

        if (eye == null)
            eye = transform;
        if (firePoint == null)
            firePoint = eye;
    }

    private void Update()
    {
        _shotThisFrame = false;

        if (_isDisabled)
        {
            _line.enabled = false;
            SetState(TurretState.PanicDisabled);
            return;
        }

        // If tipped, panic + disable
        if (IsTippedOver())
        {
            PanicAndDisable();
            return;
        }

        _line.enabled = true;

        _isPickedUp = _grabbable != null && _grabbable.IsGrabbed;

        DoVisionAndShoot(firePoint.position, firePoint.forward, _isPickedUp);

        // Decide state based on what happened this frame
        if (_isDisabled)
        {
            SetState(TurretState.PanicDisabled);
        }
        else if (_isPickedUp)
        {
            // When held, we consider it always in "picked up firing" mode
            SetState(TurretState.PickedUpFiring);
        }
        else if (_shotThisFrame)
        {
            SetState(TurretState.Firing);
        }
        else
        {
            SetState(TurretState.Scanning);
        }
    }

    private bool IsTippedOver()
    {
        // Angle between turret "up" and world up
        float angle = Vector3.Angle(transform.up, Vector3.up);
        return angle > tippedAngle;
    }

    private void DoVisionAndShoot(Vector3 origin, Vector3 direction, bool isPickedUp)
    {
        RaycastHit hit;
        Vector3 end = origin + direction * viewDistance;

        if (Physics.Raycast(origin, direction, out hit, viewDistance, visionMask, QueryTriggerInteraction.Ignore))
        {
            end = hit.point;
            if (isPickedUp || hit.collider.CompareTag("Player"))
            {
                TryShoot(direction);
            }
        }

        // Draw the "vision" line
        _line.SetPosition(0, origin);
        _line.SetPosition(1, end);
    }

    private void TryShoot(Vector3 direction)
    {
        _fireTimer += Time.deltaTime;
        if (_fireTimer < 1f / fireRate) return;
        _fireTimer = 0f;

        _shotThisFrame = true;

        // Simple ray-based shooting.
        RaycastHit hit;
        if (Physics.Raycast(firePoint.position, direction, out hit, viewDistance, visionMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.ApplyDamage(10f, hit.point, direction);
            }
        }

        // Play shooting sound (per shot)
        if (shootSfx != null)
            AudioSource.PlayClipAtPoint(shootSfx, firePoint.position);
    }

    private void PanicAndDisable()
    {
        if (_isDisabled) return;

        _isDisabled = true;
        _line.enabled = false;
        SetState(TurretState.PanicDisabled);
    }

    public void ForceDisable()
    {
        PanicAndDisable();
    }

    public void ApplyDamage(float dmg, Vector3 hitPoint, Vector3 hitNormal)
    {
        // Knockback
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForceAtPosition(-hitNormal, hitPoint, ForceMode.Impulse);

        // Play damage sound once per hit
        if (damageSfx != null && _audio != null)
            _audio.PlayOneShot(damageSfx);

        // Disable / panic
        PanicAndDisable();
    }


    // -----------------------
    // State + one-shot audio
    // -----------------------

    private void SetState(TurretState newState)
    {
        if (_state == newState) return; // no change -> no sound

        _state = newState;
        PlayStateSfx(newState);
    }

    private void PlayStateSfx(TurretState state)
    {
        if (_audio == null) return;

        AudioClip clip = null;

        switch (state)
        {
            case TurretState.Idle:
                clip = idleStateSfx;
                break;
            case TurretState.Scanning:
                clip = scanningStateSfx;
                break;
            case TurretState.Firing:
                clip = firingStateSfx;
                break;
            case TurretState.PickedUpFiring:
                clip = pickedUpStateSfx;
                break;
            case TurretState.PanicDisabled:
                clip = panicSfx;
                break;
        }

        if (clip != null)
            _audio.PlayOneShot(clip);
    }
}

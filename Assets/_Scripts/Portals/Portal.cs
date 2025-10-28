using UnityEngine;

namespace _Scripts.Portals
{
    /// <summary>
    /// "Portal" completo para trabajar junto a PortalCamera.
    /// - Expone Renderer e IsPlaced (lo que PortalCamera ya usa).
    /// - Gestiona enlace con otro portal (Linked).
    /// - Métodos para alinearse a una superficie de colocación.
    /// - Utilidades de mapeo de posición/rotación a través del par de portales.
    /// - Devuelve el plano de recorte para oblique projection.
    /// - Helpers de textura y depuración.
    /// </summary>
    [DisallowMultipleComponent]
    public class Portal : MonoBehaviour
    {
        [Header("Componentes del portal")]
        [SerializeField] private Renderer portalRenderer;                // Quad/Marco visible del portal
        [SerializeField] private Collider portalTrigger;                 // Trigger que delimita el área de cruce (opcional)
        [Tooltip("Punto de referencia para spawnear/ajustar la salida. Si no se asigna, se usará this.transform.")]
        [SerializeField] private Transform spawnPoint;                   // Centro del plano del portal

        [Header("Enlace")]
        [SerializeField] private Portal linked;                          // Portal enlazado (A<->B)

        [Header("Estado")]
        [SerializeField] private bool isPlaced = false;                  // ¿Ya está colocado en una superficie?
        [SerializeField, Min(0f)] private float surfaceOffset = 0.01f;   // Para evitar z-fighting con la pared

        // --- API esperada por PortalCamera ---
        public Renderer Renderer => portalRenderer;
        public bool IsPlaced => isPlaced;
        public Portal Linked => linked;
        public Transform SpawnPoint => spawnPoint ? spawnPoint : transform;

        /// <summary>
        /// Enlaza este portal con otro (bidireccional si se indica).
        /// </summary>
        public void Link(Portal other, bool bidirectional = true)
        {
            linked = other;
            if (bidirectional && other && other.linked != this)
                other.Link(this, false);
        }

        /// <summary>
        /// Desenlaza el portal (y opcionalmente el otro).
        /// </summary>
        public void Unlink(bool bidirectional = true)
        {
            var old = linked;
            linked = null;
            if (bidirectional && old && old.linked == this)
                old.Unlink(false);
        }

        /// <summary>
        /// Marca el portal como colocado o no.
        /// </summary>
        public void MarkPlaced(bool placed) => isPlaced = placed;

        /// <summary>
        /// Alinea el portal a una superficie dada por punto y normal.
        /// upHint permite controlar la "vertical" del portal (usa Vector3.up si no se provee).
        /// </summary>
        public void AlignToSurface(Vector3 hitPoint, Vector3 hitNormal, Vector3 upHint)
        {
            var up = upHint.sqrMagnitude > 0.0001f ? upHint.normalized : Vector3.up;
            // Portal mira "hacia afuera" de la superficie: -normal
            var rot = Quaternion.LookRotation(-hitNormal.normalized, up);
            var pos = hitPoint + hitNormal.normalized * surfaceOffset;
            transform.SetPositionAndRotation(pos, rot);
            isPlaced = true;
        }

        /// <summary>
        /// Devuelve el plano de recorte en espacio mundo para este portal (normal apuntando hacia fuera).
        /// </summary>
        public Plane GetClipPlaneWorld()
        {
            return new Plane(-transform.forward, transform.position);
        }

        /// <summary>
        /// Asigna una RenderTexture como textura del material del portal.
        /// </summary>
        public void SetRenderTexture(RenderTexture rt)
        {
            if (!portalRenderer) return;
            var mat = portalRenderer.material; // instancia si es necesario (ojo a leaks si se instancian por frame)
            if (mat) mat.mainTexture = rt;
        }

        /// <summary>
        /// Mapea una posición desde el espacio del "source" a través del par de portales.
        /// Aplica un giro de 180º alrededor del eje Y del plano del portal para simular la inversión.
        /// </summary>
        public Vector3 MapPointFrom(Portal source, Vector3 worldPos)
        {
            if (!source) return worldPos;
            // 1) Posición en espacio local del portal de entrada
            Vector3 local = source.transform.InverseTransformPoint(worldPos);
            // 2) Giro 180° alrededor del eje Y (en local) para el efecto espejo
            local = Quaternion.Euler(0f, 180f, 0f) * local;
            // 3) Llevar al espacio del portal destino
            return transform.TransformPoint(local);
        }

        /// <summary>
        /// Mapea una rotación desde el espacio del "source" a través del par de portales.
        /// </summary>
        public Quaternion MapRotationFrom(Portal source, Quaternion worldRot)
        {
            if (!source) return worldRot;
            // 1) Rotación relativa respecto al portal de entrada
            Quaternion rel = Quaternion.Inverse(source.transform.rotation) * worldRot;
            // 2) Giro 180° en Y
            rel = Quaternion.Euler(0f, 180f, 0f) * rel;
            // 3) Rotación en espacio destino
            return transform.rotation * rel;
        }

        /// <summary>
        /// Ajusta el tamaño del trigger al marco visible (si existe) para facilitar cruce/colisiones.
        /// </summary>
        public void FitTriggerToRendererBounds(float depth = 0.1f)
        {
            if (!portalRenderer) return;
            if (!(portalTrigger is BoxCollider box)) return;

            var b = portalRenderer.bounds;
            // Transformar bounds mundo a espacio local del portal
            Vector3 centerLocal = transform.InverseTransformPoint(b.center);
            Vector3 sizeLocal = transform.InverseTransformVector(b.size);
            sizeLocal.z = Mathf.Max(depth, sizeLocal.z); // un poco de grosor

            box.center = centerLocal;
            box.size = sizeLocal;
            box.isTrigger = true;
        }

        // --- Eventos Unity ---
        private void Reset()
        {
            TryAutoWire();
        }

        private void Awake()
        {
            TryAutoWire();
        }

        private void TryAutoWire()
        {
            if (!portalRenderer)
                portalRenderer = GetComponentInChildren<Renderer>();
            if (!portalTrigger)
            {
                // Prioriza un BoxCollider hijo marcado como trigger
                portalTrigger = GetComponentInChildren<Collider>();
            }
            if (!spawnPoint) spawnPoint = transform;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            TryAutoWire();
        }

        private void OnDrawGizmos()
        {
            // Dibuja el plano del portal
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            var p = transform.position;
            var r = transform.rotation;
            var s = 0.6f;
            // Cuadrado orientado al plano del portal
            Vector3 right = r * Vector3.right * s;
            Vector3 up = r * Vector3.up * s;
            Gizmos.DrawLine(p - right - up, p + right - up);
            Gizmos.DrawLine(p + right - up, p + right + up);
            Gizmos.DrawLine(p + right + up, p - right + up);
            Gizmos.DrawLine(p - right + up, p - right - up);

            // Flecha de forward
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(p, transform.forward * 0.6f);
        }
#endif
    }
}

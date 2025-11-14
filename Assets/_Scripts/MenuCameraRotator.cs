using UnityEngine;

public class MenuCameraRotator : MonoBehaviour
{
    [Header("Configuración de oscilación")]
    [SerializeField] private float maxAngle = 15f;          
    [SerializeField] private float cycleDuration = 4f;     
    [SerializeField] private Vector3 axis = Vector3.up;    

    private Quaternion _initialRotation;

    private void Awake()
    {
        _initialRotation = transform.rotation;
    }

    private void Update()
    {
        if (cycleDuration <= 0f) return;

        // Factor de tiempo para una oscilación suave
        // 2π / cycleDuration = frecuencia para completar un ciclo en 'cycleDuration' segundos
        float timeFactor = (Mathf.PI * 2f) / cycleDuration;

        // Ángulo oscilando entre -maxAngle y +maxAngle
        float angle = Mathf.Sin(Time.time * timeFactor) * maxAngle;

        // Aplicamos la rotación relativa a la rotación inicial
        Quaternion offsetRotation = Quaternion.AngleAxis(angle, axis);
        transform.rotation = _initialRotation * offsetRotation;
    }
}
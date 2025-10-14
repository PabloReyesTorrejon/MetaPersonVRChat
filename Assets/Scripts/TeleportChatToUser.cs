using UnityEngine;

public class TeleportChatToUser : MonoBehaviour
{
    [Tooltip("Distancia delante del jugador donde aparecerá el panel")]
    public float distanceFromUser = 0.6f;

    [Tooltip("Altura relativa respecto a los ojos")]
    public float verticalOffset = -0.1f;

    private Transform cameraTransform;

    void Start()
    {
        // Intenta encontrar la cámara principal
        cameraTransform = Camera.main?.transform;
        if (cameraTransform == null)
        {
            Debug.LogWarning("No se encontró la cámara principal. Asegúrate de que tenga el tag 'MainCamera'.");
        }
    }

    void Update()
    {
        // Botón A del mando derecho o tecla "T" en teclado
        if (OVRInput.GetDown(OVRInput.Button.Three))
        {
            TeleportToUser();
        }
    }

    public void TeleportToUser()
    {
        if (cameraTransform == null) return;

        // Posición delante de la cámara
        Vector3 targetPos = cameraTransform.position + cameraTransform.forward * distanceFromUser;
        targetPos.y += verticalOffset;

        // Teletransportar suavemente
        StartCoroutine(SmoothMove(targetPos, 0.3f));

        // Orientar hacia el jugador
        transform.rotation = Quaternion.LookRotation(transform.position - cameraTransform.position);
    }
    
    private System.Collections.IEnumerator SmoothMove(Vector3 target, float duration)
    {
        Vector3 start = transform.position;
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, target, t / duration);
            yield return null;
        }
        transform.position = target;
    }

}

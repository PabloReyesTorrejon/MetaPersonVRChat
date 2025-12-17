using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PokeLinkOpener : MonoBehaviour
{
    [Tooltip("Cámara que renderiza la UI / canvas. Si está vacía, se usa Camera.main")] 
    public Camera eventCamera;

    [Tooltip("Tag del collider del dedo con el que se va a 'poke' (ej: IndexTip). Si está vacío, aceptará cualquier collider.)")]
    public string fingerTag = "";

    [Tooltip("Referencia opcional al SpeechBubbleControllerVR para abrir links; si se deja vacío, buscará en padres.")]
    public SpeechBubbleControllerVR bubble;

    private void Reset()
    {
        if (eventCamera == null) eventCamera = Camera.main;
    }

    private void Awake()
    {
        if (eventCamera == null) eventCamera = Camera.main;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Si fingerTag no está vacío, requerimos que el collider entrante tenga ese tag
        if (!string.IsNullOrEmpty(fingerTag) && !other.CompareTag(fingerTag))
            return;

        if (bubble == null)
            bubble = GetComponentInParent<SpeechBubbleControllerVR>();

        if (bubble == null)
            return;

        // Punto aproximado de contacto: usamos la posición del collider entrante como proxy
        Vector3 contactPoint = other.ClosestPoint(transform.position);

        // Intentar abrir link en el punto world usando la cámara de eventos
        bubble.TryOpenLinkAtWorldPoint(contactPoint, eventCamera ?? Camera.main);
    }
}

using UnityEngine;

/// <summary>
/// Raycaster simple para VR: lanza un ray desde un origen (por ejemplo el transform de la mano)
/// y llama a SpeechBubbleControllerVR.TryOpenLinkAtWorldPoint si golpea un collider del speech bubble.
/// Funciona con OVR (trigger) y tiene un fallback a clic de ratón para pruebas en editor.
/// Requiere que el SpeechBubble (o su canvas) tenga un Collider (p. ej. BoxCollider) para detectar impactos.
/// </summary>
public class VRRayLinkInteractor : MonoBehaviour
{
    [Tooltip("Transform desde donde lanzar el ray (normalmente el anchor de la mano o el ray origin)")]
    public Transform rayOrigin;

    [Tooltip("Capa(s) que serán interactuables (p. ej. 'UI' o capa custom que tenga el speech bubble)")]
    public LayerMask interactLayers = ~0;

    [Tooltip("Cámara usada para convertir puntos world->screen para TMP; si está nulo se usa Camera.main")]
    public Camera eventCamera;

    [Tooltip("Distancia máxima del ray")]
    public float maxDistance = 10f;

    [Tooltip("Si true dibuja el ray en la escena para debugging")]
    public bool debugRay = false;

    [Tooltip("Si true dibuja el rayo continuamente (no sólo al pulsar) para facilitar el debug en Scene/Game view)")]
    public bool alwaysShowRay = true;

    [Tooltip("Color del rayo de depuración")]
    public Color debugRayColor = Color.green;

    [Tooltip("Ancho del rayo (si se usa LineRenderer)")]
    public float debugRayWidth = 0.002f;

    private LineRenderer debugLine;

    void Update()
    {
        if (rayOrigin == null) return;

        bool pressed = false;

        // Detectar input: usamos el click del ratón / botón Fire1 como fallback para pruebas en editor.
        // En proyectos Oculus con OVRIntegration se puede reemplazar por OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger)
        pressed = Input.GetMouseButtonDown(0) || UnityEngine.Input.GetButtonDown("Fire1");

        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);

        // Siempre dibujar rayo en modo debug si el usuario lo desea (mejor visibilidad)
        if (debugRay && alwaysShowRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * maxDistance, debugRayColor);
            if (debugLine == null)
            {
                // crear LineRenderer dinámico
                GameObject go = new GameObject("VRRayDebugLine");
                go.hideFlags = HideFlags.HideAndDontSave;
                debugLine = go.AddComponent<LineRenderer>();
                debugLine.positionCount = 2;
                debugLine.material = new Material(Shader.Find("Unlit/Color")) { color = debugRayColor };
                debugLine.startColor = debugRayColor;
                debugLine.endColor = debugRayColor;
                debugLine.startWidth = debugRayWidth;
                debugLine.endWidth = debugRayWidth;
                debugLine.useWorldSpace = true;
            }
            debugLine.SetPosition(0, ray.origin);
            debugLine.SetPosition(1, ray.origin + ray.direction * maxDistance);
        }

        if (pressed)
        {
            if (debugRay && !alwaysShowRay)
            {
                Debug.DrawRay(ray.origin, ray.direction * maxDistance, debugRayColor, 2f);
            }

            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactLayers))
            {
                // Buscar SpeechBubbleControllerVR en el objeto golpeado o en sus padres
                SpeechBubbleControllerVR sb = hit.collider.GetComponentInParent<SpeechBubbleControllerVR>();
                if (sb != null)
                {
                    Camera cam = eventCamera != null ? eventCamera : Camera.main;
                    bool opened = sb.TryOpenLinkAtWorldPoint(hit.point, cam);
                    if (!opened)
                    {
                        Debug.Log("VRRayLinkInteractor: no se detectó link en el punto golpeado.");
                    }
                }
            }
        }
    }

    private void OnDisable()
    {
        if (debugLine != null)
        {
            Destroy(debugLine.gameObject);
            debugLine = null;
        }
    }
}

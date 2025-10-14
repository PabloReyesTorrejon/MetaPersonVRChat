using UnityEngine;

[HelpURL("")]
public class AvatarLookAtUser : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Transform de la cabeza del jugador (CenterEyeAnchor). Si está vacío intenta usar Camera.main.")]
    public Transform playerHead;

    [Tooltip("Si quieres rotar sólo la cabeza o un hueso concreto, pon aquí ese transform. Si está vacío intentará buscar Animator -> Head.")]
    public Transform headBone;

    [Header("Ajustes de rotación")]
    [Tooltip("Si true, rota el transform raíz (this.transform). Si false, rota únicamente headBone.")]
    public bool rotateRoot = false;

    [Tooltip("Si true, solo rota en el eje Y (horizontal). Evita inclinaciones raras.")]
    public bool onlyY = true;

    [Tooltip("Velocidad de suavizado (mayor = más rápido).")]
    public float smoothSpeed = 8f;

    [Tooltip("Compensa la orientación del modelo en grados (ej. 180 si el avatar está de espaldas).")]
    public float yawOffsetDegrees = 0f;

    [Tooltip("Límite de inclinación hacia arriba/abajo (si onlyY=false).")]
    public float maxPitch = 60f;

    Animator animator;
    Transform pivot; // parent de headBone usado para convertir rotaciones
    Quaternion initialLocalRotation;

    void Start()
    {
        if (playerHead == null && Camera.main != null)
            playerHead = Camera.main.transform;

        animator = GetComponent<Animator>();

        if (headBone == null && animator != null)
        {
            // intenta obtener el bone Head si es humanoide
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        if (headBone != null)
        {
            pivot = headBone.parent;
            initialLocalRotation = headBone.localRotation;
        }
        else
        {
            pivot = transform.parent;
            initialLocalRotation = transform.localRotation;
        }
    }

    void LateUpdate()
    {
        if (playerHead == null) return;

        if (rotateRoot)
            RotateRootTowardsPlayer();
        else
            RotateBoneTowardsPlayer();
    }

    void RotateRootTowardsPlayer()
    {
        Vector3 dir = playerHead.position - transform.position;
        if (onlyY) dir.y = 0;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetWorld = Quaternion.LookRotation(dir);
        // aplica corrección de yaw (gira en Y en espacio local del mundo)
        targetWorld *= Quaternion.Euler(0f, yawOffsetDegrees, 0f);

        transform.rotation = Quaternion.Slerp(transform.rotation, targetWorld, Time.deltaTime * smoothSpeed);
    }

    void RotateBoneTowardsPlayer()
    {
        if (headBone == null)
        {
            // fallback: rota raíz
            RotateRootTowardsPlayer();
            return;
        }

        Vector3 dir = playerHead.position - headBone.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        if (onlyY) dir.y = 0;
        Quaternion worldTarget = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, yawOffsetDegrees, 0f);

        // si queremos limitar pitch (cuando onlyY==false)
        if (!onlyY)
        {
            Vector3 euler = worldTarget.eulerAngles;
            // convertir de 0..360 a -180..180
            float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
            euler.x = pitch;
            worldTarget = Quaternion.Euler(euler);
        }

        // convertimos rotación mundial a rotación local del hueso (respecto a su padre)
        Quaternion desiredLocal;
        if (pivot != null)
            desiredLocal = Quaternion.Inverse(pivot.rotation) * worldTarget;
        else
            desiredLocal = worldTarget;

        // Aplicamos suavizado (Slerp en espacio local)
        headBone.localRotation = Quaternion.Slerp(headBone.localRotation, desiredLocal, Time.deltaTime * smoothSpeed);
    }

    // debug gizmos opcional
    void OnDrawGizmosSelected()
    {
        if (playerHead == null) return;
        Gizmos.color = Color.green;
        Vector3 from = (headBone != null) ? headBone.position : transform.position;
        Gizmos.DrawLine(from, playerHead.position);
        Gizmos.DrawSphere(playerHead.position, 0.02f);
    }
}

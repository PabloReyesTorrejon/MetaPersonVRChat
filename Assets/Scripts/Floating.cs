using UnityEngine;

public class Floating : MonoBehaviour
{
    public float amplitude = 0.2f;   // Altura del movimiento (0.2 = 20 cm aprox)
    public float frequency = 1f;     // Velocidad del movimiento

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        // Movimiento vertical usando una onda seno
        float newY = startPos.y + Mathf.Sin(Time.time * frequency) * amplitude;
        transform.localPosition = new Vector3(startPos.x, newY, startPos.z);
    }
}

using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BlinkEyes : MonoBehaviour
{
    public Image eyeImage;              // Imagen UI de los ojos
    public Sprite eyesOpen;             // Sprite ojos abiertos
    public Sprite eyesClosed;           // Sprite ojos cerrados
    public float blinkInterval = 10f;   // Tiempo entre parpadeos
    public float blinkDuration = 0.12f; // Duración del parpadeo

    void Start()
    {
        if (eyeImage == null)
        {
            eyeImage = GetComponent<Image>();
        }

        StartCoroutine(BlinkRoutine());
    }

    IEnumerator BlinkRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(blinkInterval);

            // Cerrar ojos
            eyeImage.sprite = eyesClosed;
            yield return new WaitForSeconds(blinkDuration);

            // Abrir ojos
            eyeImage.sprite = eyesOpen;
        }
    }
}

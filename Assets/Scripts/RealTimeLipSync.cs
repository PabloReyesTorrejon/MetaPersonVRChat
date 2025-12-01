using UnityEngine;

public class RealTimeLipSync : MonoBehaviour
{
    public AudioSource audioSource;
    public SpriteRenderer mouthRenderer;

    [Header("Mouth Sprites por intensidad")]
    public Sprite mouthClosed;   // silencio
    public Sprite mouthSmall;    // baja intensidad
    public Sprite mouthMedium;   // media
    public Sprite mouthOpen;     // alta intensidad

    private float[] samples = new float[1024];

    void Update()
    {
        if (!audioSource.isPlaying) 
        {
            mouthRenderer.sprite = mouthClosed;
            return;
        }

        audioSource.GetOutputData(samples, 0);

        // calcular energía del audio
        float sum = 0;
        for (int i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];

        float rms = Mathf.Sqrt(sum / samples.Length); // Root Mean Square

        // Cambiar sprite por intensidad
        if (rms < 0.02f)
            mouthRenderer.sprite = mouthClosed;
        else if (rms < 0.05f)
            mouthRenderer.sprite = mouthSmall;
        else if (rms < 0.1f)
            mouthRenderer.sprite = mouthMedium;
        else
            mouthRenderer.sprite = mouthOpen;
    }
}

using UnityEngine;

public class RealTimeSpanishPhonemeLipSyncSmooth : MonoBehaviour
{
    public AudioSource audioSource;
    public SpriteRenderer mouthRenderer;

    [Header("Sprites")]
    public Sprite rest;
    public Sprite ai;
    public Sprite o;
    public Sprite u;
    public Sprite cdkg;
    public Sprite fv;
    public Sprite l;
    public Sprite mbp;

    private float[] spectrum = new float[512];

    // Smoothing parameters
    [Header("Smoothing")]
    public float smoothFactor = 0.25f;            // FFT smoothing
    public float spriteChangeDelay = 0.08f;       // seconds min between sprite changes
    
    [Header("Silence detection")]
    [Tooltip("Si la energía total cae por debajo de este umbral, se vuelve inmediatamente al sprite 'rest'.")]
    public float silenceThreshold = 0.005f;

    float smLow, smMid, smHigh, smBurst;
    float timeSinceLastChange = 0f;
    Sprite lastSprite;

    void Start()
    {
        lastSprite = rest;
    }

    void Update()
    {
        if (!audioSource || !mouthRenderer)
            return;

        timeSinceLastChange += Time.deltaTime;

        if (!audioSource.isPlaying)
        {
            mouthRenderer.sprite = rest;
            return;
        }

        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        float low = Sum(0, 15);
        float mid = Sum(16, 40);
        float high = Sum(41, 90);
        float burst = DetectBurst();

        // Apply temporal smoothing
        smLow = Mathf.Lerp(smLow, low, smoothFactor);
        smMid = Mathf.Lerp(smMid, mid, smoothFactor);
        smHigh = Mathf.Lerp(smHigh, high, smoothFactor);
        smBurst = Mathf.Lerp(smBurst, burst, smoothFactor);

        // Si la energía total es muy baja, consideramos silencio y forzamos 'rest'
        float totalEnergy = smLow + smMid + smHigh + smBurst;
        if (totalEnergy < silenceThreshold)
        {
            if (lastSprite != rest)
            {
                lastSprite = rest;
                mouthRenderer.sprite = rest;
                // reset smoothing para evitar 'ghosting'
                smLow = smMid = smHigh = smBurst = 0f;
                timeSinceLastChange = 0f;
            }
            return;
        }

        Sprite chosen = DetermineSprite(smLow, smMid, smHigh, smBurst);

        // Sprite smoothing – avoid rapid changes
        if (chosen != lastSprite && timeSinceLastChange > spriteChangeDelay)
        {
            lastSprite = chosen;
            mouthRenderer.sprite = chosen;
            timeSinceLastChange = 0f;
        }
    }

    Sprite DetermineSprite(float low, float mid, float high, float burst)
    {
        // Explosive consonants first
        if (burst > 0.08f)
            return (Random.value > 0.5f) ? mbp : cdkg;

        if (low > mid && low > high)
        {
            if (low > 0.04f)
                return o;
            else
                return u;
        }

        if (mid > low && mid > high)
            return ai;

        if (high > 0.02f)
            return fv;

        return rest;
    }

    float Sum(int start, int end)
    {
        float s = 0f;
        for (int i = start; i < end; i++)
            s += spectrum[i];
        return s;
    }

    float DetectBurst()
    {
        float s = 0f;
        for (int i = 4; i < 18; i++)
            s += spectrum[i];
        return s;
    }
}

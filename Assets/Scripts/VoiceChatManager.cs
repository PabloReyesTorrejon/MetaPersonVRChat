using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class AudioRequest
{
    public string audio;
}

[Serializable]
public class AudioResponse
{
    public string text;
    public string audio; // base64 del audio (puede ser mp3 o wav)
}

[Serializable]
public class TtsRequest
{
    public string text;
}

public class VoiceChatManager : MonoBehaviour
{
    public AudioSource audioSource;
    [Tooltip("Si true, el audio TTS estará silenciado")]
    public bool isMuted = false;

    [Header("Configuración de entrada para silenciar")]
    [Tooltip("Si true, se escuchará también la entrada OVRInput para toggle (mando Quest). Si false, sólo teclado.)")]
    public bool useOVRInput = true;

    [Tooltip("Botón OVR que alterna mute (por defecto: Button.Four — ajusta en el inspector si hace falta)")]
    public OVRInput.Button muteOVRButton = OVRInput.Button.Four;

    [Header("Referencia al panel del bocadillo de texto")]
    public GameObject speechBubble;

    [Header("Controlador de visemas (sprites de boca)")]
    public VisemePlayer visemePlayer;   // 👈 AÑADIDO

    [Header("Lip Sync FFT basado en audio")]
    public RealTimeSpanishPhonemeLipSyncSmooth realTimeLipSync;
    
    [Header("TTS endpoint")]
    [Tooltip("URL del endpoint /api/tts. Si está vacío se intentará un fallback a http://127.0.0.1:3000/api/tts")]
    public string ttsEndpoint = "https://adan-cofferlike-cris.ngrok-free.dev/api/tts";
    
    // No usar clips locales para el saludo por defecto: el TTS proviene del servidor gTTS.

    public void SendAudio(float[] samples, int sampleRate)
    {
        StartCoroutine(SendAudioCoroutine(samples, sampleRate));
    }

    void Update()
    {
        // Toggle mute con botón Y del mando o con la tecla Y del teclado (útil en editor)
        bool pressed = false;
        if (useOVRInput)
        {
            if (OVRInput.GetDown(muteOVRButton)) pressed = true;
        }

        if (Input.GetKeyDown(KeyCode.Y)) pressed = true;

        if (pressed)
        {
            ToggleMute();
        }
    }

    /// <summary>
    /// Alterna el estado de silencio del TTS (mute/unmute)
    /// </summary>
    public void ToggleMute()
    {
        isMuted = !isMuted;
        if (audioSource != null)
            audioSource.mute = isMuted;

        // Mostrar/ocultar icono de silenciado en el bocadillo (no modificar el texto)
        if (speechBubble != null)
        {
            var bubbleVR = speechBubble.GetComponent<SpeechBubbleControllerVR>();
            if (bubbleVR != null)
            {
                bubbleVR.ShowMuteIcon(true, isMuted);
            }
            else
            {
                var bubbleOld = speechBubble.GetComponent<SpeechBubble>();
                if (bubbleOld != null)
                {
                    // Si el bocadillo antiguo no soporta icono, podríamos mostrar un log o
                    // implementar una mecánica alternativa. Por ahora sólo logueamos.
                    Debug.Log("VoiceChatManager: cambio mute, pero SpeechBubble antiguo no soporta icono.");
                }
            }
        }
        Debug.Log($"VoiceChatManager: isMuted = {isMuted}");
    }

    /// <summary>
    /// Reproduce un mensaje de saludo predefinido usando TTS
    /// </summary>
    public void PlayGreeting(string greetingText = "en qué te puedo ayudar?")
    {
        StartCoroutine(PlayGreetingCoroutine(greetingText));
    }

    private IEnumerator PlayGreetingCoroutine(string greetingText)
    {
        // Mostrar el texto en el bocadillo
        if (speechBubble != null)
        {
            var bubbleVR = speechBubble.GetComponent<SpeechBubbleControllerVR>();
            if (bubbleVR != null)
            {
                bubbleVR.ShowText(greetingText);
            }
        }

        // Crear solicitud al backend solo para obtener el TTS
        TtsRequest ttsRequest = new TtsRequest { text = greetingText };
        string jsonPayload = JsonUtility.ToJson(ttsRequest);

        string primaryUrl = string.IsNullOrEmpty(ttsEndpoint) ? "" : ttsEndpoint;
        string fallbackUrl = "http://127.0.0.1:3000/api/tts";

        string[] tryUrls = string.IsNullOrEmpty(primaryUrl) ? new string[] { fallbackUrl } : new string[] { primaryUrl, fallbackUrl };

        UnityWebRequest www = null;
        string responseText = null;

        Debug.Log("VoiceChatManager: PlayGreeting -> solicitando TTS para: '" + greetingText + "'. URLs: " + string.Join(",", tryUrls));

        foreach (var url in tryUrls)
        {
            using (UnityWebRequest uw = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw2 = Encoding.UTF8.GetBytes(jsonPayload);
                uw.uploadHandler = new UploadHandlerRaw(bodyRaw2);
                uw.downloadHandler = new DownloadHandlerBuffer();
                uw.SetRequestHeader("Content-Type", "application/json");

                Debug.Log("VoiceChatManager: intentando TTS en: " + url);
                yield return uw.SendWebRequest();

                if (uw.result == UnityWebRequest.Result.Success)
                {
                    responseText = uw.downloadHandler.text;
                    Debug.Log("VoiceChatManager: respuesta TTS recibida (len=" + (responseText?.Length ?? 0) + ")");
                    www = uw; // assign for later parsing (we'll parse responseText)
                    break;
                }
                else
                {
                    string serverText = null;
                    try { serverText = uw.downloadHandler?.text; } catch { }
                    Debug.LogWarning($"VoiceChatManager: fallo petición TTS a {url}: {uw.error} -- respuesta: " + (serverText ?? "<vacío>"));
                }
            }
        }
        if (string.IsNullOrEmpty(responseText))
        {
            Debug.LogError("VoiceChatManager: No se pudo obtener respuesta TTS desde ninguna URL. Asegúrate de que el servidor /api/tts está accesible y genera audio.");
            // No reproducir clips locales: confiar únicamente en el gTTS del servidor.
            yield break;
        }

        AudioResponse json = null;
        try
        {
            json = JsonUtility.FromJson<AudioResponse>(responseText);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error al parsear respuesta TTS: " + ex.Message + " -- respuesta: " + responseText);
            yield break;
        }

        if (json == null)
        {
            Debug.LogError("VoiceChatManager: parseo JSON TTS produjo null. responseText=" + responseText);
            yield break;
        }

        if (json != null && !string.IsNullOrEmpty(json.audio))
        {
            // Reproducir el audio TTS
            yield return StartCoroutine(PlayBase64Audio(json.audio));
        }
        else
        {
            Debug.LogWarning("VoiceChatManager: la respuesta TTS no contiene campo 'audio'. response.text=" + json.text + ". No se reproducirá audio local; revisa el servidor gTTS.");
        }

        // Esperar un momento antes de finalizar
        yield return new WaitForSeconds(1f);
    }

    // Note: previously used to clear quick messages; removed to avoid overwriting assistant text.

    private IEnumerator SendAudioCoroutine(float[] samples, int sampleRate)
    {
        // --- Convertir mic input a WAV ---
        byte[] wavData = ConvertToWav(samples, sampleRate);
        string base64Audio = Convert.ToBase64String(wavData);

        // --- Enviar a servidor ---
        AudioRequest request = new AudioRequest { audio = base64Audio };
        string jsonData = JsonUtility.ToJson(request);

        // Mostrar mensajes de "pensando..."
        if (speechBubble != null)
        {
            var bubbleVR = speechBubble.GetComponent<SpeechBubbleControllerVR>();
            if (bubbleVR != null)
                bubbleVR.ShowThinking();
        }   

        //192.168.1.67
        //
        UnityWebRequest www = new UnityWebRequest("https://adan-cofferlike-cris.ngrok-free.dev/api/audio", "POST");
        www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonData));
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        // Detener "pensando..." cuando llega respuesta
        var bubbleVRthinking = speechBubble.GetComponent<SpeechBubbleControllerVR>();
        if (bubbleVRthinking != null)
            bubbleVRthinking.StopThinking();    
    
        AudioResponse json = null;
        try
        {
            json = JsonUtility.FromJson<AudioResponse>(www.downloadHandler.text);
        }
        catch (Exception ex)
        {
            Debug.LogError("⚠️ Error procesando respuesta JSON: " + ex.Message);
            yield break;
        }

        // 🗨️ Mostrar texto del asistente
        if (speechBubble != null && !string.IsNullOrEmpty(json.text))
        {
            var bubbleVR = speechBubble.GetComponent<SpeechBubbleControllerVR>();
            if (bubbleVR != null)
                bubbleVR.ShowText(json.text);
            else
            {
                var bubbleOld = speechBubble.GetComponent<SpeechBubble>();
                if (bubbleOld != null)
                    bubbleOld.SetText(json.text);
            }
        }

        // 👄 NO activar lip-sync de texto aquí - dejar que RealTimeSpanishPhonemeLipSync 
        //    maneje el lip-sync basado en el audio para mejor sincronización
        // if (visemePlayer != null && !string.IsNullOrEmpty(json.text))
        // {
        //     visemePlayer.PlayText(json.text);
        // }

        // 🔊 Reproducir audio si está disponible
        if (!string.IsNullOrEmpty(json.audio))
        {
            IEnumerator playAudio = PlayBase64Audio(json.audio);
            yield return StartCoroutine(playAudio);
        }

        Debug.Log("✅ Texto IA: " + json.text);
    }

    // ==============================
    // 🔊 FUNCIÓN UNIVERSAL
    // ==============================
    private IEnumerator PlayBase64Audio(string base64Audio)
    {
        if (audioSource == null)
        {
            Debug.LogError("VoiceChatManager: audioSource no está asignado. Asigna un AudioSource en el Inspector.");
            yield break;
        }

        string tempPath = System.IO.Path.Combine(Application.persistentDataPath, "respuesta_audio.mp3");
        byte[] audioBytes = null;
        try
        {
            audioBytes = Convert.FromBase64String(base64Audio);
        }
        catch (Exception ex)
        {
            Debug.LogError("VoiceChatManager: error decoding base64 audio: " + ex.Message + "\nprimeros 200 chars: " + (base64Audio?.Substring(0, Math.Min(200, base64Audio.Length)) ?? "<null>"));
            yield break;
        }

        try
        {
            System.IO.File.WriteAllBytes(tempPath, audioBytes);
        }
        catch (Exception ex)
        {
            Debug.LogError("⚠️ Error escribiendo audio temporal: " + ex.Message);
            yield break;
        }

        AudioType tipo = tempPath.EndsWith(".wav") ? AudioType.WAV : AudioType.MPEG;

        using (UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, tipo))
        {
            yield return uwr.SendWebRequest();
            if (uwr.result != UnityWebRequest.Result.Success)
                Debug.LogError("❌ Error cargando audio: " + uwr.error);
            else
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(uwr);
                audioSource.clip = clip;
                audioSource.Play();
                Debug.Log("VoiceChatManager: reproducción TTS iniciada, duración: " + clip.length + "s");

                // Esperar a que termine de reproducirse el audio
                yield return new WaitWhile(() => audioSource.isPlaying);

                // Forzar reset del sprite de la boca a "rest" (sonrisa)
                if (realTimeLipSync != null && realTimeLipSync.mouthRenderer != null && realTimeLipSync.rest != null)
                {
                    realTimeLipSync.mouthRenderer.sprite = realTimeLipSync.rest;
                    Debug.Log("🙂 Boca reseteada a sprite 'rest' (sonrisa)");
                }
            }
        }

        yield return new WaitForSeconds(1f);
        try { System.IO.File.Delete(tempPath); } catch { }
    }

    // ==============================
    // 🎙️ Conversión WAV
    // ==============================
    private byte[] ConvertToWav(float[] samples, int sampleRate)
    {
        int samplesLength = samples.Length;
        byte[] wav = new byte[44 + samplesLength * 2];
        int byteRate = sampleRate * 2;

        Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
        BitConverter.GetBytes(wav.Length - 8).CopyTo(wav, 4);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);
        Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
        BitConverter.GetBytes(16).CopyTo(wav, 16);
        BitConverter.GetBytes((short)1).CopyTo(wav, 20);
        BitConverter.GetBytes((short)1).CopyTo(wav, 22);
        BitConverter.GetBytes(sampleRate).CopyTo(wav, 24);
        BitConverter.GetBytes(byteRate).CopyTo(wav, 28);
        BitConverter.GetBytes((short)2).CopyTo(wav, 32);
        BitConverter.GetBytes((short)16).CopyTo(wav, 34);
        Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
        BitConverter.GetBytes(samplesLength * 2).CopyTo(wav, 40);

        int offset = 44;
        foreach (var f in samples)
        {
            short val = (short)(Mathf.Clamp(f, -1f, 1f) * short.MaxValue);
            BitConverter.GetBytes(val).CopyTo(wav, offset);
            offset += 2;
        }

        return wav;
    }
}
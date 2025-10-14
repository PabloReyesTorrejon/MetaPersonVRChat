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
    public string audio;
}

public class VoiceChatManager : MonoBehaviour
{
    public AudioSource audioSource;

    [Header("Referencia al panel del bocadillo de texto")]
    public SpeechBubbleText speechBubble;

    public void SendAudio(float[] samples, int sampleRate)
    {
        StartCoroutine(SendAudioCoroutine(samples, sampleRate));
    }

    private IEnumerator SendAudioCoroutine(float[] samples, int sampleRate)
    {
        byte[] wavData = ConvertToWav(samples, sampleRate);
        string base64Audio = Convert.ToBase64String(wavData);
        AudioRequest request = new AudioRequest { audio = base64Audio };
        string jsonData = JsonUtility.ToJson(request);

        UnityWebRequest www = new UnityWebRequest("http://192.168.1.67:3000/api/audio", "POST");
        www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonData));
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error al enviar audio: " + www.error);
            Debug.LogError("Respuesta del servidor: " + www.downloadHandler.text);
            yield break;
        }

        try
        {
            var json = JsonUtility.FromJson<AudioResponse>(www.downloadHandler.text);

            // Mostrar texto de Ollama en el panel
            if (speechBubble != null && !string.IsNullOrEmpty(json.text))
            {
                speechBubble.SetText(json.text);
            }

            // Reproducir audio si está disponible
            if (!string.IsNullOrEmpty(json.audio))
            {
                byte[] audioBytes = Convert.FromBase64String(json.audio);
                AudioClip clip = WavUtility.ToAudioClip(audioBytes, 0, "Respuesta");
                audioSource.clip = clip;
                audioSource.Play();
            }

            Debug.Log("Texto de IA: " + json.text);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error procesando respuesta: " + ex.Message);
        }
    }

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

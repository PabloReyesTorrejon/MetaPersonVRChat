using UnityEngine;
using System;

public class QuestMicRecorder : MonoBehaviour
{
    public VoiceChatManager voiceChatManager;
    public SpeechBubble speechBubblePrefab;
    private SpeechBubble activeBubble;

    private AudioClip recording;
    private string micDevice;
    private Transform headTransform;

    void Start()
    {
        headTransform = Camera.main.transform;

        if (Microphone.devices.Length > 0)
        {
            micDevice = Microphone.devices[0];
            Debug.Log("Micrófono detectado: " + micDevice);
        }
        else
        {
            Debug.LogError("No se detectó ningún micrófono");
        }

       
    }

    public void StartRecording()
    {
        if (micDevice != null)
        {
            recording = Microphone.Start(micDevice, false, 10, 16000);
            Debug.Log("Grabando...");
            //if (activeBubble) activeBubble.SetText("🎙 Grabando...");
        }
    }

    public void StopAndSendRecording()
    {
        if (recording != null && Microphone.IsRecording(micDevice))
        {
            Microphone.End(micDevice);
            Debug.Log("Grabación terminada");

            float[] samples = new float[recording.samples * recording.channels];
            recording.GetData(samples, 0);

            // Enviar al servidor y actualizar texto
            voiceChatManager.SendAudio(samples, recording.frequency);

        }
    }

    void Update()
    {
       

        // 🎮 Botón A del mando derecho para grabar
        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            StartRecording();
        }
        if (OVRInput.GetUp(OVRInput.Button.One))
        {
            StopAndSendRecording();
        }
    }
}

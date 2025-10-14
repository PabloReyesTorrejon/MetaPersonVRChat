using UnityEngine;
using TMPro;
using System.Collections;

public class SpeechBubbleControllerVR : MonoBehaviour
{
    public TextMeshProUGUI textUI;
    public float typingSpeed = 0.015f;

    private Coroutine typingCoroutine;

    public void ShowText(string message)
    {
        if (textUI == null) return;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeText(message));
    }

    private IEnumerator TypeText(string message)
    {
        textUI.text = "";
        foreach (char c in message)
        {
            textUI.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpeechBubbleText : MonoBehaviour
{
    public TextMeshProUGUI chatText;
    public ScrollRect scrollRect;

    public void SetText(string text)
    {
        chatText.text = text;

        // Esperar un frame y hacer que el scroll se posicione arriba del todo
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 1f;
    }
}

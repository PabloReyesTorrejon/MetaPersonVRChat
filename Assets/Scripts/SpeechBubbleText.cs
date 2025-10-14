using TMPro;
using UnityEngine;

public class SpeechBubbleText : MonoBehaviour
{
    [Header("Referencia al TextMeshPro dentro del panel")]
    public TextMeshProUGUI textUI;

    [Header("Opcional: límite de caracteres o autoajuste")]
    public bool autoResize = true;
    public float minFontSize = 28f;
    public float maxFontSize = 48f;

    void Start()
    {
        if (textUI == null)
            textUI = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void SetText(string newText)
    {
        if (textUI == null) return;

        textUI.text = newText;

        if (autoResize)
        {
            textUI.enableAutoSizing = true;
            textUI.fontSizeMin = minFontSize;
            textUI.fontSizeMax = maxFontSize;
        }
    }
}

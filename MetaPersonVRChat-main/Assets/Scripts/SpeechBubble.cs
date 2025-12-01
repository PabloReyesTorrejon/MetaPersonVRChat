using UnityEngine;
using TMPro;

public class SpeechBubble : MonoBehaviour
{
    public TextMeshProUGUI textMesh;

    public void SetText(string message)
    {
        if (textMesh != null)
            textMesh.text = message;
    }
}

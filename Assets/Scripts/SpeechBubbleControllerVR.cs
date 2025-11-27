using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class SpeechBubbleControllerVR : MonoBehaviour
{
    [Header("Referencias")]
    public TextMeshProUGUI textUI;            // El TextMeshProUGUI que muestra el chat
    public RectTransform textContainer;       // RectTransform que contiene el textUI (Content)
    public Image scrollBarImage;              // Imagen que actúa como la barra vertical

    [Header("Parámetros")]
    public float typingSpeed = 0.015f;
    public float verticalScrollSpeed = 200f;
    public bool autoScrollToTopWhenFinished = true; // <-- si true, tras terminar escribe, ajusta al inicio

    private Coroutine typingCoroutine;
    private float scrollOffsetY = 0f;   // valor en px (positivo = mover contenido hacia abajo visualmente)
    private float maxYOffset = 0f;      // cuánto contenido "desborda" (positivo)

    private void Update()
    {
        // Input del stick vertical
        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        float inputY = stick.y;

        if (Mathf.Abs(inputY) > 0.08f)
        {
            // Nota: si el usuario empuja hacia arriba (inputY > 0) queremos desplazar el contenido hacia abajo
            scrollOffsetY += -inputY * verticalScrollSpeed * Time.deltaTime;
            ApplyScroll();
        }
    }

    public void ShowText(string message)
    {
        if (textUI == null || textContainer == null) return;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeText(message));
    }

    private IEnumerator TypeText(string message)
    {
        textUI.text = "";
        // forzamos pequeño delay para que layouts se actualicen correctamente
        yield return null;

        foreach (char c in message)
        {
            textUI.text += c;
            yield return new WaitForSeconds(typingSpeed);

            // autoscroll hacia abajo mientras escribe
            UpdateScrollLimits();
            ScrollToBottomImmediate();
        }

        // Cuando termina de escribir forzamos recálculo de layout y límites
        LayoutRebuilder.ForceRebuildLayoutImmediate(textUI.rectTransform);
        UpdateScrollLimits();

        // Si queremos que al finalizar el texto se muestre el INICIO (parte superior):
        if (autoScrollToTopWhenFinished)
        {
            ScrollToTopImmediate();
        }
        else
        {
            // si no, mantenemos en bottom (opción)
            ScrollToBottomImmediate();
        }
    }

    private void UpdateScrollLimits()
    {
        // Forzar cálculo del preferred height
        LayoutRebuilder.ForceRebuildLayoutImmediate(textUI.rectTransform);

        float textHeight = textUI.preferredHeight;
        RectTransform viewport = (RectTransform)textContainer.parent;
        float visibleHeight = viewport.rect.height;

        // maxYOffset = cuánto hay que desplazar para ver TODO el contenido (si textHeight > visibleHeight)
        maxYOffset = Mathf.Max(0f, textHeight - visibleHeight);
    }

   private void ApplyScroll()
    {
        if (textUI == null || textContainer == null) return;

        UpdateScrollLimits();

        // Limitar desplazamiento (corregido)
        scrollOffsetY = Mathf.Clamp(scrollOffsetY, -maxYOffset, 0f);

        textContainer.anchoredPosition = new Vector2(
            textContainer.anchoredPosition.x,
            -scrollOffsetY
        );

        UpdateScrollBar();
    }

    private void ScrollToBottomImmediate()
    {
        UpdateScrollLimits();
        scrollOffsetY = maxYOffset;
        ApplyScroll();
    }

    private void ScrollToTopImmediate()
    {
        UpdateScrollLimits();
        scrollOffsetY = 0f;
        ApplyScroll();
    }

   private void UpdateScrollBar()
    {
        if (scrollBarImage == null || textUI == null) return;

        RectTransform viewport = (RectTransform)textContainer.parent;
        float visibleHeight = viewport.rect.height;
        float textHeight = textUI.preferredHeight;

        float overflow = Mathf.Max(0f, textHeight - visibleHeight);

        // Mostrar/ocultar la barra
        scrollBarImage.enabled = overflow > 1f;
        if (!scrollBarImage.enabled) return;

        RectTransform barRect = scrollBarImage.rectTransform;
        float totalHeight = viewport.rect.height;

        // Tamaño proporcional
        float visibleRatio = visibleHeight / Mathf.Max(textHeight, 1f);
        float barHeight = totalHeight * Mathf.Clamp01(visibleRatio);
        barRect.sizeDelta = new Vector2(barRect.sizeDelta.x, barHeight);

        // Posición correcta
        float normalized = (maxYOffset <= 0f) ? 0f : Mathf.InverseLerp(-maxYOffset, 0f, scrollOffsetY);
        float barY = (totalHeight - barHeight) * normalized;

        // 🟢 Barra bien posicionada (de arriba hacia abajo)
        barRect.anchoredPosition = new Vector2(barRect.anchoredPosition.x, -barY);
    }
}
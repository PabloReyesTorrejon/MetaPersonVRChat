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

    [Header("Iconos")]
    [Tooltip("Imagen que mostrará el icono de silenciado/activado en la esquina inferior derecha del texto")]
    public Image muteIcon;

    [Tooltip("Sprite para el estado silenciado")]
    public Sprite spriteMuted;

    [Tooltip("Sprite para el estado sonando")]
    public Sprite spriteUnmuted;

    [Tooltip("Tamaño del icono en píxeles")]
    public Vector2 muteIconSize = new Vector2(40, 40);

    [Tooltip("Offset desde la esquina inferior derecha del contenedor de texto (x negativo hacia la izquierda)")]
    public Vector2 muteIconOffset = new Vector2(-8, 8);

    [Tooltip("Factor multiplicador para compensar la escala del canvas/world-space (ajusta si el icono queda muy pequeño). Ajustado por defecto a un valor pequeño para burbujas en world-space.")]
    public float muteIconWorldScale = 0.00175f;

        [Header("Colocación alternativa del icono")]
        [Tooltip("Si true, el icono será hijo directo del GameObject del bocadillo y se compensará su escala inversa para ser visible en world-space")]
        public bool placeIconAsBubbleChild = true;

    [Tooltip("Si true, usa la posición local manual para el icono en vez de calcular anclajes automáticos (útil si conoces la posición exacta)")]
    public bool useManualLocalPosition = true;

    [Tooltip("Posición local a usar cuando useManualLocalPosition = true (unidades en local space del bocadillo)")]
    // Ajustado: x = -0.0989999995, mantengo y por defecto y = -0.182999998, z = 0.0500000007
    public Vector3 manualLocalPosition = new Vector3(-0.0989999995f, -0.182999998f, 0.0500000007f);

        [Tooltip("Rotación local a aplicar al icono cuando se parenta al bocadillo (grados)")]
        public Vector3 manualLocalEuler = Vector3.zero;

    [Tooltip("Desplazamiento adicional en Z (local) aplicado sobre `manualLocalPosition.z` para situar el icono delante del panel. Valores negativos acercan al camera.")]
    public float manualLocalZOffset = -1.0f;    [Header("Parámetros")]
    public float typingSpeed = 0.015f;
    public float verticalScrollSpeed = 200f;
    public bool autoScrollToTopWhenFinished = true; // <-- si true, tras terminar escribe, ajusta al inicio
    
    [Tooltip("Multiplicador extra para ajustar el tamaño final del icono (útil para calibrar)")]
    public float muteIconScaleMultiplier = 1f;
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

        // Barra bien posicionada (de arriba hacia abajo)
        barRect.anchoredPosition = new Vector2(barRect.anchoredPosition.x, -barY);
    }
    private Coroutine thinkingCoroutine;

    private void Start()
    {
        // Asegurarnos de que el icono de mute esté oculto al inicio
        if (muteIcon != null)
        {
            // si el icono ya existe, lo parentamos según la configuración
            if (placeIconAsBubbleChild)
            {
                muteIcon.rectTransform.SetParent(this.transform, worldPositionStays: false);
            }
            else
            {
                RectTransform viewport = (RectTransform)textContainer.parent;
                muteIcon.rectTransform.SetParent(viewport, worldPositionStays: false);
            }
            muteIcon.gameObject.SetActive(false);
        }
        else
        {
            // si no hay icono asignado, intentamos crear uno dinámicamente (si hay sprites)
            if (spriteMuted != null || spriteUnmuted != null)
            {
                Transform parent = placeIconAsBubbleChild ? this.transform : (Transform)textContainer.parent;
                GameObject go = new GameObject("MuteIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(parent, false);
                Image img = go.GetComponent<Image>();
                img.raycastTarget = false;
                muteIcon = img;
                muteIcon.gameObject.SetActive(false);
            }
        }

        // Fallback: intentar localizar sprites por nombre si no se asignaron en el inspector
        if ((spriteMuted == null || spriteUnmuted == null))
        {
            var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (var s in allSprites)
            {
                var n = s.name.ToLower();
                if (spriteMuted == null && (n.Contains("muted") || n.Contains("mute"))) spriteMuted = s;
                if (spriteUnmuted == null && (n.Contains("sound") || n.Contains("unmuted") || n.Contains("speaker"))) spriteUnmuted = s;
                if (spriteMuted != null && spriteUnmuted != null) break;
            }
        }
    }

public void ShowThinking()
{
    if (typingCoroutine != null)
        StopCoroutine(typingCoroutine);

    if (thinkingCoroutine != null)
        StopCoroutine(thinkingCoroutine);

    thinkingCoroutine = StartCoroutine(ThinkingLoop());
}

private IEnumerator ThinkingLoop()
{
    string[] mensajes = {
        "Un momento...",
        "Pensando...",
        "Procesando...",
        "Dame un segundo..."
    };

    int index = 0;

    while (true)
    {
        textUI.text = mensajes[index];
        index = (index + 1) % mensajes.Length;

        LayoutRebuilder.ForceRebuildLayoutImmediate(textUI.rectTransform);
        ScrollToTopImmediate();

        yield return new WaitForSeconds(6.0f);
    }
}

    public void StopThinking()
    {
        if (thinkingCoroutine != null)
            StopCoroutine(thinkingCoroutine);
        thinkingCoroutine = null;
    }

    /// <summary>
    /// Establece texto inmediato (sin tecleado ni bucles) y detiene cualquier 'thinking'.
    /// </summary>
    public void SetImmediateText(string message)
    {
        if (textUI == null) return;

        // Detener coroutines existentes
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        if (thinkingCoroutine != null)
        {
            StopCoroutine(thinkingCoroutine);
            thinkingCoroutine = null;
        }

        textUI.text = message ?? "";
        LayoutRebuilder.ForceRebuildLayoutImmediate(textUI.rectTransform);
        UpdateScrollLimits();
        ScrollToTopImmediate();
    }

    /// <summary>
    /// Muestra u oculta un icono de silenciado/sonido en la esquina inferior derecha
    /// del área de texto sin modificar el texto principal.
    /// </summary>
    public void ShowMuteIcon(bool show, bool muted)
    {
        if (muteIcon == null) return;

        // Asignar sprite según estado
        if (muted && spriteMuted != null)
            muteIcon.sprite = spriteMuted;
        else if (!muted && spriteUnmuted != null)
            muteIcon.sprite = spriteUnmuted;

        RectTransform iconRect = muteIcon.rectTransform;

        if (placeIconAsBubbleChild)
        {
            // Parentear al GameObject del bocadillo para que siga la posición/rotación del mismo
            RectTransform bubbleRect = this.GetComponent<RectTransform>();
            iconRect.SetParent(this.transform, worldPositionStays: false);

            // Anclar el icono a la esquina inferior derecha del bocadillo y usar sizeDelta en píxeles.
            iconRect.anchorMin = new Vector2(1f, 0f);
            iconRect.anchorMax = new Vector2(1f, 0f);
            iconRect.pivot = new Vector2(1f, 0f);
            iconRect.sizeDelta = muteIconSize;

            // Compensación por la escala del padre para que el icono tenga un tamaño legible en world-space
            Vector3 parentScale = this.transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(parentScale.x), Mathf.Abs(parentScale.y), Mathf.Abs(parentScale.z), 1f);
            float compensation = (1f / maxScale) * muteIconWorldScale * muteIconScaleMultiplier;
            iconRect.localScale = Vector3.one * compensation;

            // Añadir un Canvas con overrideSorting PRIMERO para forzar que el icono se dibuje encima
            Canvas iconCanvas = muteIcon.GetComponent<Canvas>();
            if (iconCanvas == null)
            {
                iconCanvas = muteIcon.gameObject.AddComponent<Canvas>();
            }
            iconCanvas.overrideSorting = true;
            iconCanvas.sortingOrder = 32767; // valor MÁXIMO posible (short.MaxValue)
            
            // Forzar el material a ser UI/Default para evitar problemas de z-buffer
            if (muteIcon.material == null || muteIcon.material.name != "UI/Default")
            {
                muteIcon.material = null; // Fuerza uso del material por defecto de UI
            }

            // Opcional: prevenir que el icono capture eventos si no se desea
            var gr = muteIcon.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (gr != null) DestroyImmediate(gr);

            // Posición: manual o offset en píxeles desde la esquina inferior derecha
            if (useManualLocalPosition)
            {
                // Aplicar manualLocalPosition pero con un pequeño offset en Z para asegurarnos
                // de que el icono quede delante del panel (puedes ajustar manualLocalZOffset en el Inspector)
                Vector3 mp = manualLocalPosition;
                mp.z = mp.z + manualLocalZOffset;
                iconRect.localPosition = mp;
                iconRect.localEulerAngles = manualLocalEuler;
            }
            else
            {
                iconRect.anchoredPosition = muteIconOffset;
                iconRect.localEulerAngles = manualLocalEuler;
            }

            // Mostrar/ocultar y asegurar que está al frente
            muteIcon.gameObject.SetActive(show);
            // Intentar asegurar orden de render por jerarquía
            muteIcon.transform.SetAsLastSibling();

            // Ajuste pequeño en Z para evitar quedar 'detrás' en canvases world-space
            // NOTA: no sobrescribimos la posición manual si el usuario pidió posición manual.
            if (!useManualLocalPosition)
            {
                Vector3 lp = iconRect.localPosition;
                iconRect.localPosition = new Vector3(lp.x, lp.y, -0.001f);
            }
        }
        else
        {
            // Comportamiento previo: anclar al viewport UI y compensar por su lossyscale
            RectTransform viewport = (RectTransform)textContainer.parent;
            iconRect.SetParent(viewport, worldPositionStays: false);
            iconRect.anchorMin = new Vector2(1f, 0f);
            iconRect.anchorMax = new Vector2(1f, 0f);
            iconRect.pivot = new Vector2(1f, 0f);

            Vector3 lossy = viewport.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));
            if (maxScale <= 0f) maxScale = 1f;
            float compensation = (1f / maxScale) * muteIconWorldScale;
            Vector2 compensatedSize = muteIconSize * compensation;
            iconRect.sizeDelta = compensatedSize;
            Vector2 compensatedOffset = muteIconOffset * compensation;
            iconRect.anchoredPosition = compensatedOffset;
            iconRect.localRotation = Quaternion.identity;
            muteIcon.gameObject.SetActive(show);
            muteIcon.transform.SetAsLastSibling();
        }
    }


}
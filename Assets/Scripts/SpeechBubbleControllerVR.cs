using System;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using System.Text.RegularExpressions;
using System.Collections;
using UnityEngine.UI;

public class SpeechBubbleControllerVR : MonoBehaviour, IPointerClickHandler
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

    [Tooltip("Factor multiplicador para compensar la escala del canvas/world-space (ajusta si el icono queda muy pequeño)")]
    public float muteIconWorldScale = 1f;
    
    [Header("Debug")]
    [Tooltip("Forzar visibilidad/escala del icono en Editor/Play para depuración")]
    public bool debugForceVisible = false;
    [Tooltip("Multiplicador usado cuando debugForceVisible para compensar canvases muy pequeños (ej. canvas scale 0.001)")]
    public float debugForceScaleMultiplier = 1000f;
    [Tooltip("Tamaño en píxeles a forzar cuando debugForceVisible (>0). Si (0,0) se usará muteIconSize.")]
    public Vector2 debugForcedSize = new Vector2(80f, 80f);
    
        [Header("Override world placement (opcional)")]
        [Tooltip("Si está activo, el icono se colocará exactamente en la posición/rotación/escala world especificada")]
        public bool useIconWorldOverride = false;
        public Vector3 iconWorldPosition = Vector3.zero;
        public Vector3 iconWorldEuler = Vector3.zero;
        public Vector3 iconWorldScale = Vector3.one;

        [Header("Local offset placement (opcional)")]
        [Tooltip("Si está activo, posiciona el icono en el offset local respecto al viewport (en unidades del transform del viewport)")]
        public bool useLocalOffset = true;
        [Tooltip("Offset local respecto al RectTransform parent (ej: Vector3(-0.07,-0.183,0.046))")]
        public Vector3 iconLocalOffset = new Vector3(-0.07f, -0.183f, 0.046f);

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

        // Procesar URLs y convertirlas en links TMP
        string processed = ConvertUrlsToLinks(message);

        typingCoroutine = StartCoroutine(TypeText(processed));
    }

    private IEnumerator TypeText(string message)
    {
        textUI.text = "";
        // forzamos pequeño delay para que layouts se actualicen correctamente
        yield return null;

        // Si el mensaje contiene tags/link (<>), evitar mostrar las etiquetas como texto
        // y establecerlo de golpe (el efecto de tecleo no es compatible fácilmente con tags)
        if (message.Contains("<") && message.Contains(">"))
        {
            textUI.text = message;
            // pequeño frame para que TMP procese tags
            yield return null;
            UpdateScrollLimits();
            ScrollToBottomImmediate();
        }
        else
        {
            foreach (char c in message)
            {
                textUI.text += c;
                yield return new WaitForSeconds(typingSpeed);

                // autoscroll hacia abajo mientras escribe
                UpdateScrollLimits();
                ScrollToBottomImmediate();
            }
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
            // Si ya existe, asegúrate de que esté como hijo del viewport para posicionarlo bien
            RectTransform viewport = (RectTransform)textContainer.parent;
            // Colocar en un Canvas hijo con overrideSorting para evitar que quede detrás de otros elementos world-space
            GameObject canvasHolder = EnsureMuteIconCanvas(viewport);
            muteIcon.rectTransform.SetParent(canvasHolder.transform, worldPositionStays: false);
            // Mostrar por defecto como 'unmuted' si hay sprite.
            if (spriteUnmuted != null)
            {
                muteIcon.sprite = spriteUnmuted;
            }
            muteIcon.gameObject.SetActive(true);
            // Forzar visualización inicial en modo debug
            if (debugForceVisible)
            {
                // Usamos ShowMuteIcon para aplicar las reglas y el ApplyDebugVisibility
                ShowMuteIcon(true, false);
            }
        }
        else
        {
            // si no hay icono asignado, intentamos crear uno dinámicamente (si hay sprites)
            if (spriteMuted != null || spriteUnmuted != null)
            {
                RectTransform viewport = (RectTransform)textContainer.parent;
                GameObject go = new GameObject("MuteIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                GameObject canvasHolder = EnsureMuteIconCanvas(viewport);
                go.transform.SetParent(canvasHolder.transform, false);
                Image img = go.GetComponent<Image>();
                img.raycastTarget = false;
                muteIcon = img;
                // Mostrar por defecto
                if (spriteUnmuted != null)
                    muteIcon.sprite = spriteUnmuted;
                muteIcon.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning("SpeechBubbleControllerVR: no hay sprites asignados para spriteMuted ni spriteUnmuted. Asigna los sprites en el inspector o arrastra las imágenes al campo correspondiente.");
            }
        }

        // Asegurarse de que el TextMeshPro pueda recibir eventos de puntero
        if (textUI != null)
        {
            textUI.raycastTarget = true;
        }
    }

    // Crea (o devuelve) un GameObject Canvas hijo usado exclusivamente para el icono de mute
    private GameObject EnsureMuteIconCanvas(RectTransform parent)
    {
        // Buscar un hijo existente
        Transform existing = parent.Find("MuteIconCanvas");
        if (existing != null)
            return existing.gameObject;

        GameObject canvasGO = new GameObject("MuteIconCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(parent, false);
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; // mantener coherencia con el canvas del viewport
        canvas.overrideSorting = true;
        canvas.sortingOrder = 999; // por encima

        // Ajustar rect transform para coincidir con viewport
        RectTransform cr = canvasGO.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0f, 0f);
        cr.anchorMax = new Vector2(1f, 1f);
        cr.sizeDelta = Vector2.zero;
        cr.anchoredPosition = Vector2.zero;

        // Desactivar raycast si no deseamos que el canvas intercepte eventos
        GraphicRaycaster gr = canvasGO.GetComponent<GraphicRaycaster>();
        gr.enabled = false;

        // Añadir (si no existe) un BoxCollider para permitir interacciones físicas tipo poke (Real Hands / Building Blocks)
        // Esto facilita que los dedos con colliders/trigger detecten la superficie del canvas y podamos
        // traducir ese punto a pantalla para que TMP_TextUtilities detecte enlaces.
        BoxCollider bc = canvasGO.GetComponent<BoxCollider>();
        if (bc == null)
        {
            bc = canvasGO.AddComponent<BoxCollider>();
            RectTransform rt = canvasGO.GetComponent<RectTransform>();
            // tamaño en unidades locales aproximado (anchura x altura) y pequeño grosor en Z
            Vector2 size = rt.rect.size;
            Vector3 lossy = rt.lossyScale;
            float sx = Mathf.Abs(size.x * (lossy.x == 0f ? 1f : lossy.x));
            float sy = Mathf.Abs(size.y * (lossy.y == 0f ? 1f : lossy.y));
            bc.size = new Vector3(sx, sy, 0.01f);
            bc.center = Vector3.zero;
            bc.isTrigger = true;
        }

        return canvasGO;
    }

    /// <summary>
    /// Convierte URLs detectadas en el texto a etiquetas <link> para TMP y las estiliza.
    /// Ej: "Visita uca.es" -> "Visita <link=\"http://uca.es\"><color=#0000EE><u>uca.es</u></color></link>"
    /// </summary>
    private string ConvertUrlsToLinks(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Simple regex para detectar urls (con o sin esquema)
        string pattern = @"(?i)\b((?:https?:\/\/)?(?:www\.)?[a-z0-9\-]+(?:\.[a-z0-9\-]+)+(?:\/[\w\-\.@?^=%&:/~+#]*)?)";
        return Regex.Replace(input, pattern, new MatchEvaluator((m) =>
        {
            string url = m.Groups[1].Value;
            string href = url;
            // Asegurar esquema
            if (!href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                href = "http://" + href;

            // Escapar comillas no necesarias en TMP link id
            string safeHref = href.Replace("\"", "");

            // Estilo: azul y subrayado
            string display = m.Groups[1].Value;
            return $"<link=\"{safeHref}\"><color=#0000EE><u>{display}</u></color></link>";
        }));
    }

    /// <summary>
    /// Maneja clicks/taps sobre el TextMeshPro y abre la URL si se hace click en un link.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (textUI == null) return;

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(textUI, eventData.position, eventData.pressEventCamera);
        if (linkIndex != -1)
        {
            TMP_LinkInfo linkInfo = textUI.textInfo.linkInfo[linkIndex];
            string linkId = linkInfo.GetLinkID();
            if (!string.IsNullOrEmpty(linkId))
            {
                Debug.Log("SpeechBubbleControllerVR: link clicked -> " + linkId);
                // Abrir en navegador (en Quest esto abrirá el navegador nativo/integrado)
                string href = linkId;
                if (!href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    href = "http://" + href;
                Application.OpenURL(href);
            }
        }
    }

    /// <summary>
    /// Intenta abrir un link presente en el TextMeshPro si se hace click en el punto world-space dado.
    /// Útil para interacciones por raycast VR: pasar el punto de impacto y la cámara que renderiza la UI.
    /// Devuelve true si se abrió un link.
    /// </summary>
    public bool TryOpenLinkAtWorldPoint(Vector3 worldPoint, Camera eventCamera)
    {
        if (textUI == null) return false;

        // Convertir el punto world a pantalla respecto a la cámara de eventos
        Vector3 screenPoint = eventCamera != null ? eventCamera.WorldToScreenPoint(worldPoint) : Camera.main.WorldToScreenPoint(worldPoint);

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(textUI, screenPoint, eventCamera ?? Camera.main);
        if (linkIndex != -1)
        {
            TMP_LinkInfo linkInfo = textUI.textInfo.linkInfo[linkIndex];
            string linkId = linkInfo.GetLinkID();
            if (!string.IsNullOrEmpty(linkId))
            {
                string href = linkId;
                if (!href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    href = "http://" + href;
                Debug.Log("SpeechBubbleControllerVR: VR link clicked -> " + href);
                Application.OpenURL(href);
                return true;
            }
        }

        return false;
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
        if (muted)
        {
            if (spriteMuted != null)
                muteIcon.sprite = spriteMuted;
            else if (spriteUnmuted != null)
            {
                // fallback
                Debug.LogWarning("SpeechBubbleControllerVR: spriteMuted no asignado, usando spriteUnmuted como fallback.");
                muteIcon.sprite = spriteUnmuted;
            }
            else
            {
                Debug.LogWarning("SpeechBubbleControllerVR: ninguno de los sprites de mute/unmute está asignado.");
            }
        }
        else
        {
            if (spriteUnmuted != null)
                muteIcon.sprite = spriteUnmuted;
            else if (spriteMuted != null)
            {
                Debug.LogWarning("SpeechBubbleControllerVR: spriteUnmuted no asignado, usando spriteMuted como fallback.");
                muteIcon.sprite = spriteMuted;
            }
            else
            {
                Debug.LogWarning("SpeechBubbleControllerVR: ninguno de los sprites de mute/unmute está asignado.");
            }
        }

    RectTransform iconRect = muteIcon.rectTransform;
    RectTransform viewport = (RectTransform)textContainer.parent;

    // Asegurarnos de que el icono sea hijo del Canvas overlay creado (MuteIconCanvas)
    GameObject canvasHolder = EnsureMuteIconCanvas(viewport);
    iconRect.SetParent(canvasHolder.transform, worldPositionStays: false);
    iconRect.anchorMin = new Vector2(1f, 0f);
    iconRect.anchorMax = new Vector2(1f, 0f);
    iconRect.pivot = new Vector2(1f, 0f);

        // Ajustar tamaño en px teniendo en cuenta la escala world-space del viewport
        // Si el chat está escalado en el mundo (por ejemplo scale = 0.0015...), necesitamos compensar
        Vector3 lossy = viewport.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));
        if (maxScale <= 0f) maxScale = 1f;

        float compensation = (1f / maxScale) * muteIconWorldScale;
        Vector2 compensatedSize = muteIconSize * compensation;
        iconRect.sizeDelta = compensatedSize;

        // Posicionar en esquina inferior derecha con offset (compensar offset también)
        Vector2 compensatedOffset = muteIconOffset * compensation;
        iconRect.anchoredPosition = compensatedOffset;

        // Forzar rotación local cero para evitar heredar rotaciones raras; mantener orientación del viewport
        iconRect.localRotation = Quaternion.identity;

        // Si el usuario quiere un placement world-space explícito, aplicarlo
        if (useIconWorldOverride)
        {
            // Aplicar transform world directamente sobre el transform del Image
            muteIcon.transform.position = iconWorldPosition;
            muteIcon.transform.rotation = Quaternion.Euler(iconWorldEuler);
            muteIcon.transform.localScale = iconWorldScale;
            // Ajustar sizeDelta en caso de que queramos un tamaño más controlado
            iconRect.sizeDelta = muteIconSize;
            return;
        }

        // Si el usuario pidió posicionamiento relativo local respecto al viewport, aplicarlo
        if (useLocalOffset)
        {
            // iconLocalOffset está en coordenadas del transform del viewport
            Vector3 worldPos = viewport.TransformPoint(iconLocalOffset);
            // Parentear al canvas overlay y establecer posición/rotación/scale
            iconRect.SetParent(canvasHolder.transform, worldPositionStays: true);
            muteIcon.transform.position = worldPos;
            muteIcon.transform.rotation = viewport.rotation;
            // Escala: reducir con respecto a la escala del viewport para que sea más pequeño que el chat
            Vector3 viewportLossy = viewport.lossyScale;
            muteIcon.transform.localScale = new Vector3(viewportLossy.x * 0.5f * muteIconWorldScale, viewportLossy.y * 0.5f * muteIconWorldScale, viewportLossy.z * 0.5f * muteIconWorldScale);
            // Ajustar tamaño en px relativo
            iconRect.sizeDelta = muteIconSize * 0.6f;
            return;
        }

        // Mostrar/ocultar y forzar orden de render por encima
        muteIcon.gameObject.SetActive(true); // siempre visible; el argumento 'show' controla si lo mostramos como visible o no semánticamente
        // Si el caller quiere ocultarlo por completo, respetarlo
        if (!show)
            muteIcon.gameObject.SetActive(false);

        // Si estamos en modo debug forzamos tamaño/alpha/escala para garantizar que se vea
        ApplyDebugVisibility(iconRect);

        // Asegurarnos que el icono esté por encima del texto en el canvas overlay
        muteIcon.transform.SetAsLastSibling();
    }

    // Forzar visibilidad/escala/alpha en modo debug para ayudar a localizar el icono
    private void ApplyDebugVisibility(RectTransform iconRect)
    {
        if (!debugForceVisible || iconRect == null || muteIcon == null) return;

        // Forzar tamaño razonable
        Vector2 forced = (debugForcedSize.sqrMagnitude > 0.0001f) ? debugForcedSize : muteIconSize;
        iconRect.sizeDelta = forced;

        // Forzar escala y color
        iconRect.localScale = Vector3.one;
        Image img = muteIcon.GetComponent<Image>();
        if (img != null)
        {
            img.color = Color.white;
            img.enabled = true;
        }

        // Asegurar que el icon se coloque visible cerca de la esquina inferior derecha
        try
        {
            iconRect.anchorMin = new Vector2(1f, 0f);
            iconRect.anchorMax = new Vector2(1f, 0f);
            iconRect.pivot = new Vector2(1f, 0f);
            iconRect.anchoredPosition = new Vector2(-20f, 8f);
        }
        catch { }
    }

    // Métodos de depuración rápidos para el Editor
    [ContextMenu("Debug: Show Unmuted Icon")]
    public void Debug_ShowUnmuted()
    {
        ShowMuteIcon(true, false);
    }

    [ContextMenu("Debug: Show Muted Icon")]
    public void Debug_ShowMuted()
    {
        ShowMuteIcon(true, true);
    }

    [ContextMenu("Debug: Hide Icon")]
    public void Debug_HideIcon()
    {
        ShowMuteIcon(false, false);
    }


}
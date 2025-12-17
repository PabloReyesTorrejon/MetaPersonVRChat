using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Muestra un Canvas con la descripción de los controles cuando se pulsa el botón "ButtonControles".
public class ControlsPanel : MonoBehaviour
{
    [TextArea(4,10)]
    public string controlsDescription = "Controles:\n• Botón A: Grabar (mantener para grabar, soltar para enviar)\n• Joystick derecho: desplazamiento vertical en el chat\n• Gatillo/Trigger: interactuar/seleccionar\n• Botón B: acción secundaria / volver\n\nAjusta estos controles según la configuración de tu dispositivo.";

    // Si quieres usar un prefab de Canvas, asígnalo aquí; si no, se creará programáticamente.
    public GameObject controlsCanvasPrefab;

    // Nombre del botón que disparará la apertura (por defecto 'ButtonControles')
    public string buttonName = "ButtonControles";

    // Offset relativo para colocar el nuevo canvas al lado del principal (en píxeles)
    public Vector2 canvasOffset = new Vector2(300f, 0f);

    [Header("Panel rect (opcional)")]
    [Tooltip("Si está activado, se usarán las dimensiones y posición personalizadas para el panel en lugar de los valores por defecto.")]
    public bool useCustomPanelRect = false;
    public float panelWidth = 420f;
    public float panelHeight = 260f;
    [Tooltip("Posición anclada X (anchoredPosition.x) del panel en relación al centro del canvas")]
    public float panelPosX = 0f;
    [Tooltip("Posición anclada Y (anchoredPosition.y) del panel en relación al centro del canvas")]
    public float panelPosY = 0f;
    [Tooltip("Posición local Z del panel (depth). Útil para ajustar orden en espacio 3D o Canvas en World Space.")]
    public float panelPosZ = 0f;

    GameObject activeCanvas;

    void Awake()
    {
        // Intentar localizar el botón automáticamente
        var btnObj = GameObject.Find(buttonName);
        if (btnObj != null)
        {
            var btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(OnButtonControlesClicked);
                return;
            }
        }

        // Si no encontramos el botón por nombre, intentamos buscar un componente en el mismo GameObject
        var localBtn = GetComponent<Button>();
        if (localBtn != null)
            localBtn.onClick.AddListener(OnButtonControlesClicked);
    }

    public void OnButtonControlesClicked()
    {
        if (activeCanvas != null)
        {
            // si ya está visible, la desactivamos
            activeCanvas.SetActive(!activeCanvas.activeSelf);
            return;
        }

        if (controlsCanvasPrefab != null)
        {
            // Instantiate prefab and keep reference
            var mainCanvas = FindObjectOfType<Canvas>();
            if (mainCanvas != null)
                activeCanvas = Instantiate(controlsCanvasPrefab, mainCanvas.transform.parent);
            else
                activeCanvas = Instantiate(controlsCanvasPrefab);
            return;
        }

        CreateControlsCanvas();
    }

    void CreateControlsCanvas()
    {
        // Find an existing Canvas to use as reference
        var mainCanvas = FindObjectOfType<Canvas>();

        // Create new Canvas GameObject
        GameObject canvasGO = new GameObject("ControlsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = (mainCanvas != null) ? mainCanvas.renderMode : RenderMode.ScreenSpaceOverlay;
        canvasGO.layer = (mainCanvas != null) ? mainCanvas.gameObject.layer : 0;

        // Parent: if there is a main canvas, place as sibling under same parent for easier positioning
        if (mainCanvas != null && mainCanvas.transform.parent != null)
            canvasGO.transform.SetParent(mainCanvas.transform.parent, false);

        // If we have a main canvas, mimic its RectTransform and CanvasScaler so the new canvas
        // has the same size/scale as the scene's primary canvas (e.g. SelectionScene)
        var canvasRect = canvasGO.GetComponent<RectTransform>();
        if (mainCanvas != null)
        {
            var mainRect = mainCanvas.GetComponent<RectTransform>();
            if (mainRect != null)
            {
                canvasRect.anchorMin = mainRect.anchorMin;
                canvasRect.anchorMax = mainRect.anchorMax;
                canvasRect.pivot = mainRect.pivot;
                canvasRect.anchoredPosition = mainRect.anchoredPosition;
                canvasRect.sizeDelta = mainRect.sizeDelta;
                canvasGO.transform.localScale = mainCanvas.transform.localScale;
            }

            // copy CanvasScaler settings if present
            var mainScaler = mainCanvas.GetComponent<CanvasScaler>();
            var thisScaler = canvasGO.GetComponent<CanvasScaler>();
            if (mainScaler != null && thisScaler != null)
            {
                thisScaler.uiScaleMode = mainScaler.uiScaleMode;
                thisScaler.referenceResolution = mainScaler.referenceResolution;
                thisScaler.screenMatchMode = mainScaler.screenMatchMode;
                thisScaler.matchWidthOrHeight = mainScaler.matchWidthOrHeight;
                thisScaler.referencePixelsPerUnit = mainScaler.referencePixelsPerUnit;
            }

            // Put the controls canvas above the main canvas in sorting order
            try { canvas.sortingOrder = mainCanvas.sortingOrder + 1; } catch { }
        }

        // Create panel
        GameObject panel = new GameObject("Panel", typeof(Image));
        panel.transform.SetParent(canvasGO.transform, false);
        var img = panel.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.75f);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);

        // Apply custom size/position if requested, otherwise use defaults
        if (useCustomPanelRect)
        {
            panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
            panelRect.anchoredPosition = new Vector2(panelPosX + canvasOffset.x, panelPosY + canvasOffset.y);
            // set local z
            panelRect.localPosition = new Vector3(panelRect.localPosition.x, panelRect.localPosition.y, panelPosZ);
        }
        else
        {
            panelRect.sizeDelta = new Vector2(420f, 260f);
            panelRect.anchoredPosition = canvasOffset;
        }

        // Title (optional)
        GameObject titleGO = new GameObject("Title", typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(panel.transform, false);
        var title = titleGO.GetComponent<TextMeshProUGUI>();
        title.text = "Controles";
        title.fontSize = 26;
        title.alignment = TextAlignmentOptions.Center;
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -12f);
        titleRect.sizeDelta = new Vector2(0f, 36f);

        // Text body
        GameObject textGO = new GameObject("ControlsText", typeof(TextMeshProUGUI));
        textGO.transform.SetParent(panel.transform, false);
        var txt = textGO.GetComponent<TextMeshProUGUI>();
        txt.text = controlsDescription;
        txt.fontSize = 18;
        txt.alignment = TextAlignmentOptions.TopLeft;
        txt.enableWordWrapping = true;
        var txtRect = txt.GetComponent<RectTransform>();
        txtRect.anchorMin = new Vector2(0f, 0f);
        txtRect.anchorMax = new Vector2(1f, 1f);
        txtRect.pivot = new Vector2(0.5f, 0.5f);
        txtRect.anchoredPosition = new Vector2(0f, -10f);
        txtRect.offsetMin = new Vector2(12f, 12f);
        txtRect.offsetMax = new Vector2(-12f, -56f);

        // Close button
        GameObject closeBtnGO = new GameObject("CloseButton", typeof(Image), typeof(Button));
        closeBtnGO.transform.SetParent(panel.transform, false);
        var closeRect = closeBtnGO.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-8f, -8f);
        closeRect.sizeDelta = new Vector2(32f, 32f);
        var closeImg = closeBtnGO.GetComponent<Image>();
        closeImg.color = new Color(1f, 0.2f, 0.2f, 1f);
        var closeBtn = closeBtnGO.GetComponent<Button>();
        closeBtn.onClick.AddListener(() => { Destroy(canvasGO); activeCanvas = null; });

        // Close button label (X)
        GameObject xGO = new GameObject("X", typeof(TextMeshProUGUI));
        xGO.transform.SetParent(closeBtnGO.transform, false);
        var xText = xGO.GetComponent<TextMeshProUGUI>();
        xText.text = "X";
        xText.alignment = TextAlignmentOptions.Center;
        xText.fontSize = 18;
        var xRect = xText.GetComponent<RectTransform>();
        xRect.anchorMin = new Vector2(0f, 0f);
        xRect.anchorMax = new Vector2(1f, 1f);
        xRect.sizeDelta = Vector2.zero;

        activeCanvas = canvasGO;
    }
}

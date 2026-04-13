using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class SimulatorStartMenu : MonoBehaviour
{
    private static SimulatorStartMenu instance;
    private static Font builtInFont;
    private static bool loggedStartedState;

    private const float MenuDistance = 1.35f;

    private Canvas desktopCanvas;
    private GameObject desktopPanel;

    private GameObject xrMenuRoot;
    private GameObject xrPanelObject;
    private GameObject xrStartButtonObject;
    private GameObject xrExitButtonObject;
    private GameObject xrTitleObject;
    private GameObject xrStartLabelObject;
    private GameObject xrExitLabelObject;

    private Transform menuAnchor;
    private bool hasStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateMenu()
    {
        EnsureCreated();
    }

    public static void EnsureCreated()
    {
        if (instance != null)
        {
            instance.ShowMenu();
            return;
        }

        GameObject root = new GameObject("SimulatorStartMenu");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<SimulatorStartMenu>();
        instance.ShowMenu();
    }

    public static bool HasStarted()
    {
        return instance != null && instance.hasStarted;
    }

    private void ShowMenu()
    {
        if (hasStarted)
        {
            return;
        }

        EnsureEventSystem();
        EnsureDesktopCanvas();
        EnsureXRMenu();

        if (desktopPanel != null)
        {
            desktopPanel.SetActive(true);
        }

        if (xrMenuRoot != null)
        {
            xrMenuRoot.SetActive(true);
        }

        UpdateMenuPlacement();

        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void StartSimulation()
    {
        hasStarted = true;
        loggedStartedState = false;

        if (desktopCanvas != null)
        {
            desktopCanvas.gameObject.SetActive(false);
        }

        if (xrMenuRoot != null)
        {
            xrMenuRoot.SetActive(false);
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private void ExitSimulation()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Update()
    {
        if (!hasStarted)
        {
            UpdateMenuPlacement();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    StartSimulation();
                    return;
                }

                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
                {
                    ExitSimulation();
                    return;
                }
            }
        }

        if (!loggedStartedState)
        {
            Debug.Log("[SimulatorStartMenu] Update running. hasStarted=" + hasStarted + ", timeScale=" + Time.timeScale);
            loggedStartedState = true;
        }
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        DontDestroyOnLoad(eventSystemObject);
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private void EnsureDesktopCanvas()
    {
        if (desktopCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("StartMenuCanvas");
        DontDestroyOnLoad(canvasObject);

        desktopCanvas = canvasObject.AddComponent<Canvas>();
        desktopCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        desktopCanvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        desktopPanel = CreateUIObject("Panel", canvasObject.transform);
        Image panelImage = desktopPanel.AddComponent<Image>();
        panelImage.color = Color.black;
        StretchToParent(desktopPanel.GetComponent<RectTransform>());

        GameObject titleObject = CreateUIObject("Title", desktopPanel.transform);
        Text titleText = titleObject.AddComponent<Text>();
        titleText.text = "Choose an option";
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.font = GetBuiltInFont();
        titleText.fontSize = 52;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = Color.white;

        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.2f, 0.62f);
        titleRect.anchorMax = new Vector2(0.8f, 0.8f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        CreateDesktopButton(
            "StartButton",
            desktopPanel.transform,
            "Start",
            new Color(0.18f, 0.62f, 0.24f, 1f),
            new Vector2(0.24f, 0.34f),
            new Vector2(0.46f, 0.48f),
            StartSimulation);

        CreateDesktopButton(
            "ExitButton",
            desktopPanel.transform,
            "Exit",
            new Color(0.65f, 0.18f, 0.18f, 1f),
            new Vector2(0.54f, 0.34f),
            new Vector2(0.76f, 0.48f),
            ExitSimulation);
    }

    private void CreateDesktopButton(
        string name,
        Transform parent,
        string label,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateUIObject(name, parent);
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = color;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = anchorMin;
        buttonRect.anchorMax = anchorMax;
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        GameObject labelObject = CreateUIObject(name + "Label", buttonObject.transform);
        Text buttonLabel = labelObject.AddComponent<Text>();
        buttonLabel.text = label;
        buttonLabel.alignment = TextAnchor.MiddleCenter;
        buttonLabel.font = GetBuiltInFont();
        buttonLabel.fontSize = 38;
        buttonLabel.fontStyle = FontStyle.Bold;
        buttonLabel.color = Color.white;
        StretchToParent(labelObject.GetComponent<RectTransform>());
    }

    private void EnsureXRMenu()
    {
        if (xrMenuRoot != null)
        {
            return;
        }

        xrMenuRoot = new GameObject("XRWorldMenu");
        DontDestroyOnLoad(xrMenuRoot);

        xrPanelObject = CreateQuad("XRPanel", xrMenuRoot.transform, Vector3.zero, new Vector3(1.35f, 0.82f, 1f), Color.black);
        xrStartButtonObject = CreateQuad("XRStartButton", xrMenuRoot.transform, new Vector3(-0.25f, -0.18f, -0.01f), new Vector3(0.38f, 0.14f, 1f), new Color(0.18f, 0.62f, 0.24f, 1f));
        xrExitButtonObject = CreateQuad("XRExitButton", xrMenuRoot.transform, new Vector3(0.25f, -0.18f, -0.01f), new Vector3(0.38f, 0.14f, 1f), new Color(0.65f, 0.18f, 0.18f, 1f));

        xrTitleObject = CreateTextQuad(
            "XRTitle",
            xrMenuRoot.transform,
            "CHOOSE AN OPTION",
            new Vector3(0f, 0.22f, -0.02f),
            new Vector3(1.1f, 0.12f, 1f));

        xrStartLabelObject = CreateTextQuad(
            "XRStartLabel",
            xrMenuRoot.transform,
            "START",
            new Vector3(-0.25f, -0.18f, -0.03f),
            new Vector3(0.26f, 0.08f, 1f));

        xrExitLabelObject = CreateTextQuad(
            "XRExitLabel",
            xrMenuRoot.transform,
            "EXIT",
            new Vector3(0.25f, -0.18f, -0.03f),
            new Vector3(0.2f, 0.08f, 1f));
    }

    private void UpdateMenuPlacement()
    {
        Transform anchor = FindMenuAnchor();
        if (anchor == null)
        {
            return;
        }

        if (menuAnchor != anchor)
        {
            menuAnchor = anchor;
            if (xrMenuRoot != null)
            {
                xrMenuRoot.transform.SetParent(menuAnchor, false);
            }
        }

        if (xrMenuRoot != null)
        {
            xrMenuRoot.transform.localPosition = new Vector3(0f, 0f, MenuDistance);
            xrMenuRoot.transform.localRotation = Quaternion.identity;
            xrMenuRoot.transform.localScale = Vector3.one;
        }
    }

    private Transform FindMenuAnchor()
    {
        Camera menuCamera = FindMenuCamera();
        if (menuCamera != null)
        {
            return menuCamera.transform;
        }

        GameObject centerEye = GameObject.Find("CenterEyeAnchor");
        if (centerEye != null)
        {
            return centerEye.transform;
        }

        GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig");
        if (rig != null)
        {
            return rig.transform;
        }

        return null;
    }

    private Camera FindMenuCamera()
    {
        GameObject centerEye = GameObject.Find("CenterEyeAnchor");
        if (centerEye != null)
        {
            Camera centerEyeCamera = centerEye.GetComponent<Camera>();
            if (centerEyeCamera != null)
            {
                return centerEyeCamera;
            }
        }

        if (Camera.main != null)
        {
            return Camera.main;
        }

        return FindFirstObjectByType<Camera>();
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        return obj;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static Font GetBuiltInFont()
    {
        if (builtInFont == null)
        {
            builtInFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return builtInFont;
    }

    private GameObject CreateQuad(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = localPosition;
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localScale = localScale;

        Material material = new Material(Shader.Find("Unlit/Color"));
        material.color = color;
        quad.GetComponent<MeshRenderer>().material = material;
        return quad;
    }

    private GameObject CreateTextQuad(string name, Transform parent, string content, Vector3 localPosition, Vector3 localScale)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = localPosition;
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localScale = localScale;

        Texture2D texture = BuildBlockTextTexture(content, Color.white, Color.clear, 10, 2);
        Material material = new Material(Shader.Find("Unlit/Transparent"));
        material.mainTexture = texture;
        material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        quad.GetComponent<MeshRenderer>().material = material;

        return quad;
    }

    private Texture2D BuildBlockTextTexture(string text, Color foreground, Color background, int pixelSize, int letterSpacing)
    {
        string upperText = text.ToUpperInvariant();
        int glyphWidth = 5;
        int glyphHeight = 7;
        int width = (upperText.Length * glyphWidth + Mathf.Max(0, upperText.Length - 1) * letterSpacing) * pixelSize;
        int height = glyphHeight * pixelSize;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = background;
        }

        for (int index = 0; index < upperText.Length; index++)
        {
            string[] glyph = GetGlyphRows(upperText[index]);
            int xOffset = index * (glyphWidth + letterSpacing) * pixelSize;

            for (int row = 0; row < glyphHeight; row++)
            {
                for (int col = 0; col < glyphWidth; col++)
                {
                    if (glyph[row][col] != '1')
                    {
                        continue;
                    }

                    for (int py = 0; py < pixelSize; py++)
                    {
                        for (int px = 0; px < pixelSize; px++)
                        {
                            int x = xOffset + col * pixelSize + px;
                            int y = height - 1 - (row * pixelSize + py);
                            pixels[y * width + x] = foreground;
                        }
                    }
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private string[] GetGlyphRows(char c)
    {
        switch (c)
        {
            case 'A': return new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" };
            case 'C': return new[] { "01111", "10000", "10000", "10000", "10000", "10000", "01111" };
            case 'E': return new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" };
            case 'H': return new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" };
            case 'I': return new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" };
            case 'N': return new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" };
            case 'O': return new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" };
            case 'P': return new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" };
            case 'R': return new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" };
            case 'S': return new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" };
            case 'T': return new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" };
            case 'U': return new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" };
            case 'X': return new[] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" };
            case ' ': return new[] { "00000", "00000", "00000", "00000", "00000", "00000", "00000" };
            default: return new[] { "11111", "00001", "00010", "00100", "00100", "00000", "00100" };
        }
    }
}

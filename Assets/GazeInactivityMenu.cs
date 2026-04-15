using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class GazeInactivityMenu : MonoBehaviour
{
    private static GazeInactivityMenu instance;
    private static Font builtInFont;

    private const float MenuDistance = 1.2f;
    private const float InactivitySeconds = 10f;
    private const float CreatureMenuOffsetX = 3.5f;
    private const float CreatureMenuHeight = 1.2f;

    private GazeTargetDetector detector;
    private Canvas desktopCanvas;
    private GameObject desktopPanel;
    private GameObject desktopCard;
    private GameObject xrMenuRoot;
    private Canvas xrCanvas;
    private float idleTimer;
    private bool menuOpen;
    private GameObject xrContinueButtonObject;
    private GameObject xrEndButtonObject;
    private bool loggedPlacementDetails;
    private OVRMenuRayDriver ovrMenuRayDriver;

    public static void EnsureCreated()
    {
        if (instance != null)
        {
            return;
        }

        GameObject root = new GameObject("GazeInactivityMenu");
        MarkRuntimeOnly(root);
        DontDestroyOnLoad(root);
        instance = root.AddComponent<GazeInactivityMenu>();
    }

    public static bool IsMenuOpen()
    {
        if (instance == null)
        {
            EnsureCreated();
            return false;
        }

        return instance.menuOpen;
    }

    private void Update()
    {
        if (!SimulatorStartMenu.HasStarted())
        {
            return;
        }

        if (detector == null)
        {
            detector = FindFirstObjectByType<GazeTargetDetector>();
            return;
        }

        if (menuOpen)
        {
            HandleKeyboard();
            UpdateXRPlacement();
            return;
        }

        if (detector.CurrentTarget != null)
        {
            idleTimer = 0f;
            return;
        }

        idleTimer += Time.unscaledDeltaTime;
        if (idleTimer >= InactivitySeconds)
        {
            ShowMenu();
        }
    }

    private void ShowMenu()
    {
        Debug.Log("[GazeInactivityMenu] Showing inactivity menu.");
        EnsureEventSystem();
        EnsureDesktopCanvas();
        EnsureXRMenu();

        menuOpen = true;
        idleTimer = 0f;
        loggedPlacementDetails = false;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        desktopCanvas.gameObject.SetActive(true);
        xrMenuRoot.SetActive(true);
        UpdateXRPlacement();

        if (ovrMenuRayDriver != null)
        {
            ovrMenuRayDriver.SetVisualTarget(xrMenuRoot.transform);
            ovrMenuRayDriver.SetXRMenuInputEnabled(true);
        }
    }

    private void ContinueGame()
    {
        Debug.Log("[GazeInactivityMenu] Continue selected. Closing inactivity menu.");
        menuOpen = false;
        idleTimer = 0f;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (desktopCanvas != null)
        {
            desktopCanvas.gameObject.SetActive(false);
        }

        if (xrMenuRoot != null)
        {
            xrMenuRoot.SetActive(false);
        }

        if (ovrMenuRayDriver != null)
        {
            ovrMenuRayDriver.SetVisualTarget(null);
            ovrMenuRayDriver.SetXRMenuInputEnabled(false);
        }

        Debug.Log("[GazeInactivityMenu] Continue complete. menuOpen=" + menuOpen + ", timeScale=" + Time.timeScale);
    }

    private void EndGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void HandleKeyboard()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            ContinueGame();
            return;
        }

        if (keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
        {
            EndGame();
        }
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            EventSystem existingSystem = FindFirstObjectByType<EventSystem>();
            EnsureOVRMenuRayDriver(existingSystem.gameObject);
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        MarkRuntimeOnly(eventSystemObject);
        DontDestroyOnLoad(eventSystemObject);
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
        EnsureOVRMenuRayDriver(eventSystemObject);
    }

    private void EnsureDesktopCanvas()
    {
        if (desktopCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("InactivityMenuCanvas");
        MarkRuntimeOnly(canvasObject);
        DontDestroyOnLoad(canvasObject);

        desktopCanvas = canvasObject.AddComponent<Canvas>();
        desktopCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        desktopCanvas.sortingOrder = short.MaxValue - 1;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        desktopPanel = CreateUIObject("Panel", canvasObject.transform);
        Image panelImage = desktopPanel.AddComponent<Image>();
        panelImage.color = new Color(0.02f, 0.04f, 0.06f, 0.82f);
        StretchToParent(desktopPanel.GetComponent<RectTransform>());

        desktopCard = CreateUIObject("Card", desktopPanel.transform);
        Image cardImage = desktopCard.AddComponent<Image>();
        cardImage.color = new Color(0.05f, 0.08f, 0.11f, 0.96f);

        RectTransform cardRect = desktopCard.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.18f, 0.18f);
        cardRect.anchorMax = new Vector2(0.82f, 0.82f);
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;

        GameObject accentObject = CreateUIObject("Accent", desktopCard.transform);
        Image accentImage = accentObject.AddComponent<Image>();
        accentImage.color = new Color(0.38f, 0.8f, 0.94f, 1f);

        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0.08f, 0.8f);
        accentRect.anchorMax = new Vector2(0.92f, 0.83f);
        accentRect.offsetMin = Vector2.zero;
        accentRect.offsetMax = Vector2.zero;

        GameObject titleObject = CreateUIObject("Title", desktopCard.transform);
        Text titleText = titleObject.AddComponent<Text>();
        titleText.text = "Need a moment?";
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.font = GetBuiltInFont();
        titleText.fontSize = 56;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = Color.white;

        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.12f, 0.56f);
        titleRect.anchorMax = new Vector2(0.88f, 0.74f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        GameObject subtitleObject = CreateUIObject("Subtitle", desktopCard.transform);
        Text subtitleText = subtitleObject.AddComponent<Text>();
        subtitleText.text = "We have not detected gaze on any target for 10 seconds.";
        subtitleText.alignment = TextAnchor.MiddleCenter;
        subtitleText.font = GetBuiltInFont();
        subtitleText.fontSize = 28;
        subtitleText.color = new Color(0.8f, 0.87f, 0.92f, 1f);

        RectTransform subtitleRect = subtitleObject.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0.12f, 0.42f);
        subtitleRect.anchorMax = new Vector2(0.88f, 0.58f);
        subtitleRect.offsetMin = Vector2.zero;
        subtitleRect.offsetMax = Vector2.zero;

        CreateDesktopButton(
            "ContinueButton",
            desktopCard.transform,
            "Continue",
            new Color(0.15f, 0.58f, 0.66f, 1f),
            new Vector2(0.14f, 0.16f),
            new Vector2(0.46f, 0.32f),
            ContinueGame);

        CreateDesktopButton(
            "EndButton",
            desktopCard.transform,
            "End Session",
            new Color(0.72f, 0.29f, 0.23f, 1f),
            new Vector2(0.54f, 0.16f),
            new Vector2(0.86f, 0.32f),
            EndGame);

        desktopCanvas.gameObject.SetActive(false);
    }

    private void EnsureXRMenu()
    {
        if (xrMenuRoot != null)
        {
            return;
        }

        xrMenuRoot = new GameObject("XRInactivityMenu");
        MarkRuntimeOnly(xrMenuRoot);
        DontDestroyOnLoad(xrMenuRoot);

        xrCanvas = xrMenuRoot.AddComponent<Canvas>();
        xrCanvas.renderMode = RenderMode.WorldSpace;
        xrCanvas.sortingOrder = short.MaxValue - 1;

        RectTransform xrCanvasRect = xrCanvas.GetComponent<RectTransform>();
        xrCanvasRect.sizeDelta = new Vector2(1040f, 640f);
        xrMenuRoot.transform.localScale = Vector3.one * 0.0015f;

        CanvasScaler xrScaler = xrMenuRoot.AddComponent<CanvasScaler>();
        xrScaler.dynamicPixelsPerUnit = 24f;

        xrMenuRoot.AddComponent<OVRRaycaster>();

        GameObject xrPanel = CreateUIObject("XRPanel", xrMenuRoot.transform);
        Image xrPanelImage = xrPanel.AddComponent<Image>();
        xrPanelImage.color = new Color(0.05f, 0.08f, 0.11f, 0.98f);
        StretchToParent(xrPanel.GetComponent<RectTransform>());

        GameObject accent = CreateUIObject("XRAccent", xrPanel.transform);
        Image accentImage = accent.AddComponent<Image>();
        accentImage.color = new Color(0.38f, 0.8f, 0.94f, 1f);

        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0.1f, 0.8f);
        accentRect.anchorMax = new Vector2(0.9f, 0.835f);
        accentRect.offsetMin = Vector2.zero;
        accentRect.offsetMax = Vector2.zero;

        GameObject title = CreateUIObject("XRTitle", xrPanel.transform);
        Text titleText = title.AddComponent<Text>();
        titleText.text = "Need a moment?";
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.font = GetBuiltInFont();
        titleText.fontSize = 54;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = Color.white;

        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.14f, 0.56f);
        titleRect.anchorMax = new Vector2(0.86f, 0.74f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        GameObject subtitle = CreateUIObject("XRSubtitle", xrPanel.transform);
        Text subtitleText = subtitle.AddComponent<Text>();
        subtitleText.text = "We have not detected gaze on any target for 10 seconds.";
        subtitleText.alignment = TextAnchor.MiddleCenter;
        subtitleText.font = GetBuiltInFont();
        subtitleText.fontSize = 28;
        subtitleText.color = new Color(0.8f, 0.87f, 0.92f, 1f);

        RectTransform subtitleRect = subtitle.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0.12f, 0.4f);
        subtitleRect.anchorMax = new Vector2(0.88f, 0.56f);
        subtitleRect.offsetMin = Vector2.zero;
        subtitleRect.offsetMax = Vector2.zero;

        xrContinueButtonObject = CreateXRButton(
            "XRContinueButton",
            xrPanel.transform,
            "Continue",
            new Color(0.15f, 0.58f, 0.66f, 1f),
            new Vector2(0.14f, 0.16f),
            new Vector2(0.46f, 0.32f),
            ContinueGame);

        xrEndButtonObject = CreateXRButton(
            "XREndButton",
            xrPanel.transform,
            "End Session",
            new Color(0.72f, 0.29f, 0.23f, 1f),
            new Vector2(0.54f, 0.16f),
            new Vector2(0.86f, 0.32f),
            EndGame);

        xrMenuRoot.SetActive(false);
    }

    private void UpdateXRPlacement()
    {
        if (xrMenuRoot == null)
        {
            return;
        }

        Transform anchor = FindMenuAnchor();
        if (anchor == null)
        {
            return;
        }

        Camera menuCamera = FindMenuCamera();
        Transform reference = FindCreatureReferenceTransform();
        if (reference == null)
        {
            reference = menuCamera != null ? menuCamera.transform : anchor;
        }

        Vector3 menuPosition = GetMenuWorldPosition(reference, menuCamera);
        Vector3 lookDirection = GetMenuLookDirection(menuPosition, reference, menuCamera);

        xrMenuRoot.transform.SetParent(null, true);
        xrMenuRoot.transform.position = menuPosition;
        xrMenuRoot.transform.rotation = Quaternion.LookRotation(lookDirection, reference.up);
        xrMenuRoot.transform.localScale = Vector3.one * 0.0015f;

        if (!loggedPlacementDetails)
        {
            string referenceName = reference != null ? reference.name : "null";
            string cameraName = menuCamera != null ? menuCamera.name : "null";
            Vector3 cameraPosition = menuCamera != null ? menuCamera.transform.position : Vector3.zero;
            Debug.Log("[GazeInactivityMenu] Placement details: reference=" + referenceName
                + ", referencePos=" + (reference != null ? reference.position.ToString() : "null")
                + ", camera=" + cameraName
                + ", cameraPos=" + cameraPosition
                + ", menuPos=" + menuPosition
                + ", menuForward=" + xrMenuRoot.transform.forward
                + ", creatureAnchorUsed=" + (FindCreatureReferenceTransform() != null));
            loggedPlacementDetails = true;
        }

        if (xrCanvas != null)
        {
            xrCanvas.worldCamera = FindMenuCamera();
        }
    }

    private Transform FindMenuAnchor()
    {
        Camera trackedCamera = FindTrackedMenuCamera();
        if (trackedCamera != null)
        {
            return trackedCamera.transform;
        }

        GameObject centerEye = GameObject.Find("CenterEyeAnchor");
        if (centerEye != null)
        {
            return centerEye.transform;
        }

        GameObject ovrRig = GameObject.Find("OVRCameraRig");
        if (ovrRig != null)
        {
            Transform centerEyeAnchor = FindChildRecursive(ovrRig.transform, "CenterEyeAnchor");
            if (centerEyeAnchor != null)
            {
                return centerEyeAnchor;
            }
        }

        GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig");
        if (rig != null)
        {
            Transform centerEyeAnchor = FindChildRecursive(rig.transform, "CenterEyeAnchor");
            if (centerEyeAnchor != null)
            {
                return centerEyeAnchor;
            }
        }

        Camera menuCamera = FindMenuCamera();
        return menuCamera != null ? menuCamera.transform : null;
    }

    private Camera FindMenuCamera()
    {
        Camera trackedCamera = FindTrackedMenuCamera();
        if (trackedCamera != null)
        {
            return trackedCamera;
        }

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

    private static Camera FindTrackedMenuCamera()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Camera fallbackCamera = null;

        foreach (Camera camera in cameras)
        {
            if (camera == null || !camera.enabled || !camera.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (fallbackCamera == null)
            {
                fallbackCamera = camera;
            }

            if (camera.stereoTargetEye != StereoTargetEyeMask.None)
            {
                return camera;
            }
        }

        return fallbackCamera;
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindChildRecursive(root.GetChild(i), targetName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static Transform FindCreatureReferenceTransform()
    {
        GazeDrivenCreatureController gazeController = FindFirstObjectByType<GazeDrivenCreatureController>();
        if (gazeController != null)
        {
            return gazeController.creatureRoot != null ? gazeController.creatureRoot : gazeController.transform;
        }

        ActionDrivenCreatureController actionController = FindFirstObjectByType<ActionDrivenCreatureController>();
        if (actionController != null)
        {
            return actionController.creatureRoot != null ? actionController.creatureRoot : actionController.transform;
        }

        GameObject tiger = GameObject.Find("Tiger");
        if (tiger != null)
        {
            return tiger.transform;
        }

        return null;
    }

    private static Vector3 GetMenuWorldPosition(Transform reference, Camera menuCamera)
    {
        if (reference == null)
        {
            return Vector3.zero;
        }

        if (reference.GetComponent<Camera>() != null)
        {
            return reference.position + reference.forward * MenuDistance;
        }

        Vector3 basePosition = reference.position + reference.right * CreatureMenuOffsetX;
        basePosition.y += CreatureMenuHeight;

        if (menuCamera != null)
        {
            basePosition.y = Mathf.Max(basePosition.y, menuCamera.transform.position.y);
        }

        return basePosition;
    }

    private static Vector3 GetMenuLookDirection(Vector3 menuPosition, Transform reference, Camera menuCamera)
    {
        Vector3 lookTarget = menuCamera != null ? menuCamera.transform.position : reference.position + Vector3.up * CreatureMenuHeight;
        Vector3 lookDirection = lookTarget - menuPosition;
        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            lookDirection = reference != null ? reference.forward : Vector3.forward;
        }

        return lookDirection.normalized;
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
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
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
        buttonLabel.fontSize = 32;
        buttonLabel.fontStyle = FontStyle.Bold;
        buttonLabel.color = Color.white;
        StretchToParent(labelObject.GetComponent<RectTransform>());
    }

    private GameObject CreateXRButton(
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
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

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
        buttonLabel.fontSize = 32;
        buttonLabel.fontStyle = FontStyle.Bold;
        buttonLabel.color = Color.white;
        StretchToParent(labelObject.GetComponent<RectTransform>());

        return buttonObject;
    }

    private GameObject CreateQuad(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        MarkRuntimeOnly(quad);
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
        MarkRuntimeOnly(quad);
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

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        MarkRuntimeOnly(obj);
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
            builtInFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        return builtInFont;
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
            case '0': return new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" };
            case '1': return new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" };
            case '?': return new[] { "01110", "10001", "00001", "00010", "00100", "00000", "00100" };
            case 'A': return new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" };
            case 'C': return new[] { "01111", "10000", "10000", "10000", "10000", "10000", "01111" };
            case 'D': return new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" };
            case 'E': return new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" };
            case 'F': return new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" };
            case 'G': return new[] { "01111", "10000", "10000", "10111", "10001", "10001", "01110" };
            case 'I': return new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" };
            case 'L': return new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" };
            case 'M': return new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" };
            case 'N': return new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" };
            case 'O': return new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" };
            case 'R': return new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" };
            case 'S': return new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" };
            case 'T': return new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" };
            case 'U': return new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" };
            case 'V': return new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" };
            case 'Z': return new[] { "11111", "00001", "00010", "00100", "01000", "10000", "11111" };
            case ' ': return new[] { "00000", "00000", "00000", "00000", "00000", "00000", "00000" };
            default: return new[] { "11111", "00001", "00010", "00100", "00100", "00000", "00100" };
        }
    }

    private static void MarkRuntimeOnly(GameObject obj)
    {
        obj.hideFlags = HideFlags.HideInHierarchy;
    }

    private void EnsureOVRMenuRayDriver(GameObject eventSystemObject)
    {
        if (eventSystemObject == null)
        {
            return;
        }

        ovrMenuRayDriver = eventSystemObject.GetComponent<OVRMenuRayDriver>();
        if (ovrMenuRayDriver == null)
        {
            ovrMenuRayDriver = eventSystemObject.AddComponent<OVRMenuRayDriver>();
        }

        EventSystem eventSystem = eventSystemObject.GetComponent<EventSystem>();
        ovrMenuRayDriver.Configure(eventSystem);
    }
}

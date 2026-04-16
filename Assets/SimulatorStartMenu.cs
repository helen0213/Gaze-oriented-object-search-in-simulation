using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR;

public class SimulatorStartMenu : MonoBehaviour
{
    private sealed class Bootstrap : MonoBehaviour
    {
        private void Start()
        {
            EnsureCreated();
            Destroy(gameObject);
        }
    }

    private static SimulatorStartMenu instance;
    private static Font builtInFont;
    private static bool loggedStartedState;
    private static bool bootstrapScheduled;

    private const float MenuDistance = 1.35f;
    private const float AnchorWaitTimeout = 1.5f;
    private const float CreatureMenuOffsetX = 3.5f;
    private const float CreatureMenuHeight = 1.2f;
    private static readonly Vector3 FixedStartMenuPosition = new Vector3(0f, 2f, 1.7f);

    private Canvas desktopCanvas;
    private GameObject desktopPanel;
    private Canvas xrCanvas;

    private GameObject xrMenuRoot;
    private GameObject xrPanelObject;
    private GameObject xrStartButtonObject;
    private GameObject xrExitButtonObject;
    private GameObject xrTitleObject;
    private GameObject xrStartLabelObject;
    private GameObject xrExitLabelObject;

    private Transform menuAnchor;
    private bool hasStarted;
    private bool menuShown;
    private bool pauseApplied;
    private bool xrMenuVisible;
    private float menuRequestRealtime;
    private bool loggedAnchorMissing;
    private bool loggedAnchorReady;
    private bool loggedPlacementDetails;
    private bool recenterAttempted;
    private OVRMenuRayDriver ovrMenuRayDriver;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ScheduleBootstrap()
    {
        if (bootstrapScheduled || instance != null)
        {
            return;
        }

        GameObject bootstrapObject = new GameObject("SimulatorStartMenuBootstrap");
        MarkRuntimeOnly(bootstrapObject);
        DontDestroyOnLoad(bootstrapObject);
        bootstrapObject.AddComponent<Bootstrap>();
        bootstrapScheduled = true;
    }

    public static void EnsureCreated()
    {
        if (instance != null)
        {
            instance.ShowMenu();
            return;
        }

        GameObject root = new GameObject("SimulatorStartMenu");
        MarkRuntimeOnly(root);
        DontDestroyOnLoad(root);
        instance = root.AddComponent<SimulatorStartMenu>();
        bootstrapScheduled = false;
        instance.ShowMenu();
    }

    public static bool HasStarted()
    {
        if (instance == null)
        {
            EnsureCreated();
            return false;
        }

        return instance.hasStarted;
    }

    private void ShowMenu()
    {
        if (hasStarted || menuShown)
        {
            return;
        }

        TryRecenterTrackingOrigin();
        EnsureEventSystem();
        EnsureDesktopCanvas();
        EnsureXRMenu();

        menuShown = true;
        pauseApplied = false;
        xrMenuVisible = false;
        menuRequestRealtime = Time.realtimeSinceStartup;
        loggedAnchorMissing = false;
        loggedAnchorReady = false;
        loggedPlacementDetails = false;

        if (desktopPanel != null)
        {
            desktopPanel.SetActive(true);
        }

        if (xrMenuRoot != null)
        {
            xrMenuRoot.SetActive(false);
        }

        if (ovrMenuRayDriver != null)
        {
            ovrMenuRayDriver.SetVisualTarget(xrMenuRoot != null ? xrMenuRoot.transform : null);
            ovrMenuRayDriver.SetXRMenuInputEnabled(false);
        }

        TryFinalizeMenuPresentation();
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

        if (ovrMenuRayDriver != null)
        {
            ovrMenuRayDriver.SetVisualTarget(null);
            ovrMenuRayDriver.SetXRMenuInputEnabled(false);
        }

        menuShown = false;
        pauseApplied = false;
        xrMenuVisible = false;
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
            TryFinalizeMenuPresentation();

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

    private void TryFinalizeMenuPresentation()
    {
        if (!menuShown)
        {
            return;
        }

        bool anchorReady = UpdateMenuPlacement();
        bool waitTimedOut = Time.realtimeSinceStartup - menuRequestRealtime >= AnchorWaitTimeout;

        if (anchorReady && !xrMenuVisible && xrMenuRoot != null)
        {
            xrMenuRoot.SetActive(true);
            xrMenuVisible = true;
            if (ovrMenuRayDriver != null)
            {
                ovrMenuRayDriver.SetVisualTarget(xrMenuRoot.transform);
                ovrMenuRayDriver.SetXRMenuInputEnabled(true);
            }
            Debug.Log("[SimulatorStartMenu] XR menu activated at world position "
                + xrMenuRoot.transform.position + " using anchor "
                + (menuAnchor != null ? menuAnchor.name : "null"));
        }

        if (pauseApplied)
        {
            return;
        }

        if (!anchorReady && !waitTimedOut)
        {
            return;
        }

        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        pauseApplied = true;
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

        GameObject canvasObject = new GameObject("StartMenuCanvas");
        MarkRuntimeOnly(canvasObject);
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

        xrPanelObject = CreateUIObject("XRPanel", xrMenuRoot.transform);
        Image xrPanelImage = xrPanelObject.AddComponent<Image>();
        xrPanelImage.color = new Color(0f, 0f, 0f, 0.94f);
        StretchToParent(xrPanelObject.GetComponent<RectTransform>());

        xrTitleObject = CreateUIObject("XRTitle", xrPanelObject.transform);
        TextMeshProUGUI xrTitleText = xrTitleObject.AddComponent<TextMeshProUGUI>();
        xrTitleText.text = "Choose an option";
        xrTitleText.alignment = TextAlignmentOptions.Center;
        xrTitleText.fontSize = 48;
        xrTitleText.fontStyle = FontStyles.Bold;
        xrTitleText.color = Color.white;

        RectTransform xrTitleRect = xrTitleObject.GetComponent<RectTransform>();
        xrTitleRect.anchorMin = new Vector2(0.14f, 0.56f);
        xrTitleRect.anchorMax = new Vector2(0.86f, 0.74f);
        xrTitleRect.offsetMin = Vector2.zero;
        xrTitleRect.offsetMax = Vector2.zero;

        xrStartButtonObject = CreateXRButton(
            "XRStartButton",
            xrPanelObject.transform,
            "Start",
            new Color(0.18f, 0.62f, 0.24f, 1f),
            new Vector2(0.14f, 0.22f),
            new Vector2(0.44f, 0.4f),
            StartSimulation,
            out xrStartLabelObject);

        xrExitButtonObject = CreateXRButton(
            "XRExitButton",
            xrPanelObject.transform,
            "Exit",
            new Color(0.65f, 0.18f, 0.18f, 1f),
            new Vector2(0.56f, 0.22f),
            new Vector2(0.86f, 0.4f),
            ExitSimulation,
            out xrExitLabelObject);
    }

    private bool UpdateMenuPlacement()
    {
        Transform anchor = FindMenuAnchor();
        if (anchor == null)
        {
            if (!loggedAnchorMissing)
            {
                Debug.LogWarning("[SimulatorStartMenu] No XR anchor found yet for start menu.");
                loggedAnchorMissing = true;
            }
            return false;
        }

        if (!loggedAnchorReady || menuAnchor != anchor)
        {
            Debug.Log("[SimulatorStartMenu] Using XR anchor " + anchor.name + " at " + anchor.position);
            loggedAnchorReady = true;
            loggedAnchorMissing = false;
        }

        if (menuAnchor != anchor)
        {
            menuAnchor = anchor;
        }

        if (xrMenuRoot != null)
        {
            Camera menuCamera = FindMenuCamera();
            Transform reference = FindCreatureReferenceTransform();
            if (reference == null)
            {
                reference = menuCamera != null ? menuCamera.transform : menuAnchor;
            }

            Vector3 menuPosition = FixedStartMenuPosition;
            Vector3 lookDirection = GetMenuLookDirection(menuPosition, reference, menuCamera);

            xrMenuRoot.transform.SetParent(null, true);
            xrMenuRoot.transform.position = menuPosition;
            xrMenuRoot.transform.rotation = Quaternion.LookRotation(-lookDirection, reference.up);
            xrMenuRoot.transform.localScale = Vector3.one * 0.0015f;

            if (!loggedPlacementDetails)
            {
                string referenceName = reference != null ? reference.name : "null";
                string cameraName = menuCamera != null ? menuCamera.name : "null";
                Vector3 cameraPosition = menuCamera != null ? menuCamera.transform.position : Vector3.zero;
                Debug.Log("[SimulatorStartMenu] Placement details: reference=" + referenceName
                    + ", referencePos=" + (reference != null ? reference.position.ToString() : "null")
                    + ", camera=" + cameraName
                    + ", cameraPos=" + cameraPosition
                    + ", menuPos=" + menuPosition
                    + ", menuForward=" + xrMenuRoot.transform.forward
                    + ", creatureAnchorUsed=" + (FindCreatureReferenceTransform() != null));
                loggedPlacementDetails = true;
            }
        }

        if (xrCanvas != null)
        {
            xrCanvas.worldCamera = FindMenuCamera();
        }

        return true;
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

    private GameObject CreateXRButton(
        string name,
        Transform parent,
        string label,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        UnityEngine.Events.UnityAction onClick,
        out GameObject labelObject)
    {
        GameObject buttonObject = CreateUIObject(name, parent);
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = color;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = anchorMin;
        buttonRect.anchorMax = anchorMax;
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        labelObject = CreateUIObject(name + "Label", buttonObject.transform);
        TextMeshProUGUI buttonLabel = labelObject.AddComponent<TextMeshProUGUI>();
        buttonLabel.text = label;
        buttonLabel.alignment = TextAlignmentOptions.Center;
        buttonLabel.fontSize = 34;
        buttonLabel.fontStyle = FontStyles.Bold;
        buttonLabel.color = Color.white;
        StretchToParent(labelObject.GetComponent<RectTransform>());

        return buttonObject;
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

    private static void MarkRuntimeOnly(GameObject obj)
    {
        obj.hideFlags = HideFlags.HideInHierarchy;
    }

    private void TryRecenterTrackingOrigin()
    {
        if (recenterAttempted)
        {
            return;
        }

        recenterAttempted = true;

        if (Application.isEditor)
        {
            return;
        }

        try
        {
            if (OVRManager.display != null)
            {
                OVRManager.display.RecenterPose();
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[SimulatorStartMenu] OVR recenter failed: " + exception.Message);
        }

        List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(inputSubsystems);
        for (int i = 0; i < inputSubsystems.Count; i++)
        {
            XRInputSubsystem subsystem = inputSubsystems[i];
            if (subsystem != null && subsystem.running)
            {
                subsystem.TryRecenter();
            }
        }
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

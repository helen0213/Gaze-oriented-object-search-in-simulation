using UnityEngine;
using System.Collections.Generic;

public class GazeTargetDetector : MonoBehaviour
{
    [Header("Startup Randomization")]
    public Transform[] animals = new Transform[3];
    public Transform[] fixedPositions = new Transform[3];
    public bool copySlotRotation = true;
    public bool autoDetectAnimalsFromGazeTargets = true;
    public bool autoDetectAnimalsFromTargetLayer = true;
    public bool requireChangedLayout = true;
    public int expectedAnimalCount = 3;

    public CombinedGaze combinedGaze;

    public LayerMask targetLayer;
    public float maxDistance = 20f;

    public bool enableHighlight = true;
    public Color highlightColor = Color.red;

    public bool debug = true;

    public GameObject CurrentTarget { get; private set; }
    public RaycastHit CurrentHit { get; private set; }

    private GameObject lastLoggedTarget;
    private Renderer lastHighlightedRenderer;
    private Color lastOriginalColor;
    private Transform[] randomizedAnimals = new Transform[0];

    void Start()
    {
        RandomizeAnimalPositions();
    }

    [ContextMenu("Randomize Animal Positions Now")]
    void RandomizeAnimalPositionsFromContextMenu()
    {
        RandomizeAnimalPositions();
    }

    void Update()
    {
        if (combinedGaze == null)
            return;

        Ray ray = combinedGaze.CombinedRay;

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, targetLayer))
        {
            GameObject targetRoot = ResolveTargetRoot(hit.collider.gameObject);

            if (targetRoot != null)
            {
                CurrentHit = hit;
                CurrentTarget = targetRoot;

                if (debug && lastLoggedTarget != targetRoot)
                {
                    Debug.Log("👁 Looking at: " + targetRoot.name);
                    lastLoggedTarget = targetRoot;
                }

                if (enableHighlight)
                {
                    Highlight(targetRoot);
                }

                return;
            }
        }

        if (debug && lastLoggedTarget != null)
        {
            Debug.Log("👁 Looking at: NONE");
            lastLoggedTarget = null;
        }

        ClearHighlight();
        CurrentTarget = null;
    }

    GameObject ResolveTargetRoot(GameObject hitObject)
    {
        Transform t = hitObject.transform;
        Transform lastOnTargetLayer = null;

        while (t != null)
        {
            if (((1 << t.gameObject.layer) & targetLayer.value) != 0)
            {
                lastOnTargetLayer = t;
            }

            t = t.parent;
        }

        return lastOnTargetLayer != null ? lastOnTargetLayer.gameObject : null;
    }

    void Highlight(GameObject target)
    {
        Renderer r = target.GetComponentInChildren<Renderer>();
        if (r == null)
            return;

        if (lastHighlightedRenderer == r)
            return;

        ClearHighlight();

        lastHighlightedRenderer = r;
        lastOriginalColor = r.material.color;
        r.material.color = highlightColor;
    }

    void ClearHighlight()
    {
        if (lastHighlightedRenderer != null)
        {
            lastHighlightedRenderer.material.color = lastOriginalColor;
            lastHighlightedRenderer = null;
        }
    }

    void RandomizeAnimalPositions()
    {
        Transform[] sourceAnimals = GetAnimalsToShuffle();
        randomizedAnimals = CompactNonNull(sourceAnimals);
        Vector3[] slotPositions;
        Quaternion[] slotRotations;
        BuildSlots(sourceAnimals, out slotPositions, out slotRotations);

        int count = Mathf.Min(sourceAnimals.Length, slotPositions.Length);
        if (count == 0)
        {
            if (debug)
                Debug.LogWarning("[GazeTargetDetector] Randomization skipped: no animals/slots found.");
            return;
        }

        int[] slotOrder = new int[count];
        for (int i = 0; i < count; i++)
            slotOrder[i] = i;

        // Fisher-Yates shuffle so each run gets a new unique mapping.
        for (int attempt = 0; attempt < 8; attempt++)
        {
            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int temp = slotOrder[i];
                slotOrder[i] = slotOrder[j];
                slotOrder[j] = temp;
            }

            if (!requireChangedLayout || count <= 1 || !IsIdentity(slotOrder, count))
                break;
        }

        for (int i = 0; i < count; i++)
        {
            Transform animal = sourceAnimals[i];
            if (animal == null)
                continue;

            int slotIndex = slotOrder[i];
            animal.position = slotPositions[slotIndex];
            if (copySlotRotation)
                animal.rotation = slotRotations[slotIndex];

            if (debug)
            {
                Debug.Log("[GazeTargetDetector] Randomized " + animal.name
                    + " -> slot " + slotIndex
                    + " at " + slotPositions[slotIndex]);
            }
        }
    }

    public Transform[] GetRandomizedAnimals()
    {
        if (randomizedAnimals != null && randomizedAnimals.Length > 0)
            return CompactNonNull(randomizedAnimals);

        return GetAnimalsToShuffle();
    }

    Transform[] GetAnimalsToShuffle()
    {
        Transform[] assignedAnimals = CompactNonNull(animals);
        if (assignedAnimals.Length > 0)
            return assignedAnimals;

        if (autoDetectAnimalsFromTargetLayer)
        {
            Transform[] fromLayer = DetectAnimalsFromTargetLayer();
            if (fromLayer.Length > 0)
                return fromLayer;
        }

        if (!autoDetectAnimalsFromGazeTargets)
            return new Transform[0];

        GazeTarget[] gazeTargets = FindObjectsOfType<GazeTarget>();
        Transform[] detected = new Transform[gazeTargets.Length];
        for (int i = 0; i < gazeTargets.Length; i++)
            detected[i] = gazeTargets[i].transform;

        return CompactNonNull(detected);
    }

    Transform[] DetectAnimalsFromTargetLayer()
    {
        Transform[] allTransforms = FindObjectsOfType<Transform>();
        List<Transform> found = new List<Transform>();
        HashSet<Transform> unique = new HashSet<Transform>();

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null)
                continue;

            if (((1 << t.gameObject.layer) & targetLayer.value) == 0)
                continue;

            Transform root = FindTopmostInTargetLayer(t);
            if (root == null || !unique.Add(root))
                continue;

            found.Add(root);
        }

        if (expectedAnimalCount > 0 && found.Count != expectedAnimalCount)
        {
            if (debug)
            {
                Debug.LogWarning("[GazeTargetDetector] Detected " + found.Count
                    + " target-layer roots; expected " + expectedAnimalCount
                    + ". Assign animals[] manually if needed.");
            }
        }
        else if (debug)
        {
            string names = "";
            for (int i = 0; i < found.Count; i++)
            {
                if (i > 0) names += ", ";
                names += found[i].name;
            }
            Debug.Log("[GazeTargetDetector] Auto-detected target-layer animals: " + names);
        }

        return found.ToArray();
    }

    Transform FindTopmostInTargetLayer(Transform start)
    {
        Transform current = start;
        Transform topmost = null;

        while (current != null)
        {
            if (((1 << current.gameObject.layer) & targetLayer.value) != 0)
                topmost = current;
            else
                break;

            current = current.parent;
        }

        return topmost;
    }

    void BuildSlots(Transform[] sourceAnimals, out Vector3[] positions, out Quaternion[] rotations)
    {
        Transform[] assignedSlots = CompactNonNull(fixedPositions);
        if (assignedSlots.Length > 0)
        {
            positions = new Vector3[assignedSlots.Length];
            rotations = new Quaternion[assignedSlots.Length];

            for (int i = 0; i < assignedSlots.Length; i++)
            {
                Transform slot = assignedSlots[i];
                positions[i] = slot.position;
                rotations[i] = slot.rotation;
            }

            return;
        }

        positions = new Vector3[sourceAnimals.Length];
        rotations = new Quaternion[sourceAnimals.Length];
        for (int i = 0; i < sourceAnimals.Length; i++)
        {
            Transform animal = sourceAnimals[i];
            if (animal == null)
                continue;

            positions[i] = animal.position;
            rotations[i] = animal.rotation;
        }
    }

    Transform[] CompactNonNull(Transform[] items)
    {
        if (items == null || items.Length == 0)
            return new Transform[0];

        int nonNullCount = 0;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
                nonNullCount++;
        }

        if (nonNullCount == 0)
            return new Transform[0];

        Transform[] compacted = new Transform[nonNullCount];
        int index = 0;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
                compacted[index++] = items[i];
        }

        return compacted;
    }

    bool IsIdentity(int[] order, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (order[i] != i)
                return false;
        }

        return true;
    }
}

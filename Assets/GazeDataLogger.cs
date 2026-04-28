using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class GazeDataLogger : MonoBehaviour
{
    [Header("Sources")]
    public CombinedGaze combinedGaze;
    public GazeTargetDetector gazeTargetDetector;

    [Header("Output")]
    public string outputDirectoryName = "GazeLogs";
    public string fileNamePrefix = "gaze";
    public bool appendTimestampToFileName = true;
    public bool logOnStart = true;

    [Header("Sampling")]
    public bool logEveryFrame = true;
    public float targetHz = 30f;

    public string CurrentLogFilePath { get; private set; }
    public bool IsLogging => writer != null;

    private StreamWriter writer;
    private float sampleInterval = 0f;
    private float sampleAccumulator = 0f;

    void Start()
    {
        RecalculateInterval();

        if (logOnStart)
        {
            StartLogging();
        }
    }

    void OnValidate()
    {
        if (targetHz <= 0f)
        {
            targetHz = 1f;
        }

        RecalculateInterval();
    }

    void Update()
    {
        if (!IsLogging || combinedGaze == null)
        {
            return;
        }

        if (SimulationMenuBlocker.IsBlockingScene())
        {
            return;
        }

        if (logEveryFrame)
        {
            WriteSample();
            return;
        }

        sampleAccumulator += Time.unscaledDeltaTime;
        if (sampleAccumulator < sampleInterval)
        {
            return;
        }

        sampleAccumulator -= sampleInterval;
        WriteSample();
    }

    void OnDisable()
    {
        StopLogging();
    }

    void OnApplicationQuit()
    {
        StopLogging();
    }

    public void StartLogging()
    {
        if (IsLogging)
        {
            return;
        }

        string directoryPath = GetLogDirectoryPath();
        Directory.CreateDirectory(directoryPath);

        string fileName = GetEffectiveFileNamePrefix();
        if (appendTimestampToFileName)
        {
            fileName += "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }

        fileName += ".csv";

        CurrentLogFilePath = Path.Combine(directoryPath, fileName);
        writer = new StreamWriter(CurrentLogFilePath, false);
        writer.AutoFlush = true;

        writer.WriteLine(
            "utc_timestamp,unity_time,frame_count,eye_tracking_supported,eye_tracking_permission_granted,using_eye_tracking," +
            "gaze_origin_x,gaze_origin_y,gaze_origin_z," +
            "gaze_direction_x,gaze_direction_y,gaze_direction_z," +
            "hit_target,hit_distance,hit_point_x,hit_point_y,hit_point_z"
        );

        Debug.Log("Gaze logging started: " + CurrentLogFilePath);
    }

    public void StopLogging()
    {
        if (!IsLogging)
        {
            return;
        }

        writer.Flush();
        writer.Close();
        writer = null;

        Debug.Log("Gaze logging stopped: " + CurrentLogFilePath);
    }

    private string GetLogDirectoryPath()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return Path.Combine(Application.persistentDataPath, outputDirectoryName);
#else
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, outputDirectoryName);
#endif
    }

    private string GetEffectiveFileNamePrefix()
    {
        if (!string.IsNullOrWhiteSpace(fileNamePrefix) && fileNamePrefix != "gaze")
        {
            return fileNamePrefix;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        return "gaze_questpro";
#else
        return "gaze_simulator";
#endif
    }

    private void RecalculateInterval()
    {
        sampleInterval = 1f / Mathf.Max(targetHz, 1f);
    }

    private void WriteSample()
    {
        Ray ray = combinedGaze.CombinedRay;

        string targetName = string.Empty;
        float hitDistance = -1f;
        Vector3 hitPoint = Vector3.zero;

        if (gazeTargetDetector != null && gazeTargetDetector.CurrentTarget != null)
        {
            targetName = EscapeCsv(gazeTargetDetector.CurrentTarget.name);
            hitDistance = gazeTargetDetector.CurrentHit.distance;
            hitPoint = gazeTargetDetector.CurrentHit.point;
        }

        string line = string.Join(",",
            DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Time.realtimeSinceStartup.ToString("F6", CultureInfo.InvariantCulture),
            Time.frameCount.ToString(CultureInfo.InvariantCulture),
            combinedGaze.EyeTrackingSupported ? "1" : "0",
            combinedGaze.EyeTrackingPermissionGranted ? "1" : "0",
            combinedGaze.UsingEyeTracking ? "1" : "0",
            ray.origin.x.ToString("F6", CultureInfo.InvariantCulture),
            ray.origin.y.ToString("F6", CultureInfo.InvariantCulture),
            ray.origin.z.ToString("F6", CultureInfo.InvariantCulture),
            ray.direction.x.ToString("F6", CultureInfo.InvariantCulture),
            ray.direction.y.ToString("F6", CultureInfo.InvariantCulture),
            ray.direction.z.ToString("F6", CultureInfo.InvariantCulture),
            targetName,
            hitDistance.ToString("F6", CultureInfo.InvariantCulture),
            hitPoint.x.ToString("F6", CultureInfo.InvariantCulture),
            hitPoint.y.ToString("F6", CultureInfo.InvariantCulture),
            hitPoint.z.ToString("F6", CultureInfo.InvariantCulture)
        );

        writer.WriteLine(line);
    }

    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        bool needsQuotes = value.Contains(",") || value.Contains("\"") || value.Contains("\n");
        if (!needsQuotes)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}

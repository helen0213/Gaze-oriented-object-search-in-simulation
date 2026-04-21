# Gaze-oriented-object-search-in-simulation

## Gaze Logging

This project includes a `GazeDataLogger` Unity component at [Assets/GazeDataLogger.cs](/Users/helenchen/Desktop/csci2951k/Gaze-oriented-object-search-in-simulation/Assets/GazeDataLogger.cs).

- Add `GazeDataLogger` to an active scene object under `GazeSystem`.
- Assign the existing `CombinedGaze` component to `combinedGaze`.
- Optionally assign `GazeTargetDetector` to `gazeTargetDetector` to record hit target data.
- Leave `logOnStart` enabled to start logging automatically when the scene runs.

Filename prefixes are chosen automatically when `fileNamePrefix` is left as `gaze`:

- Unity Editor / Meta XR Simulator: `gaze_simulator_<timestamp>.csv`
- Meta Quest Pro headset build: `gaze_questpro_<timestamp>.csv`

Log output location depends on runtime platform:

- Unity Editor / Meta XR Simulator: `GazeLogs/` in the project root
- Meta Quest Pro / Android build: `Application.persistentDataPath/GazeLogs`

## Pulling Headset Logs

After running on Quest Pro, use the helper script from the project root:

```bash
./pull_gaze_logs.sh
```

This copies gaze log CSV files from the headset's app storage into the local `GazeLogs/` directory.

## PythonSender IP Setup (Headset)

When deploying to Quest/Android, `127.0.0.1` points to the headset itself, not your Mac.

Before running the Python server, get your current Mac LAN IP:

```bash
ipconfig getifaddr en0
```

Then in Unity, set `PythonSender.androidWebSocketUrl` to:

```text
ws://<MAC_IP>:8000/ws
```

If `en0` is not your active interface, use the correct interface (for example `en1`).

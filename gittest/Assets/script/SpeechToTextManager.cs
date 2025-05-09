using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Diagnostics;
using System.Threading;

// Alias UnityEngine.Debug to avoid conflict with System.Diagnostics.Debug
using Debug = UnityEngine.Debug;

public class SpeechToTextManager : MonoBehaviour {
    [Header("UI")]
    public Button recordButton;
    public TextMeshProUGUI recordButtonText;
    public TextMeshProUGUI statusText;

    [Header("Recording Settings")]
    public int sampleRate = 16000;
    public int maxDurationSec = 60;

    [Header("Whisper Paths")]
    // Binary & model paths determined at runtime
    private string whisperExePath;
    private string modelBinPath;

    private bool isRecording = false;
    private AudioClip clip;
    private string wavPath;

    private void Start() {
        // Determine platform-specific binary name (using whisper-cli)
        string exeName = Application.platform == RuntimePlatform.WindowsPlayer ||
                         Application.platform == RuntimePlatform.WindowsEditor
                         ? "win/whisper.exe"
                         : "linux/whisper-cli";

        // Build path to binary in StreamingAssets
        whisperExePath = Path.Combine(Application.streamingAssetsPath, exeName);
        if (!File.Exists(whisperExePath)) {
            Debug.LogError($"Whisper binary not found at {whisperExePath}");
        }

        // Auto-detect a .bin model in StreamingAssets/models
        string modelsDir = Path.Combine(Application.streamingAssetsPath, "models");
        if (Directory.Exists(modelsDir)) {
            string[] bins = Directory.GetFiles(modelsDir, "*.bin");
            if (bins.Length > 0) {
                modelBinPath = bins[0];
            } else {
                Debug.LogError($"No .bin model found in {modelsDir}");
            }
        } else {
            Debug.LogError($"Models directory not found: {modelsDir}");
        }

        // Hook up UI
        recordButton.onClick.AddListener(ToggleRecording);
        UpdateUI();
    }

    private void ToggleRecording() {
        if (!isRecording) StartRecording();
        else              StopRecording();

        isRecording = !isRecording;
        UpdateUI();
    }

    private void UpdateUI() {
        recordButtonText.text = isRecording ? "Stop Recording" : "Start Recording";
        statusText.text       = isRecording ? "Listening..."    : "Idle";
    }

    private void StartRecording() {
        clip = Microphone.Start(null, false, maxDurationSec, sampleRate);
    }

    private void StopRecording() {
        int pos = Microphone.GetPosition(null);
        Microphone.End(null);

        // Trim to actual recorded length
        float[] samples = new float[pos * clip.channels];
        clip.GetData(samples, 0);
        AudioClip trimmed = AudioClip.Create("Trimmed", pos, clip.channels, sampleRate, false);
        trimmed.SetData(samples, 0);

        // Save WAV to persistent data
        wavPath = Path.Combine(Application.persistentDataPath, "recording.wav");
        SaveWav.Save(wavPath, trimmed);

        // Launch transcription
        RunWhisperTranscription(wavPath);
    }

    private void RunWhisperTranscription(string filePath) {
        ProcessStartInfo psi = new ProcessStartInfo {
            FileName               = whisperExePath,
            Arguments              = $"-m \"{modelBinPath}\" -f \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        new Thread(() => {
            try {
                Process proc = Process.Start(psi);
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                string output = stdout + stderr;
                Debug.Log($"[Whisper] {output}");
                // Optionally update UI on main thread:
                // UnityMainThreadDispatcher.Instance().Enqueue(() => statusText.text = output);
            }
            catch (System.Exception e) {
                Debug.LogError($"Whisper failed: {e}");
            }
        }).Start();
    }
}

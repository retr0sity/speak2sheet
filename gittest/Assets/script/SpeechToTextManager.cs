using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Diagnostics;
using System.Threading;

// Alias UnityEngine.Debug to avoid conflict
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
    private string whisperExePath;
    private string modelBinPath;

    private bool isRecording = false;
    private AudioClip clip;
    private string wavPath;

    private void Start() {
        // Copy and prepare binary on a native filesystem
        SetupWhisperBinary();
        // Locate model file
        SetupModelPath();

        // Hook up UI
        recordButton.onClick.AddListener(ToggleRecording);
        UpdateUI();
    }

    private void SetupWhisperBinary() {
        // Determine expected filename
        string fileName = (Application.platform == RuntimePlatform.WindowsPlayer ||
                           Application.platform == RuntimePlatform.WindowsEditor)
                           ? "whisper.exe"
                           : "whisper-cli";

        // Search StreamingAssets (and subfolders) for the binary
        string[] matches = Directory.GetFiles(Application.streamingAssetsPath, fileName, SearchOption.AllDirectories);
        if (matches.Length == 0) {
            Debug.LogError($"Whisper binary '{fileName}' not found in StreamingAssets");
            return;
        }

        string src = matches[0];
        string dest = Path.Combine(Application.persistentDataPath, fileName);

        // Copy if not already present
        if (!File.Exists(dest)) {
            File.Copy(src, dest, true);
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            // Ensure executable
            try {
                var chmod = new ProcessStartInfo {
                    FileName = "/bin/chmod",
                    Arguments = $"+x \"{dest}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(chmod).WaitForExit();
            } catch (System.Exception e) {
                Debug.LogError($"Failed to chmod binary: {e}");
            }
#endif
        }
        whisperExePath = dest;
    }

    private void SetupModelPath() {
        string modelsDir = Path.Combine(Application.streamingAssetsPath, "models");
        if (!Directory.Exists(modelsDir)) {
            Debug.LogError($"Models directory not found: {modelsDir}");
            return;
        }
        string[] bins = Directory.GetFiles(modelsDir, "*.bin", SearchOption.AllDirectories);
        if (bins.Length == 0) {
            Debug.LogError($"No .bin model found in {modelsDir}");
            return;
        }
        modelBinPath = bins[0];
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

        float[] samples = new float[pos * clip.channels];
        clip.GetData(samples, 0);
        AudioClip trimmed = AudioClip.Create("Trimmed", pos, clip.channels, sampleRate, false);
        trimmed.SetData(samples, 0);

        wavPath = Path.Combine(Application.persistentDataPath, "recording.wav");
        SaveWav.Save(wavPath, trimmed);

        RunWhisperTranscription(wavPath);
    }

    private void RunWhisperTranscription(string filePath) {
        if (string.IsNullOrEmpty(whisperExePath) || string.IsNullOrEmpty(modelBinPath)) {
            Debug.LogError("Whisper binary or model path not set");
            return;
        }

        var psi = new ProcessStartInfo {
            FileName               = whisperExePath,
            Arguments              = $"-m \"{modelBinPath}\" -f \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        new Thread(() => {
            try {
                var proc = Process.Start(psi);
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                Debug.Log($"[Whisper] {stdout}{stderr}");
            } catch (System.Exception e) {
                Debug.LogError($"Whisper failed: {e}");
            }
        }).Start();
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

// Alias UnityEngine.Debug so we can still call System.Diagnostics
using Debug = UnityEngine.Debug;

public class SpeechToTextManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button   recordButton;
    [SerializeField] private TMP_Text recordButtonText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text transcriptText;

    [Header("Recording Settings")]
    [SerializeField] private int sampleRate     = 16000;
    [SerializeField] private int maxDurationSec = 60;

    [Header("Whisper Paths & Settings")]
    [SerializeField] [Tooltip("The .gbnf file placed under StreamingAssets/models")]
    private string grammarFileName = "grades.gbnf";
    [SerializeField] [Range(0, 200)]
    private int grammarPenalty = 100;

    private string whisperExePath;
    private string modelBinPath;
    private string grammarFilePath;

    private bool isRecording;
    private AudioClip clip;
    private CancellationTokenSource cts;

    private void Awake()
    {
        InitializeWhisperPaths();
        InitializeGrammarFile();
    }

    private void Start()
    {
        recordButton.onClick.AddListener(OnRecordButtonClicked);
        UpdateUI();
    }

    private void InitializeWhisperPaths()
    {
        // existing logic…
        string fileName = Application.platform == RuntimePlatform.WindowsPlayer ||
                          Application.platform == RuntimePlatform.WindowsEditor
                          ? "whisper.exe"
                          : "whisper-cli";

        var matches = Directory.GetFiles(Application.streamingAssetsPath, fileName, SearchOption.AllDirectories);
        if (matches.Length == 0)
        {
            Debug.LogError($"[Whisper] Binary not found: {fileName}");
            return;
        }

        string src  = matches[0];
        string dest = Path.Combine(Application.persistentDataPath, fileName);

        if (!File.Exists(dest))
        {
            File.Copy(src, dest, overwrite: true);
    #if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            // make executable…
            try
            {
                var chmod = new ProcessStartInfo
                {
                    FileName        = "/bin/chmod",
                    Arguments       = $"+x \"{dest}\"",
                    UseShellExecute = false,
                    CreateNoWindow  = true
                };
                Process.Start(chmod).WaitForExit();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Whisper] chmod failed: {e}");
            }
    #endif
        }
        whisperExePath = dest;

        string modelsDir = Path.Combine(Application.streamingAssetsPath, "models");
        if (!Directory.Exists(modelsDir))
        {
            Debug.LogError($"[Whisper] Models folder not found: {modelsDir}");
            return;
        }

        var bins = Directory.GetFiles(modelsDir, "*.bin", SearchOption.AllDirectories);
        if (bins.Length == 0)
        {
            Debug.LogError($"[Whisper] No .bin found in {modelsDir}");
            return;
        }
        modelBinPath = bins[0];
    }

    private void InitializeGrammarFile()
    {
        // Copy grades.gbnf from StreamingAssets to persistentDataPath
        string src = Path.Combine(Application.streamingAssetsPath, "models", grammarFileName);
        if (!File.Exists(src))
        {
            Debug.LogError($"[Whisper] Grammar file not found: {src}");
            return;
        }
        grammarFilePath = Path.Combine(Application.persistentDataPath, grammarFileName);
        if (!File.Exists(grammarFilePath))
        {
            File.Copy(src, grammarFilePath, overwrite: true);
        }
    }

    private void OnRecordButtonClicked()
    {
        if (isRecording) StopRecording();
        else             StartRecording();

        isRecording = !isRecording;
        UpdateUI();
    }

    private void UpdateUI()
    {
        recordButtonText.text = isRecording ? "Stop Recording" : "Start Recording";
        statusText.text       = isRecording ? "Listening..."    : "Idle";
    }

    private void StartRecording()
    {
        if (Microphone.devices.Length == 0)
        {
            ShowError("No microphone detected.");
            return;
        }
        clip = Microphone.Start(null, false, maxDurationSec, sampleRate);
    }

    private void StopRecording()
    {
        int pos = Microphone.GetPosition(null);
        Microphone.End(null);

        if (pos <= 0)
        {
            ShowError("No audio was recorded.");
            return;
        }

        float[] raw = new float[pos * clip.channels];
        clip.GetData(raw, 0);
        float[] monoSamples = raw;
        if (clip.channels > 1)
        {
            monoSamples = new float[pos];
            for (int i = 0; i < pos; i++)
            {
                float sum = 0f;
                for (int ch = 0; ch < clip.channels; ch++)
                    sum += raw[i * clip.channels + ch];
                monoSamples[i] = sum / clip.channels;
            }
        }

        string wavPath = Path.Combine(Application.persistentDataPath, "recording.wav");
        statusText.text = "Saving audio…";

        Task.Run(() => WriteWavFile(wavPath, monoSamples, 1, sampleRate))
            .ContinueWith(_ => _ = TranscribeAsync(wavPath), TaskScheduler.FromCurrentSynchronizationContext());
    }

    private static void WriteWavFile(string filepath, float[] samples, int channels, int sampleRate)
    {
        byte[] wavBytes = ConvertSamplesToWav(samples, channels, sampleRate);
        File.WriteAllBytes(filepath, wavBytes);
    }

    private static byte[] ConvertSamplesToWav(float[] samples, int channels, int sampleRate)
    {
        // existing WAV conversion…
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms, Encoding.UTF8))
        {
            int sampleCount = samples.Length;
            int byteCount   = sampleCount * 2;

            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + byteCount);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));

            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(sampleRate * channels * 2);
            bw.Write((short)(channels * 2));
            bw.Write((short)16);

            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(byteCount);

            foreach (var f in samples)
            {
                float clamped = Math.Max(-1f, Math.Min(1f, f));
                short intData = (short)(clamped * short.MaxValue);
                bw.Write(intData);
            }

            bw.Flush();
            return ms.ToArray();
        }
    }

    private async Task TranscribeAsync(string wavPath)
    {
        cts?.Cancel();
        cts = new CancellationTokenSource();
        statusText.text = "Transcribing…";
        try
        {
            string output = await RunWhisperProcessAsync(wavPath, cts.Token);
            var transcriptLines = new List<string>();
            foreach (var line in output.Split(new[] {'\r','\n'}, StringSplitOptions.RemoveEmptyEntries))
            {
                // Match timestamped transcript lines
                var match = Regex.Match(line, @"^\[\d{2}:\d{2}:\d{2}\.\d+\s*-->\s*\d{2}:\d{2}:\d{2}\.\d+\]\s*(.+)$");
                if (match.Success)
                    transcriptLines.Add(match.Groups[1].Value.Trim());
            }
            string clean = string.Join("\n", transcriptLines);

            transcriptText.text = clean;
            statusText.text     = "Done";
            Debug.Log($"[Whisper Transcript] {clean}");
        }
        catch (OperationCanceledException)
        {
            statusText.text = "Canceled";
        }
        catch (Exception e)
        {
            ShowError($"Transcription failed: {e.Message}");
        }
    }

    private Task<string> RunWhisperProcessAsync(string filePath, CancellationToken token)
    {
        return Task.Run(() =>
        {
            string modelArg   = modelBinPath.Replace("\\", "/");
            string fileArg    = filePath.Replace("\\", "/");
            string grammarArg = grammarFilePath != null
                ? $" --grammar \"{grammarFilePath.Replace("\\", "/")}\" --grammar-penalty {grammarPenalty}"
                : "";

            // add the grammar args
            string args = $"-m \"{modelArg}\"{grammarArg} -f \"{fileArg}\" -l el";

            Debug.Log($"[Whisper] Executing: {whisperExePath} {args}");

            var psi = new ProcessStartInfo
            {
                FileName               = whisperExePath,
                Arguments              = args,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding  = Encoding.UTF8
            };
            var builder = new StringBuilder();
            using (var proc = new Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) lock(builder) builder.AppendLine(e.Data); };
                proc.ErrorDataReceived  += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) lock(builder) builder.AppendLine(e.Data); };
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                while (!proc.WaitForExit(100))
                {
                    if (token.IsCancellationRequested)
                    {
                        proc.Kill();
                        token.ThrowIfCancellationRequested();
                    }
                }
                return builder.ToString();
            }
        }, token);
    }

    private void ShowError(string message)
    {
        Debug.LogError(message);
        statusText.text = $"Error: {message}";
    }
}
// Note: This script assumes that the Whisper binary and model files are correctly set up in the StreamingAssets folder.
// Make sure to test the script in the Unity Editor and on the target platform to ensure compatibility.
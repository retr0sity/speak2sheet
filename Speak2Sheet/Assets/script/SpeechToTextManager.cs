// SpeechToTextManager.cs
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
using Debug = UnityEngine.Debug;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;

public class SpeechToTextManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button recordButton;
    [SerializeField] private TMP_Text recordButtonText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text transcriptText;

    [Header("Recording Settings")]
    [SerializeField] private int sampleRate = 16000;
    [SerializeField] private int maxDurationSec = 60;

    [Header("Whisper Paths & Settings")]
    [SerializeField] private string grammarFileName = "grades.gbnf";
    [SerializeField] [Range(0, 200)] private int grammarPenalty = 100;

    [Header("Excel Integration")]
    [SerializeField] private ExcelLoader excelLoader;

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
        string fileName = (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            ? "whisper.exe"
            : "whisper-cli";

        var matches = Directory.GetFiles(Application.streamingAssetsPath, fileName, SearchOption.AllDirectories);
        if (matches.Length == 0)
        {
            Debug.LogError($"[Whisper] Binary not found: {fileName}");
            return;
        }

        string src = matches[0];
        string dest = Path.Combine(Application.persistentDataPath, fileName);
        if (!File.Exists(dest))
        {
            File.Copy(src, dest, true);
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            try
            {
                var chmod = new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"+x \"{dest}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
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
        string src = Path.Combine(Application.streamingAssetsPath, "models", grammarFileName);
        if (!File.Exists(src))
        {
            Debug.LogError($"[Whisper] Grammar file not found: {src}");
            return;
        }
        grammarFilePath = Path.Combine(Application.persistentDataPath, grammarFileName);
        if (!File.Exists(grammarFilePath))
            File.Copy(src, grammarFilePath, true);
    }

    private void UpdateUI()
    {
        recordButtonText.text = isRecording ? "Stop Recording" : "Start Recording";
        statusText.text = isRecording ? "Listening..." : "Idle";
    }

    private void OnRecordButtonClicked()
    {
        if (isRecording) StopRecording();
        else StartRecording();
        isRecording = !isRecording;
        UpdateUI();
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
        float[] mono = raw;
        if (clip.channels > 1)
        {
            mono = new float[pos];
            for (int i = 0; i < pos; i++)
            {
                float sum = 0;
                for (int ch = 0; ch < clip.channels; ch++)
                    sum += raw[i * clip.channels + ch];
                mono[i] = sum / clip.channels;
            }
        }

        string wavPath = Path.Combine(Application.persistentDataPath, "recording.wav");
        statusText.text = "Saving audio...";
        Task.Run(() => WriteWavFile(wavPath, mono, 1, sampleRate))
            .ContinueWith(_ => _ = TranscribeAsync(wavPath), TaskScheduler.FromCurrentSynchronizationContext());
    }

    private static void WriteWavFile(string filepath, float[] samples, int channels, int sampleRate)
    {
        byte[] bytes = ConvertSamplesToWav(samples, channels, sampleRate);
        File.WriteAllBytes(filepath, bytes);
    }

    private static byte[] ConvertSamplesToWav(float[] samples, int channels, int sampleRate)
    {
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms, Encoding.UTF8))
        {
            int dataSize = samples.Length * 2;
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataSize);
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
            bw.Write(dataSize);
            foreach (var f in samples)
            {
                short v = (short)(Mathf.Clamp(f, -1f, 1f) * short.MaxValue);
                bw.Write(v);
            }
            bw.Flush();
            return ms.ToArray();
        }
    }

    private async Task TranscribeAsync(string wavPath)
    {
        cts?.Cancel();
        cts = new CancellationTokenSource();
        statusText.text = "Transcribing...";
        try
        {
            string output = await RunWhisperProcessAsync(wavPath, cts.Token);
            var lines = new List<string>();
            foreach (string line in output.Split(new[] {'\r','\n'}, StringSplitOptions.RemoveEmptyEntries))
            {
                var m = Regex.Match(line, @"^\[\d{2}:\d{2}:\d{2}\.\d+\s*-->\s*\d{2}:\d{2}:\d{2}\.\d+\]\s*(.+)$");
                if (m.Success) lines.Add(m.Groups[1].Value.Trim());
            }
            string clean = string.Join(" ", lines);
            transcriptText.text = clean;
            statusText.text = "Done";
            Debug.Log($"[Whisper] {clean}");
            ProcessTranscript(clean);
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
            string modelArg = modelBinPath.Replace("\\", "/");
            string fileArg = filePath.Replace("\\", "/");
            string grammarArg = grammarFilePath != null
                ? $" --grammar \"{grammarFilePath.Replace("\\", "/")}\" --grammar-penalty {grammarPenalty}"
                : string.Empty;
            string args = $"-m \"{modelArg}\"{grammarArg} -f \"{fileArg}\" -l el";
            Debug.Log($"[Whisper] Executing: {whisperExePath} {args}");
            var psi = new ProcessStartInfo
            {
                FileName = whisperExePath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            var sb = new StringBuilder();
            using (var proc = new Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) lock (sb) sb.AppendLine(e.Data); };
                proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) lock (sb) sb.AppendLine(e.Data); };
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
                return sb.ToString();
            }
        }, token);
    }

     private static readonly Dictionary<string, string> NumberMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        {"0","0"}, {"μηδέν","0"},
        {"1","1"}, {"ένα","1"},
        {"2","2"}, {"δύο","2"},
        {"3","3"}, {"τρία","3"},
        {"4","4"}, {"τέσσερα","4"}, {"τεσσερα","4"},
        {"5","5"}, {"πέντε","5"}, {"πεντε","5"},
        {"6","6"}, {"έξι","6"}, {"εξι","6"},
        {"7","7"}, {"εφτά","7"}, {"επτά","7"},
        {"8","8"}, {"οκτώ","8"}, {"οκτω","8"},
        {"9","9"}, {"εννέα","9"}, {"εννεα","9"},
        {"10","10"}, {"δέκα","10"}, {"δεκα","10"},
        {"11","11"}, {"έντεκα","11"}, {"εντεκα","11"},
        {"12","12"}, {"δώδεκα","12"}, {"δουδεκα","12"},
        {"13","13"}, {"δεκατρία","13"}, {"δεκατρια","13"},
        {"14","14"}, {"δεκατέσσερα","14"}, {"δεκατεσσερα","14"},
        {"15","15"}, {"δεκαπέντε","15"}, {"δεκαπεντε","15"},
        {"16","16"}, {"δεκαέξι","16"}, {"δεκαεξι","16"},
        {"17","17"}, {"δεκαεπτά","17"}, {"δεκαεφτα","17"},
        {"18","18"}, {"δεκαοκτώ","18"}, {"δεκαοκτω","18"},
        {"19","19"}, {"δεκαεννιά","19"}, {"δεκαεννια","19"},
        {"20","20"}, {"είκοσι","20"}, {"εικοσι","20"},
        {"30","30"}, {"τριάντα","30"}, {"τριαντα","30"},
        {"40","40"}, {"σαράντα","40"}, {"σαραντα","40"},
        {"50","50"}, {"πενήντα","50"}, {"πενηντα","50"},
        {"60","60"}, {"εξήντα","60"}, {"εξηντα","60"},
        {"70","70"}, {"εβδομήντα","70"}, {"εβδομηντα","70"},
        {"80","80"}, {"ογδόντα","80"}, {"ογδοντα","80"},
        {"90","90"}, {"ενενήντα","90"}, {"ενενηντα","90"}
    };

    private string PreprocessTranscript(string transcript)
{
    // 1) Remove accents so “Μηδέν” or “Νηδέν” both become “Μηδεν”
    transcript = transcript
        .Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
        .Aggregate("", (s, c) => s + c);
    
    // 2) Split on ANY non-alphanumeric (dots, commas, spaces, etc.)
    var rawTokens = Regex.Split(transcript.ToLowerInvariant(), @"[^\p{L}\p{N}]+")
                         .Where(t => t.Length > 0);

    var digitTokens = new List<string>();

    foreach (var tok in rawTokens)
    {
        // a) Known Greek number-word?
        if (NumberMap.TryGetValue(tok, out var val))
        {
            digitTokens.Add(val);
        }
        // b) Pure digits?  (e.g. “123”)
        else if (Regex.IsMatch(tok, @"^\d+$"))
        {
            digitTokens.Add(tok);
        }
        // c) Mixed token like “4.59.5”: strip non-digits, then split into single chars
        else if (tok.Any(char.IsDigit))
        {
            var digitsOnly = Regex.Replace(tok, @"\D", "");
            digitTokens.AddRange(digitsOnly.Select(c => c.ToString()));
        }
        // else: ignore
    }

    // 3) Flatten into one long digit string
    var allDigits = string.Concat(digitTokens);

    if (allDigits.Length < 5)
        return ""; // we’ll catch this later as “could not parse”

    // 4) First 4 digits → ID, rest → grade
    var id = allDigits.Substring(0, 4);
    var grade = allDigits.Substring(4);

    return $"{id} βαθμός {grade}";
}

    private void ProcessTranscript(string transcript)
    {
        if (excelLoader == null || !excelLoader.IsLoaded)
        {
            Debug.LogError("Please open an Excel file first!");
            statusText.text = "Error: No file loaded";
            return;
        }
       

        var cleaned = PreprocessTranscript(transcript);
        var match = Regex.Match(cleaned, @"^(\d{4})\s+βαθμός\s+(\d{1,3})$");
        if (!match.Success)
        {
            Debug.LogWarning($"Could not parse command: '{cleaned}'");
            return;
        }
        var id    = match.Groups[1].Value;
        var grade = match.Groups[2].Value;

        int rowIndex = excelLoader.FindRowById(id);
        if (rowIndex < 0)
        {
            Debug.LogError($"ID '{id}' not found in column 0.");
            return;
        }

        excelLoader.UpdateCell(rowIndex, 7, grade);
        Debug.Log($"Set grade for ID {id} to {grade}.");
    }

    private void ShowError(string message)
    {
        Debug.LogError(message);
        statusText.text = $"Error: {message}";
    }
}

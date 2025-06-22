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
using SimpleFileBrowser;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;


public class SpeechToTextManager : MonoBehaviour
{

    private enum Mode
    {
        SelectingEntry,
        RecordingGrade
    }
    [Header("UI References")]
    [SerializeField] private Button recordButton;
    [SerializeField] private TMP_Text recordButtonText;
    [SerializeField] private TMP_Text statusText;

    [Header("Recording Settings")]
    [SerializeField] private int sampleRate = 16000;
    [SerializeField] private int maxDurationSec = 60;

    [Header("Whisper Paths & Settings")]
    [SerializeField] private string grammarFileName = "grades.gbnf";
    [SerializeField][Range(0, 200)] private int grammarPenalty = 100;

    [Header("Excel Integration")]
    [SerializeField] private ExcelLoader excelLoader;

    [Header("UI References (new)")]
    [SerializeField] private GameObject resultsPanel;        // parent panel for the scroll-view
    [SerializeField] private RectTransform resultsContent;   // content under a ScrollRect
    [SerializeField] private GameObject resultItemPrefab;    // a Button + TMP_Text for each candidate

    [Header("Model Selection (optional)")]
    [SerializeField] private Button selectModelButton;     // “Choose Model…” button
    [SerializeField] private TMP_Text modelPathText;       // displays current model path

    [Header("Localized Messages")]
    [SerializeField] private LocalizedString transcribingMessage;
    [SerializeField] private LocalizedString selectMessage;


    private string whisperExePath;
    private string modelBinPath;
    private string grammarFilePath;

    private bool isRecording;
    private AudioClip clip;
    private CancellationTokenSource cts;
    private Mode currentMode = Mode.SelectingEntry;
    private int selectedRowIndex = -1;
    private string currentStatusKey = "Idle"; // or "Listening", etc.




    private void Awake()
    {
        InitializeWhisperPaths();
        InitializeGrammarFile();
    }

    private void Start()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        selectMessage.StringChanged += OnSelectStringChanged;

        recordButton.onClick.AddListener(OnRecordButtonClicked);
        // hide result list at start
        resultsPanel.SetActive(false);
        UpdateUI();
        // show default model path
        modelPathText.text = Path.GetFileName(modelBinPath);

        // wire up the user-override button
        selectModelButton.onClick.AddListener(OnSelectModelClicked);
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnRecordButtonClicked();
        }
    }

    private void OnSelectStringChanged(string localized)
    {
        if (currentStatusKey == "select") statusText.text = localized;
    }

    private string L(string key)
    {
        var table = LocalizationSettings.StringDatabase.GetTable("SpeechToTextUI");
        if (table != null && table.GetEntry(key) != null)
            return table.GetEntry(key).GetLocalizedString();
        return key; // fallback
    }

    private void OnLocaleChanged(Locale newLocale)
    {
        UpdateUI(); // Will refresh status & button text based on new language
    }


    private void OnSelectModelClicked()
    {
        // ensure only .bin model files are shown
        FileBrowser.SetFilters(true, new FileBrowser.Filter("Whisper Models", ".bin"));
        FileBrowser.SetDefaultFilter(".bin");

        FileBrowser.ShowLoadDialog(
            paths =>
            {
                string chosen = paths[0];
                if (File.Exists(chosen))
                {
                    modelBinPath = chosen;
                    modelPathText.text = Path.GetFileName(chosen);
                    Debug.Log($"[Whisper] User selected model: {chosen}");
                }
            },
            () => Debug.Log("[Whisper] Model selection canceled"),
            FileBrowser.PickMode.Files,
            false,
            null,
            null,
            "Select Whisper Model",
            "Use"
        );
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
        recordButtonText.text = isRecording ? L("stop") : L("start");
        statusText.text = isRecording ? L("listening") : L("idle");
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
            ShowError("mic_missing");
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
            ShowError("no_audio");
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
        statusText.text = "saving";
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
    currentStatusKey = "Transcribing...";
    transcribingMessage.StringChanged += value =>
    {
        if (currentStatusKey == "Transcribing...")
            statusText.text = value;
    };
    transcribingMessage.RefreshString();

    try
    {
        string output = await RunWhisperProcessAsync(wavPath, cts.Token);
        var lines = new List<string>();
        foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var m = Regex.Match(line, @"^\[\d{2}:\d{2}:\d{2}\.\d+\s*-->\s*\d{2}:\d{2}:\d{2}\.\d+\]\s*(.+)$");
            if (m.Success) lines.Add(m.Groups[1].Value.Trim());
        }
        string clean = string.Join(" ", lines);
        statusText.text = "done";
        Debug.Log($"[Whisper] {clean}");
        ProcessTranscript(clean);
    }
    catch (OperationCanceledException)
    {
        statusText.text = "cancel";
        currentStatusKey = "Idle";
    }
    catch (Exception e)
    {
        ShowError("transcription_failed", e.Message);
        currentStatusKey = "Idle";
    }
    finally
    {
        // ---- DELETE TEMP FILE ----
        try
        {
            if (File.Exists(wavPath))
                File.Delete(wavPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Could not delete temp WAV: {ex.Message}");
        }
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
        {"κομμα", "."}, {"κομα", "."}, {"κωμμα", "."}, {"κωμα", "."}, {"κόμμα", "."}, {"κόμα", "."}, {"κώμμα", "."}, {"κώμα", "."},
        {",", "."}, {".", "."},
        { "0","0"}, {"μηδέν","0"}, {"μηδεν","0"},
        {"1","1"}, {"ένα","1"}, {"ενα","1"},
        {"2","2"}, {"δύο","2"}, {"δυο","2"}, {"βιώ","2"}, {"βιο","2"}, {"βιω", "2"},
        {"3","3"}, {"τρία","3"}, {"τρια","3"}, {"τρι", "3"}, {"τριε", "3"},
        {"4","4"}, {"τέσσερα","4"}, {"τεσσερα","4"}, {"τεσσερις","4"}, {"τεσερα","4"}, 
        {"5","5"}, {"πέντε","5"}, {"πεντε","5"},
        {"6","6"}, {"έξι","6"}, {"εξι","6"}, {"εκσι","6"}, {"έκσι","6"},
        {"7","7"}, {"εφτά","7"}, {"επτά","7"}, {"επτα","7"}, {"εφτα","7"}, {"ευτα","7"},
        {"8","8"}, {"οκτώ","8"}, {"οκτω","8"}, {"οχτώ", "8"}, {"οχτω","8"},
        {"9","9"}, {"εννέα","9"}, {"εννεα","9"}, {"εννια","9"}, {"εννιά","9"}, {"ενιά","9"}, {"ενια","9"},
        {"10","10"}, {"δέκα","10"}, {"δεκα","10"},
        {"11","11"}, {"έντεκα","11"}, {"εντεκα","11"},
        {"12","12"}, {"δώδεκα","12"}, {"δουδεκα","12"}, {"δωδεκα","12"},
        {"13","13"}, {"δεκατρία","13"}, {"δεκατρια","13"},
        {"14","14"}, {"δεκατέσσερα","14"}, {"δεκατεσσερα","14"},
        {"15","15"}, {"δεκαπέντε","15"}, {"δεκαπεντε","15"},
        {"16","16"}, {"δεκαέξι","16"}, {"δεκαεξι","16"},
        {"17","17"}, {"δεκαεπτά","17"}, {"δεκαεφτα","17"}, {"δεκαεπτα","17"}, {"δεκαεφτά", "17"},
        {"18","18"}, {"δεκαοκτώ","18"}, {"δεκαοκτω","18"}, {"δεκαοχτώ","18"}, {"δεκαοχτω","18"},
        {"19","19"}, {"δεκαεννιά","19"}, {"δεκαεννια","19"}, {"δεκαεννέα","19"}, {"δεκαεννεα","19"},
        {"20","20"}, {"είκοσι","20"}, {"εικοσι","20"},
        {"30","30"}, {"τριάντα","30"}, {"τριαντα","30"}, {"τριάτα","30"}, {"τριατα","30"},
        {"40","40"}, {"σαράντα","40"}, {"σαραντα","40"},
        {"50","50"}, {"πενήντα","50"}, {"πενηντα","50"},
        {"60","60"}, {"εξήντα","60"}, {"εξηντα","60"},
        {"70","70"}, {"εβδομήντα","70"}, {"εβδομηντα","70"},
        {"80","80"}, {"ογδόντα","80"}, {"ογδοντα","80"},
        {"90","90"}, {"ενενήντα","90"}, {"ενενηντα","90"}
    };

    /// <summary>
/// Turns a transcript of Greek number‐words (and mixed digits) into one digit string.
/// E.g. "ένα τριάντα εφτά" => "137", "2.30.1" => "2301"
/// </summary>
    private string ExtractIdDigits(string transcript)
    {
        // 1) strip accents
        transcript = transcript
        .Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
        .Aggregate("", (s, c) => s + c);

        // 2) split on anything but letters, digits or dot
        var tokens = Regex
        .Split(transcript.ToUpperInvariant(), @"[^\p{L}\p{N}\.]+")
        .Where(t => t.Length > 0);

        var digitChars = new List<string>();
        foreach (var tok in tokens)
        {
            // A) exact digits or decimals → strip non‐digits
            if (Regex.IsMatch(tok, @"^\d+(\.\d+)?$"))
            {
                // remove dots for ID
                digitChars.AddRange( tok.Where(char.IsDigit).Select(c=>c.ToString()) );
            }
            // B) number‐words → map via NumberMap
            else if (NumberMap.TryGetValue(tok, out var val))
            {
                digitChars.Add(val);
            }
        }

        return string.Concat(digitChars);
    }


    private void ProcessTranscript(string transcript)
    {
        currentStatusKey = "Idle";
        if (currentMode == Mode.SelectingEntry)
            HandleSelectionTranscript(transcript);
        else
            HandleGradeTranscript(transcript);
    }

    private void HandleSelectionTranscript(string transcript)
    {
        // 1) strip all punctuation except dot & whitespace, uppercase:
        var clean = Regex
            .Replace(transcript, @"[^\p{L}\p{N}\s\.]", " ")
            .Trim()
            .ToUpperInvariant();

        // 2) extract any digits or number-words from the cleaned string:
        var idQuery = ExtractIdDigits(transcript);
        idQuery = Regex.Replace(idQuery, @"\D+", "");
        List<int> matches;

        if (!string.IsNullOrEmpty(idQuery))
        {
            // exact/partial
            matches = excelLoader.FindRowsByPartialId(idQuery);
            // if none, use fuzzy with our looser threshold
            if (matches.Count == 0)
                matches = excelLoader.FindRowsByFuzzyId(idQuery);
        }
        else
        {
            // name lookup…
            matches = excelLoader.FindRowsBySurname(clean);
            // if none, use fuzzy with our looser threshold
        }



        if (matches.Count == 0)
        {
            ShowError("no_results", clean);
            return;
        }

        // populate the scroll view
        // Clear out old items
        foreach (Transform t in resultsContent)
            Destroy(t.gameObject);

        // Log how many we found
        Debug.Log($"[Results] Found {matches.Count} candidate rows");

        // For each match…
        foreach (var row in matches)
        {
            // Instantiate as a child with local transform reset
            var go = Instantiate(resultItemPrefab, resultsContent, false);
            go.SetActive(true);
            var entry = go.GetComponent<ResultEntry>();
            entry.RowIndex = row;

            // Fill in the text
            var txt = go.GetComponentInChildren<TMP_Text>();
            string id = excelLoader.GetCellValue(row, excelLoader.IdColumnIndex);
            string name = excelLoader.GetCellValue(row, excelLoader.NameColumnIndex);
            txt.text = $"{id} – {name}";

            // Hook up the button (clear old listeners first)
            var btn = go.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnMatchSelected(row));
        }

        resultsPanel.SetActive(true);
        currentStatusKey = "select";
        selectMessage.RefreshString();
    }

    /// <summary>
/// Fuzzy‐searches only the first word (surname) in the name column.
/// </summary>
        


    private void OnMatchSelected(int rowIndex)
{
    selectedRowIndex = rowIndex;
    // …do NOT hide the panel any more!

    // highlight the chosen one, lock the rest
    foreach (Transform child in resultsContent)
    {
        var entry = child.GetComponent<ResultEntry>();
        var btn   = child.GetComponent<Button>();
        if (entry.RowIndex == rowIndex)
        {
            // highlight
            btn.image.color       = Color.yellow;
            btn.interactable      = true;
        }
        else
        {
            // lock out
            btn.interactable      = false;
        }
    }

    // switch to grade‐recording mode
    currentMode = Mode.RecordingGrade;
    isRecording = false;
    StartRecording();
    isRecording = true;
    UpdateUI();
}



    private void HandleGradeTranscript(string transcript)
    {
        var grade = ExtractGradeOnly(transcript);
        Debug.Log($"[Debug] extracted grade: '{grade}'");

        if (string.IsNullOrEmpty(grade))
        {
            statusText.text = "tryagain";
            // stay in RecordingGrade so the next Record click will try again
            return;
        }

        excelLoader.UpdateCell(selectedRowIndex, excelLoader.GradeColumnIndex, grade);
        // un‐highlight & re‐enable all entries
        foreach (Transform child in resultsContent)
        {
            var btn = child.GetComponent<Button>();
            btn.image.color = Color.white;
            btn.interactable = true;
        }

        // reset selection state
        currentMode = Mode.SelectingEntry;
        selectedRowIndex = -1;

        statusText.text = $"{L("wrote")} {grade}";
        Debug.Log($"[Whisper] Set grade for row {selectedRowIndex} to {grade}");

        currentMode = Mode.SelectingEntry;
        selectedRowIndex = -1;
        foreach (Transform child in resultsContent)
            Destroy(child.gameObject);
    }


    /// <summary>
    /// Cleans & returns the first numeric token (integer or decimal) from the transcript,
    /// or null if none found.
    /// </summary>
    private string ExtractGradeOnly(string transcript)
{
    // 0) first look for an explicit digit‐dot‐digit or digit‐comma‐digit sequence:
    //    e.g. “3.2”, “3,2”, “3 , 2”
    var m0 = Regex.Match(transcript, @"\b(\d+)[\.,]\s*(\d+)\b");
    if (m0.Success)
        return $"{m0.Groups[1].Value}.{m0.Groups[2].Value}";

    // 0.1) then look for two number‐words separated by comma/period:
    //     e.g. “τέσσερα, τρία” or “τρία. δύο”
    var mw = Regex.Match(transcript, @"\b([^\d\W]+)[\.,]\s*([^\d\W]+)\b");
    if (mw.Success)
    {
        var w1 = NormalizeToken(mw.Groups[1].Value);
        var w2 = NormalizeToken(mw.Groups[2].Value);
        if (NumberMap.TryGetValue(w1, out var ip) && NumberMap.TryGetValue(w2, out var dp))
            return $"{ip}.{dp}";
    }

    // 1) strip accents
    transcript = transcript
      .Normalize(NormalizationForm.FormD)
      .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
      .Aggregate("", (s, c) => s + c);

    // …and then your remaining steps (punctuation removal, ΚΟΜΜΑ splitting, fallback, etc.)…



    // 2) remove unwanted punctuation
    transcript = Regex.Replace(transcript, @"[^\p{L}\p{N}\.\s]+", " ");

    // 3) uppercase
    transcript = transcript.ToUpperInvariant();

    // 3.5) isolate ΚΟΜΜΑ/ΚΟΜΑ when glued to words
    transcript = Regex.Replace(transcript,
        @"(?<=\p{L})(ΚΟΜΜΑ|ΚΟΜΑ)(?=\p{L})",
        " $1 ");

    // 4) split on whitespace
    var rough = transcript
        .Split(new[]{' ','\t','\n','\r'}, StringSplitOptions.RemoveEmptyEntries);

    // 5) break apart any remaining glued ΚΟΜΜΑ+word tokens
    var tokens = new List<string>();
    foreach (var t in rough)
    {
        var m = Regex.Match(t, @"^(ΚΟΜΜΑ|ΚΟΜΑ)(.+)$");
        if (m.Success)
        {
            tokens.Add(m.Groups[1].Value);
            tokens.Add(m.Groups[2].Value);
        }
        else tokens.Add(t);
    }

    // 6) split on non‐letter/digit/dot to get rawTokens
    var rawTokens = tokens
        .SelectMany(t => Regex.Split(t, @"[^\p{L}\p{N}\.]+"))
        .Where(t => t.Length > 0)
        .ToList();

    // 7) look for number • ΚΟΜΜΑ • number
    for (int i = 0; i < rawTokens.Count - 2; i++)
    {
        var w1 = NormalizeToken(rawTokens[i]);
        var w2 = NormalizeToken(rawTokens[i + 1]);
        var w3 = NormalizeToken(rawTokens[i + 2]);

        if ((w2 == "ΚΟΜΜΑ" || w2 == "ΚΟΜΑ")
            && NumberMap.TryGetValue(w1, out var intPart)
            && NumberMap.TryGetValue(w3, out var decPart))
        {
            return $"{intPart}.{decPart}";
        }
    }

    // 8) fallback single‐token
    foreach (var raw in rawTokens)
    {
        var tok = NormalizeToken(raw);
        if (NumberMap.TryGetValue(tok, out var val))
            return val;
        if (Regex.IsMatch(tok, @"^\d+(\.\d+)?$"))
            return tok;
    }

    return null;
}



/// <summary>
/// Strip non-alphanumeric from ends, e.g. "'3." -> "3"
/// </summary>
private string NormalizeToken(string t)
{
    return Regex.Replace(t, @"^[^\p{L}\p{N}]+|[^\p{L}\p{N}]+$", "");
}





    private void ShowError(string key, string arg = null)
    {
        string message;

        var stringTable = LocalizationSettings.StringDatabase.GetTable("SpeechToTextUI");
        if (stringTable != null && stringTable.GetEntry(key) != null)
        {
            var entry = stringTable.GetEntry(key);
            message = LocalizationSettings.StringDatabase.GetLocalizedString("SpeechToTextUI", key);

            if (!string.IsNullOrEmpty(arg))
                message = string.Format(message, arg);
        }
        else
        {
            // fallback if key is missing
            message = !string.IsNullOrEmpty(arg) ? arg : key;
        }

        Debug.LogError(message);
        statusText.text = $"{GetLocalized("error")}: {message}";
    }
    private string GetLocalized(string key)
    {
        return LocalizationSettings.StringDatabase.GetLocalizedString("SpeechToTextUI", key);
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        transcribingMessage.StringChanged -= value => statusText.text = value;
        selectMessage.StringChanged -= OnSelectStringChanged;
    }

}

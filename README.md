Speak2Sheet - Voice-Activated Grading Interface

https://img.shields.io/badge/License-MIT-yellow.svg
https://img.shields.io/badge/Platform-Windows%2520%257C%2520Linux-blue.svg
![Unity](https://img.shields.io/badge/Unity-6000.0.48f1 LTS-black.svg)

A cross-platform desktop application that enables educators to enter student grades using voice commands, directly interfacing with Excel files through offline speech recognition.

    📖 Academic Thesis: This project was developed as part of my Bachelor's Thesis at University of Thessaly. Read the full thesis here

✨ Features

    🎤 Offline Speech Recognition: Powered by OpenAI's Whisper.cpp for privacy-focused, internet-free operation

    📊 Direct Excel Integration: Read/write .xls/.xlsx files without Microsoft Office installation

    🌐 Bilingual Support: Full Greek and English language support with real-time switching

    🔍 Smart Student Matching: Approximate string matching with Levenshtein distance for fuzzy student lookup

    💾 Auto-Save & Undo: Automatic saving and undo functionality to prevent data loss

    🎯 Customizable Interface: Dynamic column mapping and display preferences

    🖥️ Cross-Platform: Native builds for Windows and Linux

🚀 Quick Start
Download & Install

    Download the latest release from the Releases page

    Choose your platform:

        Windows: Download Speak2Sheet_Windows.zip, extract and run Speak2Sheet.exe

        Linux: Download Speak2Sheet_Linux.zip, extract and run the executable

System Requirements

    Windows 10/11 (64-bit) or Linux (64-bit)

    Microphone for voice input

    4GB RAM minimum, 8GB recommended

    500MB disk space for application and speech models

🎮 How to Use
Basic Workflow

    Open Excel File: Click "Open Excel" and select your grade sheet (.xls or .xlsx)

    Configure Columns: Set which columns contain Student IDs, Names, and Grades

    Find Student by Voice:

        Click "Start Recording"

        Say the student's ID or name (e.g., "three two zero zero one" or "Papadopoulos")

        Select the correct match from results

    Enter Grade by Voice:

        Speak the grade (e.g., "eight point five" or "οχτώ κόμμα πέντε")

        The grade is automatically entered into the correct cell

    Save: Changes are auto-saved or manually saved with the Save button

Voice Command Examples
Action	English	Greek
Student ID	"three two zero zero one"	"τρία δύο μηδέν μηδέν ένα"
Grade	"seven point five"	"επτά κόμμα πέντε"
Student Name	"Maria"	"Μαρία"
🛠️ For Developers
Building from Source
bash

# Clone the repository
git clone https://github.com/retr0sity/speak2sheet.git
cd speak2sheet

# Open in Unity 6000.0.48f1 LTS or later

Dependencies

    Unity 6000.0.48f1 LTS

    NPOI: Excel file manipulation

    ExcelDataReader: Fast Excel file loading

    Whisper.cpp: Offline speech recognition

    Unity Localization: Bilingual UI support

Project Structure
text

Speak2Sheet/
├── Scripts/
│   ├── ExcelLoader.cs          # Excel file management
│   ├── SpeechToTextManager.cs  # Voice recognition handling
│   ├── ColumnSettingsUIManager.cs # UI configuration
│   └── AutoSaveController.cs   # Data persistence
├── Prefabs/                   # UI components
├── Localization/              # Greek/English text assets
└── StreamingAssets/           # Whisper models

🎯 Technical Highlights
Speech Recognition Pipeline

    Offline Processing: All audio processed locally using Whisper.cpp

    Dual Language Models: Optimized for both Greek and English speech

    Real-time Processing: <3 second response time for typical inputs

Excel Integration

    Format Preservation: Maintains Excel formatting and formulas

    Large File Support: Efficient handling of 100,000+ row spreadsheets

    Streaming Architecture: Low memory footprint during operation

Smart Matching Algorithm
csharp

// Levenshtein distance-based fuzzy matching
int dynamicMax = Math.Min(6, Math.Max(1, (int)Math.Ceiling(query.Length * 0.5)));
// Adaptive threshold based on input length for optimal accuracy

📊 Performance
Metric	Result
Speech Recognition Accuracy	>85% (Greek/English)
File Load Time	<2s (100k rows)
Voice Processing	<3s (10s audio)
Memory Usage	<1GB typical
🚧 Future Enhancements

    Mobile App Version (Android/iOS)

    Cloud Storage Integration (Google Drive, OneDrive)

    Advanced Analytics Dashboard

    Multi-worksheet Support

    Additional Language Support

🤝 Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for:

    Bug fixes

    New features

    Additional language support

    Performance improvements

📜 License

This project is licensed under the MIT License - see the LICENSE file for details.
📚 Academic Reference

If you use this software in academic work, please cite:
bibtex

@thesis{karkalas2025speak2sheet,
  title={Voice-based Grading Interface},
  author={Karkalas, Ioannis},
  year={2025},
  institution={University of Thessaly},
  url={https://ir.lib.uth.gr/handle/11615/86513}
}

👨‍💻 Author

Ioannis Karkalas

    GitHub: @retr0sity

    Thesis: University of Thessaly Institutional Repository

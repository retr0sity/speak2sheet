# Speech2Sheet – Voice-Based Grading Interface

A cross-platform (Windows/Linux) application that enables **voice-controlled grading** using **offline speech recognition** and **direct Excel file manipulation**.  
This project was developed as part of my **Bachelor Thesis** in the Digital Systems Department, University of Thessaly.

📄 **Official Thesis PDF:**  
https://ir.lib.uth.gr/xmlui/bitstream/handle/11615/86513/32043.pdf?sequence=4

---

## 📌 Overview

**Speech2Sheet** allows educators to record grades using only their voice—without internet access—while automatically updating Excel spreadsheets.  
The system aims to reduce grading time, minimize human errors, and provide an accessible workflow for educators managing large student datasets.

The application supports:

- Offline speech recognition (Greek & English)
- Real-time Excel editing inside Unity
- Automatic saving & undo history
- Student matching using approximate string matching (Levenshtein)
- Unity-based dynamic grid UI
- Cross-platform builds (Windows & Linux)

---

## ✨ Features

- 🎤 **Offline Speech-to-Text** using Whisper.cpp  
- 🇬🇷🇬🇧 **Bilingual Input & Interface** (Greek + English)  
- 📊 **Live Excel Grid** generated dynamically  
- 🔎 **Approximate student search** using Levenshtein distance  
- 📝 **Voice-controlled grade entry**  
- ↩️ **Undo stack** for safe editing  
- 💾 **Auto-Save + Manual Save** options  
- 📁 **Built-in file browser** (Excel + Whisper model selection)  
- 🖥️ **Works on Windows & Linux**  

---

## 🛠️ Technologies Used

| Component | Technology |
|----------|------------|
| Engine | Unity 6000.0.48f1 LTS |
| Speech Recognition | Whisper.cpp |
| Excel Reading | ExcelDataReader |
| Excel Writing | NPOI |
| UI Framework | UI Toolkit, UGUI, TextMeshPro |
| File Dialog | Simple File Browser |
| String Matching | Levenshtein Distance |
| Language Support | Unity Localization |
| Programming Language | C# |

---

## 📦 Installation

### 1. Download the Latest Release

Download the current executable build from the GitHub Releases page:

👉 **[Download Latest Release](PUT_YOUR_RELEASE_LINK_HERE)**

### 2. Extract the Folder

Unzip the downloaded `.zip` file.

### 3. Run the Application

**Windows:**  
Run `Speech2Sheet.exe`

**Linux:**  
```bash
chmod +x Speech2Sheet.x86_64
./Speech2Sheet.x86_64
```

No installation required.  
No internet connection required.

---

## 🚀 Usage Guide

### 1. Open Excel File

Load an `.xls` or `.xlsx` file containing:
- Student IDs  
- Names  
- Grades  

The grid will appear automatically.

---

### 2. Configure Columns

In **Settings**, choose:
- Which column is Student ID  
- Which column is Name  
- Which column is Grade  
- Which columns you want displayed in the grid  

---

### 3. Load Whisper Model

Select a `.bin` Whisper model such as:
- `tiny.bin`
- `base.bin`
- `medium.bin`

Heavier models provide better accuracy.

---

### 4. Start Voice Recording

Press **Start Recording** to:
- Identify a student (e.g., “Παπαδόπουλος” or “3120052”)
- Dictate a grade (e.g., “επτά κόμμα πέντε” → 7.5)

The system will:
1. Transcribe your speech  
2. Match the student using approximate string matching  
3. Insert the grade into the Excel grid  

---

### 5. Saving

- Use **Save** to write the updated file back to disk  
- Or enable **Auto-Save** to save automatically after every change  

---

## 🧠 System Architecture

The application is built with a modular design.

### Core Modules

- **ExcelLoader**  
  Handles reading, displaying, updating, and saving Excel files.

- **SpeechToTextManager**  
  Handles microphone recording, Whisper CLI execution, regex parsing, token processing, and number extraction.

- **ColumnSettingsUIManager**  
  Manages dropdowns/toggles for choosing ID/Name/Grade columns.

- **AutoSaveController**  
  Triggers automatic saving after grid modifications.

- **LanguageToggle**  
  Switches between English and Greek through Unity Localization.

### Algorithms Used

- Regex parsing  
- Word-to-number mapping (Greek & English)  
- Levenshtein distance for approximate name/ID matching  
- Dynamic thresholding for fuzzy search  
- Grid-building with prefabs  

---

## 📚 Thesis Reference

This repository is based on the bachelor thesis:

**“Φωνητική Διεπαφή Καταχώρισης Βαθμολογιών – Voice-Based Grading Interface”**  
University of Thessaly, 2025  

📄 PDF Link:  
https://ir.lib.uth.gr/xmlui/bitstream/handle/11615/86513/32043.pdf?sequence=4

---

## 🤝 Acknowledgements

- Supervisor: **Fotios Kokkoras**, Assistant Professor  
- University of Thessaly — Digital Systems Department  

---

## 📜 License

Released under the **MIT License**.

---

## 📬 Contact

**Developer:** Ioannis Karkalas  
For questions or suggestions, please open an Issue on GitHub.

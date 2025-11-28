Speech2Sheet: Voice-Based Grading Interface

Speech2Sheet is a cross-platform tool developed to streamline the academic grading process. It combines offline speech recognition (OpenAI Whisper) with direct Excel file manipulation to reduce the time and administrative burden of manual grade entry.

This application was developed as part of a Bachelor Thesis at the University of Thessaly, Department of Digital Systems.

📖 Table of Contents

About the Project

Key Features

System Architecture

Installation

How to Use

Settings

Academic Details

🧐 About the Project

Traditional grading involves repetitive manual data entry into spreadsheets or web forms, a process prone to errors and time-consuming for educators. Speech2Sheet offers a unified workspace where instructors can:

Dictate student names or IDs to find them in a list.

Dictate grades (integers or decimals).

Edit spreadsheets in real-time without needing Microsoft Office installed.

The system runs entirely offline, ensuring data privacy and usability in environments without stable internet access.

✨ Key Features

🎙️ Offline Speech Recognition: Powered by Whisper.cpp (OpenAI Whisper), supporting both English and Greek.

📊 Direct Excel Manipulation: Reads and writes .xls and .xlsx files directly using NPOI and ExcelDataReader. No MS Office installation required.

🔍 Fuzzy Search: Uses Levenshtein distance algorithms to find students even if the pronunciation isn't perfect (Approximate String Matching).

⚡ Dynamic Grid: View and edit specific columns (ID, Name, Grade) in a responsive UI.

🛡️ Safety Features: Includes an Undo Stack and configurable Auto-Save.

🌍 Bilingual UI: Fully localized interface with instant switching between English and Greek.

🛠 System Architecture

The application is built using a modular architecture within the Unity Engine (LTS 6000.0.48f1).

Component

Technology

Purpose

Core Engine

Unity

UI Toolkit, Cross-platform build target.

ASR Engine

Whisper.cpp

Native C++ plugin for offline, high-performance speech-to-text.

Data I/O

ExcelDataReader

Fast, stream-based reading of Excel files.

Data Manipulation

NPOI

Writing and saving changes to .xls/.xlsx files.

UI/UX

Unity UI / TMP

Dynamic grid generation and responsive layout.

📥 Installation

Pre-requisites

Windows: Windows 10 or 11 (64-bit).

Linux: A standard 64-bit distribution.

Microphone: A functional input device.

Steps

Go to the [suspicious link removed] page.

Download the .zip file corresponding to your operating system.

Extract the archive.

Run the executable:

Windows: Speech2Sheet.exe

Linux: ./Speech2Sheet.x86_64

🚀 How to Use

Open File: Click Open Excel File and select your grading spreadsheet.

Start Recording: Click Start Recording.

Find Student: Speak the Student ID (e.g., "3120052") or Name (e.g., "Karkalas").

The system will display a list of matching students based on fuzzy logic.

Select Entry: Click on the correct student from the results list.

Dictate Grade: Speak the grade (e.g., "Eight point five" or "Οκτώ μισό").

The system automatically parses the number and updates the cell.

Save: Click Save to write changes to disk (or enable Auto-Save).

⚙ Settings

Click the Settings button to configure:

Language: Toggle between English and Greek flags.

Column Mapping: Map which columns in your Excel file correspond to ID, Name, and Grade.

Visible Columns: Toggle which columns should be visible in the UI grid.

AI Model: Swap the Whisper model (e.g., tiny, base, medium) to balance speed vs. accuracy.

Auto-Save: Toggle automatic saving after every change.

🎓 Academic Details

This software was developed as a Bachelor Thesis.

Thesis Title: Voice-based Grading Interface (Φωνητική Διεπαφή Καταχώρισης Βαθμολογιών)

Full Thesis: University of Thessaly Institutional Repository

Author: Ioannis Karkalas

Supervisor: Fotios Kokkoras, Assistant Professor

Institution: University of Thessaly, School of Technology, Digital Systems Department

Date: June 2025

This project is for educational and academic purposes.

# 📄 PDF Ortner Sorter

**Sortiere massenhaft gescannte PDFs mit drei Klicks – Vorschau, Auswahl, Move.**

Eine moderne WPF-Anwendung zum schnellen Sortieren und Organisieren von eingescannten PDF-Dokumenten.

---

## ✨ Features

- 🔍 **Automatische PDF-Erkennung** – Scanne Quellordner (inkl. Unterordner) und zeige alle PDFs übersichtlich an
- 🖼️ **Live-Vorschau** – Erste drei Seiten jedes PDFs werden als Thumbnails gerendert (mit intelligentem Cache)
- ☑️ **Komfortable Auswahl** – Einzelklick zum Auswählen, Shift+Klick für Bereiche, Strg+Klick für einzelne Dateien
- ✅ **Bestätigung vor dem Verschieben** – Passe den Zielordner-Namen vor dem Move an
- 💾 **Einstellungen speichern** – Quelle, Ziel und Cache-Limit werden automatisch gespeichert
- 📦 **Portable Build** – Selbstständige EXE ohne Installation (inkl. nativer Pdfium-Bibliothek)

---

## 🚀 Installation & Start

### Portable Version (empfohlen)
1. **Download** der neuesten `PDFOrtnerSorter_Portable.zip` aus dem [Releases](../../releases)-Bereich
2. **Entpacke** das Archiv in einen beliebigen Ordner
3. **Starte** `PDFOrtnerSorter.exe` – keine Installation notwendig!

### Von Quellcode bauen
**Voraussetzungen:**
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) oder neuer
- Windows 10/11

**Build-Schritte:**
```powershell
# Repository klonen
git clone https://github.com/NorphyOG/PDF-Ortner-Sorter.git
cd "PDF Ortner Sorter"

# Entwicklungsversion starten
.\run-dev.ps1

# Portable Version erstellen
.\publish-portable.ps1
```

Die portable EXE + DLL findest du dann in `PDFOrtnerSorter\bin\Portable\`.

---

## 📖 Benutzung

1. **Einstellungen öffnen** – Klicke oben auf „Einstellungen"
2. **Quellordner wählen** – Wo liegen deine gescannten PDFs?
3. **Zielordner wählen** – Wohin sollen die ausgewählten Dateien verschoben werden?
4. **Unterordnername** festlegen (z. B. `Sortierung_20251126`)
5. **Speichern** – Ordner-Einstellungen werden für die nächste Nutzung gespeichert

6. **PDFs durchsuchen** – Die App zeigt automatisch alle gefundenen PDFs mit Vorschau an
7. **Auswahl treffen** – Klicke oder nutze Strg/Shift für Mehrfachauswahl
8. **Verschieben** – Klicke „Auswahl verschieben", bestätige den Ordnernamen und fertig!

---

## 🛠️ Technologien

- **.NET 8 WPF** – Moderne Windows-Desktop-Anwendung
- **MVVM (CommunityToolkit.Mvvm)** – Saubere Trennung von UI und Logik
- **PdfiumViewer** – Native PDF-Rendering-Engine für schnelle Vorschau
- **Microsoft.Extensions.Hosting** – Dependency Injection und Service-Architektur
- **Ookii.Dialogs.Wpf** – Moderne Ordner-Auswahl-Dialoge

---

## 📁 Projektstruktur

```
PDF Ortner Sorter/
├── PDFOrtnerSorter/
│   ├── Dialogs/              # Bestätigungs- & Einstellungsfenster
│   ├── Infrastructure/       # Helper & Converter
│   ├── Models/               # Datenmodelle (AppSettings, PdfDocumentInfo, etc.)
│   ├── Services/             # Business-Logik (FileService, PreviewService, MoveService, etc.)
│   ├── ViewModels/           # MVVM ViewModels
│   ├── MainWindow.xaml       # Haupt-UI
│   └── App.xaml.cs           # DI-Container & Startup
├── publish-portable.ps1      # Skript für portable Build
├── run-dev.ps1               # Skript für Entwicklungsstart
└── README.md                 # Diese Datei
```

---

## 🐛 Bekannte Einschränkungen

- **PdfiumViewer-Warnung (NU1701):** Das Paket wurde für .NET Framework entwickelt, funktioniert aber einwandfrei unter .NET 8.
- **Single-File-Publish:** Die native `pdfium.dll` (~15 MB) muss neben der EXE liegen – sie kann nicht in die EXE eingebettet werden.

---

## 📝 Lizenz & Autor

**© 2025 PDF Ortner Sorter – made by Norphy**

Dieses Projekt steht unter der [MIT-Lizenz](LICENSE) – nutze, modifiziere und teile es frei!

---

## 🤝 Beiträge & Support

Probleme gefunden oder Ideen für neue Features? Erstelle ein [Issue](../../issues) oder sende einen Pull Request!

**Viel Spaß beim Sortieren! 🎉**

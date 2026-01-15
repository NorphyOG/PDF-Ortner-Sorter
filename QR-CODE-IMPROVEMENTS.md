# QR-Code Verbesserungen - PDF Ortner Sorter

## Änderungen

Der QR-Code wurde verbessert und enthält jetzt strukturierte JSON-Daten, die gescannt und weiterverarbeitet werden können.

### JSON-Format im QR-Code

Wenn Sie den QR-Code auf dem Etikett scannen, erhalten Sie JSON-Daten in folgendem Format:

```json
{
  "ordnerName": "Sortierung_20260114",
  "zielordner": "\\\\nora-server\\Scans\\Sortierung_20260114",
  "zeitstempel": "2026-01-14 15:39:00",
  "dokumentAnzahl": 3,
  "erstelltAm": "20260114"
}
```

### Felderbeschreibung

- **ordnerName**: Der Name des Ordners aus dem Etikettendruck-Dialog
- **zielordner**: Der vollständige Zielpfad, wohin die Dokumente verschoben wurden
- **zeitstempel**: Datum und Uhrzeit der Erstellung im Format `yyyy-MM-dd HH:mm:ss`
- **dokumentAnzahl**: Anzahl der Dokumente in diesem Ordner
- **erstelltAm**: Datum im kompakten Format `yyyyMMdd`

### Verwendung in anderen Programmen

Die JSON-Daten können einfach geparst und weiterverarbeitet werden:

#### C# Beispiel
```csharp
using System.Text.Json;

var scannedData = "... QR-Code Scan Ergebnis ...";
var data = JsonSerializer.Deserialize<LabelData>(scannedData);

Console.WriteLine($"Ordner: {data.ordnerName}");
Console.WriteLine($"Pfad: {data.zielordner}");
Console.WriteLine($"Anzahl: {data.dokumentAnzahl}");
```

#### Python Beispiel
```python
import json

scanned_data = "... QR-Code Scan Ergebnis ..."
data = json.loads(scanned_data)

print(f"Ordner: {data['ordnerName']}")
print(f"Pfad: {data['zielordner']}")
print(f"Anzahl: {data['dokumentAnzahl']}")
```

### Technische Details

- **QR-Code-Bibliothek**: QRCoder 1.6.0
- **Fehlerkorrektur-Level**: Medium (M) - kann bis zu 15% Beschädigung tolerieren
- **Format**: JSON mit kompaktem Format (keine Einrückungen für kleinere QR-Codes)
- **Fallback**: Bei Generierungsfehler wird ein Platzhalter mit "QR" Text angezeigt

## Installation

Die neue Version wurde bereits kompiliert und ist verfügbar unter:
`PDFOrtnerSorter\bin\PDFOrtnerSorter_Portable.zip`

Entpacken Sie das ZIP-Archiv und starten Sie die Anwendung. Die QR-Code-Funktionalität ist automatisch aktiviert.

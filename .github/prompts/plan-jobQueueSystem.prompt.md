# Plan: Umwandlung zu asynchronem Job-Queue-System (final)

**TL;DR:** Sequenzielle Job-Queue mit zwei Tabs (Laufend/Fertig), persistierter Job-Historie, Lazy-Load-Paginierung, Detail-Popups, Benutzer-getriggerte Retries und Completion-Sound-Benachrichtigungen für sichere Scanner-PDFs.

## Steps

### 1: Erstelle Job-Datenmodelle

- `Job` Klasse: ID, Ordnernamen, Dateienliste, Status (Running/Completed), Fortschritt%, Fehler-Liste, CreatedAt, CompletedAt, TotalBytes
- `JobStatus` Enum: Running, Completed (auch mit teilweisen Fehlern)
- `JobError` Klasse: Dateiname, Fehlertyp (Transient/Disk-Space/Permissions), Retry-Count, Timestamp

### 2: Implementiere JobQueue Service (sequenziell)

- Ein aktiver Job zur Zeit, Warteschlange für weitere
- Nutzt existierenden `MoveService` mit Progress-Tracking
- Bei File-Fehler: Klassifiziert als transient/permanent, loggt, setzt fort mit nächster Datei
- Job → Completed wenn alle Dateien verarbeitet (auch mit Fehlern)
- Benutzer kann fehlgeschlagene Dateien später manuell via Retry-Button neu verarbeiten

### 3: Erstelle JobStore für Persistierung

- JSON-basiert im AppData-Ordner, speichert alle Jobs
- Beim App-Start: Lädt Jobs, setzt "Running" auf "Completed" (mit Fehler-Marker)
- Auto-Save nach jeder Status-Änderung

### 4: Implementiere zwei-Tab-UI mit Lazy-Load-Paginierung

- **Tab "Laufend":** Zeigt aktiven Job + wartende Jobs mit Live-Fortschritt
- **Tab "Fertig":** Zeigt abgeschlossene Jobs mit Lazy-Load (10 pro Seite), "Mehr laden" Button unten
- Sortierung: Neueste zuerst
- Jeder Tab: Klick auf Job → Detail-Popup mit Dateien, Fehler, Zeitstempeln

### 5: Refaktoriere MainWindow für Zwei-Tab-System

- **Oben:** Job-Erstellungsbereich (Ordnername TextBox + Datei-Select Button + "Job erstellen" Button)
- **Unten:** TabControl (Laufend/Fertig)
- Jeder Tab: DataGrid mit Job-Liste
- "Fertig" Tab: "Mehr laden" Button unten mit Paginierungs-Counter

### 6: Update MainViewModel für Job-Orchestrierung

- `CreateJobCommand`: Validierung, Datei-Dialog, Job erstellen, Verarbeitung starten
- `RetryJobCommand`: Neustart fehlgeschlagener Job (setzt auf Running, startet Verarbeitung)
- `ShowJobDetailsCommand`: Öffnet JobDetailsWindow-Popup
- `LoadMoreJobsCommand`: Lädt nächste 10 abgeschlossene Jobs
- Background Task Loop: `JobQueue.ProcessNextAsync()` mit Cancellation
- Observable Collections: `RunningJobs`, `CompletedJobs` (mit Manual Refresh)

### 7: Erstelle JobDetailsWindow (modales Popup)

- Header: Job-ID, Ordnername, Status, CreatedAt, CompletedAt
- Section "Dateien": List mit Namen, Größe, Status (OK/Error)
- Section "Fehler" (wenn vorhanden): Tabelle mit Dateinamen, Fehlertyp, Retry-Count
- Button "Fehlerhafte Dateien wiederholen": Setzt Job neu auf Running, startet Verarbeitung

### 8: Implementiere Completion-Sound-Benachrichtigungen

- Bei Job-Completion: Spielt Sound ab (kleine `.wav`/`.mp3` aus Resources oder externe URL)
- Toast-Benachrichtigung zusätzlich (optional, je nach Design)
- Sound-Setting in SettingsWindow konfigurierbar (On/Off)

## Implementation Sequence

1. Models & Services (JobQueue, JobStore, Notification)
2. MainViewModel Refactoring (Job-Management Commands)
3. MainWindow.xaml Umstrukturierung (Zwei Tabs, Layout)
4. JobDetailsWindow erstellen
5. Integration & Testing

---

**Dieser Plan ist ready for Implementation!**

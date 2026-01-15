# Plan: Maximale Transfer-Geschwindigkeit + Moderne UI + Settings-Bugfix

Kritischer Performance-Bug (400MB in 30min→1h) wird durch 16MB-Buffer Stream-Copying mit dynamischer Parallelität behoben. Komplette UI-Modernisierung mit MaterialDesignThemes (Blau/Türkis), Smooth-Animations (200ms Updates), Echtzeit-Progress, Speed-Warnungen. Settings-Bug gefixt, neue Optionen, 3x Retry-Logik, Rollback-Mechanismus, erweitertes Logging.

## Steps

1. **Behebe Settings-Persistence-Bug** in [MainViewModel.cs](PDFOrtnerSorter/ViewModels/MainViewModel.cs#L102-L119) `InitializeAsync()`: Wrappen der Property-Zuweisungen (Zeilen 107-115) mit `_suspendSettingsPropagation = true` vor den Assignments und `false` im finally-Block um redundante/fehlerhafte Saves zu verhindern

2. **Installiere MaterialDesignThemes NuGet** und konfiguriere in [App.xaml](PDFOrtnerSorter/App.xaml): Package `MaterialDesignThemes.Wpf` (neueste Version) hinzufügen, ResourceDictionaries einbinden (`MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml` + Defaults), Primary Color auf `BaseColor="Blue"` und Secondary auf `Cyan/Teal`, Theme-Switching Support für Dark/Light Mode

3. **Ersetze `File.Move()` durch High-Performance Chunked Stream-Copying mit Rollback** in [MoveService.cs](PDFOrtnerSorter/Services/Implementations/MoveService.cs#L39-L65): Implementiere `CopyFileWithProgressAsync()` mit 16MB Buffer (const), Report nach jedem Chunk, nutze `BackgroundJobQueue` mit dynamischer Concurrency (MaxConcurrency=4 wenn Dateigröße <50MB, sonst 2), Retry-Logik mit 3 Versuchen und exponential backoff (1s, 2s, 4s), **Rollback-Mechanismus**: Bei Fehler bereits kopierte Dateien in temporäres Backup-Verzeichnis verschieben mit Option zur Wiederherstellung, nach erfolgreichem Batch Quelldateien löschen

4. **Erweitere Progress-Modell** in [MoveBatchResult.cs](PDFOrtnerSorter/Models/MoveBatchResult.cs): Neue `DetailedMoveProgress` record struct mit Properties: `long BytesTransferred`, `long TotalBytes`, `double SpeedMBps`, `TimeSpan? EstimatedTimeRemaining`, `string? CurrentFileName`, `long CurrentFileBytes`, `long CurrentFileTotalBytes`, `int CompletedFiles`, `int TotalFiles`, `bool IsSlowTransfer` (true wenn <10 MB/s)

5. **Implementiere Speed-Tracking mit 200ms Update-Frequenz und Slow-Transfer-Detection** in [MainViewModel.cs](PDFOrtnerSorter/ViewModels/MainViewModel.cs#L235-L264): Sliding-Window Queue für Byte-Samples (Timestamp + Bytes) über 2 Sekunden, berechne MB/s als Durchschnitt, Throttle UI-Updates auf 200ms mit DispatcherTimer, neue Properties: `TransferSpeedMBps`, `EstimatedTimeRemaining`, `CurrentFileProgress`, `CurrentFileName`, `BytesTransferredFormatted`, `TotalBytesFormatted`, `IsSlowTransferWarningVisible` (true wenn Speed <10 MB/s für >5 Sekunden)

6. **Erweitere Logging mit Transfer-Statistiken** in [FileLoggerService.cs](PDFOrtnerSorter/Services/Implementations/FileLoggerService.cs) und neue Methode in [IMoveService](PDFOrtnerSorter/Services/Abstractions/IMoveService.cs): Neue `LogTransferStatistics()` Methode loggt: Durchschnittsgeschwindigkeit, Peak-Speed, Gesamt-Bytes, Dauer, Fehler-Rate, erfolgreich/fehlgeschlagen Dateien, Rollback-Events, wird nach jedem Move-Batch aufgerufen

7. **Komplett neue moderne Material Design UI mit Speed-Warnung** in [MainWindow.xaml](PDFOrtnerSorter/MainWindow.xaml): MaterialDesign Cards mit `md:ElevationAssist.Elevation="Dp4"`, Top-Header kompakt mit Icon, Hauptbereich: Große **Transfer-Stats-Card** mit zwei ProgressBars (Gesamt: "Datei 3/15" + aktuelle Datei: "150MB/400MB") mit Storyboard smooth-Animation, Live-Speed als große Zahl "**120 MB/s**" mit MaterialDesign Typography, ETA-Countdown "Verbleibend: 00:02:35", **Speed-Warnung Banner** (Visibility gebunden an `IsSlowTransferWarningVisible`) mit Icon und Text "⚠️ Langsame Übertragung (<10 MB/s) - HDD-Fragmentierung oder Netzwerk-Problem möglich", kompaktes Grid-Layout (Linke Sidebar 280px, Rest für Dokumente), Blau/Türkis Color Scheme

8. **Erweitere Settings-Dialog mit Performance-Optionen** in [SettingsWindow.xaml](PDFOrtnerSorter/Dialogs/SettingsWindow.xaml), [SettingsDialogViewModel.cs](PDFOrtnerSorter/ViewModels/SettingsDialogViewModel.cs), [AppSettings.cs](PDFOrtnerSorter/Models/AppSettings.cs): Neue Settings-Properties `BufferSizeMB` (8/16/32 ComboBox, Default 16), `DynamicConcurrencyEnabled` (bool, Default true), `SmallFileSizeThresholdMB` (int, Default 50), `AutoRefreshEnabled` (bool), `ShowMoveConfirmation` (bool, Default true), `ThemeMode` enum (Light/Dark/System), `EnableRollbackOnError` (bool, Default true), `ShowSlowTransferWarning` (bool, Default true), alle Properties vollständig in Save/Load/ViewModel integriert mit MaterialDesign Controls (Switches, Sliders, ComboBoxes)

9. **Optimiere Layout für 16:9 Single-Screen ohne Scrollen** in [MainWindow.xaml](PDFOrtnerSorter/MainWindow.xaml): Kombiniere "Ordner & Batch" + "Session" zu einer kompakten Card links, "Kennzahlen" als 2x2 Grid mit MaterialDesign Icons (PDFs gefunden, Ausgewählt, Cache, Quelle), **Transfer-Stats prominent im Hauptbereich oben** (große Card über Dokumentliste mit Speed-Warnung-Banner), nur Dokumentliste scrollbar mit VirtualizingStackPanel, Rest fix auf Screen sichtbar bei 1920x1080

10. **Implementiere Etikettendruck-Funktion für nicht-scannbare Dokumente**: Neuer Button "Etiketten drucken" in Toolbar und Context-Menu, öffnet `LabelPrintDialog.xaml` mit Vorschau, zeigt Ordnername, Datum, Zielordner-Pfad, Anzahl ausgewählter Dokumente, Layout anpassbar (Etikettenformat: Avery Zweckform, Brother, Dymo), Settings in [AppSettings.cs](PDFOrtnerSorter/Models/AppSettings.cs) für Netzwerkdrucker (`LabelPrinterName`, `LabelFormat`, `AutoPrintEnabled`), neue Services: [ILabelPrintService](PDFOrtnerSorter/Services/Abstractions/ILabelPrintService.cs) und [LabelPrintService](PDFOrtnerSorter/Services/Implementations/LabelPrintService.cs) mit `PrintLabelAsync()` und `GetAvailablePrinters()`, unterstützt Batch-Druck für mehrere Dokumente, Vorschau mit Live-Preview bevor Druck

## Technische Details

### Performance-Optimierung
- **Buffer-Größe:** 16MB (16 * 1024 * 1024 Bytes) für maximale Geschwindigkeit
- **Parallelität:** Dynamisch - 4 Threads für Dateien <50MB, 2 Threads für große Dateien (HDD-optimiert)
- **Retry-Logik:** 3 Versuche mit exponential backoff (1s, 2s, 4s Wartezeit)
- **Rollback:** Temporäres Backup-Verzeichnis für Wiederherstellung bei kritischen Fehlern

### UI-Updates
- **Update-Frequenz:** 200ms (5 Updates pro Sekunde) für smooth Animationen ohne CPU-Überlast
- **Speed-Berechnung:** Sliding-Window über 2 Sekunden für stabilen Durchschnitt
- **Slow-Transfer-Detection:** Warnung wenn <10 MB/s für >5 Sekunden kontinuierlich

### MaterialDesign Theme
- **Primary Color:** Blue (Material Design Blue)
- **Secondary Color:** Cyan/Teal
- **Cards:** Elevation Dp4 für modernen Schatten-Effekt
- **Theme-Switching:** Light/Dark/System Support

### Logging-Erweiterung
- Durchschnittsgeschwindigkeit (MB/s)
- Peak-Speed (maximale MB/s während Transfer)
- Gesamt-Bytes übertragen
- Transfer-Dauer (TimeSpan)
- Fehler-Rate (Anzahl Retries / erfolgreiche Transfers)
- Erfolgreich/Fehlgeschlagen Dateien
- Rollback-Events mit Zeitstempel

### Etikettendruck
- **Etikettenformat:** Avery Zweckform 3474 (70x36mm), Brother DK-11208 (38x90mm), Dymo 99012 (36x89mm)
- **Etiketteninhalt:** Ordnername (groß, fett), Zielordner-Pfad, Datum/Uhrzeit, optional Barcode/QR-Code
- **Druckoptionen:** Einzeldruck, Batch-Druck (1 Etikett pro Dokument), Anzahl-Steuerung
- **Netzwerkdrucker:** Support für Windows-Netzwerkdrucker, automatische Drucker-Erkennung
- **Vorschau:** Live-Vorschau mit Zoom, Export als PDF möglich

## Erwartete Verbesserungen

### Performance
- **Vor:** 400MB in 30-60 Minuten (≈0.1-0.2 MB/s) ❌
- **Nach:** 400MB in 3-4 Sekunden bei SSD (≈100-120 MB/s) ✅
- **Nach:** 400MB in 5-10 Sekunden bei HDD (≈40-80 MB/s) ✅

### User Experience
- Echtzeit-Fortschritt für große Dateien sichtbar
- Genaue ETA-Anzeige statt "hängt"
- Geschwindigkeits-Monitoring mit Warnungen
- Moderne, professionelle UI
- Kein Scrollen nötig auf 16:9 Monitoren (außer Dateiliste)
- Settings gespeichert und wiederhergestellt
- **Etikettendruck für physische Dokumenten-Organisation**

### Robustheit
- Automatische Retries bei Netzwerk-/IO-Fehlern
- Rollback-Mechanismus bei kritischen Fehlern
- Detailliertes Logging für Fehleranalyse
- Kein Datenverlust durch Backup-System

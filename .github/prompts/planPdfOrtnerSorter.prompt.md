Plan: High-Volume PDF Sorter

Create a .NET 8 WPF single-file app that runs as one process, moves (not copies) PDFs from a chosen source folder into a user-defined destination folder, shows selectable thumbnails of the first three pages per document, and scales to huge datasets via streaming enumeration, cached previews, and resilient move orchestration.

Steps
1. Scaffold `PDFOrtnerSorter` WPF solution (`App.xaml`, `MainWindow.xaml`, MVVM base) and configure single-instance startup plus settings persistence.
2. Design `MainWindow.xaml` UI: source chooser, virtualized PDF grid with three-page thumbnail strip, preview pane, destination selector, progress + error surface.
3. Implement services (`FileService`, `CatalogStore`) to batch-enumerate massive folders, track metadata/status, and resume interrupted sessions.
4. Build `PreviewService` (PDFium/WebView2) to rasterize first three pages asynchronously, cache in `%LocalAppData%` with hash keys and LRU eviction.
5. Develop `MoveService` to stream move operations, handle conflicts, log actions, rollback on failure, and create the single destination subfolder within the chosen target.
6. Add background job controller to throttle preview generation/moves, expose progress bindings, and finalize publish profile (`dotnet publish -r win-x64 -p:PublishSingleFile=true`).

Further Considerations
1. Confirm acceptable cache size/location and retention policy for three-page thumbnails.
2. Define expected maximum folder size/file count to tune batching and virtualization thresholds.
3. Specify behavior for locked/inaccessible PDFs and whether to surface skip/retry options.

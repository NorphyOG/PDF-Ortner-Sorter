param(
    [ValidateSet("run", "watch")]
    [string]$Mode = "run"
)

$project = "PDFOrtnerSorter\\PDFOrtnerSorter.csproj"

if ($Mode -eq "watch") {
    Write-Host "Starting dotnet watch for $project" -ForegroundColor Cyan
    dotnet watch run --project $project
} else {
    Write-Host "Running once: $project" -ForegroundColor Cyan
    dotnet run --project $project
}

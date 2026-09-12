$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "KaraokeStudio.csproj"
$output = Join-Path $PSScriptRoot "Publicacao\win-x64"

Write-Host "Publicando Karaokê Studio para Windows 64 bits..." -ForegroundColor Cyan
dotnet restore $project
dotnet publish $project -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $output

Write-Host "Concluído: $output" -ForegroundColor Green
Start-Process explorer.exe $output

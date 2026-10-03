$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 não encontrado. Instale com: winget install JRSoftware.InnoSetup" }

if (Get-Process Vikings_ActivityViewer -ErrorAction SilentlyContinue) { throw "Feche o Vikings Activity Viewer antes de gerar o instalador." }

dotnet publish "$root\src\ActivityViewer\ActivityViewer.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "$root\publish"
if ($LASTEXITCODE -ne 0) { throw "Falha no dotnet publish." }

& $iscc "$PSScriptRoot\Vikings_ActivityViewer.iss"
if ($LASTEXITCODE -ne 0) { throw "Falha ao gerar o instalador." }

Get-ChildItem "$PSScriptRoot\output" | Select-Object Name, Length

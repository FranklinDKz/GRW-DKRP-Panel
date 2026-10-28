param(
    [string]$GameRoot
)

$ErrorActionPreference = 'Stop'
$packageName = 'GRW-ScriptHook-Reforged-1.0-Beta6.zip'
$packagePath = Join-Path $PSScriptRoot $packageName
$expectedHash = 'e712721902b8d1e403d08d69c2164c0ad3109c0bec130bc3c21f9162bff05b1e'

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    Add-Type -AssemblyName System.Windows.Forms
    $picker = [System.Windows.Forms.FolderBrowserDialog]::new()
    $picker.Description = 'Selecione a pasta que contém GRW.exe (Ghost Recon Wildlands).'
    $picker.UseDescriptionForTitle = $true
    if ($picker.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { exit 1 }
    $GameRoot = $picker.SelectedPath
}

$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
if (-not (Test-Path -LiteralPath (Join-Path $GameRoot 'GRW.exe') -PathType Leaf)) {
    throw "GRW.exe não foi encontrado em '$GameRoot'. Selecione a pasta principal do jogo."
}
if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
    throw "Pacote oficial ausente ao lado deste instalador: $packageName"
}
$actualHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $expectedHash) { throw 'O hash do pacote oficial não confere. A instalação foi cancelada.' }

$stage = Join-Path ([IO.Path]::GetTempPath()) ('dkrp-scripthook-' + [guid]::NewGuid().ToString('N'))
$backup = Join-Path $GameRoot ('.dkrp-scripthook-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $stage | Out-Null
try {
    Expand-Archive -LiteralPath $packagePath -DestinationPath $stage
    if (-not (Test-Path -LiteralPath (Join-Path $stage 'dinput8.dll') -PathType Leaf)) {
        throw 'O arquivo dinput8.dll não está no pacote. Nenhum arquivo foi instalado.'
    }

    $installed = 0
    Get-ChildItem -LiteralPath $stage -File -Recurse | ForEach-Object {
        $relative = $_.FullName.Substring($stage.Length).TrimStart([char[]]@('\', '/'))
        $destination = Join-Path $GameRoot $relative
        $destinationDirectory = Split-Path -Parent $destination
        if (-not (Test-Path -LiteralPath $destinationDirectory -PathType Container)) {
            New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        }
        if (Test-Path -LiteralPath $destination -PathType Leaf) {
            $backupPath = Join-Path $backup $relative
            $backupDirectory = Split-Path -Parent $backupPath
            New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
            Copy-Item -LiteralPath $destination -Destination $backupPath
        }
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
        $installed++
    }

    $languageInstaller = Join-Path $PSScriptRoot 'Install-ptBR.ps1'
    if (-not (Test-Path -LiteralPath $languageInstaller -PathType Leaf)) {
        throw 'Os arquivos de idioma ou o instalador PT-BR não estão presentes neste pacote.'
    }
    & $languageInstaller -GameRoot $GameRoot
    Write-Host "ScriptHook Reforged beta6 e idioma PT-BR instalados em: $GameRoot"
    Write-Host "Arquivos copiados: $installed"
    if (Test-Path -LiteralPath $backup) { Write-Host "Cópia dos arquivos substituídos: $backup" }
    Write-Host 'Feche e abra o jogo para carregar o ScriptHook e a tradução. Uso recomendado somente na campanha solo/offline.'
}
finally {
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
}

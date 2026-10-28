param(
    [string]$Version = "1.1.0-standalone-beta.9"
)

$ErrorActionPreference = "Stop"
if ($Version -notmatch '^[A-Za-z0-9.-]+$') { throw "Use uma versão contendo apenas letras, números, ponto e hífen." }
$repo = $PSScriptRoot
$artifacts = Join-Path $repo "artifacts"
$out = Join-Path $artifacts $Version
$payload = Join-Path $artifacts "payload"

if (Test-Path -LiteralPath $out) {
    throw "A pasta de saída já existe: $out. Escolha outra versão ou remova-a manualmente após conferir o conteúdo."
}

New-Item -ItemType Directory -Path $out -Force | Out-Null
New-Item -ItemType Directory -Path $payload -Force | Out-Null

$portableDir = Join-Path $out "portable"
$appPublish = Join-Path $portableDir "publish"
dotnet publish (Join-Path $repo "GRW_DKRP_Panel.csproj") -c Release -r win-x64 --self-contained true -o $appPublish
if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar o painel portátil." }

$appExe = Join-Path $appPublish "DKRP-GRW-Panel.exe"
if (-not (Test-Path -LiteralPath $appExe)) { throw "O executável portátil não foi gerado." }
Copy-Item -LiteralPath $appExe -Destination (Join-Path $payload "DKRP-GRW-Panel.exe")

$zipStage = Join-Path $out "zip-stage"
New-Item -ItemType Directory -Path $zipStage | Out-Null
Copy-Item -LiteralPath $appExe -Destination (Join-Path $zipStage "DKRP-GRW-Panel.exe")
Copy-Item -LiteralPath (Join-Path $repo "README.md") -Destination $zipStage
Copy-Item -LiteralPath (Join-Path $repo "LICENSE") -Destination $zipStage
New-Item -ItemType Directory -Path (Join-Path $zipStage "Cheats") | Out-Null
Copy-Item -LiteralPath (Join-Path $repo "Cheats\GRW_Atualizado_PTBR.CT") -Destination (Join-Path $zipStage "Cheats\GRW_Atualizado_PTBR.CT")
Copy-Item -LiteralPath (Join-Path $repo "ScriptHook-Lang") -Destination (Join-Path $zipStage "ScriptHook-Lang") -Recurse
Compress-Archive -Path (Join-Path $zipStage "*") -DestinationPath (Join-Path $out "DKRP-GRW-Panel-Portable.zip")

$setupOut = Join-Path $out "setup-publish"
dotnet publish (Join-Path $repo "SetupApp\DKRP-GRW-Panel-Setup.csproj") -c Release -r win-x64 --self-contained true -o $setupOut
if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar o instalador." }
$setupExe = Join-Path $setupOut "DKRP-GRW-Panel-Setup.exe"
if (-not (Test-Path -LiteralPath $setupExe)) { throw "O executável do instalador não foi gerado." }

Copy-Item -LiteralPath $appExe -Destination (Join-Path $out "DKRP-GRW-Panel.exe")
Copy-Item -LiteralPath $setupExe -Destination (Join-Path $out "DKRP-GRW-Panel-Setup.exe")
$assets = @(
    (Join-Path $out "DKRP-GRW-Panel.exe"),
    (Join-Path $out "DKRP-GRW-Panel-Setup.exe"),
    (Join-Path $out "DKRP-GRW-Panel-Portable.zip")
)
$ctAsset = Join-Path $out "GRW_Atualizado_PTBR.CT"
Copy-Item -LiteralPath (Join-Path $repo "Cheats\GRW_Atualizado_PTBR.CT") -Destination $ctAsset
$assets += $ctAsset
$hashLines = foreach ($asset in $assets) {
    $hash = (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($asset))"
}
Set-Content -LiteralPath (Join-Path $out "SHA256SUMS.txt") -Value $hashLines -Encoding ascii
Write-Host "Pacote criado em: $out"

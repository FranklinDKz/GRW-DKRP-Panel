param(
    [string]$Version = "1.1.0-standalone-beta.12"
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

$hookZipName = "GRW-ScriptHook-Reforged-1.0-Beta6.zip"
$hookZip = Join-Path $payload $hookZipName
$hookSourceName = "grw-scripthook-beedf74-source.zip"
$hookSource = Join-Path $payload $hookSourceName
if (-not (Test-Path -LiteralPath $hookZip)) {
    Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/GameXueRen/grw-scripthook/releases/download/v1.0-beta6/$hookZipName" -OutFile $hookZip
}
if (-not (Test-Path -LiteralPath $hookSource)) {
    Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/GameXueRen/grw-scripthook/archive/beedf74.zip" -OutFile $hookSource
}
$hookZipHash = (Get-FileHash -LiteralPath $hookZip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($hookZipHash -ne "e712721902b8d1e403d08d69c2164c0ad3109c0bec130bc3c21f9162bff05b1e") { throw "O hash do pacote oficial do ScriptHook não confere; nenhum pacote será criado." }
$hookSourceHash = (Get-FileHash -LiteralPath $hookSource -Algorithm SHA256).Hash.ToLowerInvariant()
if ($hookSourceHash -ne "69038da0ba7816beb56fa9ab081c1f304a13535282e0eda1a3dfc30659238f9c") { throw "O hash do código-fonte correspondente ao ScriptHook não confere." }

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
$portableLang = Join-Path $zipStage "ScriptHook-Lang"
New-Item -ItemType Directory -Path $portableLang | Out-Null
Copy-Item -LiteralPath (Join-Path $repo "ScriptHook-Lang\Install-ptBR.ps1") -Destination $portableLang
Copy-Item -LiteralPath (Join-Path $repo "ScriptHook-Lang\README.md") -Destination $portableLang
Copy-Item -LiteralPath (Join-Path $repo "ScriptHook-Lang\pt-BR") -Destination $portableLang -Recurse
Compress-Archive -Path (Join-Path $zipStage "*") -DestinationPath (Join-Path $out "DKRP-GRW-Panel-Portable.zip")

$hookBundle = Join-Path $out "scripthook-ptbr-stage"
New-Item -ItemType Directory -Path $hookBundle | Out-Null
Copy-Item -LiteralPath (Join-Path $repo "ScriptHook-Lang\Install-ScriptHook-PTBR.ps1") -Destination $hookBundle
Copy-Item -LiteralPath (Join-Path $repo "ScriptHook-Lang\Install-ptBR.ps1") -Destination $hookBundle
Copy-Item -LiteralPath (Join-Path $repo "ScriptHook-Lang\README-Instalacao.md") -Destination (Join-Path $hookBundle "LEIA-ME.md")
Copy-Item -LiteralPath (Join-Path $repo "ScriptHook-Lang\THIRD_PARTY_NOTICES.md") -Destination $hookBundle
Copy-Item -LiteralPath (Join-Path $repo "ScriptHook-Lang\GRW-ScriptHook-LICENSE-GPL-3.0.txt") -Destination $hookBundle
Copy-Item -LiteralPath $hookZip -Destination $hookBundle
Copy-Item -LiteralPath $hookSource -Destination $hookBundle
Copy-Item -LiteralPath (Join-Path $repo "ScriptHook-Lang\pt-BR") -Destination $hookBundle -Recurse
$hookInstallerZip = Join-Path $out "DKRP-ScriptHook-PTBR-Instalador.zip"
Compress-Archive -Path (Join-Path $hookBundle "*") -DestinationPath $hookInstallerZip
Copy-Item -LiteralPath $hookZip -Destination (Join-Path $out $hookZipName)
Copy-Item -LiteralPath $hookSource -Destination (Join-Path $out $hookSourceName)

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
    (Join-Path $out "DKRP-GRW-Panel-Portable.zip"),
    $hookInstallerZip,
    (Join-Path $out $hookZipName),
    (Join-Path $out $hookSourceName)
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

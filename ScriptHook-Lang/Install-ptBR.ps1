param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath (Join-Path $_ 'GRW.exe') -PathType Leaf })]
    [string]$GameRoot
)

$ErrorActionPreference = 'Stop'
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
$LanguageRoot = Join-Path $PSScriptRoot 'pt-BR'
$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Backup-Once([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return }
    $backup = "$Path.dkrp-ptbr.bak"
    if (-not (Test-Path -LiteralPath $backup)) { Copy-Item -LiteralPath $Path -Destination $backup }
}

function Set-IniValue([string]$Text, [string]$Section, [string]$Key, [string]$Value) {
    $sectionPattern = '(?ms)^\[' + [regex]::Escape($Section) + '\][ \t]*\r?\n.*?(?=^\[[^\]]+\][ \t]*$|\z)'
    $match = [regex]::Match($Text, $sectionPattern)
    if (-not $match.Success) { return ($Text.TrimEnd() + "`r`n`r`n[$Section]`r`n$Key=$Value`r`n") }
    $body = $match.Value
    $keyPattern = '(?m)^\s*' + [regex]::Escape($Key) + '\s*=.*$'
    if ([regex]::IsMatch($body, $keyPattern)) {
        $body = [regex]::Replace($body, $keyPattern, "$Key=$Value", 1)
    } else {
        $body = $body.TrimEnd() + "`r`n$Key=$Value`r`n"
    }
    return $Text.Substring(0, $match.Index) + $body + $Text.Substring($match.Index + $match.Length)
}

function Replace-LanguageSection([string]$Existing, [string]$Translation) {
    $section = 'pt-BR'
    $pattern = '(?ms)^\[' + [regex]::Escape($section) + '\][ \t]*\r?\n.*?(?=^\[[^\]]+\][ \t]*$|\z)'
    $kept = [regex]::Replace($Existing, $pattern, '').TrimEnd()
    return $kept + "`r`n`r`n" + $Translation.Trim() + "`r`n"
}

function Install-LanguageFile([string]$Source, [string]$Destination) {
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) { throw "Tradução ausente: $Source" }
    if (Test-Path -LiteralPath $Destination -PathType Leaf) {
        Backup-Once $Destination
        $existing = [IO.File]::ReadAllText($Destination)
    } else {
        $existing = ''
    }
    $translation = [IO.File]::ReadAllText($Source)
    [IO.File]::WriteAllText($Destination, (Replace-LanguageSection $existing $translation), $Utf8NoBom)
    Write-Host "Traduzido: $Destination"
}

$rootLang = Join-Path $GameRoot 'lang.ini'
if (Test-Path -LiteralPath $rootLang -PathType Leaf) {
    Backup-Once $rootLang
    $rootText = [IO.File]::ReadAllText($rootLang)
} else { $rootText = '' }
$rootSection = [IO.File]::ReadAllText((Join-Path $LanguageRoot 'lang.ini'))
$languageNames = [regex]::Match($rootSection, '(?ms)^\[LanguageNames\][ \t]*\r?\n.*?(?=^\[[^\]]+\][ \t]*$|\z)').Value
$ptSection = [regex]::Match($rootSection, '(?ms)^\[pt-BR\][ \t]*\r?\n.*\z').Value
$rootText = [regex]::Replace($rootText, '(?ms)^\[pt-BR\][ \t]*\r?\n.*?(?=^\[[^\]]+\][ \t]*$|\z)', '').TrimEnd()
$rootText = Set-IniValue $rootText 'LanguageNames' 'pt-BR' 'Português (Brasil)'
$rootText = Replace-LanguageSection $rootText $ptSection
[IO.File]::WriteAllText($rootLang, $rootText, $Utf8NoBom)
Write-Host "Traduzido: $rootLang"

$pluginSource = Join-Path $LanguageRoot 'plugins'
foreach ($folder in Get-ChildItem -LiteralPath $pluginSource -Directory) {
    $source = Join-Path $folder.FullName 'lang.ini'
    $destination = Join-Path $GameRoot (Join-Path 'plugins' (Join-Path $folder.Name 'lang.ini'))
    if (-not (Test-Path -LiteralPath (Split-Path -Parent $destination) -PathType Container)) {
        Write-Warning "Plugin ausente; pulei: $($folder.Name)"
        continue
    }
    Install-LanguageFile $source $destination
}

$settings = Join-Path $GameRoot 'scripthook.ini'
if (-not (Test-Path -LiteralPath $settings -PathType Leaf)) { throw "scripthook.ini não encontrado em $GameRoot" }
Backup-Once $settings
$settingsText = [IO.File]::ReadAllText($settings)
$settingsText = Set-IniValue $settingsText 'Settings' 'Language' 'pt-BR'
$settingsText = Set-IniValue $settingsText 'Settings' 'Languages' 'pt-BR,en-US'
[IO.File]::WriteAllText($settings, $settingsText, $Utf8NoBom)
Write-Host 'Idioma do menu selecionado: pt-BR. Reinicie o GRW para carregar os textos.'

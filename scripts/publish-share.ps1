# Bringt ein fertiges GitHub-Release auf den Update-Share, von dem die App ihre
# Updates bezieht.
#
#   scripts\publish-share.ps1 -Version 0.2.0
#   scripts\publish-share.ps1 -Version 0.2.0 -Share D:\Test\Rollout   # Probelauf
#
# Ablauf: Pakete und Versionshinweise des Tags v<Version> per gh herunterladen
# und in den Share kopieren. Reihenfolge und Kopierweg sind Absicht:
#   1. ZUERST die Notes — sonst sieht ein Client, der genau dazwischen prüft,
#      das neue Paket ohne Versionshinweise.
#   2. Pakete erst unter .tmp-Namen kopieren und dann umbenennen. Die App sucht
#      nur nach *-win-x64.zip usw.; ein halb kopiertes Paket ist für sie unsichtbar.
#
# Voraussetzung: gh ist angemeldet und hat Lesezugriff auf LHP542/QrCodeBuilder.

param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$Share = '\\samba01\542$\5424_IT-Basis-Dienste\QrCodeBuilder',

    [string]$Repo = 'LHP542/QrCodeBuilder'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Share)) {
    throw "Share nicht erreichbar: $Share"
}

$arbeit = Join-Path ([System.IO.Path]::GetTempPath()) "QrCodeBuilder-publish-$Version"
if (Test-Path $arbeit) { Remove-Item $arbeit -Recurse -Force }
New-Item -ItemType Directory -Path $arbeit | Out-Null

Write-Host "Lade Release v$Version aus $Repo …"
gh release download "v$Version" --repo $Repo --dir $arbeit `
    --pattern "QrCodeBuilder-$Version-*"
if ($LASTEXITCODE -ne 0) { throw "gh release download ist fehlgeschlagen (Exitcode $LASTEXITCODE)." }

$notes = Join-Path $arbeit "QrCodeBuilder-$Version-notes.md"
if (-not (Test-Path $notes)) {
    throw "Zum Release gehört keine Notes-Datei — ohne Versionshinweise wird nicht veröffentlicht."
}

$pakete = Get-ChildItem $arbeit -File | Where-Object { $_.Name -notlike '*-notes.md' }
if (-not ($pakete | Where-Object { $_.Name -like '*-win-x64.zip' })) {
    throw "Das Windows-Paket fehlt im Release."
}

# 1. Notes zuerst.
Copy-Item -LiteralPath $notes -Destination $Share -Force
Write-Host "kopiert: $(Split-Path $notes -Leaf)"

# 2. Pakete über einen Zwischennamen.
foreach ($paket in $pakete) {
    $ziel = Join-Path $Share $paket.Name
    $zwischen = "$ziel.tmp"
    Copy-Item -LiteralPath $paket.FullName -Destination $zwischen -Force
    Move-Item -LiteralPath $zwischen -Destination $ziel -Force
    Write-Host "kopiert: $($paket.Name) ($([math]::Round($paket.Length / 1MB, 1)) MB)"
}

Remove-Item $arbeit -Recurse -Force
Write-Host ""
Write-Host "Version $Version liegt in $Share."
Write-Host "Die Clients bieten das Update beim nächsten Start an."

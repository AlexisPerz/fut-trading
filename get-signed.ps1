# Downloads the latest signed build from GitHub Actions and installs it into "..\FUT Trading",
# keeping the local data folder (base.json and the browser profile).
# Needs the GitHub CLI, logged in once with: gh auth login
param([string]$Repo)

$ErrorActionPreference = 'Stop'
if (-not $Repo) { $Repo = (gh repo view --json nameWithOwner -q .nameWithOwner) }
$app = Join-Path $PSScriptRoot '..\FUT Trading'
$tmp = Join-Path $env:TEMP 'futtrading-signed'
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }

$run = gh run list --repo $Repo --workflow build-and-sign --status success --limit 1 --json databaseId -q '.[0].databaseId'
if (-not $run) { throw "Aucune compilation signée réussie trouvée sur $Repo." }
gh run download $run --repo $Repo --name FUTTrading-signed --dir $tmp

$sig = Get-AuthenticodeSignature (Join-Path $tmp 'FUTTrading.exe')
if ($sig.Status -ne 'Valid') { throw "L'exécutable téléchargé n'est pas signé correctement ($($sig.Status))." }

New-Item -ItemType Directory -Force $app | Out-Null
Get-ChildItem $app -Exclude data | Remove-Item -Recurse -Force
Copy-Item (Join-Path $tmp '*') $app -Recurse -Force
Write-Host "FUT Trading signé installé dans $app (signé par : $($sig.SignerCertificate.Subject))"

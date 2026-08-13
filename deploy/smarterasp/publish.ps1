<#
.SYNOPSIS
    Publishes Beep.KocAiCommunity.Web to the SmarterASP.NET site behind
    kocaitraining.premiumasp.net, using the Web Deploy profile downloaded from the control panel.

.DESCRIPTION
    Three steps, in order:

      1. dotnet publish (Release, framework-dependent, pinned to the site's runtime identifier so the
         other platforms' native libraries in runtimes/ are pruned — 814 MB portable becomes ~230 MB).
      2. Copy the production settings file into the publish output. It is NOT in the project, because
         it holds the database password and the token signing key; it lives beside this script and is
         gitignored.
      3. msdeploy sync to the site, with the app taken offline for the duration so IIS is not holding
         the DLLs while they are replaced.

    Uploaded content that the running site writes — App_Data (artifacts, setup state) and
    wwwroot/uploads (competition hero images) — is skipped, so a publish never deletes it.

.EXAMPLE
    ./deploy/smarterasp/publish.ps1

.EXAMPLE
    ./deploy/smarterasp/publish.ps1 -SkipUpload      # build and stage only, no deployment
#>
[CmdletBinding()]
param(
    # The Web Deploy profile downloaded from the SmarterASP control panel (Deploy -> WebDeploy access).
    [string]$PublishSettings = "$env:USERPROFILE\OneDrive\Desktop\kocaitraining.premiumasp.net-WebDeploy.publishSettings",

    # Production configuration, copied into the publish output as appsettings.Production.json.
    [string]$Settings = "$PSScriptRoot\appsettings.Production.json",

    # Must match the app pool's bitness in the control panel (".NET 10.x ... [x64]"). Pass win-x86 if
    # that is changed back, or "" to publish portable (larger, but bitness-agnostic).
    [string]$Runtime = "win-x64",

    [string]$Configuration = "Release",

    [string]$OutputPath = "$env:TEMP\koc-web-publish",

    # Build and stage the payload, then stop before touching the server.
    [switch]$SkipUpload
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path "$PSScriptRoot\..\.."
$project = Join-Path $repoRoot 'src\Beep.KocAiCommunity.Web\Beep.KocAiCommunity.Web.csproj'

# --- 1. Inputs -------------------------------------------------------------------------------------

if (-not (Test-Path $Settings)) {
    throw "No production settings at '$Settings'. Copy appsettings.Production.template.json to appsettings.Production.json and fill in the database password."
}

# A placeholder left in the file would deploy a site that cannot reach its database — catch it here
# rather than in a 500 on the server.
$settingsText = Get-Content $Settings -Raw
if ($settingsText -match 'REPLACE_WITH_') {
    throw "'$Settings' still contains a REPLACE_WITH_ placeholder. Fill it in before publishing."
}

if (-not $SkipUpload) {
    if (-not (Test-Path $PublishSettings)) {
        throw "No publish profile at '$PublishSettings'. Download it from the control panel: Deploy -> WebDeploy access."
    }

    [xml]$profileDoc = Get-Content $PublishSettings
    $deployProfile = $profileDoc.publishData.publishProfile | Where-Object { $_.publishMethod -eq 'MSDeploy' } | Select-Object -First 1
    if ($null -eq $deployProfile) {
        throw "'$PublishSettings' has no MSDeploy profile in it."
    }

    $site = $deployProfile.msdeploySite
    $serviceUrl = "https://$($deployProfile.publishUrl):8172/msdeploy.axd?site=$site"

    $msdeploy = "$env:ProgramFiles\IIS\Microsoft Web Deploy V3\msdeploy.exe"
    if (-not (Test-Path $msdeploy)) {
        $msdeploy = "${env:ProgramFiles(x86)}\IIS\Microsoft Web Deploy V3\msdeploy.exe"
    }
    if (-not (Test-Path $msdeploy)) {
        throw "Web Deploy is not installed. Get it from https://aka.ms/webdeploy, or publish from Visual Studio instead."
    }
}

# --- 2. Build --------------------------------------------------------------------------------------

if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Recurse -Force
}

Write-Host "Publishing $Configuration$(if ($Runtime) { " ($Runtime)" }) to $OutputPath ..." -ForegroundColor Cyan

$publishArgs = @(
    'publish', $project,
    '-c', $Configuration,
    '-o', $OutputPath,
    '--nologo'
)
if ($Runtime) {
    $publishArgs += @('-r', $Runtime, '--self-contained', 'false')
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

# --- 3. Production configuration -------------------------------------------------------------------

# Development settings are excluded from publish by the csproj, so the host resolves to Production on
# its own (see KocHostEnvironment). This file is what makes that Production configuration a valid one —
# without it KocProductionPreflight refuses to start the app.
Copy-Item $Settings (Join-Path $OutputPath 'appsettings.Production.json') -Force

$staged = '{0:N0} MB' -f ((Get-ChildItem $OutputPath -Recurse -File | Measure-Object Length -Sum).Sum / 1MB)
Write-Host "Staged $staged in $OutputPath" -ForegroundColor Green

if ($SkipUpload) {
    Write-Host "-SkipUpload was set; nothing was deployed." -ForegroundColor Yellow
    return
}

# --- 4. Deploy -------------------------------------------------------------------------------------

Write-Host "Deploying to $site at $($deployProfile.publishUrl) ..." -ForegroundColor Cyan

$dest = 'contentPath={0},computerName="{1}",userName="{2}",password="{3}",authType="Basic",includeAcls="False"' -f `
    $site, $serviceUrl, $deployProfile.userName, $deployProfile.userPWD

$msdeployArgs = @(
    '-verb:sync',
    "-source:contentPath=`"$OutputPath`"",
    "-dest:$dest",
    '-enableRule:AppOffline',
    '-allowUntrusted',
    '-retryAttempts:3',
    # Content the running site writes. Left alone so a publish never deletes uploaded images,
    # stored artifacts, or the ASP.NET Core stdout logs.
    '-skip:objectName=dirPath,absolutePath=.*\\App_Data',
    '-skip:objectName=dirPath,absolutePath=.*\\wwwroot\\uploads',
    '-skip:objectName=dirPath,absolutePath=.*\\logs'
)

& $msdeploy @msdeployArgs
if ($LASTEXITCODE -ne 0) {
    throw "msdeploy failed with exit code $LASTEXITCODE."
}

Write-Host "Published. $($deployProfile.destinationAppUrl)" -ForegroundColor Green

# Ask the site whether it actually has accounts rather than announcing "first run" on every deploy.
# Printed unconditionally, that line reads on a redeploy as though the publish wiped the database —
# which it cannot: this syncs files, and App_Data is skipped.
try
{
    $state = Invoke-RestMethod -Uri "$($deployProfile.destinationAppUrl.TrimEnd('/'))/api/v1/auth/state" -TimeoutSec 90
    if ($state.isFirstAccount)
    {
        Write-Host "This site has no accounts yet — the FIRST registration claims it as administrator. Register before sharing the address." -ForegroundColor Yellow
    }
    else
    {
        Write-Host "Existing accounts are intact (isFirstAccount = false). Nothing was reset." -ForegroundColor DarkGray
    }
}
catch
{
    Write-Host "Could not read the site's account state — check it manually at /api/v1/auth/state." -ForegroundColor DarkYellow
}

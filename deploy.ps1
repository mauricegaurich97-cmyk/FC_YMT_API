<#
.SYNOPSIS
    Publish FC_YMT_API for Linux/Render deployment and push to GitHub.
.DESCRIPTION
    Automates: dotnet publish → cleanup → fix runtimeconfig → git push.
    Run from the FC_YMT_API project folder.
#>
param(
    [string]$CommitMessage = "Update API deployment"
)

$ErrorActionPreference = "Stop"
$projectDir = $PSScriptRoot

Write-Host "`n=== FC YMT API - Publish & Deploy ===" -ForegroundColor Cyan

# Step 1: Publish for Linux
Write-Host "`n[1/5] Publishing for linux-x64..." -ForegroundColor Yellow
dotnet publish -c Release -r linux-x64 --self-contained false -o "$projectDir\publish-linux" 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "Publish failed!" -ForegroundColor Red; exit 1 }
Write-Host "      Done." -ForegroundColor Green

# Step 2: Remove nested directories (dotnet publish copies these from workspace)
Write-Host "[2/5] Cleaning nested directories..." -ForegroundColor Yellow
Get-ChildItem "$projectDir\publish-linux" -Directory | ForEach-Object {
    Remove-Item $_.FullName -Recurse -Force
    Write-Host "      Removed: $($_.Name)" -ForegroundColor DarkGray
}
Write-Host "      Done." -ForegroundColor Green

# Step 3: Fix runtimeconfig.json - remove WindowsDesktop.App
Write-Host "[3/5] Fixing runtimeconfig.json..." -ForegroundColor Yellow
$configPath = "$projectDir\publish-linux\FC_YMT_API.runtimeconfig.json"
$config = Get-Content $configPath -Raw | ConvertFrom-Json

$config.runtimeOptions.frameworks = @(
    $config.runtimeOptions.frameworks | Where-Object { $_.name -ne "Microsoft.WindowsDesktop.App" }
)

# Remove CSWINRT property if present
$props = $config.runtimeOptions.configProperties
if ($props.PSObject.Properties["CSWINRT_USE_WINDOWS_UI_XAML_PROJECTIONS"]) {
    $props.PSObject.Properties.Remove("CSWINRT_USE_WINDOWS_UI_XAML_PROJECTIONS")
}

$config | ConvertTo-Json -Depth 10 | Set-Content $configPath -Encoding UTF8
Write-Host "      Removed WindowsDesktop.App framework." -ForegroundColor Green

# Step 4: Git commit
Write-Host "[4/5] Committing changes..." -ForegroundColor Yellow
Push-Location $projectDir
git add -A
git commit -m $CommitMessage 2>&1 | Out-Null
Write-Host "      Done." -ForegroundColor Green

# Step 5: Push
Write-Host "[5/5] Pushing to GitHub..." -ForegroundColor Yellow
git push 2>&1 | Out-Null
Write-Host "      Done." -ForegroundColor Green
Pop-Location

Write-Host "`n=== Deploy complete! Render will auto-deploy. ===" -ForegroundColor Cyan
Write-Host ""

#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Prepares a new release branch for Schema-Unity.

.DESCRIPTION
    Creates a new release branch and updates version references in package.json and README.md.
    Supports both minor and patch version bumps.

.PARAMETER ReleaseType
    The type of release: "minor" or "patch"

.EXAMPLE
    .\prepare-release.ps1 -ReleaseType minor
    Creates a minor version release (e.g., 0.2.0 -> 0.3.0)

.EXAMPLE
    .\prepare-release.ps1 -ReleaseType patch
    Creates a patch version release (e.g., 0.2.0 -> 0.2.1)
#>

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("minor", "patch")]
    [string]$ReleaseType
)

# Configuration
$PackageJsonPath = "SchemaPlayground/Packages/com.devzappy.schema.unity/package.json"
$ReadmePath = "README.md"

# Function to parse version string
function Get-VersionComponents {
    param([string]$Version)
    
    if ($Version -match '^(\d+)\.(\d+)\.(\d+)$') {
        return @{
            Major = [int]$matches[1]
            Minor = [int]$matches[2]
            Patch = [int]$matches[3]
        }
    }
    throw "Invalid version format: $Version"
}

# Function to format version string
function Format-Version {
    param($Major, $Minor, $Patch)
    return "$Major.$Minor.$Patch"
}

# Check if we're on a clean working directory
$gitStatus = git status --porcelain
if ($gitStatus) {
    Write-Host "Warning: You have uncommitted changes. Consider committing or stashing them first." -ForegroundColor Yellow
    $continue = Read-Host "Continue anyway? (y/n)"
    if ($continue -ne "y") {
        exit 1
    }
}

# Read current version from package.json
Write-Host "Reading current version from package.json..." -ForegroundColor Cyan
$packageJson = Get-Content $PackageJsonPath -Raw | ConvertFrom-Json
$currentVersion = $packageJson.version
Write-Host "Current version: $currentVersion" -ForegroundColor Green

# Parse version
$versionComponents = Get-VersionComponents -Version $currentVersion
$major = $versionComponents.Major
$minor = $versionComponents.Minor
$patch = $versionComponents.Patch

# Calculate new version
if ($ReleaseType -eq "minor") {
    $minor++
    $patch = 0
} elseif ($ReleaseType -eq "patch") {
    $patch++
}

$newVersion = Format-Version -Major $major -Minor $minor -Patch $patch
Write-Host "New version: $newVersion" -ForegroundColor Green

# Confirm with user before proceeding
Write-Host ""
Write-Host "Proceed with preparing release v$newVersion ? (y/n): " -NoNewline -ForegroundColor Yellow
$confirmation = Read-Host
if ($confirmation -ne "y") {
    Write-Host "Release preparation cancelled." -ForegroundColor Yellow
    exit 0
}

# Create or switch to release branch
$branchName = "release/v$newVersion"
Write-Host "`nPreparing branch: $branchName" -ForegroundColor Cyan

# Check if branch already exists
$branchExists = git branch --list $branchName
if ($branchExists) {
    Write-Host "Branch $branchName already exists. Switching to it..." -ForegroundColor Yellow
    git checkout $branchName
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Failed to switch to branch $branchName" -ForegroundColor Red
        exit 1
    }
    Write-Host "Successfully switched to existing branch: $branchName" -ForegroundColor Green
} else {
    # Create and checkout new branch
    git checkout -b $branchName
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Failed to create branch $branchName" -ForegroundColor Red
        exit 1
    }
    Write-Host "Successfully created and switched to branch: $branchName" -ForegroundColor Green
}

# Update package.json
Write-Host "`nUpdating package.json..." -ForegroundColor Cyan

# Read the package.json as text to preserve formatting
$packageJsonText = Get-Content $PackageJsonPath -Raw -Encoding UTF8

# Update version field
$packageJsonText = $packageJsonText -replace '("version"\s*:\s*)"[^"]*"', "`$1`"$newVersion`""

# Update changelogUrl
$newChangelogUrl = "https://github.com/zappy-dev/Schema-Unity/compare/v$currentVersion...v$newVersion"
$packageJsonText = $packageJsonText -replace '("changelogUrl"\s*:\s*)"[^"]*"', "`$1`"$newChangelogUrl`""

# Write back to file with UTF-8 encoding (no BOM)
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$absolutePackageJsonPath = (Resolve-Path $PackageJsonPath).Path
[System.IO.File]::WriteAllText($absolutePackageJsonPath, $packageJsonText, $utf8NoBom)
Write-Host "Updated package.json to version $newVersion" -ForegroundColor Green

# Update README.md
Write-Host "`nUpdating README.md..." -ForegroundColor Cyan
$readmeContent = Get-Content $ReadmePath -Raw -Encoding UTF8

# Replace version references
$readmeContent = $readmeContent -replace [regex]::Escape($currentVersion), $newVersion

# Save README.md with UTF-8 encoding (no BOM)
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$absoluteReadmePath = (Resolve-Path $ReadmePath).Path
[System.IO.File]::WriteAllText($absoluteReadmePath, $readmeContent, $utf8NoBom)
Write-Host "Updated README.md version references" -ForegroundColor Green

# Summary
Write-Host "`n=== Release Preparation Complete ===" -ForegroundColor Cyan
Write-Host "Branch: $branchName" -ForegroundColor White
Write-Host "Old version: $currentVersion" -ForegroundColor White
Write-Host "New version: $newVersion" -ForegroundColor White
Write-Host "`nFiles updated:" -ForegroundColor White
Write-Host "  - $PackageJsonPath" -ForegroundColor White
Write-Host "  - $ReadmePath" -ForegroundColor White
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "  1. Review the changes: git diff" -ForegroundColor White
Write-Host "  2. Commit the changes: git add . && git commit -m 'Prepare release v$newVersion'" -ForegroundColor White
Write-Host "  3. Push the branch: git push -u origin $branchName" -ForegroundColor White
Write-Host "  4. Create a pull request on GitHub" -ForegroundColor White


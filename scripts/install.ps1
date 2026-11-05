<#
.SYNOPSIS
Installs memshare on Windows: downloads the release for this platform, checks it against the release's
SHA256SUMS, runs `memshare install` (which copies it to %LOCALAPPDATA%\Programs\memshare and sets up
the coding agent), and adds that directory to the user's PATH.

.EXAMPLE
& ([scriptblock]::Create((irm https://github.com/praaline/agent-memshare/releases/latest/download/install.ps1))) -Machine desktop
#>
param(
    # This machine's name (lower-case letters, digits, hyphens); default: the computer's name.
    [string] $Machine,
    # The release to install (vX.Y.Z); default: the latest.
    [string] $Version = 'latest',
    # Where stores are checked out; default: ~/agent-memshare.
    [string] $StoresRoot,
    # owner/repository to install from (a fork, for example).
    [string] $Repo = $(if ($env:MEMSHARE_REPO) { $env:MEMSHARE_REPO } else { 'praaline/agent-memshare' })
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$arch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
$asset = "memshare-win-$arch.zip"
$base = if ($Version -eq 'latest') { "https://github.com/$Repo/releases/latest/download" } else { "https://github.com/$Repo/releases/download/$Version" }
if (-not $Machine) {
    $Machine = ($env:COMPUTERNAME.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
    if ($Machine.Length -gt 31) { $Machine = $Machine.Substring(0, 31).TrimEnd('-') }
}

$tmp = Join-Path ([IO.Path]::GetTempPath()) ("memshare-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    Write-Host "downloading $asset ($Version) from $Repo"
    Invoke-WebRequest -UseBasicParsing -Uri "$base/$asset" -OutFile (Join-Path $tmp $asset)
    Invoke-WebRequest -UseBasicParsing -Uri "$base/SHA256SUMS" -OutFile (Join-Path $tmp 'SHA256SUMS')

    $line = Get-Content (Join-Path $tmp 'SHA256SUMS') | Where-Object { ($_ -split '\s+')[1] -in @($asset, "*$asset") } | Select-Object -First 1
    if (-not $line) { throw "$asset is not listed in SHA256SUMS" }
    $expected = ($line -split '\s+')[0].ToLowerInvariant()
    $actual = (Get-FileHash -Algorithm SHA256 -Path (Join-Path $tmp $asset)).Hash.ToLowerInvariant()
    if ($expected -ne $actual) { throw "checksum mismatch for ${asset}: not installed" }
    Write-Host 'checksum ok'

    Expand-Archive -Path (Join-Path $tmp $asset) -DestinationPath (Join-Path $tmp 'package')
    $arguments = @('install', '--machine', $Machine)
    if ($StoresRoot) { $arguments += @('--stores-root', $StoresRoot) }
    & (Join-Path $tmp 'package\memshare.exe') @arguments
    if ($LASTEXITCODE -ne 0) { throw "memshare install failed ($LASTEXITCODE)" }

    $bin = Join-Path $env:LOCALAPPDATA 'Programs\memshare'
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    if (-not (($userPath -split ';') -contains $bin)) {
        [Environment]::SetEnvironmentVariable('Path', ($userPath.TrimEnd(';') + ";$bin").TrimStart(';'), 'User')
        Write-Host "added $bin to your PATH (open a new terminal to use memshare)"
    }
}
finally {
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}

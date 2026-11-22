[CmdletBinding()]
param(
    [ValidateSet('x64', 'arm64')]
    [string] $Architecture = 'x64',
    # Leave blank to clone the Core repository. A local checkout can be supplied
    # when a particular set of Core patches must be included instead.
    [string] $CoreSource = '',
    # Optional branch, tag or commit to fetch when cloning. Blank uses HEAD.
    [string] $CoreRef = '',
    [string] $OutputDirectory = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-Native([string] $Program, [string[]] $Arguments) {
    $previousPreference = $ErrorActionPreference
    try {
        # Native tools such as git and Go normally write progress to stderr.
        $ErrorActionPreference = 'Continue'
        & $Program @Arguments
        $exitCode = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousPreference }
    if ($exitCode -ne 0) { throw "$Program failed with exit code $exitCode." }
}

if ($env:OS -ne 'Windows_NT') { throw 'Run this script on Windows.' }
foreach ($name in @('dotnet', 'go')) {
    if (-not (Get-Command $name -CommandType Application -ErrorAction SilentlyContinue)) {
        throw "Required tool not found: $name. Install .NET 10 SDK and Go 1.24 or newer."
    }
}
if (-not $CoreSource -and -not (Get-Command git -CommandType Application -ErrorAction SilentlyContinue)) {
    throw 'Git is required to download Core, or pass -CoreSource with a local Core checkout.'
}

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$hostSource = Join-Path $repo 'tools/core-host'
$project = Join-Path $repo 'src/ShitBotUltra.Desktop/ShitBotUltra.Desktop.csproj'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'artifacts' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$temp = Join-Path ([IO.Path]::GetTempPath()) ('shitbotultra-package-' + [guid]::NewGuid().ToString('N'))
$publish = Join-Path $temp 'publish'
$workspace = Join-Path $temp 'go.work'
$oldWork = $env:GOWORK
$oldGoOs = $env:GOOS
$oldGoArch = $env:GOARCH
$oldCgo = $env:CGO_ENABLED

try {
    New-Item -ItemType Directory -Path $temp, $publish -Force | Out-Null
    if (-not $CoreSource) {
        $CoreSource = Join-Path $temp 'core'
        if ($CoreRef) {
            Invoke-Native git @('init', '-q', $CoreSource)
            Invoke-Native git @('-C', $CoreSource, 'remote', 'add', 'origin', 'https://github.com/ShitBot-Team/ShitBotUltra.git')
            Invoke-Native git @('-C', $CoreSource, 'fetch', '--depth', '1', 'origin', $CoreRef)
            Invoke-Native git @('-C', $CoreSource, 'checkout', '--detach', 'FETCH_HEAD')
        } else {
            Invoke-Native git @('clone', '--depth', '1', 'https://github.com/ShitBot-Team/ShitBotUltra.git', $CoreSource)
        }
    }
    $CoreSource = (Resolve-Path $CoreSource).Path
    if (-not (Test-Path (Join-Path $CoreSource 'runtime/runtime.go')) -or -not (Test-Path (Join-Path $CoreSource 'go.mod'))) {
        throw 'CoreSource must be a ShitBotUltra Core source directory with go.mod and runtime/runtime.go.'
    }

    $revision = 'local-source'
    $dirty = 'unknown'
    if ((Test-Path (Join-Path $CoreSource '.git')) -and (Get-Command git -CommandType Application -ErrorAction SilentlyContinue)) {
        $gitRoot = & git -C $CoreSource rev-parse --show-toplevel 2>$null
        if ($LASTEXITCODE -eq 0 -and [IO.Path]::GetFullPath($gitRoot) -eq [IO.Path]::GetFullPath($CoreSource)) {
            $revision = (& git -C $CoreSource rev-parse HEAD).Trim()
            if ($LASTEXITCODE -ne 0 -or $revision -notmatch '^[0-9a-f]{40}$') { throw 'Could not identify the Core revision.' }
            $status = & git -C $CoreSource status --porcelain --untracked-files=normal
            if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the Core checkout.' }
            $dirty = if ($status) { 'yes' } else { 'no' }
        }
    }

    # A workspace keeps the two repositories independent and never rewrites
    # the supplied Core go.mod. Dependencies are resolved by the Go toolchain.
    $env:GOWORK = 'off'
    Push-Location $temp
    try { Invoke-Native go @('work', 'init', $hostSource, $CoreSource) }
    finally { Pop-Location }
    $env:GOWORK = $workspace
    $env:GOOS = 'windows'
    $env:GOARCH = if ($Architecture -eq 'x64') { 'amd64' } else { 'arm64' }
    $env:CGO_ENABLED = '0'
    Push-Location $hostSource
    try {
        Invoke-Native go @('build', '-trimpath', '-buildvcs=false', '-ldflags', "-X main.version=$revision", '-o', (Join-Path $temp 'sbu.exe'), '.')
    } finally { Pop-Location }

    # WinUI's compiler runs on Windows. Publish the desktop with its runtime.
    $rid = "win-$Architecture"
    $platform = if ($Architecture -eq 'x64') { 'x64' } else { 'ARM64' }
    Invoke-Native dotnet @('publish', $project, '-c', 'Release', '-r', $rid, '--self-contained', 'true', ('-p:Platform=' + $platform), '-p:WindowsAppSDKSelfContained=true', '-p:PublishSingleFile=false', '-o', $publish)
    Copy-Item (Join-Path $temp 'sbu.exe') (Join-Path $publish 'sbu.exe')
    if (-not (Test-Path (Join-Path $publish 'ShitBotUltra.Desktop.exe')) -or -not (Test-Path (Join-Path $publish 'sbu.exe'))) {
        throw 'The Desktop executable or bundled Core host is missing from the publish directory.'
    }
    Copy-Item (Join-Path $repo 'LICENSE') (Join-Path $publish 'LICENSE-Desktop.txt')
    Copy-Item (Join-Path $CoreSource 'LICENSE') (Join-Path $publish 'LICENSE-Core.txt')
    # Keep the bundled Core's third-party attributions with their documented paths.
    $notices = Join-Path $publish 'core-source-notices'
    New-Item -ItemType Directory -Path $notices -Force | Out-Null
    foreach ($name in @('THIRD_PARTY_NOTICES.md', 'go.mod', 'go.sum')) {
        $sourceFile = Join-Path $CoreSource $name
        if (Test-Path $sourceFile) { Copy-Item $sourceFile (Join-Path $notices $name) }
    }
    foreach ($relative in @('internal/webui/assets/vendor/LICENSE', 'internal/webui/assets/vendor/PROVENANCE.json')) {
        $sourceFile = Join-Path $CoreSource $relative
        if (Test-Path $sourceFile) {
            $target = Join-Path $notices $relative
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            Copy-Item $sourceFile $target
        }
    }
    $templateRoot = Join-Path $CoreSource 'internal/rendering/builtin/templates'
    if (Test-Path $templateRoot) {
        $targetRoot = Join-Path $notices 'internal/rendering/builtin/templates'
        Get-ChildItem $templateRoot -Directory | ForEach-Object {
            $templateTarget = Join-Path $targetRoot $_.Name
            foreach ($name in @('LICENSE', 'THIRD_PARTY_NOTICES.md', 'NOTICE.md', 'docs/ADDITIONAL_TERMS.md', 'docs/ASSET-LICENSES.md')) {
                $sourceFile = Join-Path $_.FullName $name
                if (Test-Path $sourceFile) {
                    $target = Join-Path $templateTarget $name
                    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
                    Copy-Item $sourceFile $target
                }
            }
        }
    }
    @(
        'ShitBotUltra Desktop and Core',
        "Core revision: $revision",
        "Core working tree has uncommitted changes: $dirty",
        "Target: $rid",
        'Launch ShitBotUltra.Desktop.exe. Core data is stored in %APPDATA%\ShitBotUltra.',
        'Core host updates are not configured in this bundle.'
    ) | Set-Content -Encoding UTF8 (Join-Path $publish 'SOURCE-INFO.txt')
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $zip = Join-Path $OutputDirectory ("ShitBotUltra-Desktop-$rid-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8) + '.zip')
    Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -CompressionLevel Optimal
    Write-Host "Created $zip"
} finally {
    $env:GOWORK = $oldWork
    $env:GOOS = $oldGoOs
    $env:GOARCH = $oldGoArch
    $env:CGO_ENABLED = $oldCgo
    if (Test-Path $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}

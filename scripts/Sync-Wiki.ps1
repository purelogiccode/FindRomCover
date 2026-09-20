<#
.SYNOPSIS
    Generates GitHub wiki pages from the MkDocs documentation in docs/.

.DESCRIPTION
    Converts every Markdown page under docs/ into a flat wiki page, rewrites
    relative links to wiki page names, copies assets, and writes the wiki
    sidebar and footer.

.PARAMETER DocsPath
    Path to the documentation source folder. Defaults to docs.

.PARAMETER OutputPath
    Folder that receives the generated wiki files. Defaults to wiki-out.

.PARAMETER RepoUrl
    Repository URL used for links to non-documentation files.
    Defaults to https://github.com/purelogiccode/FindRomCover.

.EXAMPLE
    pwsh ./scripts/Sync-Wiki.ps1 -OutputPath wiki-out
#>
[CmdletBinding()]
param(
    [string]$DocsPath = 'docs',
    [string]$OutputPath = 'wiki-out',
    [string]$RepoUrl = 'https://github.com/purelogiccode/FindRomCover'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$docsRoot = [System.IO.Path]::GetFullPath((Join-Path $root $DocsPath))
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $root $OutputPath))

if (-not (Test-Path -LiteralPath $docsRoot)) {
    throw "Documentation folder not found: $docsRoot"
}

$script:WikiNameOverrides = @{
    'ai'     = 'AI'
    'api'    = 'API'
    'cd'     = 'CD'
    'ci'     = 'CI'
    'faq'    = 'FAQ'
    'mame'   = 'MAME'
    'rom'    = 'ROM'
    'sqlite' = 'SQLite'
    'url'    = 'URL'
}

function ConvertTo-WikiName {
    param([Parameter(Mandatory)][string]$RelativePath)

    $segments = ($RelativePath -replace '\.md$', '') -split '/'
    if ($segments[-1] -eq 'index') {
        if ($segments.Count -eq 1) { return 'Home' }
        $segments = $segments[0..($segments.Count - 2)]
    }

    $words = [System.Collections.Generic.List[string]]::new()
    foreach ($segment in $segments) {
        foreach ($word in ($segment -split '-')) {
            $lower = $word.ToLowerInvariant()
            if ($script:WikiNameOverrides.ContainsKey($lower)) {
                $words.Add($script:WikiNameOverrides[$lower])
            }
            elseif ($word.Length -gt 0) {
                $words.Add($word.Substring(0, 1).ToUpperInvariant() + $word.Substring(1))
            }
        }
    }

    return ($words -join '-')
}

$docFiles = Get-ChildItem -LiteralPath $docsRoot -Recurse -File -Filter '*.md' | Sort-Object FullName
$wikiNames = @{}
$pageTitles = @{}
foreach ($file in $docFiles) {
    $relative = [System.IO.Path]::GetRelativePath($docsRoot, $file.FullName) -replace '\\', '/'
    $wikiNames[$relative] = ConvertTo-WikiName -RelativePath $relative

    $heading = Get-Content -LiteralPath $file.FullName -Encoding UTF8 |
        Where-Object { $_ -match '^#\s+\S' } |
        Select-Object -First 1
    $pageTitles[$relative] = if ($heading) { ($heading -replace '^#\s+', '').Trim() } else { $wikiNames[$relative] }
}

$duplicates = $wikiNames.Values | Group-Object | Where-Object Count -gt 1
if ($duplicates) {
    throw "Wiki page name collision: $($duplicates.Name -join ', ')"
}

if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

$linkPattern = '(!?\[[^\]]*\]\()([^)\s]+)(\))'
$linkEvaluator = [System.Text.RegularExpressions.MatchEvaluator] {
    param($match)

    $prefix = $match.Groups[1].Value
    $target = $match.Groups[2].Value
    $suffix = $match.Groups[3].Value

    if ($target -match '^(https?:|mailto:|ftp:|#)') { return $match.Value }

    $anchor = ''
    if ($target.Contains('#')) {
        $parts = $target.Split('#', 2)
        $target = $parts[0]
        $anchor = '#' + $parts[1]
    }

    if ([string]::IsNullOrWhiteSpace($target)) { return $match.Value }

    $sourceDir = Split-Path -Parent $script:CurrentSourceFile
    $resolved = [System.IO.Path]::GetFullPath((Join-Path $sourceDir $target))
    $relative = [System.IO.Path]::GetRelativePath($docsRoot, $resolved) -replace '\\', '/'

    if ($relative.StartsWith('..')) {
        throw "Link escapes the documentation folder in $($script:CurrentRelativeFile): $target"
    }

    if ($relative -eq '.') { return $match.Value }

    if ($relative.EndsWith('.md')) {
        if (-not $wikiNames.ContainsKey($relative)) {
            throw "Unknown documentation page in $($script:CurrentRelativeFile): $target"
        }
        $newTarget = $wikiNames[$relative]
    }
    elseif ($relative.StartsWith('assets/')) {
        $newTarget = $relative
    }
    else {
        $newTarget = "$RepoUrl/blob/master/$relative"
    }

    return "$prefix$newTarget$anchor$suffix"
}

foreach ($file in $docFiles) {
    $script:CurrentSourceFile = $file.FullName
    $script:CurrentRelativeFile = [System.IO.Path]::GetRelativePath($docsRoot, $file.FullName) -replace '\\', '/'
    $wikiName = $wikiNames[$script:CurrentRelativeFile]

    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    $content = [System.Text.RegularExpressions.Regex]::Replace($content, $linkPattern, $linkEvaluator)
    $content = $content.TrimEnd() + [Environment]::NewLine

    $destination = Join-Path $outputRoot "$wikiName.md"
    Set-Content -LiteralPath $destination -Value $content -Encoding UTF8NoBOM
    Write-Host "wiki: $($script:CurrentRelativeFile) -> $wikiName.md"
}

$assetsSource = Join-Path $docsRoot 'assets'
if (Test-Path -LiteralPath $assetsSource) {
    Copy-Item -LiteralPath $assetsSource -Destination $outputRoot -Recurse -Force
    Write-Host 'wiki: copied assets/'
}

$sidebarSections = [ordered]@{
    'Getting Started' = @(
        'getting-started/installation.md',
        'getting-started/quick-start.md'
    )
    'User Guide' = @(
        'user-guide/interface.md',
        'user-guide/folders-and-scanning.md',
        'user-guide/local-files.md',
        'user-guide/web-search.md',
        'user-guide/google-api.md',
        'user-guide/image-handling.md',
        'user-guide/missing-covers.md'
    )
    'AI Vision Assist' = @(
        'ai/index.md',
        'ai/providers.md',
        'ai/settings.md',
        'ai/picking-covers.md',
        'ai/batch-fill.md',
        'ai/query-history.md',
        'ai/recommended-models.md',
        'ai/troubleshooting.md'
    )
    'Configuration' = @(
        'configuration/index.md',
        'configuration/themes.md',
        'configuration/similarity.md',
        'configuration/extensions.md',
        'configuration/data-storage.md',
        'configuration/logs.md'
    )
    'Reference' = @(
        'reference/command-line.md',
        'reference/keyboard-shortcuts.md',
        'reference/file-formats.md'
    )
    'Development' = @(
        'development/architecture.md',
        'development/project-structure.md',
        'development/building.md',
        'development/testing.md',
        'development/ci-cd.md',
        'development/contributing.md'
    )
    'More' = @(
        'release-notes.md',
        'faq.md',
        'troubleshooting.md',
        'license.md'
    )
}

function Get-SidebarLabel {
    param([Parameter(Mandatory)][string]$RelativePath)
    return $pageTitles[$RelativePath]
}

$sidebar = [System.Collections.Generic.List[string]]::new()
$sidebar.Add('### FindRomCover')
$sidebar.Add('')
$sidebar.Add('- [Home](Home)')
$sidebar.Add('')

foreach ($section in $sidebarSections.Keys) {
    $sidebar.Add("**$section**")
    foreach ($path in $sidebarSections[$section]) {
        if (-not $wikiNames.ContainsKey($path)) {
            throw "Sidebar references a missing page: $path"
        }
        $sidebar.Add("- [$(Get-SidebarLabel -RelativePath $path)]($($wikiNames[$path]))")
    }
    $sidebar.Add('')
}

$sidebar.Add('---')
$sidebar.Add('')
$sidebar.Add('- [Releases](' + $RepoUrl + '/releases)')
$sidebar.Add('- [Issues](' + $RepoUrl + '/issues)')
$sidebar.Add('- [Source Code](' + $RepoUrl + ')')

Set-Content -LiteralPath (Join-Path $outputRoot '_Sidebar.md') -Value ($sidebar -join [Environment]::NewLine) -Encoding UTF8NoBOM

$footer = @(
    'FindRomCover — find and download missing cover art for your retro gaming ROM collection.',
    '',
    "[Documentation site](https://purelogiccode.github.io/FindRomCover/) · [Repository]($RepoUrl) · [Releases]($RepoUrl/releases) · [GPL-3.0]($RepoUrl/blob/master/LICENSE.txt)"
)
Set-Content -LiteralPath (Join-Path $outputRoot '_Footer.md') -Value ($footer -join [Environment]::NewLine) -Encoding UTF8NoBOM

Write-Host "Generated $($docFiles.Count) wiki pages in $outputRoot"

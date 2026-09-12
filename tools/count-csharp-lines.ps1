[CmdletBinding()]
param(
    [string]$Path = (Split-Path -Parent $PSScriptRoot),
    [switch]$Detailed
)

$root = (Resolve-Path -LiteralPath $Path).Path
$files = Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.cs' |
    Where-Object {
        $relativePath = $_.FullName.Substring($root.Length).TrimStart('\', '/')
        $segments = $relativePath -split '[\\/]'
        $segments -notcontains 'RefMods'
    }

$stats = foreach ($file in $files) {
    $lines = @(Get-Content -LiteralPath $file.FullName)
    [pscustomobject]@{
        File          = $file.FullName.Substring($root.Length).TrimStart('\', '/')
        Lines         = $lines.Count
        NonBlankLines = @($lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count
    }
}

if ($Detailed) {
    $stats | Sort-Object File | Format-Table Lines, NonBlankLines, File -AutoSize
}

$totalLines = ($stats | Measure-Object -Property Lines -Sum).Sum
$totalNonBlankLines = ($stats | Measure-Object -Property NonBlankLines -Sum).Sum

Write-Host ''
Write-Host "C# files:       $($stats.Count)"
Write-Host "All lines:      $totalLines"
Write-Host "Non-blank lines: $totalNonBlankLines"

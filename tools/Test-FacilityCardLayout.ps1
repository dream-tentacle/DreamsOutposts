$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([IO.Path]::GetTempPath()) ('DreamsOutposts-card-layout-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
$source = [IO.File]::ReadAllText((Join-Path $projectRoot 'Source\DreamsOutposts\UI\Pages\Page_OutpostFacilities.cs'))
$parts = foreach ($method in @('LayoutModernFacilityInfoGroup', 'LayoutModernInfoPair', 'LayoutModernInfoText', 'FitSingleLine')) {
    $pattern = '(?ms)^\t\tprivate (?:static )?[^\r\n]+\b' + [regex]::Escape($method) + '\([^\{]*\{.*?^\t\t\}\r?$'
    $matches = [regex]::Matches($source, $pattern)
    if ($matches.Count -ne 1) { throw "Expected one rendering method: $method" }
    $matches[0].Value
}
$extracted = 'using System; using System.Collections.Generic; using UnityEngine; using Verse; namespace DreamsOutposts { public class CardLayout {' + "`n" + ($parts -join "`n") + "`n}}"
$path = Join-Path $testDir 'CardLayout.cs'
[IO.File]::WriteAllText($path, $extracted)
$testExe = Join-Path $testDir 'CardLayoutTests.exe'
$files = @($path, (Join-Path $PSScriptRoot 'Tests\FacilityCardLayoutTests.cs'), (Join-Path $projectRoot 'Source\DreamsOutposts\UI\UiFacilityInfo.cs'))
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe' /nologo /target:exe "/out:$testExe" @files
if ($LASTEXITCODE -ne 0) { throw 'Card layout harness failed to compile.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Card layout tests failed.' }

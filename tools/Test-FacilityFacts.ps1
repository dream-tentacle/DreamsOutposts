$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([IO.Path]::GetTempPath()) ('DreamsOutposts-facts-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
# Compile the actual cache methods under test with controlled game inputs.
# This does not start RimWorld or exercise Unity rendering.
$cacheSource = [IO.File]::ReadAllText((Join-Path $projectRoot 'Source\DreamsOutposts\UI\OutpostUiCache.cs'))
$methods = @(
    'AddFacilityFacts', 'AddLevelProductionFactorFacts', 'BuildModifierFacts',
    'ProductionModifierFactId', 'AddFacilityProductionModifierFacts',
    'RefreshFacilityFacts', 'AppendFactChips', 'AddOperationInfo', 'AddOperationChips',
    'RefreshInstallCardFacts', 'TrainingChipLabel', 'TrainingTip', 'TrainingFactor', 'TrainingFactorLine'
)
$parts = foreach ($method in $methods) {
    $pattern = '(?ms)^\t\t(?:private|public) (?:static )?[^\r\n]+\b' + [regex]::Escape($method) + '\([^\{]*\{.*?^\t\t\}\r?$'
    $matches = [regex]::Matches($cacheSource, $pattern)
    if ($matches.Count -ne 1) { throw "Expected exactly one cache method: $method" }
    $matches[0].Value
}
$header = 'using System; using System.Collections.Generic; using UnityEngine; using Verse; namespace DreamsOutposts { public sealed class OutpostUiCache { private readonly Outpost outpost; public OutpostUiCache(Outpost value) { outpost = value; }'
$extracted = $header + "`n" + ($parts -join "`n") + "`n}}"
$cachePath = Join-Path $testDir 'CacheMethods.cs'
[IO.File]::WriteAllText($cachePath, $extracted)
$testExe = Join-Path $testDir 'FacilityFactsTests.exe'
$files = @(
    (Join-Path $PSScriptRoot 'Tests\FacilityFactsTests.cs'),
    (Join-Path $projectRoot 'Source\DreamsOutposts\UI\UiFacilityInfo.cs'),
    $cachePath
)
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe' /nologo /target:exe "/out:$testExe" @files
if ($LASTEXITCODE -ne 0) { throw 'Facility facts harness failed to compile.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Facility facts tests failed.' }

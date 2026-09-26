$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'Source\DreamsOutposts'
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ('DreamsOutposts-compat-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
$testExe = Join-Path $testDir 'OutpostCompatibilityTests.exe'
$files = @(
    (Join-Path $PSScriptRoot 'Tests\OutpostCompatibilityTests.cs'),
    (Join-Path $source 'Compatibility\OutpostFacilityCompMigration.cs'),
    (Join-Path $source 'Compatibility\OutpostFacilityComp_Research.cs'),
    (Join-Path $source 'Process\OutpostProcessState.cs'),
    (Join-Path $source 'Production\OutpostFacilityComp_Production.cs'),
    (Join-Path $source 'Production\OutpostProductionState.cs'),
    (Join-Path $source 'Production\OutpostProductionState_AdaptiveMining.cs'),
    (Join-Path $source 'Production\OutpostProductionState_Farming.cs'),
    (Join-Path $source 'Production\OutpostProductionState_MechMachining.cs'),
    (Join-Path $source 'Production\OutpostProductionState_MiliraSolar.cs'),
    (Join-Path $source 'Production\OutpostProductionState_XianluQi.cs')
)
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe' /nologo /target:exe "/out:$testExe" @files
if ($LASTEXITCODE -ne 0) { throw 'Compatibility regression harness failed to compile.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Compatibility regression tests failed.' }

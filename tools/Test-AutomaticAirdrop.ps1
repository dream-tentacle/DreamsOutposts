$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([IO.Path]::GetTempPath()) ('DreamsOutposts-airdrop-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
$testExe = Join-Path $testDir 'AutomaticAirdropTests.exe'
$files = @(
    (Join-Path $PSScriptRoot 'Tests\AutomaticAirdropTests.cs'),
    (Join-Path $projectRoot 'Source\DreamsOutposts\Logistics\OutpostAutomaticAirdropUtility.cs')
)
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe' /nologo /target:exe "/out:$testExe" @files
if ($LASTEXITCODE -ne 0) { throw 'Airdrop harness failed to compile.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Airdrop tests failed.' }

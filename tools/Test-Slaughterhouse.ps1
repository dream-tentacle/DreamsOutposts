$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([IO.Path]::GetTempPath()) ('DreamsOutposts-slaughter-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
$testExe = Join-Path $testDir 'SlaughterhouseTests.exe'
$files = @(
    (Join-Path $PSScriptRoot 'Tests\SlaughterhouseTests.cs'),
    (Join-Path $projectRoot 'Source\DreamsOutposts\Facilities\OutpostFacilityComp.cs'),
    (Join-Path $projectRoot 'Source\DreamsOutposts\UI\UiFacilityInfo.cs'),
    (Join-Path $projectRoot 'Source\DreamsOutposts\Taming\OutpostFacilityComp_Slaughterhouse.cs')
)
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe' /nologo /target:exe "/out:$testExe" @files
if ($LASTEXITCODE -ne 0) { throw 'Slaughterhouse harness failed to compile.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Slaughterhouse tests failed.' }
foreach ($relativePath in @(
    'Defs\OutpostFacilities\Slaughterhouse.xml',
    'Languages\English\Keyed\DreamsOutposts_Slaughterhouse.xml',
    'Languages\ChineseSimplified (简体中文)\Keyed\DreamsOutposts_Slaughterhouse.xml',
    'Languages\ChineseSimplified (简体中文)\DefInjected\DreamsOutposts.OutpostFacilityDef\Slaughterhouse.xml'
)) {
    $document = New-Object System.Xml.XmlDocument
    $document.Load((Join-Path $projectRoot $relativePath))
}
Write-Output 'Slaughterhouse XML: 4 files parsed successfully.'

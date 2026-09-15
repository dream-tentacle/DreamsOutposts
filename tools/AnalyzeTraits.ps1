#requires -Version 5.1
<#
  扫描原版所有 TraitDef（Core + 全部 DLC），按「可能是负面」的信号分类列出。

  原版没有任何"负面特性"标记（TraitDef / TraitDegreeData 都没有），所以只能靠数据推断。
  本脚本只做客观罗列 + 保守信号提示，最终清单由人确认。

  用法:
    powershell -File .\Tools\AnalyzeTraits.ps1
#>
[CmdletBinding()]
param(
	[string]$DataRoot = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Data',
	[string]$OutJson  = (Join-Path $PSScriptRoot 'traits_report.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ChildElement {
	param([System.Xml.XmlNode]$Node, [string]$Name)
	if ($null -eq $Node) { return $null }
	foreach ($c in $Node.ChildNodes) {
		if ($c.NodeType -eq [System.Xml.XmlNodeType]::Element -and $c.Name -eq $Name) { return $c }
	}
	return $null
}

function Get-ChildText {
	param([System.Xml.XmlNode]$Node, [string]$Name)
	$e = Get-ChildElement -Node $Node -Name $Name
	if ($null -eq $e) { return $null }
	return $e.InnerText.Trim()
}

function Get-ChildBool {
	param([System.Xml.XmlNode]$Node, [string]$Name)
	$t = Get-ChildText -Node $Node -Name $Name
	if ([string]::IsNullOrWhiteSpace($t)) { return $null }
	return ($t -eq 'true' -or $t -eq 'True')
}

# 取 statOffsets / statFactors：返回 @( @{stat=..; value=..}, ... )
function Get-StatModifiers {
	param([System.Xml.XmlNode]$DegreeNode, [string]$NodeName)
	$out = [System.Collections.Generic.List[object]]::new()
	$holder = Get-ChildElement -Node $DegreeNode -Name $NodeName
	if ($null -eq $holder) { return $out }
	foreach ($li in $holder.ChildNodes) {
		if ($li.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }
		$stat = Get-ChildText -Node $li -Name 'stat'
		$val = Get-ChildText -Node $li -Name 'value'
		if ([string]::IsNullOrWhiteSpace($stat)) { continue }
		$out.Add([pscustomobject]@{ stat = $stat; value = $val })
	}
	return $out
}

function Get-ListNodeTexts {
	param([System.Xml.XmlNode]$Node, [string]$Name)
	$out = @()
	$holder = Get-ChildElement -Node $Node -Name $Name
	if ($null -eq $holder) { return $out }
	foreach ($li in $holder.ChildNodes) {
		if ($li.NodeType -eq [System.Xml.XmlNodeType]::Element) { $out += $li.InnerText.Trim() }
	}
	return $out
}

# ---------------------------------------------------------------- sweep

Write-Host "枚举 $DataRoot ..."
$allFiles = Get-ChildItem -Path $DataRoot -Recurse -Filter *.xml -File |
	Where-Object { $_.FullName -notmatch '\\Languages\\' }

$traitFiles = [System.Collections.Generic.List[object]]::new()
foreach ($f in $allFiles) {
	$text = [System.IO.File]::ReadAllText($f.FullName)
	if ($text.Contains('<TraitDef')) { $traitFiles.Add($f) }
}
Write-Host ("含 TraitDef 的文件: {0}" -f $traitFiles.Count)

function Get-ModuleOf {
	param([string]$FullPath)
	$rel = $FullPath.Substring($DataRoot.Length).TrimStart([char[]]@('\', '/'))
	return ($rel -split '[\\/]')[0]
}

$traitElems = @{}
$records = [System.Collections.Generic.List[object]]::new()

foreach ($f in $traitFiles) {
	$doc = New-Object System.Xml.XmlDocument
	$doc.Load($f.FullName)
	foreach ($node in $doc.SelectNodes('//TraitDef')) {
		$dn = Get-ChildText -Node $node -Name 'defName'
		$nm = $node.GetAttribute('Name')
		if (-not [string]::IsNullOrWhiteSpace($dn)) { $traitElems[$dn] = $node }
		if (-not [string]::IsNullOrWhiteSpace($nm)) { $traitElems[$nm] = $node }
		if ([string]::IsNullOrWhiteSpace($dn)) { continue }
		$records.Add([pscustomobject]@{
			DefName = $dn
			Module  = (Get-ModuleOf -FullPath $f.FullName)
			Elem    = $node
		})
	}
}

# 把 defName 解析成 Elem（ParentName 链上的同名引用）
function Resolve-DegreeList {
	param([System.Xml.XmlElement]$Elem)
	$cur = $Elem
	$guard = 0
	while ($null -ne $cur -and $guard -lt 32) {
		$guard++
		$dl = Get-ChildElement -Node $cur -Name 'degreeDatas'
		if ($null -ne $dl) { return $dl }
		$pn = $cur.GetAttribute('ParentName')
		if ([string]::IsNullOrWhiteSpace($pn) -or -not $traitElems.ContainsKey($pn)) { return $null }
		$cur = $traitElems[$pn]
	}
	return $null
}

# def 级字段（可能写在 ParentName 链上）
function Get-DefLevel {
	param([System.Xml.XmlElement]$Elem, [string]$Name)
	$cur = $Elem
	$guard = 0
	while ($null -ne $cur -and $guard -lt 32) {
		$guard++
		$c = Get-ChildElement -Node $cur -Name $Name
		if ($null -ne $c) { return $c }
		$pn = $cur.GetAttribute('ParentName')
		if ([string]::IsNullOrWhiteSpace($pn) -or -not $traitElems.ContainsKey($pn)) { return $null }
		$cur = $traitElems[$pn]
	}
	return $null
}

# ---------------------------------------------------------------- extract degrees

$degrees = [System.Collections.Generic.List[object]]::new()

foreach ($rec in $records) {
	$dl = Resolve-DegreeList -Elem $rec.Elem
	if ($null -eq $dl) { continue }

	foreach ($li in $dl.ChildNodes) {
		if ($li.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }

		$statOffsets = @(Get-StatModifiers -DegreeNode $li -NodeName 'statOffsets')
		$statFactors = @(Get-StatModifiers -DegreeNode $li -NodeName 'statFactors')

		$skillGains = @()
		$sg = Get-ChildElement -Node $li -Name 'skillGains'
		if ($null -ne $sg) {
			foreach ($s in $sg.ChildNodes) {
				if ($s.NodeType -eq [System.Xml.XmlNodeType]::Element) {
					$skillGains += ("{0}:{1}" -f (Get-ChildText -Node $s -Name 'skill'), (Get-ChildText -Node $s -Name 'amount'))
				}
			}
		}

		$degrees.Add([pscustomobject]@{
			Trait            = $rec.DefName
			Module           = $rec.Module
			Degree           = (Get-ChildText -Node $li -Name 'degree')
			Label            = (Get-ChildText -Node $li -Name 'label')
			MarketOffset     = (Get-ChildText -Node $li -Name 'marketValueFactorOffset')
			SocialFight      = (Get-ChildText -Node $li -Name 'socialFightChanceFactor')
			RandomDiseaseMtb = (Get-ChildText -Node $li -Name 'randomDiseaseMtbDays')
			HungerRate       = (Get-ChildText -Node $li -Name 'hungerRateFactor')
			PainOffset       = (Get-ChildText -Node $li -Name 'painOffset')
			PainFactor       = (Get-ChildText -Node $li -Name 'painFactor')
			ForcedMental     = (Get-ChildText -Node $li -Name 'forcedMentalState')
			RandomMental     = (Get-ChildText -Node $li -Name 'randomMentalState')
			DisallowsMental  = @(Get-ListNodeTexts -Node $li -Name 'disallowedMentalStates')
			DisablesNeeds    = @(Get-ListNodeTexts -Node $li -Name 'disablesNeeds')
			StatOffsets      = $statOffsets
			StatFactors      = $statFactors
			SkillGains       = $skillGains
			DisabledWorkTypes = @(Get-ListNodeTexts -Node (Get-DefLevel -Elem $rec.Elem -Name 'disabledWorkTypes') -Name 'li')
			DisabledWorkTags  = @(Get-ListNodeTexts -Node (Get-DefLevel -Elem $rec.Elem -Name 'disabledWorkTags') -Name 'li')
			RequiredWorkTags  = @(Get-ListNodeTexts -Node (Get-DefLevel -Elem $rec.Elem -Name 'requiredWorkTags') -Name 'li')
			ConflictsWith     = @(Get-ListNodeTexts -Node (Get-DefLevel -Elem $rec.Elem -Name 'conflictingTraits') -Name 'li')
		})
	}
}

Write-Host ("TraitDef {0} 个，degree {1} 条" -f $records.Count, $degrees.Count)
Write-Host ''

# ---------------------------------------------------------------- classify

function To-Float {
	param($Text)
	if ([string]::IsNullOrWhiteSpace($Text)) { return $null }
	$v = 0.0
	if ([double]::TryParse($Text, [ref]$v)) { return $v }
	return $null
}

$negSignals = [System.Collections.Generic.List[object]]::new()
$ambiguous = [System.Collections.Generic.List[object]]::new()

foreach ($d in $degrees) {
	$signals = [System.Collections.Generic.List[string]]::new()

	$mo = To-Float $d.MarketOffset
	if ($null -ne $mo -and $mo -lt 0) { $signals.Add("marketValueFactorOffset=$mo") }

	if ($d.DisabledWorkTypes.Count -gt 0) { $signals.Add("禁用工作类型: " + ($d.DisabledWorkTypes -join ',')) }
	if ($d.DisabledWorkTags.Count -gt 0)  { $signals.Add("禁用工作标签: " + ($d.DisabledWorkTags -join ',')) }

	$sf = To-Float $d.SocialFight
	if ($null -ne $sf -and $sf -gt 1) { $signals.Add("社交打架率 x$sf") }

	$rd = To-Float $d.RandomDiseaseMtb
	if ($null -ne $rd -and $rd -gt 0) { $signals.Add("患病MTB=$rd") }

	$hr = To-Float $d.HungerRate
	if ($null -ne $hr -and $hr -gt 1) { $signals.Add("饥饿率 x$hr") }

	$po = To-Float $d.PainOffset
	if ($null -ne $po -and $po -ne 0) { $signals.Add("痛觉偏移 $po") }
	$pf = To-Float $d.PainFactor
	if ($null -ne $pf -and $pf -ne 1) { $signals.Add("痛觉系数 x$pf") }

	if (-not [string]::IsNullOrWhiteSpace($d.ForcedMental)) { $signals.Add("强制精神状态: " + $d.ForcedMental) }
	if (-not [string]::IsNullOrWhiteSpace($d.RandomMental)) { $signals.Add("随机精神状态: " + $d.RandomMental) }
	if ($d.DisallowsMental.Count -gt 0) { $signals.Add("禁止精神状态: " + ($d.DisallowsMental -join ',')) }
	if ($d.DisablesNeeds.Count -gt 0) { $signals.Add("禁用需求: " + ($d.DisablesNeeds -join ',')) }

	foreach ($m in $d.StatFactors) {
		$v = To-Float $m.value
		if ($null -ne $v -and $v -lt 1) { $signals.Add("$($m.stat) x$($m.value)") }
		elseif ($null -ne $v -and $v -gt 1) { $ambiguous.Add([pscustomobject]@{ Trait=$d.Trait; Label=$d.Label; Note="$($m.stat) x$($m.value)" }) }
	}
	foreach ($m in $d.StatOffsets) {
		$v = To-Float $m.value
		if ($null -ne $v -and $v -lt 0) { $signals.Add("$($m.stat) $($m.value)") }
		elseif ($null -ne $v -and $v -gt 0) { $ambiguous.Add([pscustomobject]@{ Trait=$d.Trait; Label=$d.Label; Note="$($m.stat) +$($m.value)" }) }
	}
	foreach ($sg in $d.SkillGains) {
		$parts = $sg -split ':'
		$v = To-Float $parts[1]
		if ($null -ne $v -and $v -lt 0) { $signals.Add("技能惩罚 $sg") }
	}

	if ($signals.Count -gt 0) {
		$negSignals.Add([pscustomobject]@{ Trait=$d.Trait; Degree=$d.Degree; Label=$d.Label; Module=$d.Module; Signals=$signals })
	}
}

# ---------------------------------------------------------------- report

Write-Host '============================================================'
Write-Host ' 原版特性：带「偏负面」信号的 degree'
Write-Host '============================================================'
Write-Host ("共 {0} 条（涉及 {1} 个 TraitDef）" -f $negSignals.Count, (@($negSignals | Group-Object Trait).Count))
Write-Host ''
foreach ($g in @($negSignals | Group-Object Trait | Sort-Object Name)) {
	foreach ($r in $g.Group) {
		$deg = ''
		if (-not [string]::IsNullOrWhiteSpace($r.Degree)) { $deg = " (degree $($r.Degree))" }
		Write-Host ("  {0}{1}  [{2}]" -f $r.Trait, $deg, $r.Module)
		foreach ($s in $r.Signals) { Write-Host ("      - {0}" -f $s) }
	}
}

Write-Host ''
Write-Host '============================================================'
Write-Host ' 建议的「负面特性」defName 清单（上面涉及的 TraitDef 去重）'
Write-Host '============================================================'
$list = @($negSignals | Group-Object Trait | Sort-Object Name | ForEach-Object { $_.Name })
Write-Host ("共 {0} 个：" -f $list.Count)
Write-Host ($list -join ', ')

Write-Host ''
Write-Host '============================================================'
Write-Host ' 属性方向不明（可能有正面效果，我未计入上面清单）'
Write-Host '============================================================'
foreach ($a in @($ambiguous | Sort-Object Trait, Note -Unique)) {
	Write-Host ("  {0,-24} {1}" -f $a.Trait, $a.Note)
}

$payload = [pscustomobject]@{
	generatedAt = (Get-Date).ToString('s')
	dataRoot    = $DataRoot
	traitDefs   = $records.Count
	degrees     = $degrees.Count
	suspectedNegative = $list
	details     = @($negSignals)
	ambiguous   = @($ambiguous)
	allDegrees  = @($degrees)
}
$payload | ConvertTo-Json -Depth 8 | Set-Content -Path $OutJson -Encoding UTF8
Write-Host ''
Write-Host ("明细 JSON 已写入: {0}" -f $OutJson)

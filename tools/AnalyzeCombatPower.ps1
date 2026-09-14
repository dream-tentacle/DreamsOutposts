#requires -Version 5.1
<#
  统计原版（Data 目录）所有人类 PawnKindDef 的 combatPower 分布。

  - 自行解析 RimWorld 的 XML 继承（ParentName / Name + Abstract），
    因此写在抽象基类里的 combatPower / race / isFighter 会被子 Def 正确继承。
  - “人类”判定：PawnKindDef.race 指向的 ThingDef 的 <race><Humanlike> 为 true。
  - 只解析含目标的文件，并直接用 XPath 取元素（避免遍历全部节点导致极慢）。

  用法:
    powershell -File .\Tools\AnalyzeCombatPower.ps1
    powershell -File .\Tools\AnalyzeCombatPower.ps1 -OutJson .\out.json
#>
[CmdletBinding()]
param(
	[string]$DataRoot = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Data',
	[string]$OutJson  = (Join-Path $PSScriptRoot 'combatpower_humanlike.json')
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

# 沿 ParentName 链向上找第一个「自己声明了 $ChildName 子节点」的祖先。
# 模拟 RimWorld 的字段级继承：越派生（越靠下）的值优先。
function Get-InheritedChild {
	param([System.Xml.XmlElement]$Elem, [string]$ChildName, [hashtable]$Lookup)
	$cur = $Elem
	$guard = 0
	while ($null -ne $cur -and $guard -lt 64) {
		$guard++
		$c = Get-ChildElement -Node $cur -Name $ChildName
		if ($null -ne $c) { return $c }
		$pn = $cur.GetAttribute('ParentName')
		if ([string]::IsNullOrWhiteSpace($pn)) { return $null }
		if (-not $Lookup.ContainsKey($pn)) { return $null }
		$cur = $Lookup[$pn]
	}
	return $null
}

function Get-InheritedText {
	param([System.Xml.XmlElement]$Elem, [string]$ChildName, [hashtable]$Lookup)
	$c = Get-InheritedChild -Elem $Elem -ChildName $ChildName -Lookup $Lookup
	if ($null -eq $c) { return $null }
	return $c.InnerText.Trim()
}

function Get-InheritedBool {
	param([System.Xml.XmlElement]$Elem, [string]$ChildName, [hashtable]$Lookup, [bool]$Default)
	$t = Get-InheritedText -Elem $Elem -ChildName $ChildName -Lookup $Lookup
	if ([string]::IsNullOrWhiteSpace($t)) { return $Default }
	return ($t -eq 'true' -or $t -eq 'True')
}

function Get-LeadingComment {
	param([System.Xml.XmlNode]$Node)
	$c = $Node.PreviousSibling
	$guard = 0
	while ($null -ne $c -and $guard -lt 8) {
		$guard++
		if ($c.NodeType -eq [System.Xml.XmlNodeType]::Comment) { return $c.Value.Trim() }
		if ($c.NodeType -eq [System.Xml.XmlNodeType]::Element) { return $null }
		$c = $c.PreviousSibling
	}
	return $null
}

# ---------------------------------------------------------------- sweep files

Write-Host "枚举 $DataRoot ..."
$allFiles = Get-ChildItem -Path $DataRoot -Recurse -Filter *.xml -File |
	Where-Object { $_.FullName -notmatch '\\Languages\\' }

$kindFiles    = [System.Collections.Generic.List[object]]::new()
$factionFiles = [System.Collections.Generic.List[object]]::new()
$raceFiles    = [System.Collections.Generic.List[object]]::new()

foreach ($f in $allFiles) {
	$text = [System.IO.File]::ReadAllText($f.FullName)
	$hasKind = $text.Contains('<PawnKindDef')
	$hasFac  = $text.Contains('<FactionDef')
	$hasRace = $text.Contains('<race>') -and $text.Contains('<ThingDef')
	if ($hasKind) { $kindFiles.Add($f) }
	if ($hasFac)  { $factionFiles.Add($f) }
	if ($hasRace) { $raceFiles.Add($f) }
}

Write-Host ("候选文件：总数 {0} / 含 PawnKindDef {1} / 含 FactionDef {2} / 含 race 的 ThingDef {3}" -f `
	$allFiles.Count, $kindFiles.Count, $factionFiles.Count, $raceFiles.Count)

function Get-ModuleOf {
	param([string]$FullPath)
	$rel = $FullPath.Substring($DataRoot.Length).TrimStart([char[]]@('\', '/'))
	return ($rel -split '[\\/]')[0]
}

function Get-RelPath {
	param([string]$FullPath)
	return $FullPath.Substring($DataRoot.Length).TrimStart([char[]]@('\', '/'))
}

$parseErrors = [System.Collections.Generic.List[string]]::new()

function Import-Doc {
	param([string]$Path)
	try {
		$doc = New-Object System.Xml.XmlDocument
		$doc.Load($Path)
		return $doc
	} catch {
		$script:parseErrors.Add(("{0}: {1}" -f $Path, $_.Exception.Message))
		return $null
	}
}

# ---------------------------------------------------------------- races

$thingElems = @{}
foreach ($f in $raceFiles) {
	$doc = Import-Doc -Path $f.FullName
	if ($null -eq $doc) { continue }
	foreach ($node in $doc.SelectNodes('//ThingDef')) {
		$dn = Get-ChildText -Node $node -Name 'defName'
		$nm = $node.GetAttribute('Name')
		if (-not [string]::IsNullOrWhiteSpace($dn)) { $thingElems[$dn] = $node }
		if (-not [string]::IsNullOrWhiteSpace($nm)) { $thingElems[$nm] = $node }
	}
}

# RaceProperties.Humanlike 是计算属性：intelligence >= Intelligence.Humanlike(2)
# 而 XML 里写的是 <race><intelligence>Humanlike</intelligence></race>
function Test-HumanlikeRace {
	param([string]$RaceDefName)
	if ([string]::IsNullOrWhiteSpace($RaceDefName)) { return $false }
	if (-not $thingElems.ContainsKey($RaceDefName)) { return $false }
	$cur = $thingElems[$RaceDefName]
	$guard = 0
	while ($null -ne $cur -and $guard -lt 64) {
		$guard++
		$race = Get-ChildElement -Node $cur -Name 'race'
		if ($null -ne $race) {
			$v = Get-ChildText -Node $race -Name 'intelligence'
			if (-not [string]::IsNullOrWhiteSpace($v)) { return ($v -eq 'Humanlike') }
		}
		$pn = $cur.GetAttribute('ParentName')
		if ([string]::IsNullOrWhiteSpace($pn) -or -not $thingElems.ContainsKey($pn)) { return $false }
		$cur = $thingElems[$pn]
	}
	return $false
}

# ---------------------------------------------------------------- pawn kinds

$kindElems = @{}
$kindMeta  = [System.Collections.Generic.List[object]]::new()

foreach ($f in $kindFiles) {
	$doc = Import-Doc -Path $f.FullName
	if ($null -eq $doc) { continue }
	foreach ($node in $doc.SelectNodes('//PawnKindDef')) {
		$dn = Get-ChildText -Node $node -Name 'defName'
		$nm = $node.GetAttribute('Name')
		$rec = [pscustomobject]@{
			Elem    = $node
			DefName = $dn
			Name    = $nm
			Module  = (Get-ModuleOf -FullPath $f.FullName)
			File    = (Get-RelPath -FullPath $f.FullName)
			Comment = (Get-LeadingComment -Node $node)
		}
		if (-not [string]::IsNullOrWhiteSpace($dn)) { $kindElems[$dn] = $node }
		if (-not [string]::IsNullOrWhiteSpace($nm)) { $kindElems[$nm] = $node }
		if (-not [string]::IsNullOrWhiteSpace($dn)) { $kindMeta.Add($rec) }
	}
}

# ---------------------------------------------------------------- factions

$factionElems = @{}
$factionRecords = [System.Collections.Generic.List[object]]::new()
foreach ($f in $factionFiles) {
	$doc = Import-Doc -Path $f.FullName
	if ($null -eq $doc) { continue }
	foreach ($node in $doc.SelectNodes('//FactionDef')) {
		$dn = Get-ChildText -Node $node -Name 'defName'
		$nm = $node.GetAttribute('Name')
		if (-not [string]::IsNullOrWhiteSpace($dn)) { $factionElems[$dn] = $node }
		if (-not [string]::IsNullOrWhiteSpace($nm)) { $factionElems[$nm] = $node }
		if (-not [string]::IsNullOrWhiteSpace($dn)) {
			$factionRecords.Add([pscustomobject]@{ DefName = $dn; Elem = $node })
		}
	}
}

$kindFactionUsage = @{}
$factionMeta = @{}

foreach ($fr in $factionRecords) {
	# FactionDef.humanlikeFaction 字段默认值为 true
	$factionMeta[$fr.DefName] = [pscustomobject]@{
		DefName   = $fr.DefName
		IsPlayer  = (Get-InheritedBool -Elem $fr.Elem -ChildName 'isPlayer' -Lookup $factionElems -Default $false)
		Hidden    = (Get-InheritedBool -Elem $fr.Elem -ChildName 'hidden' -Lookup $factionElems -Default $false)
		Humanlike = (Get-InheritedBool -Elem $fr.Elem -ChildName 'humanlikeFaction' -Lookup $factionElems -Default $true)
	}
	$mgrs = Get-InheritedChild -Elem $fr.Elem -ChildName 'pawnGroupMakers' -Lookup $factionElems
	if ($null -eq $mgrs) { continue }
	foreach ($mgr in $mgrs.ChildNodes) {
		if ($mgr.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }
		$groupKind = Get-ChildText -Node $mgr -Name 'kindDef'
		foreach ($groupName in @('options', 'traders', 'carriers', 'guards')) {
			$grp = Get-ChildElement -Node $mgr -Name $groupName
			if ($null -eq $grp) { continue }
			foreach ($opt in $grp.ChildNodes) {
				if ($opt.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }
				$kind = $opt.Name
				if (-not $kindFactionUsage.ContainsKey($kind)) {
					$kindFactionUsage[$kind] = [System.Collections.Generic.List[string]]::new()
				}
				$tag = "{0}/{1}" -f $fr.DefName, $groupKind
				if (-not $kindFactionUsage[$kind].Contains($tag)) { $kindFactionUsage[$kind].Add($tag) }
			}
		}
	}
}

# ---------------------------------------------------------------- extract rows

$rows = [System.Collections.Generic.List[object]]::new()
$skippedNonHuman = 0

foreach ($r in $kindMeta) {
	if ($r.Elem.GetAttribute('Abstract') -eq 'true') { continue }

	$race = Get-InheritedText -Elem $r.Elem -ChildName 'race' -Lookup $kindElems
	if (-not (Test-HumanlikeRace -RaceDefName $race)) { $skippedNonHuman++; continue }

	$cpText = Get-InheritedText -Elem $r.Elem -ChildName 'combatPower' -Lookup $kindElems
	$cp = -1
	if (-not [string]::IsNullOrWhiteSpace($cpText)) {
		$parsed = 0
		if ([int]::TryParse($cpText, [ref]$parsed)) { $cp = $parsed }
	}

	$skillList = [System.Collections.Generic.List[string]]::new()
	$skNode = Get-InheritedChild -Elem $r.Elem -ChildName 'skills' -Lookup $kindElems
	if ($null -ne $skNode) {
		foreach ($li in $skNode.ChildNodes) {
			if ($li.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }
			$s = Get-ChildText -Node $li -Name 'skill'
			$rg = Get-ChildText -Node $li -Name 'range'
			if (-not [string]::IsNullOrWhiteSpace($s)) { $skillList.Add(("{0}:{1}" -f $s, $rg)) }
		}
	}

	$usage = @()
	if ($kindFactionUsage.ContainsKey($r.DefName)) { $usage = $kindFactionUsage[$r.DefName].ToArray() }

	$rows.Add([pscustomobject]@{
		defName        = $r.DefName
		label          = (Get-InheritedText -Elem $r.Elem -ChildName 'label' -Lookup $kindElems)
		module         = $r.Module
		file           = $r.File
		combatPower    = $cp
		race           = $race
		isFighter      = (Get-InheritedBool -Elem $r.Elem -ChildName 'isFighter' -Lookup $kindElems -Default $true)
		factionLeader  = (Get-InheritedBool -Elem $r.Elem -ChildName 'factionLeader' -Lookup $kindElems -Default $false)
		trader         = (Get-InheritedBool -Elem $r.Elem -ChildName 'trader' -Lookup $kindElems -Default $false)
		isBoss         = (Get-InheritedBool -Elem $r.Elem -ChildName 'isBoss' -Lookup $kindElems -Default $false)
		resistance     = (Get-InheritedText -Elem $r.Elem -ChildName 'initialResistanceRange' -Lookup $kindElems)
		weaponMoney    = (Get-InheritedText -Elem $r.Elem -ChildName 'weaponMoney' -Lookup $kindElems)
		apparelMoney   = (Get-InheritedText -Elem $r.Elem -ChildName 'apparelMoney' -Lookup $kindElems)
		minAge         = (Get-InheritedText -Elem $r.Elem -ChildName 'minGenerationAge' -Lookup $kindElems)
		maxAge         = (Get-InheritedText -Elem $r.Elem -ChildName 'maxGenerationAge' -Lookup $kindElems)
		skills         = ($skillList -join ', ')
		factionUsage   = ($usage -join '; ')
		inCombatPool   = (@($usage | Where-Object { $_ -like '*/Combat' }).Count -gt 0)
		tierComment    = $r.Comment
	})
}

$rows = @($rows | Sort-Object defName | Sort-Object combatPower, defName)

# ---------------------------------------------------------------- mod pool
# 复刻 AdventurerRecruitUtility.BuildPawnKindPool 的过滤条件：
#   非玩家 / 非隐藏 / humanlikeFaction / 有 pawnGroupMakers
#   maker.kindDef ∈ {Combat, Peaceful, Settlement} / maker.options（不含 traders/carriers/guards）
#   Eligible(kind): 人类 + combatPower > 0 + 非 factionLeader/trader/isBoss
# 注意：defeated / temporary 是运行时状态，这里无法建模。

$rowByDef = @{}
foreach ($r in $rows) { $rowByDef[$r.defName] = $r }

$poolEntries = [System.Collections.Generic.List[object]]::new()

foreach ($fr in $factionRecords) {
	$meta = $factionMeta[$fr.DefName]
	if ($null -eq $meta) { continue }
	if ($meta.IsPlayer -or $meta.Hidden -or -not $meta.Humanlike) { continue }
	$mgrs = Get-InheritedChild -Elem $fr.Elem -ChildName 'pawnGroupMakers' -Lookup $factionElems
	if ($null -eq $mgrs) { continue }
	foreach ($mgr in $mgrs.ChildNodes) {
		if ($mgr.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }
		$gk = Get-ChildText -Node $mgr -Name 'kindDef'
		if ($gk -ne 'Combat' -and $gk -ne 'Peaceful' -and $gk -ne 'Settlement') { continue }
		$grp = Get-ChildElement -Node $mgr -Name 'options'
		if ($null -eq $grp) { continue }
		foreach ($opt in $grp.ChildNodes) {
			if ($opt.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }
			$kind = $opt.Name
			if (-not $rowByDef.ContainsKey($kind)) { continue }
			$row = $rowByDef[$kind]
			if ($row.combatPower -le 0) { continue }
			if ($row.factionLeader -or $row.trader -or $row.isBoss) { continue }
			$poolEntries.Add([pscustomobject]@{
				faction     = $fr.DefName
				groupKind   = $gk
				kind        = $kind
				combatPower = $row.combatPower
			})
		}
	}
}

# ---------------------------------------------------------------- report

$valid   = @($rows | Where-Object { $_.combatPower -gt 0 })
$invalid = @($rows | Where-Object { $_.combatPower -le 0 })

function Get-Quantile {
	param([double[]]$Sorted, [double]$Q)
	if ($Sorted.Count -eq 0) { return [double]::NaN }
	$idx = [int][Math]::Round(($Sorted.Count - 1) * $Q)
	return $Sorted[$idx]
}

$cps = @($valid | Select-Object -ExpandProperty combatPower | Sort-Object)

Write-Host ''
Write-Host '============================================================'
Write-Host ' 原版人类 PawnKindDef combatPower 统计'
Write-Host '============================================================'
Write-Host ("PawnKindDef(有defName)     : {0}" -f $kindMeta.Count)
Write-Host ("非人类                       : {0}" -f $skippedNonHuman)
Write-Host ("人类                         : {0}" -f $rows.Count)
Write-Host ("  其中 combatPower > 0       : {0}" -f $valid.Count)
Write-Host ("  其中 combatPower <= 0/缺失 : {0}" -f $invalid.Count)
if ($invalid.Count -gt 0) {
	Write-Host ("      -> {0}" -f (($invalid | ForEach-Object { "{0}({1})" -f $_.defName, $_.combatPower }) -join ', '))
}
Write-Host ''

if ($cps.Count -gt 0) {
	$arr = [double[]]$cps
	Write-Host '--- 分位数（仅 combatPower > 0）---'
	Write-Host ("  min    {0,7}" -f $arr[0])
	Write-Host ("  p10    {0,7}" -f (Get-Quantile -Sorted $arr -Q 0.10))
	Write-Host ("  p25    {0,7}" -f (Get-Quantile -Sorted $arr -Q 0.25))
	Write-Host ("  median {0,7}" -f (Get-Quantile -Sorted $arr -Q 0.50))
	Write-Host ("  p75    {0,7}" -f (Get-Quantile -Sorted $arr -Q 0.75))
	Write-Host ("  p90    {0,7}" -f (Get-Quantile -Sorted $arr -Q 0.90))
	Write-Host ("  p95    {0,7}" -f (Get-Quantile -Sorted $arr -Q 0.95))
	Write-Host ("  max    {0,7}" -f $arr[$arr.Count - 1])
	Write-Host ("  mean   {0,7:N1}" -f ($arr | Measure-Object -Average).Average)
	Write-Host ''

	Write-Host '--- 每个 combatPower 值（每种类的 defName）---'
	$byCp = @($valid | Group-Object combatPower | Sort-Object { [int]$_.Name })
	$maxCount = ($byCp | Measure-Object -Property Count -Maximum).Maximum
	foreach ($g in $byCp) {
		$barLen = [int][Math]::Ceiling(($g.Count / [double]$maxCount) * 30)
		$bar = '#' * $barLen
		Write-Host ("  {0,5} | {1,-30} {2,3}   {3}" -f $g.Name, $bar, $g.Count, (($g.Group | ForEach-Object { $_.defName }) -join ', '))
	}
	Write-Host ''

	Write-Host '--- 区间分布 ---'
	$buckets = @(
		@{ Label = '   1 -  29'; Min = 1;   Max = 29 },
		@{ Label = '  30 -  49'; Min = 30;  Max = 49 },
		@{ Label = '  50 -  69'; Min = 50;  Max = 69 },
		@{ Label = '  70 -  89'; Min = 70;  Max = 89 },
		@{ Label = '  90 - 109'; Min = 90;  Max = 109 },
		@{ Label = ' 110 - 129'; Min = 110; Max = 129 },
		@{ Label = ' 130 - 149'; Min = 130; Max = 149 },
		@{ Label = ' 150 - 199'; Min = 150; Max = 199 },
		@{ Label = ' 200 - 299'; Min = 200; Max = 299 },
		@{ Label = ' 300+     '; Min = 300; Max = [int]::MaxValue }
	)
	foreach ($b in $buckets) {
		$n = @($valid | Where-Object { $_.combatPower -ge $b.Min -and $_.combatPower -le $b.Max }).Count
		if ($n -eq 0) { continue }
		$bar = '#' * [int][Math]::Ceiling(($n / [double]$valid.Count) * 40)
		Write-Host ("  {0} | {1,-40} {2,3} ({3,5:N1}%)" -f $b.Label, $bar, $n, (100.0 * $n / $valid.Count))
	}
	Write-Host ''

	Write-Host '--- 按来源模块 ---'
	foreach ($m in @($valid | Group-Object module | Sort-Object Name)) {
		$v = [double[]]@($m.Group | Select-Object -ExpandProperty combatPower | Sort-Object)
		Write-Host ("  {0,-9} n={1,3}  min={2,4}  median={3,5}  max={4,4}  mean={5,6:N1}" -f `
			$m.Name, $m.Count, $v[0], (Get-Quantile -Sorted $v -Q 0.5), $v[$v.Count - 1], ($v | Measure-Object -Average).Average)
	}
	Write-Host ''

	Write-Host '--- 是否出现在 Combat 组池 ---'
	foreach ($g in @($valid | Group-Object inCombatPool | Sort-Object Name)) {
		$v = [double[]]@($g.Group | Select-Object -ExpandProperty combatPower | Sort-Object)
		$tag = '不在 Combat 组'
		if ($g.Name -eq 'True') { $tag = '在 Combat 组' }
		Write-Host ("  {0,-14} n={1,3}  min={2,4}  median={3,5}  max={4,4}" -f `
			$tag, $g.Count, $v[0], (Get-Quantile -Sorted $v -Q 0.5), $v[$v.Count - 1])
	}
	Write-Host ''

	Write-Host '--- 排除 leader/trader/boss 后（本 mod 可用池的近似）---'
	$pool = @($valid | Where-Object { -not $_.factionLeader -and -not $_.trader -and -not $_.isBoss })
	$pv = [double[]]@($pool | Select-Object -ExpandProperty combatPower | Sort-Object)
	Write-Host ("  n={0}  min={1}  p25={2}  median={3}  p75={4}  p90={5}  max={6}  mean={7:N1}" -f `
		$pool.Count, $pv[0], (Get-Quantile -Sorted $pv -Q 0.25), (Get-Quantile -Sorted $pv -Q 0.5), `
		(Get-Quantile -Sorted $pv -Q 0.75), (Get-Quantile -Sorted $pv -Q 0.9), $pv[$pv.Count - 1], ($pv | Measure-Object -Average).Average)
	Write-Host ''
}

# ---------------------------------------------------------------- pool report

$pcp = @($poolEntries | Select-Object -ExpandProperty combatPower | Sort-Object)
if ($pcp.Count -gt 0) {
	$parr = [double[]]$pcp
	Write-Host '============================================================'
	Write-Host ' 本 mod 候选池（复刻 BuildPawnKindPool 过滤条件）'
	Write-Host '============================================================'
	Write-Host ("池条目数（种类 x 派系）  : {0}" -f $poolEntries.Count)
	Write-Host ("去重后的种类数           : {0}" -f (@($poolEntries | Group-Object kind).Count))
	Write-Host ("参与派系数               : {0}" -f (@($poolEntries | Group-Object faction).Count))
	Write-Host ''
	Write-Host '--- 池内 combatPower 分位数（按条目加权）---'
	Write-Host ("  min    {0,7}" -f $parr[0])
	Write-Host ("  p10    {0,7}" -f (Get-Quantile -Sorted $parr -Q 0.10))
	Write-Host ("  p25    {0,7}" -f (Get-Quantile -Sorted $parr -Q 0.25))
	Write-Host ("  median {0,7}" -f (Get-Quantile -Sorted $parr -Q 0.50))
	Write-Host ("  p75    {0,7}" -f (Get-Quantile -Sorted $parr -Q 0.75))
	Write-Host ("  p90    {0,7}" -f (Get-Quantile -Sorted $parr -Q 0.90))
	Write-Host ("  max    {0,7}" -f $parr[$parr.Count - 1])
	Write-Host ("  mean   {0,7:N1}" -f ($parr | Measure-Object -Average).Average)
	Write-Host ''

	Write-Host '--- 池内每个 combatPower 值的条目数 ---'
	$byPoolCp = @($poolEntries | Group-Object combatPower | Sort-Object { [int]$_.Name })
	$maxPoolCount = ($byPoolCp | Measure-Object -Property Count -Maximum).Maximum
	foreach ($g in $byPoolCp) {
		$bar = '#' * [int][Math]::Ceiling(($g.Count / [double]$maxPoolCount) * 30)
		$kinds = @($g.Group | Group-Object kind | ForEach-Object { $_.Name })
		Write-Host ("  {0,5} | {1,-30} {2,3}  ({3} 种: {4})" -f $g.Name, $bar, $g.Count, $kinds.Count, ($kinds -join ', '))
	}
	Write-Host ''

	Write-Host '--- 参与池的派系 ---'
	foreach ($g in @($poolEntries | Group-Object faction | Sort-Object Name)) {
		$v = [double[]]@($g.Group | Select-Object -ExpandProperty combatPower | Sort-Object)
		Write-Host ("  {0,-28} n={1,3}  min={2,4}  median={3,5}  max={4,4}" -f `
			$g.Name, $g.Count, $v[0], (Get-Quantile -Sorted $v -Q 0.5), $v[$v.Count - 1])
	}
	Write-Host ''
}

Write-Host '--- 全部人类 PawnKindDef（按 combatPower 升序）---'
Write-Host ("{0,5}  {1,-26} {2,-9} {3,-4} {4,-8} {5,-16} {6}" -f 'CP', 'defName', 'module', '斗?', '抵抗', '武器钱', 'tier 注释 / 标记')
foreach ($row in $rows) {
	$fighter = 'n'
	if ($row.isFighter) { $fighter = 'Y' }
	$flags = @()
	if ($row.factionLeader) { $flags += 'leader' }
	if ($row.trader) { $flags += 'trader' }
	if ($row.isBoss) { $flags += 'boss' }
	$note = $row.tierComment
	if ($flags.Count -gt 0) {
		if ($note) { $note = "$note [$($flags -join ',')]" } else { $note = "[$($flags -join ',')]" }
	}
	Write-Host ("{0,5}  {1,-26} {2,-9} {3,-4} {4,-8} {5,-16} {6}" -f `
		$row.combatPower, $row.defName, $row.module, $fighter, $row.resistance, $row.weaponMoney, $note)
}

# ---------------------------------------------------------------- json

$summary = [ordered]@{
	validCount   = $valid.Count
	invalidCount = $invalid.Count
	min = $null; p10 = $null; p25 = $null; median = $null; p75 = $null; p90 = $null; p95 = $null; max = $null; mean = $null
}
if ($cps.Count -gt 0) {
	$sarr = [double[]]$cps
	$summary.min    = $sarr[0]
	$summary.p10    = Get-Quantile -Sorted $sarr -Q 0.10
	$summary.p25    = Get-Quantile -Sorted $sarr -Q 0.25
	$summary.median = Get-Quantile -Sorted $sarr -Q 0.50
	$summary.p75    = Get-Quantile -Sorted $sarr -Q 0.75
	$summary.p90    = Get-Quantile -Sorted $sarr -Q 0.90
	$summary.p95    = Get-Quantile -Sorted $sarr -Q 0.95
	$summary.max    = $sarr[$sarr.Count - 1]
	$summary.mean   = ($cps | Measure-Object -Average).Average
}

$poolSummary = [ordered]@{
	entries = $poolEntries.Count
	uniqueKinds = (@($poolEntries | Group-Object kind).Count)
	factions = (@($poolEntries | Group-Object faction).Count)
	min = $null; p10 = $null; p25 = $null; median = $null; p75 = $null; p90 = $null; max = $null; mean = $null
}
if ($pcp.Count -gt 0) {
	$parr2 = [double[]]$pcp
	$poolSummary.min    = $parr2[0]
	$poolSummary.p10    = Get-Quantile -Sorted $parr2 -Q 0.10
	$poolSummary.p25    = Get-Quantile -Sorted $parr2 -Q 0.25
	$poolSummary.median = Get-Quantile -Sorted $parr2 -Q 0.50
	$poolSummary.p75    = Get-Quantile -Sorted $parr2 -Q 0.75
	$poolSummary.p90    = Get-Quantile -Sorted $parr2 -Q 0.90
	$poolSummary.max    = $parr2[$parr2.Count - 1]
	$poolSummary.mean   = ($pcp | Measure-Object -Average).Average
}

$payload = [pscustomobject]@{
	generatedAt    = (Get-Date).ToString('s')
	dataRoot       = $DataRoot
	totalHumanlike = $rows.Count
	summary        = [pscustomobject]$summary
	byCombatPower  = @($valid | Group-Object combatPower | Sort-Object { [int]$_.Name } | ForEach-Object {
		[pscustomobject]@{ combatPower = [int]$_.Name; count = $_.Count; kinds = @($_.Group | ForEach-Object { $_.defName }) }
	})
	poolSummary    = $poolSummary
	poolEntries    = @($poolEntries)
	kinds          = @($rows)
	parseErrors    = @($parseErrors)
}

$payload | ConvertTo-Json -Depth 8 | Set-Content -Path $OutJson -Encoding UTF8
Write-Host ''
Write-Host ("明细 JSON 已写入: {0}" -f $OutJson)
if ($parseErrors.Count -gt 0) { Write-Warning ("{0} 个 XML 解析失败" -f $parseErrors.Count) }

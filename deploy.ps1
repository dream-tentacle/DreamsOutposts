param([string]$ModName = "")
# 可选：通过 -ModName 指定部署目标名；未指定时使用模板所在文件夹名（即 mod 根目录名）

$ErrorActionPreference = "Stop"

$mods = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods"

if (-not $ModName) {
    $ModName = Split-Path -Leaf $PSScriptRoot
}

$target = "$mods\$ModName"

# 主干目录：逐个复制，某个目录被占用（如游戏正在播放音效锁定 Sounds）则跳过该目录并继续
$folders = @("About", "Assemblies", "Defs", "Languages", "Textures")

$skipped = @()

foreach ($folder in $folders) {
    $src = Join-Path $PSScriptRoot $folder
    $dst = Join-Path $target $folder

    if (-not (Test-Path $src)) {
        Write-Warning "源目录不存在，跳过: $folder"
        $skipped += $folder
        continue
    }

    # 先清空目标旧目录，避免残留过期文件；清理失败说明被占用 → 跳过该目录（保留旧版本）
    if (Test-Path $dst) {
        try {
            Remove-Item -Recurse -Force $dst -ErrorAction Stop
        }
        catch {
            Write-Warning "旧目录被占用，或权限不足无法修改外部文件。无法清理，跳过: $folder"
            $skipped += $folder
            continue
        }
    }

    # robocopy 退出码 0~7 为成功，>= 8 表示复制失败
    robocopy $src $dst /E /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) {
        Write-Warning "复制失败，跳过: $folder"
        $skipped += $folder
        continue
    }
    Write-Output "已复制: $folder"
}

if ($skipped.Count -gt 0) {
    Write-Warning "以下目录因占用/权限不足/复制失败被跳过（保留旧版本）: $($skipped -join ', ')"
    Write-Warning "若大量目录被跳过，更可能是权限问题"
    exit 2
}

Write-Output "Deployed $ModName to Mods"

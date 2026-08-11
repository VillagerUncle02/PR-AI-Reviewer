# publish.ps1 —— 发布产物构建脚本（002-package-release / T003）
#
# 职责（contracts/release-cli.md、contracts/release-artifact.md、research.md D2/D3/D5、plan.md）：
#   SemVer 校验 -> 已跟踪工作区干净校验（untracked 仅警告）-> 清理旧 dist/<version>/
#   -> dotnet publish（win-x64 框架依赖）-> 写 VERSION / BUILD_INFO
#   -> 敏感扫描（仅产物目录）-> 生成 zip 与 sha256（dist/ 根）。
#
# 退出码：0 成功 / 1 前置校验或执行失败 / 2 参数非法。
# -DryRun：只校验参数与工作区并打印计划，不构建、不清理、不写任何文件。

[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# SemVer 2.0.0（semver.org 规范正则，不带 v 前缀）
# 完整支持 pre-release（如 1.0.0-rc.1）与 build metadata（如 1.0.0+build.5），拒绝前导零（如 01.0.0）。
# 该正则将作为 publish/smoke 脚本的统一 SemVer 标准。
$SemVerPattern = '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$'

function Write-ErrorLine {
    param([string]$Message)
    [Console]::Error.WriteLine("publish.ps1: $Message")
}

# 可选改进：校验前去除首尾空白
$Version = $Version.Trim()

# ---- 0. 参数校验：非法 SemVer 退出 2 ----
if ($Version -notmatch $SemVerPattern) {
    Write-ErrorLine "非法版本号 '$Version'（必须符合 SemVer 2.0.0，例如 1.0.0，无 v 前缀）"
    exit 2
}

try {
    $RepoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
    $DistRoot = [IO.Path]::GetFullPath((Join-Path $RepoRoot "dist"))
    $OutDir = [IO.Path]::GetFullPath((Join-Path $DistRoot $Version))
    $Project = [IO.Path]::GetFullPath((Join-Path $RepoRoot "src\PrReviewSubmit\PrReviewSubmit.csproj"))
    $ZipName = "PrReviewSubmit-$Version-win-x64.zip"
    $ZipPath = Join-Path $DistRoot $ZipName
    $ShaPath = "$ZipPath.sha256"
    $Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

    if (-not (Test-Path -LiteralPath $Project)) {
        throw "找不到项目文件: $Project"
    }
    # 防御：产物目录必须位于 dist/ 之下（版本号已过 SemVer 校验，路径安全）
    if (-not $OutDir.StartsWith($DistRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "产物目录越界: $OutDir"
    }

    # ---- 1. 工作区校验：已跟踪改动阻止；untracked 仅警告 ----
    $trackedChanges = @(git -C $RepoRoot status --porcelain --untracked-files=no 2>$null)
    if ($LASTEXITCODE -ne 0) {
        throw "git status 失败（退出码 $LASTEXITCODE），无法校验工作区"
    }
    if ($trackedChanges.Count -gt 0) {
        throw "已跟踪工作区不干净，请先提交或还原以下改动：`n$($trackedChanges -join "`n")"
    }

    $porcelainAll = @(git -C $RepoRoot status --porcelain 2>$null)
    if ($LASTEXITCODE -ne 0) {
        throw "git status 失败（退出码 $LASTEXITCODE），无法读取未跟踪文件"
    }
    $untracked = @($porcelainAll | Where-Object { $_ -match '^\?\?' })
    if ($untracked.Count -gt 0) {
        Write-Output "警告：存在未跟踪文件（不阻止构建，但请确认未遗漏应提交内容）："
        $untracked | ForEach-Object { Write-Output "  $_" }
    }

    # ---- 2. 构建 commit（BUILD_INFO 可追溯，research D10）----
    $commitLines = @(git -C $RepoRoot rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or $commitLines.Count -ne 1 -or $commitLines[0] -notmatch '^[0-9a-f]{40}$') {
        throw "无法获取 40 位 HEAD commit（git rev-parse HEAD 失败）"
    }
    $Commit = $commitLines[0].Trim()

    # ---- 3. DryRun：打印计划后退出 0，不产生任何外部变更 ----
    if ($DryRun) {
        Write-Output "[DryRun] publish.ps1 发布计划（不执行任何变更）"
        Write-Output "  版本        : $Version（SemVer 校验通过）"
        Write-Output "  工作区      : 已跟踪文件干净"
        if ($untracked.Count -gt 0) {
            Write-Output "  未跟踪文件  : $($untracked.Count) 个（仅警告，不阻止）"
        }
        Write-Output "  构建 commit : $Commit"
        Write-Output "  计划步骤    :"
        Write-Output "    1. 清理旧产物目录 dist/$Version/ 及同版本旧 zip/sha256（如存在）"
        Write-Output "    2. dotnet publish $Project -c Release -r win-x64 --self-contained false -o $OutDir /p:Version=$Version"
        Write-Output "    3. 写入 dist/$Version/VERSION（内容: $Version）与 BUILD_INFO（version=$Version / commit=$Commit）"
        Write-Output "    4. 敏感扫描 dist/$Version/（文件名黑名单 + 私钥/令牌内容模式）"
        Write-Output "    5. 生成 $ZipPath"
        Write-Output "    6. 生成 $ShaPath（格式: <64位小写sha256>  $ZipName）"
        Write-Output "  预期产物    :"
        Write-Output "    产物目录   : $OutDir"
        Write-Output "    VERSION    : $Version"
        Write-Output "    BUILD_INFO : version=$Version / commit=$Commit"
        Write-Output "    zip        : $ZipPath"
        Write-Output "    sha256     : $ShaPath"
        exit 0
    }

    # ---- 4. 清理旧 dist/<version>/ ----
    Write-Output "== 清理旧产物 =="
    if (Test-Path -LiteralPath $OutDir) {
        Remove-Item -LiteralPath $OutDir -Recurse -Force
        Write-Output "已清理 $OutDir"
    }
    else {
        Write-Output "无需清理：$OutDir 不存在"
    }
    # 同版本旧 zip/sha256 一并清理，避免构建失败时残留误导性产物
    foreach ($staleFile in @($ZipPath, $ShaPath)) {
        if (Test-Path -LiteralPath $staleFile) {
            Remove-Item -LiteralPath $staleFile -Force
            Write-Output "已清理 $staleFile"
        }
    }

    # ---- 5. dotnet publish：Windows x64 框架依赖（research D2），/p:Version 对齐程序集版本（research D3）----
    Write-Output "== dotnet publish（win-x64 框架依赖）=="
    dotnet publish $Project -c Release -r win-x64 --self-contained false -o $OutDir /p:Version=$Version
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish 失败（退出码 $LASTEXITCODE）"
    }

    $ExePath = Join-Path $OutDir "PrReviewSubmit.exe"
    if (-not (Test-Path -LiteralPath $ExePath)) {
        throw "产物缺少可执行入口: $ExePath"
    }

    # ---- 6. 写 VERSION 与 BUILD_INFO（UTF-8 无 BOM）----
    Write-Output "== 写版本与构建信息 =="
    $versionFile = Join-Path $OutDir "VERSION"
    $buildInfoFile = Join-Path $OutDir "BUILD_INFO"
    [IO.File]::WriteAllText($versionFile, $Version, $Utf8NoBom)
    [IO.File]::WriteAllText($buildInfoFile, "version=$Version`ncommit=$Commit", $Utf8NoBom)
    Write-Output "已写入 $versionFile（$Version）"
    Write-Output "已写入 $buildInfoFile（version=$Version, commit=$Commit）"

    # ---- 7. 敏感扫描（仅 dist/<version>/，research D5 / FR-003）----
    Write-Output "== 敏感扫描（仅 dist/$Version/）=="
    $namePattern = '(?i)((^|/)private-key(/|$)|\.(pem|key|p12|pfx)$|(^|/)\.env)'
    $contentPattern = '(-----BEGIN[^\r\n]*PRIVATE KEY-----|ghp_|github_pat_|gho_|ghs_)'
    $sensitiveHits = @()
    Get-ChildItem -LiteralPath $OutDir -Recurse -Force | ForEach-Object {
        $rel = $_.FullName.Substring($OutDir.Length).TrimStart([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar).Replace('\', '/')
        if ($rel -match $namePattern) {
            $sensitiveHits += "文件名命中: $rel"
        }
        elseif (-not $_.PSIsContainer) {
            $text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($_.FullName))
            if ($text -match $contentPattern) {
                $sensitiveHits += "内容命中: $rel"
            }
        }
    }
    if ($sensitiveHits.Count -gt 0) {
        foreach ($hit in $sensitiveHits) {
            Write-ErrorLine "敏感扫描失败: $hit"
        }
        throw "敏感扫描失败（$($sensitiveHits.Count) 个命中），已中止打包"
    }
    Write-Output "敏感扫描通过（0 个命中）"

    # ---- 8. 生成 zip（dist/ 根；产物平铺，保留 runtimes/ 子目录）----
    Write-Output "== 生成 zip =="
    Compress-Archive -Path (Join-Path $OutDir "*") -DestinationPath $ZipPath -Force
    if (-not (Test-Path -LiteralPath $ZipPath)) {
        throw "zip 生成失败: $ZipPath"
    }

    # ---- 9. 生成 sha256（<64位小写十六进制>  <文件名>，research D4）----
    Write-Output "== 生成 sha256 =="
    $hash = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText($ShaPath, "$hash  $ZipName`n", $Utf8NoBom)

    Write-Output ""
    Write-Output "== 发布产物完成 =="
    Write-Output "产物目录: $OutDir"
    Write-Output "VERSION: $Version"
    Write-Output "BUILD_INFO: version=$Version / commit=$Commit"
    Write-Output "zip: $ZipPath"
    Write-Output "sha256: $ShaPath"
    Write-Output "敏感扫描: 通过"
    exit 0
}
catch {
    Write-ErrorLine $_.Exception.Message
    exit 1
}

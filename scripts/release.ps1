#Requires -Version 7.0
# release.ps1 —— 正式发布脚本（specs/002-package-release / T009）
#
# 契约：specs/002-package-release/contracts/release-cli.md
#        specs/002-package-release/contracts/release-process.md
# 职责（research D6/D13/D15、plan.md）：
#   SemVer 校验 -> scripts/gates.ps1（非 DryRun）-> 前置校验
#   （产物/VERSION/BUILD_INFO、VERSION 内容==请求版本、BUILD_INFO.commit==origin/main HEAD、
#    sha256 匹配、冒烟审计 success、gh 认证、tag 不存在或存在但无 Release（补建路径 FR-015））
#   -> 生成/使用发布说明 -> git tag + push 或补建跳过 -> gh release create
#   -> 写审计 notes/reviews/<version>-release.md。
#
# 退出码：0 成功 / 1 前置校验或执行失败 / 2 参数非法。
# -DryRun：打印全部校验结果与将执行的 tag/Release/notes 计划；
#          不运行 gates、不创建 tag/Release、不写文件、不调用 GitHub（仅执行只读本地查询）。

[CmdletBinding()]
param(
    [string]$Version,
    [string]$NotesFile,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)

$script:checkResults = [System.Collections.Generic.List[object]]::new()
$script:checkFailures = [System.Collections.Generic.List[string]]::new()

function Write-Err {
    param([string]$Message)
    [Console]::Error.WriteLine("release.ps1: $Message")
}

function Add-CheckResult {
    param([string]$Name, [bool]$Pass, [string]$Detail)
    $script:checkResults.Add([pscustomobject]@{ Name = $Name; Pass = $Pass; Detail = $Detail })
    if (-not $Pass) {
        $script:checkFailures.Add("$Name：$Detail")
    }
    $mark = if ($Pass) { "[PASS]" } else { "[FAIL]" }
    Write-Output ("{0} {1}：{2}" -f $mark, $Name, $Detail)
}

function Get-PreviousReleaseTag {
    param([string]$Commit, [string]$RepoRoot, [string]$CurrentTag)
    if ($Commit -notmatch '^[0-9a-f]{40}$') {
        return ""
    }
    $tags = @(& git -C $RepoRoot tag --merged $Commit --list "v*" --sort=-version:refname 2>$null)
    foreach ($t in $tags) {
        $name = ([string]$t).Trim()
        if (-not [string]::IsNullOrWhiteSpace($name) -and $name -cne $CurrentTag) {
            return $name
        }
    }
    return ""
}

function Test-TagPointsToBuildCommit {
    param([string]$RepoRoot, [string]$TagName, [string]$BuildCommit)
    # 补建路径（FR-015）安全校验：既有 tag 必须指向 BUILD_INFO.buildCommit。
    # 返回空字符串表示一致；否则返回失败详情（纯本地 git 操作，DryRun 保留执行）。
    if ([string]::IsNullOrWhiteSpace($BuildCommit)) {
        return "tag $TagName 已存在但 BUILD_INFO 未提供 buildCommit，无法校验其指向；请先确认产物后重试"
    }
    $tagCommitOut = & git -C $RepoRoot rev-parse "$TagName^{commit}" 2>&1
    if ($LASTEXITCODE -ne 0) {
        return "tag $TagName 已存在但本地无法解析其 commit（git rev-parse `"$TagName^{commit}`" 失败）：$((($tagCommitOut | Out-String)).Trim())；请人工核对本地 tag（严禁删除远端 tag），必要时人工删除本地错误 tag 后重试"
    }
    $tagCommit = (($tagCommitOut | Out-String)).Trim()
    if ($tagCommit -cne $BuildCommit) {
        return "tag $TagName 已存在但指向 $tagCommit，与 BUILD_INFO.buildCommit=$BuildCommit 不一致；请人工核对本地 tag（严禁删除远端 tag），必要时人工删除本地错误 tag 后重试"
    }
    return ""
}

# ---- 0. 参数校验：-Version 必填；SemVer 校验与 scripts/publish.ps1 完全一致 ----
if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Err "错误: 缺少必填参数 -Version（语义化版本号，如 1.0.0）"
    exit 2
}
$Version = $Version.Trim()
# SemVer 2.0.0（semver.org 规范正则，不带 v 前缀；与 scripts/publish.ps1 完全一致）
# 完整支持 pre-release（如 1.0.0-rc.1）与 build metadata（如 1.0.0+build.5），拒绝前导零（如 01.0.0）。
$SemVerPattern = '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$'
if ($Version -notmatch $SemVerPattern) {
    Write-Err "错误: -Version 不是有效 SemVer 2.0.0：$Version（允许形如 1.0.0 / 1.0.0-beta.1 / 1.0.0+build.5，不允许 v 前缀）"
    exit 2
}

try {
    $RepoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
    $DistRoot = [IO.Path]::GetFullPath((Join-Path $RepoRoot "dist"))
    $OutDir = [IO.Path]::GetFullPath((Join-Path $DistRoot $Version))
    $VersionFile = Join-Path $OutDir "VERSION"
    $BuildInfoFile = Join-Path $OutDir "BUILD_INFO"
    $ZipName = "PrReviewSubmit-$Version-win-x64.zip"
    $ZipPath = Join-Path $DistRoot $ZipName
    $ShaPath = "$ZipPath.sha256"
    $TagName = "v$Version"
    $SmokeAuditPath = Join-Path $RepoRoot "notes\reviews\$Version-smoke.md"
    $ReleaseAuditPath = Join-Path $RepoRoot "notes\reviews\$Version-release.md"
    $Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $buildCommit = ""
    $ghRepo = ""
    $tagExists = $false
    $localTagExists = $false
    $remoteTagExists = $false
    $releaseExists = $false
    $releaseUrl = ""

    # -NotesFile：可选；提供时必须是已存在文件（文件缺失属前置校验失败，退出 1）
    $notesPath = ""
    if (-not [string]::IsNullOrWhiteSpace($NotesFile)) {
        $notesPath = if ([IO.Path]::IsPathRooted($NotesFile)) {
            [IO.Path]::GetFullPath($NotesFile)
        } else {
            [IO.Path]::GetFullPath((Join-Path $RepoRoot $NotesFile))
        }
        if (-not (Test-Path -LiteralPath $notesPath -PathType Leaf)) {
            Write-Err "错误: -NotesFile 文件不存在：$notesPath"
            exit 1
        }
    }

    # ---- 1. 自动运行 gates（非 DryRun；DryRun 只打印计划）----
    if (-not $DryRun) {
        Write-Output "== 运行 scripts/gates.ps1（全量门禁）=="
        $pwshExe = Join-Path $PSHOME "pwsh.exe"
        & $pwshExe -NoProfile -File (Join-Path $PSScriptRoot "gates.ps1")
        if ($LASTEXITCODE -ne 0) {
            throw "scripts/gates.ps1 失败（退出码 $LASTEXITCODE），发布中止"
        }
    }
    else {
        Write-Output "[DryRun] 步骤 0：将运行 scripts/gates.ps1（全量门禁：构建/测试/私钥排除/格式/脚本语法）——本模式跳过"
    }

    # ---- 2. 前置校验（全部执行并打印结果；任一失败 -> 退出 1，不产生外部变更）----
    Write-Output ""
    Write-Output "== 前置校验 =="

    # 2.1 产物与 VERSION/BUILD_INFO 存在
    $artifactOk = $true
    $artifactDetail = ""
    if (-not (Test-Path -LiteralPath $OutDir -PathType Container)) {
        $artifactOk = $false
        $artifactDetail = "产物目录不存在: $OutDir"
    }
    elseif (-not (Test-Path -LiteralPath $VersionFile -PathType Leaf)) {
        $artifactOk = $false
        $artifactDetail = "缺少 VERSION: $VersionFile"
    }
    elseif (-not (Test-Path -LiteralPath $BuildInfoFile -PathType Leaf)) {
        $artifactOk = $false
        $artifactDetail = "缺少 BUILD_INFO: $BuildInfoFile"
    }
    else {
        $artifactDetail = "产物目录与 VERSION/BUILD_INFO 存在"
    }
    Add-CheckResult "产物与 VERSION/BUILD_INFO 存在" $artifactOk $artifactDetail

    # 2.2 VERSION 内容 == 请求版本
    $versionOk = $false
    $versionDetail = ""
    if (-not (Test-Path -LiteralPath $VersionFile -PathType Leaf)) {
        $versionDetail = "VERSION 文件缺失: $VersionFile"
    }
    else {
        $versionContent = (Get-Content -LiteralPath $VersionFile -Raw).Trim()
        if ($versionContent -ceq $Version) {
            $versionOk = $true
            $versionDetail = "VERSION 内容 '$versionContent' == 请求版本 '$Version'"
        }
        else {
            $versionDetail = "VERSION 内容 '$versionContent' != 请求版本 '$Version'"
        }
    }
    Add-CheckResult "VERSION 内容 == 请求版本" $versionOk $versionDetail

    # 2.3 BUILD_INFO.commit == origin/main HEAD
    $commitOk = $false
    $commitDetail = ""
    if (-not (Test-Path -LiteralPath $BuildInfoFile -PathType Leaf)) {
        $commitDetail = "BUILD_INFO 文件缺失: $BuildInfoFile"
    }
    else {
        $buildInfoText = Get-Content -LiteralPath $BuildInfoFile -Raw
        $commitMatch = [regex]::Match($buildInfoText, '(?im)^commit=([0-9a-f]{40})\s*$')
        if (-not $commitMatch.Success) {
            $commitDetail = "BUILD_INFO 缺少 commit=<40位sha> 行"
        }
        else {
            $buildCommit = $commitMatch.Groups[1].Value.ToLowerInvariant()
            $originMainOut = & git -C $RepoRoot rev-parse origin/main 2>&1
            if ($LASTEXITCODE -ne 0) {
                $commitDetail = "无法获取 origin/main HEAD（git rev-parse 失败）：$((($originMainOut | Out-String)).Trim())"
            }
            else {
                $originMain = ($originMainOut | Out-String).Trim()
                if ($originMain -notmatch '^[0-9a-f]{40}$') {
                    $commitDetail = "origin/main 返回值异常: $originMain"
                }
                elseif ($buildCommit -ceq $originMain) {
                    $commitOk = $true
                    $commitDetail = "BUILD_INFO.commit=$buildCommit == origin/main HEAD"
                }
                else {
                    $commitDetail = "BUILD_INFO.commit=$buildCommit != origin/main HEAD=$originMain（变更未合入 main，或 origin/main 过期需先 git fetch origin main）"
                }
            }
        }
    }
    Add-CheckResult "BUILD_INFO.commit == origin/main HEAD" $commitOk $commitDetail

    # 2.4 zip 与 .sha256 存在且哈希匹配
    $hashOk = $false
    $hashDetail = ""
    if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
        $hashDetail = "zip 不存在: $ZipPath"
    }
    elseif (-not (Test-Path -LiteralPath $ShaPath -PathType Leaf)) {
        $hashDetail = "sha256 不存在: $ShaPath"
    }
    else {
        $expectedHash = ""
        foreach ($shaLine in @(Get-Content -LiteralPath $ShaPath)) {
            if ($shaLine -match '^\s*([0-9a-fA-F]{64})\s+') {
                $expectedHash = $matches[1].ToLowerInvariant()
                break
            }
        }
        if ([string]::IsNullOrWhiteSpace($expectedHash)) {
            $hashDetail = "sha256 文件格式非法（应为 <64位小写十六进制>  <文件名>）: $ShaPath"
        }
        else {
            $actualHash = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($actualHash -ceq $expectedHash) {
                $hashOk = $true
                $hashDetail = "SHA-256 匹配（$actualHash）"
            }
            else {
                $hashDetail = "SHA-256 不匹配：期望 $expectedHash，实际 $actualHash"
            }
        }
    }
    Add-CheckResult "zip 与 sha256 存在且哈希匹配" $hashOk $hashDetail

    # 2.5 冒烟审计 success（notes/reviews/<version>-smoke.md）
    # 契约字面要求 status=success；smoke-published.ps1 当前审计文件写「- 状态：success」，
    # 两者都接受，避免发布被既有冒烟实现永久阻断（任务不允许修改 smoke-published.ps1）。
    # 匹配使用行锚定精确匹配（允许可选的 Markdown 列表前缀 -），避免 status=successful 等误命中。
    $smokeOk = $false
    $smokeDetail = ""
    if (-not (Test-Path -LiteralPath $SmokeAuditPath -PathType Leaf)) {
        $smokeDetail = "冒烟审计不存在: $SmokeAuditPath"
    }
    else {
        $smokeText = Get-Content -LiteralPath $SmokeAuditPath -Raw
        if ($smokeText -match '(?m)^\s*(?:-\s*)?(?:status|状态)\s*[:=：]\s*success\s*$') {
            $smokeOk = $true
            $smokeDetail = "冒烟审计存在且记录 success: $SmokeAuditPath"
        }
        else {
            $smokeDetail = "冒烟审计存在但未记录 success（需 status=success 或 状态：success）: $SmokeAuditPath"
        }
    }
    Add-CheckResult "冒烟审计 success" $smokeOk $smokeDetail

    # 2.6 gh 认证（GH_TOKEN 设置时视为可用，keyring default 失效不影响；参考 wait-ci 判定）
    # DryRun 契约：不调用 GitHub（release-cli.md），gh auth status 留待真实模式执行。
    $authOk = $false
    $authDetail = ""
    if ($DryRun) {
        $authOk = $true
        $authDetail = "DryRun 不调用 GitHub；真实模式将执行 gh auth status 确认认证"
    }
    elseif ($null -eq (Get-Command gh -ErrorAction SilentlyContinue)) {
        $authDetail = "未找到 gh CLI（请安装 GitHub CLI）"
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:GH_TOKEN)) {
        $authOk = $true
        $authDetail = "GH_TOKEN 已设置，视为已认证（keyring default 失效不影响）"
    }
    else {
        $authOut = & gh auth status 2>&1
        if ($LASTEXITCODE -eq 0) {
            $authOk = $true
            $authDetail = "gh auth status 通过"
        }
        else {
            $authDetail = "gh auth status 失败：$((($authOut | Out-String)).Trim())"
        }
    }
    Add-CheckResult "gh 认证" $authOk $authDetail

    # 2.7 tag/Release 状态：tag 不存在，或 tag 存在但无对应 Release（补建路径 FR-015）。
    # 补建路径必须先确认既有 tag 指向 BUILD_INFO.buildCommit（FR-015 安全校验）。
    # DryRun 契约：不调用 GitHub（release-cli.md），只做本地 tag 查询/校验，远端状态留待真实模式。
    $tagCheckPass = $false
    $tagDetail = ""
    $remoteUrl = (& git -C $RepoRoot remote get-url origin 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($remoteUrl)) {
        $tagDetail = "无法获取 origin 远程地址（git remote get-url origin 失败）"
    }
    elseif ($remoteUrl -notmatch 'github\.com[/:]([^/]+)/([^/]+?)(\.git)?$') {
        $tagDetail = "origin 不是 GitHub 远程仓库：$remoteUrl"
    }
    else {
        $ghRepo = "$($Matches[1])/$($Matches[2])"
        $tagCheckPass = $true

        $localTagOut = & git -C $RepoRoot rev-parse -q --verify "refs/tags/$TagName" 2>$null
        $localTagExists = ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace(([string]$localTagOut).Trim()))

        if ($DryRun) {
            # 仅执行本地 git 操作；不调用 gh api / gh release view。
            if ($localTagExists) {
                $tagExists = $true
                $tagCommitError = Test-TagPointsToBuildCommit -RepoRoot $RepoRoot -TagName $TagName -BuildCommit $buildCommit
                if ($tagCommitError) {
                    $tagCheckPass = $false
                    $tagDetail = $tagCommitError
                }
                else {
                    $tagDetail = "本地 tag $TagName 已存在且指向 buildCommit=$buildCommit；真实模式将查询远端 tag/Release 状态（DryRun 不调用 GitHub）"
                }
            }
            else {
                $tagDetail = "未发现本地 tag $TagName；真实模式将查询远端 tag/Release 状态（DryRun 不调用 GitHub）"
            }
        }
        else {
            $remoteTagOut = & gh api "repos/$ghRepo/git/ref/tags/$TagName" 2>&1
            if ($LASTEXITCODE -eq 0) {
                $remoteTagExists = $true
            }
            elseif (($remoteTagOut | Out-String) -notmatch '404') {
                $tagCheckPass = $false
                $tagDetail = "无法查询远端 tag $TagName：$((($remoteTagOut | Out-String)).Trim())"
            }

            if ($tagCheckPass) {
                $tagExists = $localTagExists -or $remoteTagExists
                if (-not $tagExists) {
                    $tagDetail = "tag $TagName 不存在（将创建并推送）"
                }
                else {
                    $releaseViewOut = & gh release view $TagName --repo $ghRepo --json id,url 2>&1
                    if ($LASTEXITCODE -eq 0) {
                        $releaseJson = (($releaseViewOut | Out-String) | ConvertFrom-Json)
                        $releaseExists = $true
                        $releaseUrl = [string]$releaseJson.url
                        $tagCheckPass = $false
                        $tagDetail = "tag 与 GitHub Release 均已存在（禁止覆盖 FR-004/FR-015）：$releaseUrl"
                    }
                    elseif (($releaseViewOut | Out-String) -match 'not found|404|Could not resolve to a Release') {
                        $tagCommitError = Test-TagPointsToBuildCommit -RepoRoot $RepoRoot -TagName $TagName -BuildCommit $buildCommit
                        if ($tagCommitError) {
                            $tagCheckPass = $false
                            $tagDetail = $tagCommitError
                        }
                        elseif ($localTagExists -and $remoteTagExists) {
                            $tagDetail = "tag $TagName 已存在（本地+远端）且指向 buildCommit=$buildCommit，无对应 Release（补建路径：跳过打 tag，直接创建 Release）"
                        }
                        elseif ($localTagExists) {
                            $tagDetail = "本地 tag $TagName 已存在且指向 buildCommit=$buildCommit、远端缺失（补建路径：推送已有 tag 后创建 Release）"
                        }
                        else {
                            $tagDetail = "远端 tag $TagName 已存在且指向 buildCommit=$buildCommit、本地缺失（补建路径：跳过打 tag，直接创建 Release）"
                        }
                    }
                    else {
                        $tagCheckPass = $false
                        $tagDetail = "tag $TagName 已存在，但无法确认 Release 状态：$((($releaseViewOut | Out-String)).Trim())"
                    }
                }
            }
        }
    }
    Add-CheckResult "tag/Release 状态（不存在或可补建）" $tagCheckPass $tagDetail

    $anyFailed = $script:checkFailures.Count -gt 0

    # ---- 3. DryRun：打印发布计划后按校验结果退出 ----
    if ($DryRun) {
        Write-Output ""
        Write-Output "[DryRun] 发布计划（仅在全部校验通过时执行；本模式未执行任何变更）"
        if ($tagCheckPass) {
            if ($localTagExists) {
                Write-Output "[DryRun] tag：本地 tag $TagName 已存在（已校验指向 buildCommit）；真实模式将查询远端 tag/Release 状态（DryRun 不调用 GitHub）后决定推送或跳过（补建路径 FR-015）"
            }
            else {
                Write-Output "[DryRun] tag：本地未发现 $TagName；真实模式将查询远端 tag/Release 状态（DryRun 不调用 GitHub）后决定创建/补建"
            }
        }
        else {
            Write-Output "[DryRun] tag：待 tag/Release 校验通过后确定"
        }

        if (-not [string]::IsNullOrWhiteSpace($notesPath)) {
            Write-Output "[DryRun] 发布说明：使用指定文件 $notesPath"
        }
        else {
            $autoNotesPath = Join-Path $RepoRoot "RELEASE_NOTES-$Version.md"
            $planRange = ""
            if ($buildCommit -match '^[0-9a-f]{40}$') {
                $prevTag = Get-PreviousReleaseTag -Commit $buildCommit -RepoRoot $RepoRoot -CurrentTag $TagName
                $planRange = if ($prevTag) { "$prevTag..$buildCommit" } else { "仓库全部历史（首次发布，无上一 tag）" }
            }
            Write-Output "[DryRun] 发布说明：将自动生成 $autoNotesPath"
            if ($planRange) {
                Write-Output "[DryRun]   git log 范围：$planRange"
            }
            Write-Output "[DryRun]   生成后提示可人工编辑（确认后继续发布）"
        }

        if (-not [string]::IsNullOrWhiteSpace($ghRepo)) {
            Write-Output "[DryRun] Release：将执行 gh release create $TagName $ZipPath $ShaPath --repo $ghRepo --title `"PrReviewSubmit $TagName`" --notes-file <notes>"
        }
        else {
            Write-Output "[DryRun] Release：待 gh 仓库标识解析通过后执行"
        }
        Write-Output "[DryRun] 审计：将写 $ReleaseAuditPath（版本/commit/tag/Release URL/操作时间）"
        Write-Output ""

        if ($anyFailed) {
            Write-Err "前置校验失败（$($script:checkFailures.Count) 项），DryRun 退出 1："
            foreach ($f in $script:checkFailures) {
                Write-Err "  - $f"
            }
            exit 1
        }
        Write-Output "[DryRun] 全部前置校验通过，将按上述计划执行。"
        exit 0
    }

    # ---- 4. 非 DryRun：任一校验失败立即退出 ----
    if ($anyFailed) {
        Write-Err "前置校验失败（$($script:checkFailures.Count) 项），不执行发布："
        foreach ($f in $script:checkFailures) {
            Write-Err "  - $f"
        }
        exit 1
    }

    # ---- 5. 发布说明：-NotesFile 或自动生成（git log 自上一 tag；首次发布用全部历史）----
    if ([string]::IsNullOrWhiteSpace($notesPath)) {
        $autoNotesPath = Join-Path $RepoRoot "RELEASE_NOTES-$Version.md"
        $prevTag = Get-PreviousReleaseTag -Commit $buildCommit -RepoRoot $RepoRoot -CurrentTag $TagName
        if (-not [string]::IsNullOrWhiteSpace($prevTag)) {
            $rangeText = "$prevTag..$buildCommit"
            $logArgs = @("log", "--oneline", "--no-decorate", "--no-merges", $rangeText)
        }
        else {
            $rangeText = "仓库全部历史（首次发布，无上一 tag）"
            $logArgs = @("log", "--oneline", "--no-decorate", "--no-merges", $buildCommit)
        }
        $logLines = @(& git -C $RepoRoot @logArgs 2>&1)
        if ($LASTEXITCODE -ne 0) {
            throw "git log 获取变更摘要失败（退出码 $LASTEXITCODE）"
        }

        $featLines = @($logLines | Where-Object { ([string]$_) -match '^(feat|feature)(\([^)]*\))?\s*:' })
        $fixLines = @($logLines | Where-Object { ([string]$_) -match '^(fix|bugfix)(\([^)]*\))?\s*:' })
        $otherLines = @($logLines | Where-Object { ([string]$_) -notmatch '^(feat|feature|fix|bugfix)(\([^)]*\))?\s*:' })

        $noteLines = [System.Collections.Generic.List[string]]::new()
        $noteLines.Add("# PrReviewSubmit $TagName 发布说明")
        $noteLines.Add("")
        $noteLines.Add("- 版本：$Version")
        $noteLines.Add("- 日期：$([DateTime]::UtcNow.ToString('yyyy-MM-dd'))")
        $noteLines.Add("- 构建 commit：$buildCommit")
        $noteLines.Add("- 变更范围：$rangeText")
        $noteLines.Add("")
        $noteLines.Add("## 变更摘要")
        $noteLines.Add("")
        if ($featLines.Count -gt 0) {
            $noteLines.Add("### 新功能")
            foreach ($l in $featLines) {
                $noteLines.Add("- $([string]$l)")
            }
            $noteLines.Add("")
        }
        if ($fixLines.Count -gt 0) {
            $noteLines.Add("### 修复")
            foreach ($l in $fixLines) {
                $noteLines.Add("- $([string]$l)")
            }
            $noteLines.Add("")
        }
        $noteLines.Add("### 其他")
        if ($otherLines.Count -gt 0) {
            foreach ($l in $otherLines) {
                $noteLines.Add("- $([string]$l)")
            }
        }
        else {
            $noteLines.Add("- 无")
        }
        $noteLines.Add("")
        $noteLines.Add("> 由 release.ps1 自动生成；可人工编辑后用 -NotesFile 指定编辑后的文件。")
        [IO.File]::WriteAllText($autoNotesPath, ($noteLines -join "`n") + "`n", $Utf8NoBom)
        $notesPath = $autoNotesPath
        Write-Output "已生成发布说明：$notesPath（git log 范围：$rangeText）"

        # 提示可人工编辑：回车继续，输入 no 取消
        $answer = ""
        try {
            $answer = Read-Host "请人工检查/编辑发布说明（编辑后请用 -NotesFile 指定）。按 Enter 继续发布，输入 no 取消"
        }
        catch {
            Write-Err "无法读取交互确认（$($_.Exception.Message)），发布已取消"
            exit 1
        }
        if ($answer -match '^(n|no|q|quit|c|cancel)$') {
            Write-Err "已取消发布（未创建 tag/Release；发布说明已生成：$notesPath）"
            exit 1
        }
    }
    else {
        Write-Output "使用指定发布说明：$notesPath"
    }

    # ---- 6. tag：不存在 -> 创建并推送；已存在且无 Release -> 补建（FR-015）----
    if (-not $tagExists) {
        Write-Output "== 创建并推送 tag $TagName =="
        & git -C $RepoRoot tag $TagName $buildCommit
        if ($LASTEXITCODE -ne 0) {
            throw "git tag $TagName 失败（退出码 $LASTEXITCODE）"
        }
        & git -C $RepoRoot push origin $TagName
        if ($LASTEXITCODE -ne 0) {
            throw "git push origin $TagName 失败（退出码 $LASTEXITCODE；tag 已本地创建，请排查后重试，重试将走补建路径 FR-015）"
        }
        Write-Output "tag $TagName 已创建并推送（$buildCommit）"
    }
    elseif ($localTagExists -and -not $remoteTagExists) {
        Write-Output "== 推送已有本地 tag $TagName（远端缺失，补建路径 FR-015）=="
        & git -C $RepoRoot push origin $TagName
        if ($LASTEXITCODE -ne 0) {
            throw "git push origin $TagName 失败（退出码 $LASTEXITCODE）"
        }
        Write-Output "tag $TagName 已推送（$buildCommit）"
    }
    else {
        Write-Output "== tag $TagName 已存在，跳过创建（补建路径 FR-015）=="
    }

    # ---- 7. gh release create ----
    Write-Output "== 创建 GitHub Release $TagName =="
    $createOut = & gh release create $TagName $ZipPath $ShaPath --repo $ghRepo --title "PrReviewSubmit $TagName" --notes-file $notesPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "gh release create 失败（退出码 $LASTEXITCODE）：$((($createOut | Out-String)).Trim())"
    }
    $createText = ($createOut | Out-String).Trim()
    if ($createText) {
        Write-Output $createText
    }

    $viewOut = & gh release view $TagName --repo $ghRepo --json url 2>&1
    if ($LASTEXITCODE -eq 0) {
        $releaseUrl = [string]((($viewOut | Out-String)) | ConvertFrom-Json).url
    }
    else {
        $releaseUrl = "https://github.com/$ghRepo/releases/tag/$TagName"
        Write-Err "警告: Release 已创建，但读取 URL 失败（退出码 $LASTEXITCODE），使用 tag URL：$releaseUrl"
    }

    # ---- 8. 写审计 notes/reviews/<version>-release.md ----
    $auditDir = Split-Path -Parent $ReleaseAuditPath
    New-Item -ItemType Directory -Force -Path $auditDir | Out-Null
    $timestamp = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
    $auditLines = @(
        "# 发布审计：PrReviewSubmit $Version",
        "",
        "- 状态：success",
        "- 版本：$Version",
        "- commit：$buildCommit",
        "- tag：$TagName",
        "- Release URL：$releaseUrl",
        "- 时间：$timestamp",
        "- 审计文件：$ReleaseAuditPath"
    )
    try {
        [IO.File]::WriteAllText($ReleaseAuditPath, ($auditLines -join "`n") + "`n", $Utf8NoBom)
    }
    catch {
        throw "Release 已创建（$releaseUrl），但审计文件写入失败：$($_.Exception.Message)；请人工补写 $ReleaseAuditPath"
    }

    Write-Output ""
    Write-Output "== 发布完成 =="
    Write-Output "tag=$TagName"
    Write-Output "releaseUrl=$releaseUrl"
    Write-Output "审计文件=$ReleaseAuditPath"
    exit 0
}
catch {
    Write-Err $_.Exception.Message
    exit 1
}

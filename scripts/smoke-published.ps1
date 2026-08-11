#Requires -Version 7.0
# smoke-published.ps1 —— 发布产物冒烟脚本（specs/002-package-release）
#
# 契约：specs/002-package-release/contracts/release-cli.md
#        specs/002-package-release/contracts/release-artifact.md
# 职责：解压 zip 到临时目录 -> 以 MCP stdio 直连解压副本 PrReviewSubmit.exe
#       （initialize / tools/list / tools/call submit_pr_review）
#       -> GitHub 回读校验（内容一致 + user.type == Bot）
#       -> 写审计 notes/reviews/<version>-smoke.md
#
# 退出码：0 = 成功；1 = 前置校验/执行失败；2 = 参数或环境变量缺失/非法
# -DryRun：只打印目标与载荷预览，不调用 GitHub、不写审计、不创建临时目录。

[CmdletBinding()]
param(
    [string]$Version,
    [string]$ZipPath,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)

$script:RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$script:McpTimeoutSeconds = 60
$script:HttpTimeoutSeconds = 60
$script:stderrDrainTask = $null

function Write-Err {
    param([string]$Message)
    [Console]::Error.WriteLine($Message)
}

# ---- 1. 参数校验（缺失/非法 SemVer -> 退出 2）----
if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Err "错误: 缺少必填参数 -Version（语义化版本号，如 1.0.0）"
    exit 2
}
$Version = $Version.Trim()
# SemVer 2.0.0（semver.org 规范正则，不带 v 前缀；与 scripts/publish.ps1 完全一致）
$SemVerPattern = '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$'
if ($Version -notmatch $SemVerPattern) {
    Write-Err "错误: -Version 不是有效 SemVer 2.0.0：$Version（允许形如 1.0.0 / 1.0.0-beta.1 / 1.0.0+build.5，不允许 v 前缀）"
    exit 2
}

# ---- 2. 必填环境变量校验（缺失/非法 -> 退出 2 并列出）----
$requiredEnvNames = @(
    "GITHUB_APP_ID",
    "GITHUB_APP_INSTALLATION_ID",
    "GITHUB_PRIVATE_KEY_PATH",
    "GITHUB_SMOKE_OWNER",
    "GITHUB_SMOKE_REPO",
    "GITHUB_SMOKE_PR_NUMBER",
    "GITHUB_SMOKE_PATH",
    "GITHUB_SMOKE_LINE",
    "GITHUB_SMOKE_SIDE"
)

$missing = @($requiredEnvNames | Where-Object {
    [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_))
})
if ($missing.Count -gt 0) {
    Write-Err "错误: 缺少必填环境变量：$($missing -join ', ')"
    exit 2
}

$envErrors = @()
$appId = 0L
$installationId = 0L
$pullNumber = 0
$commentLine = 0

if (-not [long]::TryParse($env:GITHUB_APP_ID, [ref]$appId) -or $appId -le 0) {
    $envErrors += "GITHUB_APP_ID 必须是正整数（当前值：$($env:GITHUB_APP_ID)）"
}
if (-not [long]::TryParse($env:GITHUB_APP_INSTALLATION_ID, [ref]$installationId) -or $installationId -le 0) {
    $envErrors += "GITHUB_APP_INSTALLATION_ID 必须是正整数（当前值：$($env:GITHUB_APP_INSTALLATION_ID)）"
}
if (-not [int]::TryParse($env:GITHUB_SMOKE_PR_NUMBER, [ref]$pullNumber) -or $pullNumber -le 0) {
    $envErrors += "GITHUB_SMOKE_PR_NUMBER 必须是正整数（当前值：$($env:GITHUB_SMOKE_PR_NUMBER)）"
}
if (-not [int]::TryParse($env:GITHUB_SMOKE_LINE, [ref]$commentLine) -or $commentLine -le 0) {
    $envErrors += "GITHUB_SMOKE_LINE 必须是正整数（当前值：$($env:GITHUB_SMOKE_LINE)）"
}
if ($env:GITHUB_SMOKE_SIDE -cne "LEFT" -and $env:GITHUB_SMOKE_SIDE -cne "RIGHT") {
    $envErrors += "GITHUB_SMOKE_SIDE 必须是 RIGHT 或 LEFT（当前值：$($env:GITHUB_SMOKE_SIDE)）"
}

$privateKeyPath = $env:GITHUB_PRIVATE_KEY_PATH
if (-not [System.IO.Path]::IsPathRooted($privateKeyPath)) {
    $privateKeyPath = [System.IO.Path]::GetFullPath((Join-Path $script:RepoRoot $privateKeyPath))
}
if (-not (Test-Path -LiteralPath $privateKeyPath -PathType Leaf)) {
    $envErrors += "GITHUB_PRIVATE_KEY_PATH 文件不存在：$privateKeyPath"
}
else {
    try {
        $rsaProbe = [System.Security.Cryptography.RSA]::Create()
        try {
            $rsaProbe.ImportFromPem((Get-Content -LiteralPath $privateKeyPath -Raw))
        }
        finally {
            $rsaProbe.Dispose()
        }
    }
    catch {
        $envErrors += "GITHUB_PRIVATE_KEY_PATH 无法解析为 RSA PEM：$privateKeyPath（$($_.Exception.Message)）"
    }
}

if ($envErrors.Count -gt 0) {
    Write-Err "错误: 环境变量值非法："
    $envErrors | ForEach-Object { Write-Err "  $_" }
    exit 2
}

# ---- 3. zip 目标解析（不存在 -> 前置校验失败，退出 1）----
$zipFullPath = if ([string]::IsNullOrWhiteSpace($ZipPath)) {
    Join-Path $script:RepoRoot "dist\PrReviewSubmit-$Version-win-x64.zip"
} else {
    [System.IO.Path]::GetFullPath($ZipPath)
}
if (-not (Test-Path -LiteralPath $zipFullPath -PathType Leaf)) {
    Write-Err "错误: zip 文件不存在：$zipFullPath"
    exit 1
}

# ---- 4. -DryRun：只打印目标与载荷预览，不产生任何外部变更 ----
$marker = "smoke-" + [guid]::NewGuid().ToString("N")
$owner = $env:GITHUB_SMOKE_OWNER
$repo = $env:GITHUB_SMOKE_REPO
$commentPath = $env:GITHUB_SMOKE_PATH
$commentSide = $env:GITHUB_SMOKE_SIDE
$body = "发布产物冒烟验收 $marker"
$commentBody = "发布产物冒烟逐行评论 $marker"
$prUrl = "https://github.com/$owner/$repo/pull/$pullNumber"
$auditPath = Join-Path $script:RepoRoot "notes\reviews\$Version-smoke.md"

if ($DryRun) {
    Write-Output "[DryRun] 目标 zip：$zipFullPath"
    Write-Output "[DryRun] 解压目录：$([System.IO.Path]::GetTempPath())pr-review-submit-smoke-<guid>（本模式不创建）"
    Write-Output "[DryRun] 执行对象：<解压目录>\PrReviewSubmit.exe（MCP stdio 直连）"
    Write-Output "[DryRun] MCP 流程：initialize -> tools/list（断言仅返回 submit_pr_review，SC-007）-> tools/call submit_pr_review"
    Write-Output "[DryRun] 载荷预览："
    Write-Output "[DryRun]   owner=$owner"
    Write-Output "[DryRun]   repo=$repo"
    Write-Output "[DryRun]   pullNumber=$pullNumber"
    Write-Output "[DryRun]   body=$body"
    Write-Output "[DryRun]   comments[0]={ path=$commentPath; line=$commentLine; side=$commentSide; body=$commentBody }"
    Write-Output "[DryRun] 回读校验：GET repos/$owner/$repo/pulls/$pullNumber/reviews/<reviewId>（断言 body 一致且 user.type == Bot）"
    Write-Output "[DryRun] 审计文件：$auditPath"
    Write-Output "[DryRun] 预期输出：status=success reviewId=<id> bot=true prUrl=$prUrl 退出码 0"
    Write-Output "[DryRun] 完成：未调用 GitHub、未写审计、未创建临时目录（不产生外部变更）"
    exit 0
}

# ---- 5. 辅助函数：GitHub App JWT + 安装令牌交换、GitHub API、MCP stdio ----
function ConvertTo-Base64Url {
    param([byte[]]$Bytes)
    return [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Invoke-GitHubApi {
    param(
        [string]$Uri,
        [string]$Method,
        [string]$Token,
        [string]$Body,
        [int]$TimeoutSeconds,
        [string]$Phase
    )
    $headers = @{
        Authorization = "Bearer $Token"
        Accept = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
        "User-Agent" = "PR-AI-Reviewer-Smoke/1.0"
    }
    $params = @{
        Uri = $Uri
        Method = $Method
        Headers = $headers
        TimeoutSec = $TimeoutSeconds
        SkipHttpErrorCheck = $true
    }
    if (-not [string]::IsNullOrWhiteSpace($Body)) {
        $params.ContentType = "application/json"
        $params.Body = $Body
    }
    $response = $null
    try {
        $response = Invoke-WebRequest @params
    }
    catch {
        throw "GitHub $Phase 请求失败：$($_.Exception.Message)"
    }
    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
        $detail = [string]$response.Content
        if ($detail.Length -gt 800) {
            $detail = $detail.Substring(0, 800)
        }
        throw "GitHub $Phase 请求失败（HTTP $($response.StatusCode)）：$detail"
    }
    if ([string]::IsNullOrWhiteSpace([string]$response.Content)) {
        throw "GitHub $Phase 响应为空"
    }
    try {
        return ([string]$response.Content | ConvertFrom-Json)
    }
    catch {
        throw "GitHub $Phase 响应不是有效 JSON"
    }
}

function Get-GitHubInstallationToken {
    param(
        [long]$AppId,
        [long]$InstallationId,
        [string]$PrivateKeyPath,
        [string]$Repo,
        [int]$TimeoutSeconds
    )
    # JWT：iss=App ID、iat=now-60s、exp<=now+10min（对齐 src/PrReviewSubmit/GitHub/GitHubAppAuthClient.cs）
    $pem = $null
    try {
        $pem = Get-Content -LiteralPath $PrivateKeyPath -Raw
    }
    catch {
        throw "读取私钥失败：$($_.Exception.Message)"
    }
    $rsa = [System.Security.Cryptography.RSA]::Create()
    try {
        try {
            $rsa.ImportFromPem($pem)
        }
        catch {
            throw "私钥无法解析为 RSA PEM：$($_.Exception.Message)"
        }
        $now = [DateTimeOffset]::UtcNow
        $headerJson = @{ alg = "RS256"; typ = "JWT" } | ConvertTo-Json -Compress
        $payloadJson = @{
            iat = $now.AddSeconds(-60).ToUnixTimeSeconds()
            exp = $now.AddMinutes(10).ToUnixTimeSeconds()
            iss = "$AppId"
        } | ConvertTo-Json -Compress
        $headerB64 = ConvertTo-Base64Url ([System.Text.Encoding]::UTF8.GetBytes($headerJson))
        $payloadB64 = ConvertTo-Base64Url ([System.Text.Encoding]::UTF8.GetBytes($payloadJson))
        $signingInput = "$headerB64.$payloadB64"
        $signature = $rsa.SignData(
            [System.Text.Encoding]::UTF8.GetBytes($signingInput),
            [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $jwt = "$signingInput.$(ConvertTo-Base64Url $signature)"
        $tokenBody = @{ repositories = @($Repo) } | ConvertTo-Json -Compress
        $tokenResponse = Invoke-GitHubApi `
            -Uri "https://api.github.com/app/installations/$InstallationId/access_tokens" `
            -Method "Post" `
            -Token $jwt `
            -Body $tokenBody `
            -TimeoutSeconds $TimeoutSeconds `
            -Phase "安装令牌交换"
        if ($null -eq $tokenResponse.token -or [string]::IsNullOrWhiteSpace([string]$tokenResponse.token)) {
            throw "GitHub 安装令牌交换响应缺少 token"
        }
        return [string]$tokenResponse.token
    }
    finally {
        $rsa.Dispose()
    }
}

function Send-McpLine {
    param(
        [System.Diagnostics.Process]$Process,
        [string]$Line,
        [string]$Phase
    )
    try {
        $Process.StandardInput.WriteLine($Line)
        $Process.StandardInput.Flush()
    }
    catch {
        throw "MCP $Phase 写入 stdin 失败：$($_.Exception.Message)"
    }
}

function Get-DrainedStderr {
    # 仅诊断：后台排空任务的结果，最多等 2 秒，避免阻塞主流程
    if ($null -eq $script:stderrDrainTask) {
        return ""
    }
    try {
        if ($script:stderrDrainTask.Wait(2000)) {
            return [string]$script:stderrDrainTask.Result
        }
    }
    catch {
    }
    return ""
}

function Invoke-McpRequest {
    param(
        [System.Diagnostics.Process]$Process,
        [long]$RequestId,
        [string]$Method,
        $Params,
        [string]$Phase,
        [int]$TimeoutSeconds
    )
    $request = @{ jsonrpc = "2.0"; id = $RequestId; method = $Method }
    if ($null -ne $Params) {
        $request.params = $Params
    }
    $json = $request | ConvertTo-Json -Compress -Depth 20
    Send-McpLine -Process $Process -Line $json -Phase $Phase

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ($true) {
        $remainingMs = [int](($deadline - [DateTime]::UtcNow).TotalMilliseconds)
        if ($remainingMs -le 0) {
            throw "MCP $Phase 响应超时（超过 $TimeoutSeconds 秒）"
        }
        $readTask = $null
        try {
            $readTask = $Process.StandardOutput.ReadLineAsync()
        }
        catch {
            throw "MCP $Phase 读取 stdout 失败：$($_.Exception.Message)"
        }
        $completed = $false
        try {
            $completed = $readTask.Wait($remainingMs)
        }
        catch {
            throw "MCP $Phase 读取 stdout 失败：$($_.Exception.Message)"
        }
        if (-not $completed) {
            throw "MCP $Phase 响应超时（超过 $TimeoutSeconds 秒）"
        }
        $line = $null
        try {
            $line = $readTask.Result
        }
        catch {
            throw "MCP $Phase 读取 stdout 失败：$($_.Exception.Message)"
        }
        if ($null -eq $line) {
            $stderrTail = Get-DrainedStderr
            throw "MCP $Phase 进程提前退出（stdout 已关闭）：$stderrTail"
        }
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        $msg = $null
        try {
            $msg = $line | ConvertFrom-Json
        }
        catch {
            throw "MCP $Phase 收到非 JSON 响应行：$line"
        }
        if ($null -ne $msg.id -and ([string]$msg.id -eq [string]$RequestId)) {
            if ($null -ne $msg.error) {
                $errorText = $msg.error | ConvertTo-Json -Compress -Depth 8
                throw "MCP $Phase 返回 JSON-RPC 错误：$errorText"
            }
            return $msg.result
        }
        # 其它消息（如服务器通知）按协议忽略，继续等待目标响应
    }
}

# ---- 6. 真实冒烟 ----
$tempDir = $null
$process = $null
$exitCode = 0
try {
    # 校验和验证（release-artifact.md 验收项 5）：.sha256 缺失或哈希不一致均视为产物不完整
    $shaPath = "$zipFullPath.sha256"
    if (-not (Test-Path -LiteralPath $shaPath -PathType Leaf)) {
        throw "产物不完整：缺少校验和文件 $shaPath（.sha256 必须与 zip 同名同目录）"
    }
    $expectedHash = ""
    foreach ($shaLine in @(Get-Content -LiteralPath $shaPath)) {
        if ($shaLine -match '^\s*([0-9a-fA-F]{64})\s+') {
            $expectedHash = $matches[1].ToLowerInvariant()
            break
        }
    }
    if ([string]::IsNullOrWhiteSpace($expectedHash)) {
        throw "校验和文件格式非法：$shaPath（应为 <64位小写十六进制>  <文件名>）"
    }
    $actualHash = (Get-FileHash -LiteralPath $zipFullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "SHA-256 校验失败：期望 $expectedHash，实际 $actualHash（zip: $zipFullPath）"
    }

    $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    $tempDir = Join-Path $tempRoot ("pr-review-submit-smoke-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $tempDir | Out-Null
    try {
        Expand-Archive -LiteralPath $zipFullPath -DestinationPath $tempDir -Force
    }
    catch {
        throw "解压 zip 失败：$($_.Exception.Message)"
    }

    # 解压副本完整性：单一可执行入口 + VERSION 交叉校验（release-artifact.md 验收项 4）
    $exePath = Join-Path $tempDir "PrReviewSubmit.exe"
    if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
        throw "解压副本缺少可执行入口：$exePath"
    }
    $versionFilePath = Join-Path $tempDir "VERSION"
    if (-not (Test-Path -LiteralPath $versionFilePath -PathType Leaf)) {
        throw "解压副本缺少 VERSION 文件：$versionFilePath"
    }
    $versionContent = (Get-Content -LiteralPath $versionFilePath -Raw).Trim()
    if ($versionContent -ne $Version) {
        throw "VERSION 内容与 -Version 不一致：zip 内为 '$versionContent'，请求为 '$Version'"
    }

    # 以 stdio 启动解压副本（相对私钥路径按仓库根解析后传给子进程）
    $env:GITHUB_PRIVATE_KEY_PATH = $privateKeyPath
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $exePath
    $psi.WorkingDirectory = $tempDir
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.StandardInputEncoding = [System.Text.UTF8Encoding]::new($false)
    $psi.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
    $psi.StandardErrorEncoding = [System.Text.UTF8Encoding]::new($false)
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $psi
    try {
        [void]$process.Start()
    }
    catch {
        throw "启动解压副本失败：$($_.Exception.Message)"
    }
    # 后台排空 stderr（仅诊断），避免子进程大量写 stderr 时与 MCP stdout 读取互相阻塞
    $script:stderrDrainTask = $process.StandardError.ReadToEndAsync()

    # MCP：initialize
    $initResult = Invoke-McpRequest -Process $process -RequestId 1 -Method "initialize" -Phase "initialize" -TimeoutSeconds $script:McpTimeoutSeconds -Params @{
        protocolVersion = "2025-06-18"
        capabilities = @{}
        clientInfo = @{ name = "smoke-published"; version = $Version }
    }
    if ($null -eq $initResult -or [string]::IsNullOrWhiteSpace([string]$initResult.protocolVersion)) {
        throw "MCP initialize 响应缺少 protocolVersion"
    }
    Send-McpLine -Process $process -Line (@{ jsonrpc = "2.0"; method = "notifications/initialized" } | ConvertTo-Json -Compress) -Phase "initialized"

    # MCP：tools/list（SC-007：发布产物仅暴露 submit_pr_review）
    $listResult = Invoke-McpRequest -Process $process -RequestId 2 -Method "tools/list" -Params $null -Phase "tools/list" -TimeoutSeconds $script:McpTimeoutSeconds
    $toolNames = @()
    if ($null -ne $listResult -and $null -ne $listResult.tools) {
        $toolNames = @($listResult.tools | ForEach-Object { $_.name })
    }
    if ($toolNames.Count -ne 1 -or $toolNames[0] -cne "submit_pr_review") {
        throw "SC-007 职责范围校验失败：tools/list 返回 [$($toolNames -join ', ')]，必须仅返回 submit_pr_review"
    }

    # MCP：tools/call submit_pr_review（真实上传；body 与 comment 均含唯一 marker）
    $arguments = @{
        owner = $owner
        repo = $repo
        pullNumber = $pullNumber
        body = $body
        comments = @(
            @{
                path = $commentPath
                line = $commentLine
                side = $commentSide
                body = $commentBody
            }
        )
    }
    $callResult = Invoke-McpRequest -Process $process -RequestId 3 -Method "tools/call" -Phase "tools/call submit_pr_review" -TimeoutSeconds $script:McpTimeoutSeconds -Params @{
        name = "submit_pr_review"
        arguments = $arguments
    }
    $textContents = @($callResult.content | Where-Object { $_.type -eq "text" })
    if ($textContents.Count -eq 0 -or [string]::IsNullOrWhiteSpace([string]$textContents[0].text)) {
        throw "MCP tools/call 响应缺少 text 内容"
    }
    $toolResult = $null
    try {
        $toolResult = [string]$textContents[0].text | ConvertFrom-Json
    }
    catch {
        throw "MCP tools/call 结果不是有效 JSON：$($textContents[0].text)"
    }
    if ($toolResult.status -ne "success") {
        throw "submit_pr_review 返回错误：status=$($toolResult.status) code=$($toolResult.code) message=$($toolResult.message)"
    }
    if ($null -eq $toolResult.reviewId -or [long]$toolResult.reviewId -le 0) {
        throw "submit_pr_review 成功响应缺少有效 reviewId"
    }
    $reviewId = [long]$toolResult.reviewId

    # 回读校验：GitHub App 安装令牌读取已创建 review，断言内容一致且 user.type == Bot
    $installToken = Get-GitHubInstallationToken -AppId $appId -InstallationId $installationId -PrivateKeyPath $privateKeyPath -Repo $repo -TimeoutSeconds $script:HttpTimeoutSeconds
    $reviewUri = "https://api.github.com/repos/$owner/$repo/pulls/$pullNumber/reviews/$reviewId"
    $review = Invoke-GitHubApi -Uri $reviewUri -Method "Get" -Token $installToken -TimeoutSeconds $script:HttpTimeoutSeconds -Phase "review 回读"
    if ($null -eq $review.body -or [string]$review.body -ne $body) {
        throw "回读校验失败：review body 与输入不一致（reviewId=$reviewId）"
    }
    if ($null -eq $review.user -or $review.user.type -ne "Bot") {
        throw "回读校验失败：user.type 不是 Bot（reviewId=$reviewId，实际=$($review.user.type)）"
    }

    # 写审计文件 notes/reviews/<version>-smoke.md
    $timestamp = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
    $auditDir = Split-Path -Parent $auditPath
    New-Item -ItemType Directory -Force -Path $auditDir | Out-Null
    $auditLines = @(
        "# 发布冒烟审计：PrReviewSubmit $Version",
        "",
        "- 状态：success",
        "- 版本：$Version",
        "- reviewId：$reviewId",
        "- bot：true（user.type == Bot）",
        "- prUrl：$prUrl",
        "- 时间：$timestamp",
        "- marker：$marker",
        "- 审计文件：$auditPath"
    )
    Set-Content -LiteralPath $auditPath -Value $auditLines -Encoding utf8

    Write-Output "status=success"
    Write-Output "reviewId=$reviewId"
    Write-Output "bot=true"
    Write-Output "prUrl=$prUrl"
    Write-Output "审计文件=$auditPath"
}
catch {
    Write-Err "错误: $($_.Exception.Message)"
    $exitCode = 1
}
finally {
    if ($null -ne $process) {
        try {
            if (-not $process.HasExited) {
                $process.Kill($true)
            }
            $process.WaitForExit(5000) | Out-Null
        }
        catch {
        }
        $process.Dispose()
    }
    if (-not [string]::IsNullOrWhiteSpace($tempDir) -and (Test-Path -LiteralPath $tempDir)) {
        try {
            $resolvedTemp = [System.IO.Path]::GetFullPath($tempDir)
            $tempRootCheck = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
            $leaf = Split-Path -Leaf $resolvedTemp
            if ($resolvedTemp.StartsWith($tempRootCheck, [System.StringComparison]::OrdinalIgnoreCase) -and $leaf -like "pr-review-submit-smoke-*") {
                Remove-Item -LiteralPath $resolvedTemp -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        catch {
        }
    }
}
exit $exitCode

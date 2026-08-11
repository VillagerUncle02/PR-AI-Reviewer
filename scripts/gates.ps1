# gates.ps1 —— PR-AI-Reviewer 项目门禁（.NET 10 / C#）
#
# 用途：spec-kit implement-loop 本地门禁；最终门禁为 GitHub Actions CI。
# 约定：
#   - 不传 -Quick：全量门禁（构建 + 测试 + 私钥排除检查 + 格式检查 + 脚本语法检查）
#   - -Quick：仅构建 + 测试 + 脚本语法检查（审查修复后的快速验证）
#   - 任一步失败立即以非 0 退出

[CmdletBinding()]
param([switch]$Quick)

$ErrorActionPreference = "Stop"

# 沙箱/首次运行兼容：将 dotnet 首启 sentinel 与缓存重定向到临时目录
$env:DOTNET_CLI_HOME = Join-Path ([System.IO.Path]::GetTempPath()) "dotnet-cli-home"
New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME | Out-Null

# ---- 1. 构建 ----
Write-Host "== gates: dotnet build =="
dotnet build PrReviewSubmit.sln
if ($LASTEXITCODE -ne 0) { throw "构建失败" }

# ---- 2. 测试 ----
Write-Host "== gates: dotnet test =="
dotnet test PrReviewSubmit.sln --no-build
if ($LASTEXITCODE -ne 0) { throw "测试失败" }

# ---- 3. 私钥排除检查（CHK156：任何情况下密钥不得进入版本库） ----
Write-Host "== gates: private-key 排除检查 =="
$leaks = git ls-files | Select-String -Pattern "private-key/|\.pem$|\.key$"
if ($leaks) {
    Write-Host "发现密钥文件进入版本库索引："
    $leaks | ForEach-Object { Write-Host "  $_" }
    throw "私钥排除检查失败"
}

# ---- 4. 格式检查（仅全量模式） ----
if (-not $Quick) {
    Write-Host "== gates: dotnet format --verify-no-changes =="
    dotnet format PrReviewSubmit.sln --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) { throw "格式检查失败" }
}

# ---- 5. PowerShell 语法检查（scripts/*.ps1，D15：发布脚本语法错误在门禁阶段提前暴露） ----
Write-Host "== gates: scripts PowerShell AST 语法检查 =="
$psSyntaxErrors = @()
Get-ChildItem -Path (Join-Path $PSScriptRoot "*.ps1") -File | ForEach-Object {
    $tokens = $null
    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$tokens, [ref]$parseErrors) | Out-Null
    if ($parseErrors.Count -gt 0) {
        $parseErrors | ForEach-Object {
            $psSyntaxErrors += "  $($_.Extent.File): 第 $($_.Extent.StartLineNumber) 行: $($_.Message)"
        }
    }
}
if ($psSyntaxErrors.Count -gt 0) {
    Write-Host "发现 PowerShell 语法错误："
    $psSyntaxErrors | ForEach-Object { Write-Host $_ }
    throw "PowerShell 语法检查失败"
}

Write-Host "== 门禁通过 =="

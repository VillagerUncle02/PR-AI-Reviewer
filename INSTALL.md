# PR-AI-Reviewer 安装指南

本指南说明如何将 PR-AI-Reviewer（`submit_pr_review` MCP 工具）安装到 Codex，使 Agent 能以 GitHub App Bot 身份向指定 PR 提交 AI 代码审查。

**当前版本**：v1.0.0（Windows x64 框架依赖，需 .NET 10 运行时，无需 SDK）

## 前置要求

- Windows x64 机器，已安装 **.NET 10 运行时**（[下载](https://dotnet.microsoft.com/download/dotnet/10.0)）
- Codex CLI（`codex --version` 可运行）
- GitHub App 三项配置（见下文"环境变量"）；私钥文件仅存于本机 `private-key/`，**绝不进入版本库或发布包**

## 一、获取发布包

### 方式 A：从 GitHub Releases 下载（推荐）

1. 打开 [GitHub Release v1.0.0](https://github.com/VillagerUncle02/PR-AI-Reviewer/releases/tag/v1.0.0)；
2. 下载 `PrReviewSubmit-1.0.0-win-x64.zip` 与 `PrReviewSubmit-1.0.0-win-x64.zip.sha256`；
3. 校验完整性：

```powershell
Get-FileHash .\PrReviewSubmit-1.0.0-win-x64.zip -Algorithm SHA256
# 输出的小写哈希应与 .sha256 文件内容一致
```

4. 解压 zip 到目标目录，例如 `D:\tools\pr-ai-reviewer\1.0.0\`。

### 方式 B：本地构建

在仓库根（main 分支、工作区干净）执行：

```powershell
pwsh scripts\publish.ps1 -Version 1.0.0
```

产物在 `dist\1.0.0\`（含 `PrReviewSubmit.exe`、`VERSION`、`BUILD_INFO`）。

## 二、注册到 Codex

### 全局注册（Codex CLI）

```powershell
codex mcp add pr-ai-reviewer `
  --env GITHUB_APP_ID=<APP_ID> `
  --env GITHUB_APP_INSTALLATION_ID=<INSTALLATION_ID> `
  --env GITHUB_PRIVATE_KEY_PATH=<私钥文件绝对路径> `
  -- <发布包解压目录>\PrReviewSubmit.exe
```

示例（占位符替换为真实值）：

```powershell
codex mcp add pr-ai-reviewer `
  --env GITHUB_APP_ID=12345678 `
  --env GITHUB_APP_INSTALLATION_ID=12345678 `
  --env GITHUB_PRIVATE_KEY_PATH=D:\tools\pr-ai-reviewer\private-key\app.private-key.pem `
  -- D:\tools\pr-ai-reviewer\1.0.0\PrReviewSubmit.exe
```

### 项目级注册（.codex/config.toml）

> 项目级配置含本机路径，**不要提交到版本库**（FR-014）。以下仅为占位符示例。

```toml
[mcp_servers.pr-ai-reviewer]
command = "D:\\tools\\pr-ai-reviewer\\1.0.0\\PrReviewSubmit.exe"
env = {
  GITHUB_APP_ID = "<APP_ID>",
  GITHUB_APP_INSTALLATION_ID = "<INSTALLATION_ID>",
  GITHUB_PRIVATE_KEY_PATH = "D:\\tools\\pr-ai-reviewer\\private-key\\app.private-key.pem"
}
```

## 三、环境变量

| 变量 | 必填 | 说明 |
|------|------|------|
| `GITHUB_APP_ID` | 是 | GitHub App ID（正整数） |
| `GITHUB_APP_INSTALLATION_ID` | 是 | GitHub App 安装 ID（正整数，目标仓库须在该安装授权范围内） |
| `GITHUB_PRIVATE_KEY_PATH` | 是（发布形态） | 私钥 PEM 文件的本地绝对路径；文件存在、可读且可解析为 RSA 密钥 |

配置缺失或非法时，工具启动即失败（非零退出码 + stderr 明确错误），不会带病运行。

## 四、验证安装

1. 确认服务已注册：

```powershell
codex mcp list
```

应看到 `pr-ai-reviewer`（enabled）。

2. **重启 Codex 并打开新会话**，指示 Agent：

> 用 `submit_pr_review` 给 <owner/repo> 的 PR #<编号> 提交 review，整体结论为"<结论>"，并在 <path>:<line>（<LEFT/RIGHT>）加一条评论"<评论内容>"。

3. 打开目标 PR 页面，确认出现一条 **Bot 标识** 的已提交 review，内容与输入一致。

## 五、升级 / 回退 / 卸载

- **升级**：下载新版本 zip 解压后，重跑 `codex mcp add pr-ai-reviewer ... -- <新版本 exe 路径>`（覆盖同名注册；若 CLI 拒绝同名覆盖，先 `codex mcp remove pr-ai-reviewer` 再 add）；旧版本目录可保留或删除。
- **回退**：重新注册指向旧版本 exe 路径即可；历史 Release 与产物保留，不做版本回滚操作。
- **卸载**：全局注册执行 `codex mcp remove pr-ai-reviewer`；项目级注册删除 `config.toml` 中 `[mcp_servers.pr-ai-reviewer]` 段。
- 升级/卸载不影响本地 `private-key/` 与已发布的历史 Release。

## 六、排错清单

| 现象 | 可能原因 | 处置 |
|------|----------|------|
| `codex mcp list` 看不到服务 | 注册未成功或会话未重启 | 重跑注册命令；重启 Codex 开新会话 |
| 启动报配置错误 | App ID / 安装 ID 缺失或非正整数、私钥路径无效 | 核对三项环境变量；私钥文件存在、可读、可解析为 RSA |
| 调用 `submit_pr_review` 失败或 PR 未出现 review | 载荷/目标条件不正确，或 GitHub 限流、网络异常 | 查看返回的 `details.retryable`：仅 `RATE_LIMITED` / `NETWORK_ERROR` 可重试，其余先修正载荷与条件（见 README"重试与去重语义"） |
| 启动后立即退出 | 目标机器未安装 .NET 10 运行时 | 安装 .NET 10 运行时（无需 SDK） |
| `gh` 相关命令报凭据失效 | keyring 默认 token 失效 | 设置有效的 `GH_TOKEN` 或 `gh auth login` |

## 相关文档

- [README.md](README.md)（使用说明、重试与去重语义）
- [specs/002-package-release/quickstart.md](specs/002-package-release/quickstart.md)（发布与验证场景）
- [specs/002-package-release/contracts/codex-install.md](specs/002-package-release/contracts/codex-install.md)（注册契约）

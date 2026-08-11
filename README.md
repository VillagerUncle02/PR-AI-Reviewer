# PR-AI-Reviewer

一个**特化的 MCP Tool**：AI Agent 完成代码审查后，调用本工具，以 GitHub App 的 Bot 身份，将审查结论通过 GitHub PR 的 file change 下的 **submit review** 上传到指定仓库的指定 PR，从而完成 AI PR review。

## 职责边界

本工具**只做一件事**：接收 Agent 传来的 PR review 内容，并利用 GitHub App 渠道上传到指定仓库的指定 PR。

- 不生成、不修改审查内容，不分析代码
- 不持久化任何业务数据
- 不负责 CI/CD、构建、部署、通知
- 不操作调用方未指定的仓库或 PR

## 使用说明

本工具作为 MCP stdio server 运行，由 MCP 客户端按需拉起，且只暴露 `submit_pr_review` 一个工具（FR-001）：接收调用方传来的整体审查结论与逐文件行内评论，以 GitHub App Bot 身份提交到指定仓库的指定 PR。本工具不生成审查内容，也不执行 CI/CD。

### 注册到 MCP 客户端

在 MCP 客户端（如 Claude Desktop / Codex）中注册为 stdio server，name 可使用 `pr-ai-reviewer`（客户端侧名称，可按需修改），command 使用 `dotnet run`：

```json
{
  "mcpServers": {
    "pr-ai-reviewer": {
      "command": "dotnet",
      "args": ["run", "--project", "src/PrReviewSubmit"]
    }
  }
}
```

已构建发布产物时，也可以将 `command` 改为构建产物的可执行文件路径。

### 环境变量

| 环境变量 | 必填 | 默认值 | 说明 |
|----------|------|--------|------|
| `GITHUB_APP_ID` | 是 | 无 | GitHub App 的 App ID，须为正整数 |
| `GITHUB_APP_INSTALLATION_ID` | 是 | 无 | GitHub App 的安装 ID（目标仓库所在账号），须为正整数 |
| `GITHUB_PRIVATE_KEY_PATH` | 否（源码运行） | `private-key/github-app.pem` | GitHub App 私钥 PEM 文件路径；发布形态注册时必填（见"发布与安装"章节） |

### 私钥约定

- 私钥放在仓库根目录下的 `private-key/` 目录中；该目录已被 `.gitignore` 排除，**任何情况下不得提交进版本库**。
- 启动时工具会校验私钥文件存在、可读且可解析为 RSA 密钥（001 FR-015，启动即失败语义）；缺失或非法时以非零退出码退出，并在 stderr 输出明确错误。

### 快速开始

端到端验证指南（前置条件、环境变量设置、本地运行、自动化验证与手工冒烟场景）见 [specs/001-pr-review-submit/quickstart.md](specs/001-pr-review-submit/quickstart.md)。

## 发布与安装

> 完整的分步安装指南（发布包获取、Codex 注册、验证、升级/回退/卸载、排错）见 [INSTALL.md](INSTALL.md)。

本工具以 **Windows x64 框架依赖** 形态发布：发布包内不含 .NET 运行时，目标机器需已安装 **.NET 10 运行时**（无需 SDK）。发布产物不含私钥或任何敏感配置，安装时通过三项环境变量指向本地的 GitHub App 配置。

> 发布/安装的完整契约与验证场景见 [specs/002-package-release/contracts/codex-install.md](specs/002-package-release/contracts/codex-install.md)、[specs/002-package-release/contracts/release-cli.md](specs/002-package-release/contracts/release-cli.md) 与 [specs/002-package-release/quickstart.md](specs/002-package-release/quickstart.md)。

### 生成发布产物

前置条件：PowerShell 7、.NET 10 SDK（构建机）、已跟踪工作区干净（脚本会校验）。

```powershell
pwsh scripts/publish.ps1 -Version 1.0.0
```

执行后你会得到：

- `dist/1.0.0/`：`PrReviewSubmit.exe` 及运行所需文件，内含 `VERSION`（如 `1.0.0`）与 `BUILD_INFO`（构建 commit）；
- `dist/PrReviewSubmit-1.0.0-win-x64.zip` 与同名 `.sha256` 校验和文件；
- 敏感扫描结果：产物目录中出现 `private-key/`、`*.pem`、`*.key`、`*.p12`、`*.pfx`、`.env*` 或私钥/令牌文本模式时，脚本失败（退出码 1），不会继续打包。

`dist/` 已被 `.gitignore` 排除，不会进入版本库；同一版本重复构建的产物语义一致、校验和可复算。想先预览而不产生任何构建产物，使用 `-DryRun`：

```powershell
pwsh scripts/publish.ps1 -Version 1.0.0 -DryRun
```

退出码约定：`0` 成功；`1` 前置校验或执行失败（stderr 含明确原因）；`2` 参数或环境变量缺失/非法。正式发布（git tag + GitHub Release）须先完成真实冒烟，再执行 `pwsh scripts/release.ps1 -Version 1.0.0`（脚本由 002 任务 T009 提供，见 [quickstart 场景 4](specs/002-package-release/quickstart.md)）。

### 注册到 Codex（全局）

使用发布产物的 exe 绝对路径注册为 stdio server（将 `<APP_ID>`、`<INSTALLATION_ID>`、`<私钥文件绝对路径>` 与 `<exe 绝对路径>` 替换为你的实际值）：

```powershell
codex mcp add pr-ai-reviewer `
  --env GITHUB_APP_ID=<APP_ID> `
  --env GITHUB_APP_INSTALLATION_ID=<INSTALLATION_ID> `
  --env GITHUB_PRIVATE_KEY_PATH=<私钥文件绝对路径> `
  -- <exe 绝对路径>
```

- 真实 App ID、安装 ID 与私钥路径**绝不写入任何会提交到版本库的文件**（FR-007/FR-014）。
- 私钥路径含空格或非 ASCII 字符时，务必保持正确的引用方式（FR-005）。
- 若已存在同名 `pr-ai-reviewer` 服务，先执行 `codex mcp remove pr-ai-reviewer` 再注册。

### 注册到 Codex（项目级，占位符示例）

项目级注册通过 `.codex/config.toml` 完成。**不要向版本库提交 `.codex/config.toml`，也不要提交真实 App ID、安装 ID 或私钥路径**（FR-014）。以下仅为占位符示例：

```toml
[mcp_servers.pr-ai-reviewer]
command = "D:\\...\\dist\\1.0.0\\PrReviewSubmit.exe"
env = {
  GITHUB_APP_ID = "<APP_ID>",
  GITHUB_APP_INSTALLATION_ID = "<INSTALLATION_ID>",
  GITHUB_PRIVATE_KEY_PATH = "D:\\...\\private-key\\<私钥文件名>.pem"
}
```

### 三项环境变量

| 环境变量 | 必填 | 语义 | 校验 |
|----------|------|------|------|
| `GITHUB_APP_ID` | 是 | GitHub App 的 App ID | 正整数 |
| `GITHUB_APP_INSTALLATION_ID` | 是 | App 的安装 ID（目标仓库所在账号的安装） | 正整数 |
| `GITHUB_PRIVATE_KEY_PATH` | 是（发布形态注册） | 私钥 PEM 文件的本地绝对路径 | 文件存在、可读且可解析为 RSA 私钥 |

注册配置中只包含 ID 与私钥**路径**，不含密钥内容；私钥文件仅存放在本地 `private-key/`（已被 `.gitignore` 排除）。任一配置缺失或非法时，工具启动即失败并给出明确错误，不会向 GitHub 发起任何请求。

### 验证安装

1. 执行 `codex mcp list`，确认可见 `pr-ai-reviewer`（FR-005）。
2. 开启**新** Codex 会话，调用 `submit_pr_review` 完成一次真实上传：目标 PR 应出现带 bot 标识（`user.type == Bot`）的 review，调用方收到明确成功结果（FR-006）。
3. 正式发布前，还应对 zip 解压副本执行真实冒烟：`pwsh scripts/smoke-published.ps1 -Version 1.0.0`（需要 `GITHUB_SMOKE_*` 环境变量，FR-011）。

端到端验证场景（含 SC-004/SC-006 口径）见 [specs/002-package-release/quickstart.md](specs/002-package-release/quickstart.md)。

### 升级、回退与卸载

- **升级**：生成新版本产物后，重新执行 `codex mcp add pr-ai-reviewer ... -- <新版本 exe 绝对路径>`（或更新项目级 `config.toml` 中的 `command` 路径）。`codex mcp add` 会覆盖同名注册；若 CLI 拒绝同名覆盖，先 `codex mcp remove pr-ai-reviewer` 再 add。旧版本产物目录可保留，也可手动删除。
- **回退**：重新注册指向旧版本 exe 绝对路径即可；历史 Release 与产物保留，不做版本回滚操作。
- **卸载**：全局注册执行 `codex mcp remove pr-ai-reviewer`；项目级注册删除 `config.toml` 中的 `[mcp_servers.pr-ai-reviewer]` 段。
- 升级/卸载不影响本地 `private-key/` 与已发布的历史 Release。

### 排错清单

| 现象 | 原因 | 处置 |
|------|------|------|
| 启动或调用时提示配置缺失/非法 | 三项 env 缺失或非法、ID 非正整数、私钥路径无效 | 检查注册命令与 `config.toml` 的三项配置；确认私钥文件存在、可读且为 RSA PEM；修复后重启 Codex 会话 |
| `codex mcp list` 看不到 `pr-ai-reviewer` | 注册未生效、同名服务冲突或会话未重启 | 重新执行注册（同名先 `codex mcp remove`）；重启 Codex 会话/客户端后复查 |
| 调用 `submit_pr_review` 失败或 PR 未出现 review | 载荷或目标 PR 条件不正确，或 GitHub 限流、网络异常 | 先修正载荷与条件再重发；仅当返回 `details.retryable=true`（`RATE_LIMITED`/`NETWORK_ERROR`）时才可重试，其余错误不要盲目重试；详见 [重试与去重语义](#重试与去重语义) |
| exe 无法启动，提示缺少 .NET 运行时 | 目标机未安装 .NET 10 运行时（框架依赖产物不含运行时） | 安装 .NET 10 运行时（无需 SDK），用 `dotnet --list-runtimes` 确认 |
| `release.ps1`（002 发布脚本）前置校验报 `gh auth status` 失败 | gh 凭据失效（keyring 凭据或 `GH_TOKEN` 无效） | `gh auth login` 重新认证或刷新 `GH_TOKEN`；工具运行本身使用 GitHub App 凭据，不依赖 gh |

## 重试与去重语义

本工具**永不自动重试**：任一步骤失败都会立即返回 `error` 终态，没有后台重试，也没有自动补偿。失败后只能由调用方修正载荷或条件后重新发起上传。

- **不保证幂等**：提交请求已发出但响应超时时，review 可能已经创建。调用方重试前，必须先核验目标 PR 是否已存在 review，避免重复提交。
- **可重试信号**：错误返回的 `details.retryable=true` 表示调用方可重试，只有 `RATE_LIMITED` 与 `NETWORK_ERROR`（含 GitHub 5xx）会携带该信号；其它错误码（`false` 或省略该字段）不应盲目重试，应先修正载荷或条件。
- **调用相互独立**：每次调用都会重新读取 GitHub App 私钥并生成新的安装令牌，不缓存令牌、不共享状态；每次调用之间互不影响。

## 技术栈

- C# / .NET LTS
- 官方 MCP C# SDK（mcpdotnet）
- Octokit / GitHub REST API
- GitHub 版本管理

## 凭据安全

GitHub App 私钥保存在本地 `private-key/` 目录，该目录已被 `.gitignore` 排除，**任何情况下不得提交进版本库**。

## 开发流程

项目遵循 spec-kit 规约驱动工作流（constitution → specify → plan → tasks → implement）。

- 当前功能规格：[specs/001-pr-review-submit/spec.md](specs/001-pr-review-submit/spec.md)
- 002 打包发布规格：[specs/002-package-release/spec.md](specs/002-package-release/spec.md)

## 项目状态

**已实现**：Phase 1-6（T001-T038）任务全部完成。实现完成，待人工审查与真实 GitHub App 凭据冒烟验证。

**002 打包发布**：发布脚本与安装文档已实现，文档一致性终审完成；v1.0.0 已正式发布（[GitHub Release](https://github.com/VillagerUncle02/PR-AI-Reviewer/releases/tag/v1.0.0)，含 zip + sha256 资产）。

- 任务清单：[specs/001-pr-review-submit/tasks.md](specs/001-pr-review-submit/tasks.md)
- 端到端验证指南：[specs/001-pr-review-submit/quickstart.md](specs/001-pr-review-submit/quickstart.md)
- 002 任务清单：[specs/002-package-release/tasks.md](specs/002-package-release/tasks.md)
- 002 验证指南：[specs/002-package-release/quickstart.md](specs/002-package-release/quickstart.md)

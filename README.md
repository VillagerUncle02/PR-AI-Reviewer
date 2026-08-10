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
| `GITHUB_PRIVATE_KEY_PATH` | 否 | `private-key/github-app.pem` | GitHub App 私钥 PEM 文件路径 |

### 私钥约定

- 私钥放在仓库根目录下的 `private-key/` 目录中；该目录已被 `.gitignore` 排除，**任何情况下不得提交进版本库**。
- 启动时工具会校验私钥文件存在、可读且可解析为 RSA 密钥（FR-015）；缺失或非法时以非零退出码退出，并在 stderr 输出明确错误。

### 快速开始

端到端验证指南（前置条件、环境变量设置、本地运行、自动化验证与手工冒烟场景）见 [specs/001-pr-review-submit/quickstart.md](specs/001-pr-review-submit/quickstart.md)。

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

## 项目状态

**已实现**：Phase 1-6（T001-T038）任务全部完成。实现完成，待人工审查与真实 GitHub App 凭据冒烟验证。

- 任务清单：[specs/001-pr-review-submit/tasks.md](specs/001-pr-review-submit/tasks.md)
- 端到端验证指南：[specs/001-pr-review-submit/quickstart.md](specs/001-pr-review-submit/quickstart.md)

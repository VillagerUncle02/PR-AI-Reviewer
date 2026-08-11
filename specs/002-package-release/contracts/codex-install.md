# Contract: Codex MCP 注册（安装契约）

## 服务标识

- server name：`pr-ai-reviewer`
- 命令：发布产物 `PrReviewSubmit.exe` 的绝对路径（stdio server）

## 环境变量（必填三项）

| 变量 | 语义 | 示例 |
|------|------|------|
| `GITHUB_APP_ID` | GitHub App ID（正整数） | `<APP_ID>` |
| `GITHUB_APP_INSTALLATION_ID` | App 安装 ID（正整数） | `<INSTALLATION_ID>` |
| `GITHUB_PRIVATE_KEY_PATH` | 私钥文件本地绝对路径 | `<绝对路径>\private-key\<key-file>.pem` |

- 私钥路径含空格/非 ASCII 时，注册命令须正确引用（FR-005）。
- 私钥文件只在本地，绝不进入版本库或发布产物（FR-007）。

## 注册方式

### 全局（Codex CLI）

```powershell
codex mcp add pr-ai-reviewer `
  --env GITHUB_APP_ID=<APP_ID> `
  --env GITHUB_APP_INSTALLATION_ID=<INSTALLATION_ID> `
  --env GITHUB_PRIVATE_KEY_PATH=<绝对路径> `
  -- <发布产物>\PrReviewSubmit.exe
```

### 项目级（.codex/config.toml）

> 仅以 README 占位符示例说明，不向版本库提交 `.codex/config.toml`（FR-014）；真实 ID 与私钥路径绝不入库。

```toml
[mcp_servers.pr-ai-reviewer]
command = "D:\\...\\PrReviewSubmit.exe"
env = {
  GITHUB_APP_ID = "<APP_ID>",
  GITHUB_APP_INSTALLATION_ID = "<INSTALLATION_ID>",
  GITHUB_PRIVATE_KEY_PATH = "D:\\...\\private-key\\xxx.pem"
}
```

## 验证

1. `codex mcp list` 显示 `pr-ai-reviewer`；
2. 新 Codex 会话中调用 `submit_pr_review` 成功，目标 PR 出现带 bot 标识的 review（FR-006）。

## 安全边界

- 注册配置中的 env 只含 ID 与密钥**路径**，不含密钥内容；
- 密钥文件仅存于 `private-key/`（.gitignore 排除）；
- 若已存在同名 server，先 `codex mcp remove pr-ai-reviewer` 再注册。

## 升级与卸载

- **升级**：新版本发布后，重新执行 `codex mcp add pr-ai-reviewer ... -- <新版本 exe 路径>`（覆盖注册）或更新项目级 config.toml 中的 command 路径；旧版本产物目录可保留或手动删除。
- **回退**：重新注册指向旧版本 exe 路径即可；历史 Release 与产物保留，不做版本回滚操作。
- **卸载**：`codex mcp remove pr-ai-reviewer`（全局）或删除项目级 config.toml 中的 `[mcp_servers.pr-ai-reviewer]` 段。
- 升级/卸载操作不影响本地 `private-key/` 与已发布的历史 Release。

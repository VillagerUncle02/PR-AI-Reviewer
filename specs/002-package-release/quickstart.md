# Quickstart: 打包发布与 Codex 安装验证

**日期**: 2026-08-11

## 前置条件

- PowerShell 7（本机 7.6.4）
- .NET 10 SDK（构建机）
- `gh` 已认证（`GH_TOKEN` 或有效 keyring 凭据）
- GitHub App 三项环境变量可用（`GITHUB_APP_ID` / `GITHUB_APP_INSTALLATION_ID` / `GITHUB_PRIVATE_KEY_PATH`）
- Codex CLI（安装验证场景）
- 冒烟目标：测试仓库 + open 状态测试 PR（含可评论的改动位置）

## 场景 1：生成发布产物（SC-001/SC-003）

```powershell
pwsh scripts/publish.ps1 -Version 1.0.0
```

预期结果：

- `dist/1.0.0/` 含 `PrReviewSubmit.exe` 与依赖文件、`VERSION`、`BUILD_INFO`（构建 commit）；
- 生成 `PrReviewSubmit-1.0.0-win-x64.zip` 与 `.sha256`；
- 敏感扫描通过，退出码 0；
- 重复执行同一版本得到一致的 VERSION/zip 语义与可复算校验和。

## 场景 2：发布产物真实冒烟（SC-002/FR-011）

```powershell
pwsh scripts/smoke-published.ps1 -Version 1.0.0
```

预期结果：

- MCP stdio 直连解压副本（脚本先解压 zip 到临时目录），`tools/list` 仅含 `submit_pr_review`；
- 测试 PR 上出现一条带 bot 标识（`user.type == Bot`）的 review；
- 回读内容与输入一致，脚本输出 `status=success`，退出码 0。
- 可重复执行：重试产生的新 review 不清理，以最近一次成功为准（FR-013）。

## 场景 3：Codex 安装与调用（SC-004/FR-005/FR-006）

```powershell
codex mcp add pr-ai-reviewer `
  --env GITHUB_APP_ID=<APP_ID> `
  --env GITHUB_APP_INSTALLATION_ID=<INSTALLATION_ID> `
  --env GITHUB_PRIVATE_KEY_PATH=<绝对路径>\private-key\<key-file>.pem `
  -- <绝对路径>\dist\1.0.0\PrReviewSubmit.exe
codex mcp list
```

预期结果：

- `codex mcp list` 可见 `pr-ai-reviewer`；
- 新 Codex 会话中调用 `submit_pr_review` 成功上传（FR-006）。
- 项目级注册仅以 README 占位符示例提供，不提交 `.codex/config.toml`（FR-014）。

## 场景 4：正式发布（SC-005/FR-004/FR-009）

```powershell
pwsh scripts/release.ps1 -Version 1.0.0 -DryRun
```

先以 `-DryRun` 预览校验结果与将执行的变更（不创建 tag/Release），校验通过后实际发布：

```powershell
pwsh scripts/release.ps1 -Version 1.0.0
```

预期结果：

- 创建唯一 git tag `v1.0.0` 并推送；
- GitHub Release 含发布说明、zip 与 sha256 资产；
- tag 与 Release 均已存在时重复执行明确失败、不覆盖；tag 已存在但 Release 缺失时自动补建（FR-015）。
- 发布成功后生成审计文件 `notes/reviews/1.0.0-release.md`。

发布脚本的参数、退出码与 `-DryRun` 预览约定见 `contracts/release-cli.md`。

## 验收对照

| 成功标准 | 验证场景 |
|----------|----------|
| SC-001 发布构建可重复 | 场景 1 |
| SC-002 产物真实冒烟 | 场景 2 |
| SC-003 0 敏感内容 | 场景 1 扫描 + gates 私钥检查 |
| SC-004 Codex 安装可用 | 场景 3 |
| SC-005 版本唯一可追溯 | 场景 4 |
| SC-006 30 分钟内完成安装验证 | 场景 3（含前置准备） |
| SC-007 职责范围不变 | 场景 2/3 产物仅暴露 `submit_pr_review` |

详细布局与接口约定见 `contracts/release-artifact.md`、`contracts/codex-install.md`、`contracts/release-process.md`、`contracts/release-cli.md`；实体与校验规则见 `data-model.md`。

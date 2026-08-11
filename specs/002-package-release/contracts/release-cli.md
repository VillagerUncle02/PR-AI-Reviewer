# Contract: 发布脚本 CLI（publish / smoke / release）

## 通用约定

- 退出码：`0` = 成功；`1` = 前置校验/执行失败（stderr 含明确原因）；`2` = 参数或环境变量缺失/非法。
- 输出：错误写 stderr；状态与结果写 stdout（便于审计与脚本化）。
- 编码：UTF-8。
- `-DryRun`：所有脚本支持预览模式，只打印将执行的操作与预期结果，**不产生任何外部变更**（不构建产物、不调用 GitHub、不创建 tag/Release、不写审计文件）。

## publish.ps1

| 参数 | 必填 | 默认 | 说明 |
|------|------|------|------|
| `-Version` | 否 | `1.0.0` | SemVer；非法值退出 2 |
| `-DryRun` | 否 | - | 校验参数与工作区并打印计划 |

行为：校验 SemVer 与已跟踪工作区干净 → 清理旧 `dist/<version>/` → `dotnet publish`（win-x64 框架依赖）→ 写 `VERSION` / `BUILD_INFO` → 敏感扫描 → 生成 zip 与 sha256（`dist/` 根）。

输出：产物目录、zip 路径、sha256 路径、扫描结论。

## smoke-published.ps1

| 参数 | 必填 | 默认 | 说明 |
|------|------|------|------|
| `-Version` | 是 | - | SemVer |
| `-ZipPath` | 否 | `dist/PrReviewSubmit-<v>-win-x64.zip` | 指定 zip |
| `-DryRun` | 否 | - | 打印目标与载荷预览，不调用 GitHub |

必填环境变量：`GITHUB_APP_ID`、`GITHUB_APP_INSTALLATION_ID`、`GITHUB_PRIVATE_KEY_PATH`、`GITHUB_SMOKE_OWNER`、`GITHUB_SMOKE_REPO`、`GITHUB_SMOKE_PR_NUMBER`、`GITHUB_SMOKE_PATH`、`GITHUB_SMOKE_LINE`、`GITHUB_SMOKE_SIDE`；缺失任一 → 退出 2 并列出缺失项。

行为：解压 zip 到临时目录 → MCP stdio 直连解压副本 → `initialize` / `tools/list` / `tools/call submit_pr_review` → 回读校验（内容一致 + bot 标识）→ 写审计 `notes/reviews/<version>-smoke.md`。

输出：`status`、`reviewId`、`bot`、`prUrl`、审计文件路径。

## release.ps1

| 参数 | 必填 | 默认 | 说明 |
|------|------|------|------|
| `-Version` | 是 | - | SemVer |
| `-NotesFile` | 否 | 自动生成 | 发布说明文件，可人工编辑 |
| `-DryRun` | 否 | - | 打印校验结果与将创建的 tag/Release/notes，不执行 |

行为：运行 `scripts/gates.ps1` → 前置校验（产物与 VERSION/BUILD_INFO 存在、**VERSION 内容 == 请求版本**、`BUILD_INFO.commit` == 远程 main HEAD、sha256 匹配、冒烟审计 success、`gh auth status` 通过、tag 不存在或存在但无 Release（补建路径））→ 生成发布说明 → 打 tag/push 或跳过 → `gh release create` → 写审计 `notes/reviews/<version>-release.md`。

输出：tag、Release URL、审计文件路径。

## 失败语义

- 任一校验失败：退出 1（参数/env 错误为 2），不产生外部变更；
- 禁止 force push、禁止删除或覆盖 tag/Release（FR-004/FR-015）。

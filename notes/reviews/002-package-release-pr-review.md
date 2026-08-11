# PR #54 AI 审查报告：002-package-release 打包发布与 Codex 安装

**审查时间**: 2026-08-11

**PR**: [#54](https://github.com/VillagerUncle02/PR-AI-Reviewer/pull/54) `002-package-release` → `main`

**Head**: `5680872`（审查时 `d2158b6`，后续提交为审查修复）

**改动规模**: 30 文件，+2789/−3（发布脚本 ×3、门禁 AST 检查、.gitignore、README、002 全套规约文档、8 轮审查审计 + 真实冒烟审计）

## 改动范围

1. 发布脚本：`publish.ps1`（产物构建 + 敏感扫描 + 可复现 zip + sha256）、`smoke-published.ps1`（zip 解压副本 MCP stdio 真实冒烟 + GitHub 回读校验）、`release.ps1`（gates + 前置校验 + notes + tag/Release + 审计 + 补建恢复 FR-015）。
2. 门禁：`gates.ps1` 增加 scripts/*.ps1 PowerShell AST 语法检查。
3. 仓库配置：`dist/` 与 `.codex/config.toml` 排除、feature.json 指向 002、REVIEWER/DEVOPS_OPINION 固化。
4. 文档：README"发布与安装"章节 + 002 全套规约产物（spec/plan/research/data-model/contracts×4/quickstart/tasks/checklists/agent-assignments）。

## 关键决策

- 发布形态：Windows x64 框架依赖（.NET 10 运行时，无需 SDK）。
- 产物契约：VERSION + BUILD_INFO(commit) + zip + `.sha256`；ZipArchive 固定条目时间戳实现可复现校验和（SC-001，T005 双构建同哈希证据）。
- 敏感扫描：文件名黑名单 + 内容模式（BEGIN PRIVATE KEY、ghp_/github_pat_/gho_/ghs_），命中退出 1。
- 冒烟：解压副本执行、tools/list 断言仅 submit_pr_review（SC-007）、真实上传回读 bot 标识、可重复执行。
- 发布：手动、DryRun 不调用 GitHub、补建路径校验既有 tag 指向 BUILD_INFO.commit（FR-015）。

## 门禁与 CI 证据

- CI run 31484454317（head d2158b6）success；后续审查修复提交 5680872 推送后 CI 重新排队。
- 本地全量门禁（T011）通过：构建 0 错误、82 测试、格式、私钥排除、AST 检查。
- T005 可复现构建：两次 sha256 一致（6f0a97a4…）。
- T006 真实冒烟：reviewId 4905118356、bot=true、回读一致（PR #54 上）。
- T008 安装验证：codex mcp list 可见，SC-006 计时满足。
- 敏感信息扫描：README/quickstart/contracts/scripts 中 4525509/152380612/私钥路径 0 残留；`git ls-files` 无 private-key/pem/key/dist/.codex/config.toml。

## 宪法合规

- 单一职责：未改 src/ 业务代码，产物仅暴露 submit_pr_review。✅
- 凭据安全：私钥仅本地、env 只传路径、双重扫描、.gitignore 纵深防御。✅
- 失败透明：三脚本统一退出码 0/1/2、stderr 明确、DryRun 零外部变更、禁止覆盖。✅
- Bot 合规：安装令牌 + 回读 user.type == Bot。✅
- 无 CI/CD 自动发布（FR-009）。✅

## 审查结论

**PASS**（无 🔴；5 个 🟡 不阻塞合并）

## Findings

| # | 级别 | 问题 | 状态 |
|---|------|------|------|
| 1 | 🟡 | PR body 任务清单过时（只列 T001-T005） | 已修复：更新为 T001-T009/T011-T013，T010 待合并后 |
| 2 | 🟡 | README 项目状态与 tasks.md 矛盾（T013 标待完成） | 已修复：改为仅 T010 待执行 |
| 3 | 🟡 | data-model.md SemVer 正则与脚本不一致（宽松版） | 已修复：对齐严格 SemVer 2.0.0 |
| 4 | 🟡 | publish.ps1 内容扫描缺 ghu_/ghr_ 前缀 | 遗留 TODO：随 T010 处理 |
| 5 | 🟡 | release.ps1 main HEAD 校验基于本地 ref 未先 fetch | 遗留 TODO：随 T010 处理（非 DryRun 时先 git fetch origin main） |

💭 记录：README env 表作用域说明、FR-015 编号消歧、smoke 审计绝对路径、r8 记录中的 ID 脱敏、release notes breaking 标记——均不阻塞，随后续小版本处理。

## 遗留 TODO

- **T010（issue #50）**：PR 合并到 main 后，在 main 上重新 `publish.ps1 -Version 1.0.0`（BUILD_INFO.commit == 合并后 main HEAD）→ 真实冒烟 → `release.ps1 -DryRun` → 正式发布 v1.0.0（GitHub Release + zip/sha256）。
- 🟡 4/5 随 T010 处理；其余 💭 记录在案。

# Implementation Plan: 打包发布（发布产物与 Codex 安装）

**Branch**: `002-package-release` | **Date**: 2026-08-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-package-release/spec.md`

## Summary

为已完成的 `submit_pr_review` MCP 工具提供可重复的本地打包与手动发布流程：以 Windows x64 框架依赖形态产出带版本标识与 SHA-256 校验和的发布包，通过真实 GitHub 冒烟验证发布产物，文档化 Codex 全局/项目级注册，并以手动 GitHub Releases 完成版本存档。全程不引入 CI/CD 自动发布，不改变工具运行时职责。

## Technical Context

**Language/Version**: C# / .NET 10（`global.json` 已锁定；本机 SDK 已装）

**Primary Dependencies**: dotnet CLI、PowerShell 7、gh CLI、Codex CLI（安装验证）；产品运行时依赖沿用 001（ModelContextProtocol 2.1.0 等）

**Storage**: N/A（无持久化；产物为本地文件，存档为 GitHub Releases）

**Testing**: `dotnet test`（现有 82 单测/组件 + 5 真实冒烟，门禁已验证）；新增 `scripts/smoke-published.ps1` 对发布产物做 MCP stdio 真实上传冒烟

**Target Platform**: Windows x64 框架依赖（目标机需 .NET 10 运行时，无需 SDK）

**Project Type**: CLI/MCP 服务器发行（打包、安装、发布流程）

**Performance Goals**: 单版本发布构建 ≤ 5 分钟；首次完整安装验证 ≤ 30 分钟（SC-006）

**Constraints**: 手动发布、禁止 CI/CD 自动发布（FR-009）；密钥不入库/产物（FR-003/007）；单一 GitHub App 安装；产物零敏感内容；每版本 1 产物 + 1 Release

**Scale/Scope**: 单机维护者使用；每版本一个 `PrReviewSubmit-<v>-win-x64.zip`

## Constitution Check

*GATE: 已通过（Phase 0 前）。Phase 1 后复检仍通过。*

- 单一职责：只新增发布/安装外壳，不改工具行为；发布产物仍仅暴露 `submit_pr_review`（FR-008）。
- 显式目标与最小作用域：发布脚本只操作本仓库产物与目标仓库 Release；安装文档要求显式三项配置。
- Bot 身份合规：冒烟必须回读 `user.type == Bot`，不伪造身份。
- 凭据安全：私钥仅本地 `private-key/`，env 只传路径；发布脚本内置敏感扫描（文件名 + 内容模式），命中即失败。
- 失败透明：publish/smoke/release 任一失败返回非 0 与明确原因，不静默、不覆盖已有 tag/Release。

**门禁证据**：`scripts/gates.ps1` 已运行通过——构建 0 错误（仅 4 条离线 NU1900 告警）、测试 82 通过 / 5 冒烟按预期跳过、私钥排除检查通过、`dotnet format --verify-no-changes` 通过。

## Project Structure

### Documentation (this feature)

```text
specs/002-package-release/
├── plan.md              # 本文件
├── research.md          # Phase 0 输出（决策记录）
├── data-model.md        # Phase 1 输出
├── quickstart.md        # Phase 1 输出
├── contracts/           # Phase 1 输出
│   ├── release-artifact.md
│   ├── codex-install.md
│   ├── release-process.md
│   └── release-cli.md
└── tasks.md             # Phase 2 输出（$speckit-tasks，本命令不创建）
```

### Source Code (repository root)

```text
scripts/
├── gates.ps1              # 既有门禁；建议增加 scripts/*.ps1 PowerShell AST 语法检查（发布可靠性）
├── publish.ps1            # 新增：发布产物 + 敏感扫描 + 校验和 + zip
├── smoke-published.ps1    # 新增：MCP stdio 直连发布产物真实冒烟
└── release.ps1            # 新增：前置校验 + git tag + GitHub Release

scripts 的 CLI 契约（参数/退出码/DryRun/env 必填）见 `contracts/release-cli.md`。

src/PrReviewSubmit/
└── PrReviewSubmit.csproj  # 可选：加 <Version> 属性配合 /p:Version（不改业务代码）

README.md                  # 更新：新增"发布与安装"章节（产物生成 / Codex 注册 / 冒烟验收 / 正式发布 / 排错；项目级注册仅占位符示例）
.codex/config.toml         # 不提交：项目级注册仅 README 占位符说明（FR-014）
```

**Structure Decision**: 采用单仓库根 `scripts/` 存放发布工具脚本（与既有 `gates.ps1` 同层）；不新增子项目、不改动 `src/` 业务代码，保持宪法"最小上传接口"边界。产物输出到仓库根 `dist/<version>/`，由 .gitignore 排除、不提交（已确认）；实现阶段需把 `dist/` 加入排除规则。

## 发布脚本职责（Phase 1 补充）

- `publish.ps1`：校验版本号 SemVer 与已跟踪工作区干净（untracked 仅警告）→ 清理旧 `dist/<version>/` → `dotnet publish`（win-x64 框架依赖）→ 写入 `VERSION` 与 `BUILD_INFO`（构建 commit）→ 敏感扫描（仅产物目录）→ 生成 zip 与 sha256（输出到 `dist/` 根）。
- `smoke-published.ps1`：先解压 zip 到临时目录 → MCP stdio 直连解压副本 → `initialize` / `tools/list` / `tools/call submit_pr_review` → 回读校验 bot 标识与内容一致性 → 输出状态与证据（reviewId、bot 标识、PR URL）并写审计文件 `notes/reviews/<version>-smoke.md`；可重复执行，历史 review 不清理，以最近一次成功为准。
- `release.ps1`：自动运行 `scripts/gates.ps1` 作为门禁校验 → 前置校验（产物与 VERSION/BUILD_INFO 存在、`BUILD_INFO.commit` 等于远程 main HEAD、sha256 匹配、冒烟审计 success、`gh auth status` 通过、tag 不存在或存在但无对应 Release（补建路径））→ 生成发布说明（git log 自上一 tag，`-NotesFile` 可人工编辑）→ 若 tag 已存在则跳过打 tag、直接 `gh release create`（FR-015）；否则 `git tag` + `git push` → `gh release create`；成功后写审计文件 `notes/reviews/<version>-release.md`。
- 任一前置校验失败：非 0 退出并给出明确原因，不产生 tag/Release。
- 明确范围外：不做代码签名、不做多平台产物、不做 CI/CD 自动发布（Q1/Q2 与宪法边界）。
- `-DryRun` 预览与退出码约定（0 成功 / 1 前置失败 / 2 参数或 env 缺失）见 `contracts/release-cli.md`；`release.ps1` 额外校验 VERSION 内容与请求版本一致；`smoke-published.ps1` 校验全部 `GITHUB_SMOKE_*` env 后执行。

## Complexity Tracking

无宪法违规，无需复杂度豁免。

---

description: "Task list for feature 002-package-release"
---

# Tasks: 打包发布（发布产物与 Codex 安装）

**Input**: Design documents from `specs/002-package-release/`

**Prerequisites**: plan.md、spec.md、research.md、data-model.md、contracts/、quickstart.md

**Tests**: 本功能以 quickstart.md 的端到端验证场景作为每个用户故事的独立测试（真实冒烟需要 GitHub App env）；未要求 TDD 单元测试。

**Organization**: 任务按用户故事分组（US1/US2/US3），支持独立实现与验证。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 可并行（不同文件、无依赖）
- **[Story]**: US1（发布包）/ US2（Codex 安装）/ US3（版本发布）
- 每个任务含明确文件路径

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 仓库级发布基础设施准备

- [x] T001 在 .gitignore 增加 `dist/` 排除规则，确保发布产物不进入版本库

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 发布可靠性基础设施，阻塞后续所有故事

- [x] T002 [P] 在 scripts/gates.ps1 增加对 scripts/*.ps1 的 PowerShell AST 语法检查（发布脚本错误提前暴露；门禁增强，非 FR 直接要求，源于 research D15）

**Checkpoint**: 门禁可发现发布脚本语法问题；用户故事可开始

---

## Phase 3: User Story 1 - 生成可分发的发布包 (Priority: P1) 🎯 MVP

**Goal**: 一条命令产出 Windows x64 框架依赖发布包（含 VERSION/BUILD_INFO/校验和/zip），零敏感内容。

**Independent Test**: quickstart 场景 1（产物生成与扫描）+ 场景 2（对解压副本真实冒烟上传并回读校验）。

### Implementation for User Story 1

- [x] T003 [P] [US1] 实现 scripts/publish.ps1：-Version（默认 1.0.0，SemVer 校验）、已跟踪工作区干净校验、清理旧 dist/<version>/、dotnet publish（win-x64 框架依赖）、写 VERSION 与 BUILD_INFO、敏感扫描（文件名黑名单 + 内容模式，命中退出 1）、生成 zip 与 sha256（dist/ 根）、-DryRun 预览、退出码 0/1/2 —— scripts/publish.ps1
- [x] T004 [P] [US1] 实现 scripts/smoke-published.ps1：GITHUB_SMOKE_* 等 env 必填校验（缺失退出 2 并列出）、解压 zip 到临时目录、MCP stdio 直连解压副本（initialize / tools/list / tools/call submit_pr_review）、断言 tools/list 仅返回 submit_pr_review（SC-007）、回读校验内容一致与 bot 标识、写审计 notes/reviews/<version>-smoke.md、可重复执行、-DryRun —— scripts/smoke-published.ps1
- [ ] T005 [US1] 按 quickstart 场景 1 验证 publish.ps1：产物/VERSION/BUILD_INFO/zip/sha256/敏感扫描通过，同版本重复构建校验和可复算 —— 验证（quickstart.md）
- [ ] T006 [US1] 按 quickstart 场景 2 用发布产物在真实测试 PR 完成一次冒烟（bot review + 回读一致），确认审计文件生成 —— 验证（需 GITHUB_SMOKE_* env）

**Checkpoint**: US1 完整可用——产物可生成、可验证、可真实冒烟

---

## Phase 4: User Story 2 - 按文档在 Codex 中注册并调用 (Priority: P1)

**Goal**: README 提供完整安装指南（全局/项目级注册、三项 env、升级/回退/卸载、排错），Codex 会话可调用 submit_pr_review。

**Independent Test**: quickstart 场景 3（codex mcp list 可见 + 新会话调用成功）。

### Implementation for User Story 2

- [ ] T007 [US2] 在 README.md 新增"发布与安装"章节：产物生成命令、Codex 注册（全局 codex mcp add + 项目级占位符 config.toml，不提交真实配置）、三项 env 语义、验证步骤、升级/回退/卸载、排错清单（配置缺失、服务不可见、运行时缺失、gh 凭据）—— README.md
- [ ] T008 [US2] 按 quickstart 场景 3 验证安装：codex mcp list 可见 pr-ai-reviewer，新 Codex 会话调用 submit_pr_review 成功，并记录"注册到首次成功调用"耗时对照 SC-006（≤30 分钟）—— 验证

**Checkpoint**: US1 + US2 闭环——产物可安装、Agent 可用

---

## Phase 5: User Story 3 - 可追溯的版本发布 (Priority: P2)

**Goal**: 手动发布 v1.0.0：前置校验（含 commit=main HEAD、VERSION 交叉校验、冒烟审计）、release notes、git tag、GitHub Release、审计文件与补建恢复。

**Independent Test**: quickstart 场景 4（先 -DryRun 预览，再实际发布并核对资产与审计）。

### Implementation for User Story 3

- [ ] T009 [US3] 实现 scripts/release.ps1：自动运行 gates、前置校验（产物与 VERSION/BUILD_INFO 存在、VERSION 内容==请求版本、BUILD_INFO.commit==origin/main HEAD、sha256 匹配、冒烟审计 success、gh auth status、tag 不存在或存在但无 Release（补建路径））、notes 生成（git log 自上一 tag，-NotesFile 可人工编辑）、tag+push 或跳过、gh release create、写审计 notes/reviews/<version>-release.md、-DryRun、退出码 0/1/2 —— scripts/release.ps1
- [ ] T010 [US3] 按 quickstart 场景 4 先 -DryRun 验证校验与预览，再实际发布 v1.0.0，核对 GitHub Release 资产（zip+sha256）与审计文件 —— 验证

**Checkpoint**: 全部用户故事独立可用，版本可追溯

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: 跨故事收尾

- [ ] T011 运行全量门禁 scripts/gates.ps1 确认通过（含新增发布脚本语法检查）
- [ ] T012 文档一致性终审：spec/plan/research/contracts/quickstart/README 的关键决策与引用一致（含 release-cli.md 引用、版本号、占位符示例）
- [ ] T013 按 Conventional Commits 提交并推送 002 分支（推送方式按用户指示：PR 或直推 main）

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 无依赖，立即开始
- **Foundational (Phase 2)**: 依赖 Setup；阻塞所有用户故事
- **US1 (Phase 3)**: 依赖 Phase 1-2
- **US2 (Phase 4)**: 依赖 US1 产物（README 引用产物路径）
- **US3 (Phase 5)**: 依赖 US1 产物与冒烟审计（T004/T006）
- **Polish (Phase 6)**: 依赖所有故事完成

### User Story Dependencies

- **US1 (P1)**: 无故事间依赖
- **US2 (P1)**: 依赖 US1（引用产物与命令）
- **US3 (P2)**: 依赖 US1（冒烟审计）；与 US2 可并行

### 任务依赖

- T001 → T002 → T003/T004（并行）→ T005 → T006 → T007 → T008
- T004/T006 → T009 → T010
- T007 与 T009 可并行（不同文件）
- T011/T012 → T013

### Parallel Opportunities

- T002 独立于其他任务（不同文件）
- T003 与 T004 可并行（publish.ps1 / smoke-published.ps1 不同文件）
- US2（T007/T008）与 US3（T009/T010）可并行（在 US1 完成后）

## Parallel Example: User Story 1

```text
Task: "实现 scripts/publish.ps1（产物构建+扫描+校验和）"
Task: "实现 scripts/smoke-published.ps1（解压副本真实冒烟）"
```

## Implementation Strategy

### MVP First (US1 + US2)

1. 完成 Phase 1-2（Setup + 门禁语法检查）
2. 完成 US1：publish + smoke + 场景 1/2 验证
3. 完成 US2：README 安装章节 + 场景 3 验证
4. **STOP and VALIDATE**: 产物可生成、可安装、Agent 可调用（P1 闭环）

### Incremental Delivery

1. Foundation ready（dist 排除 + 门禁语法检查）
2. US1 → 独立验证（产物+冒烟）→ MVP 核心
3. US2 → 独立验证（安装闭环）
4. US3 → 独立验证（发布 v1.0.0，P2 可后置）

## Notes

- [P] 任务 = 不同文件、无依赖
- [Story] 标签映射到 spec.md 的用户故事
- 冒烟/发布验证需要真实 GitHub 目标与 GitHub App env（quickstart 前置条件）
- 发布冒烟与发布操作均支持 -DryRun 预览（contracts/release-cli.md）
- 每个逻辑组提交（Conventional Commits），审计文件随组提交

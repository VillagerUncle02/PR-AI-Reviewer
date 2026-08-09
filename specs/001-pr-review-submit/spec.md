# Feature Specification: PR Review Submit（PR 审查结果上传）

**Feature Branch**: `001-pr-review-submit`

**Created**: 2026-08-09

**Status**: Draft

**Input**: User description: "为「PR-AI-Reviewer」创建 feature spec：特化 MCP Tool，在 AI Agent 完成 GitHub PR 代码审查后，以 GitHub App Bot 身份将审查结论通过 submit review 上传到指定 PR（整体结论作为 review body，逐条建议作为文件改动位置的 review comments）；上传成功后调用方收到明确成功结果，失败时返回明确原因；工具不生成审查内容、不持久化数据、不做 submit review 之外的其他 GitHub 操作。技术背景（供计划阶段使用）：C# / .NET LTS、官方 MCP C# SDK、Octokit / GitHub REST API、GitHub 版本管理。"

## Clarifications

### Session 2026-08-09

- Q: GitHub App 私钥与安装令牌等凭据存放在哪里？ → A: 本地 `private-key` 文件夹（仓库根目录下，登记在排除规则中，不进入版本库）。

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 以 Bot 身份用 submit review 上传审查 (Priority: P1)

调用方 Agent 已完成对某个 PR 的代码审查，生成了整体审查结论与逐文件评论；调用工具并显式指定目标账号、目标仓库与 PR 编号。工具通过该仓库已安装的 GitHub App 认证，将整体结论作为 review 主体、逐条评论作为对应文件改动位置的 review comments 一并提交。提交成功后，PR 上出现一条带 bot 标识的已提交 review，调用方收到明确成功结果。

**Why this priority**: 这是本工具的唯一天职与核心价值——把 Agent 已完成的审查内容变成 GitHub 官方 review。没有它就没有本工具，属于 P1。

**Independent Test**: 对任意一个已安装 GitHub App 的测试仓库，向一个打开状态的测试 PR 传入有效的整体结论与若干条指向文件改动的评论；可独立验证 PR 上出现 bot 身份 review、内容与输入完全一致、评论出现在正确的文件与行位置。

**Acceptance Scenarios**:

1. **Given** 目标仓库存在且已安装 GitHub App、目标 PR 处于打开状态、载荷有效（非空整体结论 + 每条评论含文件路径、行位置与评论内容），**When** Agent 发起上传，**Then** PR 上出现一条带 bot 标识的已提交 review，整体结论与全部评论内容与传入一致。
2. **Given** 评论指定了目标 PR file change 范围内真实存在的文件路径与行位置，**When** 上传成功，**Then** 每条评论显示在对应的文件改动位置（含正确的代码侧与行）。
3. **Given** 上传成功，**When** 调用返回，**Then** 调用方收到可程序化解析的明确成功结果，无需人工查看 GitHub 页面即可确认成功。

---

### User Story 2 - 上传失败并返回明确错误 (Priority: P1)

当无法完成上传时（目标仓库/PR 不存在、GitHub App 未安装、权限不足、PR 已关闭或已合并、载荷无效、凭据缺失或过期、GitHub API 网络错误或限流、部分评论无法提交等），工具必须向调用方返回明确、可操作的失败原因，不得静默吞掉错误，也不得产生不确定的半成功状态。

**Why this priority**: 调用方是 AI Agent，依赖确定性的成功/失败信号决定下一步；模糊或静默失败会导致重复上传或审查缺失。该行为与"失败透明"原则一致，属于 P1。

**Independent Test**: 分别对不存在的 PR、已关闭的 PR、缺少必要字段的载荷发起上传；可独立验证调用方每次均收到明确失败原因，且 GitHub 上未产生任何新 review、未发出任何提交请求。

**Acceptance Scenarios**:

1. **Given** 目标 PR 不存在、已合并或已关闭，**When** Agent 发起上传，**Then** 调用方收到能区分"目标不可提交"的明确失败原因，且目标 PR 上无任何新 review 产生。
2. **Given** 载荷缺少必要字段（如整体结论为空、评论缺文件路径或行位置）或结构无效，**When** Agent 发起上传，**Then** 调用方收到格式错误提示，且工具未向 GitHub 发出任何提交。
3. **Given** 任一失败场景发生，**When** 调用结束，**Then** 调用方收到的结果必须为明确失败（含原因），不存在"部分成功"或静默无结果。

---

### User Story 3 - 重复提交由调用方控制 (Priority: P2)

工具不做隐式重试或自动补偿；同一次调用失败后，由调用方修正载荷或条件后再次发起上传。调用方可在任意时刻发起新的上传尝试。

**Why this priority**: 保持行为确定性，避免工具在 Agent 不知情的情况下重复提交或产生多余 review；重试策略属于调用方编排职责，属 P2。

**Independent Test**: 制造一次失败上传，观察工具不会自行发起任何后续 GitHub 请求；随后由调用方再次发起有效上传，可成功产生一条新的 review。

**Acceptance Scenarios**:

1. **Given** 一次上传失败，**When** 经过任意等待时间，**Then** 工具不会自行重试或自动补偿，GitHub 上不会出现由工具自动发起的额外请求或 review。
2. **Given** 调用方修正载荷或条件后再次发起上传，**When** 载荷与目标条件有效，**Then** 上传成功并返回明确成功结果。

---

### Edge Cases

- 目标账号或仓库不存在、拼写错误或无访问权限：返回明确失败，不产生任何新 review。
- GitHub App 未安装到目标仓库（或安装已撤销）：返回明确失败，说明 App 未安装/无权限。
- PR 编号不存在、已合并或已关闭：返回明确失败，不提交、不产生新 review。
- 载荷缺少必要字段或结构无效（整体结论为空、评论缺文件路径或行位置等）：在向 GitHub 发起提交前即拒绝并返回格式错误。
- 评论指向的文件或行不在目标 PR 的 file change 范围内：整次调用判为失败并返回明确原因；不得忽略该评论而部分提交。
- 凭据缺失、过期或无效：返回明确失败，且不向 GitHub 发起任何操作。
- GitHub API 网络错误、超时或限流：返回明确失败原因；由调用方决定是否重试。
- 提交过程中部分评论失败：整次调用判为整体失败并报告；调用方不得收到成功或部分成功信号（整体原子性的实现方式以计划阶段确定）。

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**：工具 MUST 仅向调用方暴露"提交 PR review"一个操作；每次调用不得触发该操作之外的任何 GitHub 操作。
- **FR-002**：每次调用 MUST 显式指定并仅使用目标账号、目标仓库与 PR 编号；MUST NOT 通过默认值、推断、环境上下文或通配符选择目标。
- **FR-003**：工具 MUST 校验载荷完整性：整体结论 MUST 为非空文本；每条评论 MUST 包含文件路径、行位置与评论内容。缺失任一必需字段或结构无效时，MUST 返回格式错误且不向 GitHub 发起任何提交。
- **FR-004**：工具 MUST 校验每条评论的文件路径与行位置位于目标 PR 的 file change 范围内；任何评论超出范围 MUST 使本次调用整体失败并返回明确原因。
- **FR-005**：工具 MUST 将传入的整体结论作为 review 主体、将逐条评论作为对应文件位置的 review comments 一并提交；MUST NOT 修改、增删或重新生成审查内容。
- **FR-006**：工具 MUST 通过目标仓库已安装的 GitHub App 认证，并以该 App 的 bot 身份完成提交；MUST NOT 以人类账号身份代发、隐藏或伪造身份。
- **FR-007**：提交成功时，工具 MUST 返回明确成功结果；该结果 MUST 可被调用方程序化解析，并对应 GitHub 上一条带 bot 标识、包含整体结论与全部评论的已提交 review。
- **FR-008**：当目标不存在/无权限、App 未安装、PR 不存在/已关闭/已合并、载荷无效、凭据缺失/过期/无效、网络错误或限流、部分评论失败等任何失败发生时，工具 MUST 返回明确、可操作的失败原因；MUST NOT 静默吞错、返回成功或产生不确定状态。
- **FR-009**：若提交过程中出现部分评论失败，工具 MUST 将整次调用判为整体失败并报告；MUST NOT 向调用方返回部分成功状态。
- **FR-010**：工具 MUST NOT 隐式重试或自动补偿；一次失败后仅可由调用方重新发起上传。
- **FR-011**：工具 MUST NOT 持久化、缓存或存储任何业务数据；每次调用 MUST 为无状态处理。
- **FR-012**：工具 MUST 仅使用本地 `private-key` 文件夹（仓库根目录下，受保护位置）中的 GitHub App 凭据；该文件夹 MUST 登记在 .gitignore（或等效排除规则）中，MUST NOT 出现在任何版本库内容中，并 MUST 使用短期令牌及时失效。

### Key Entities *(include if feature involves data)*

- **上传请求（Review Submit Request）**：单次调用的全部输入，包含目标账号、目标仓库、PR 编号、整体结论与逐文件评论列表。
- **整体结论（Review Body）**：调用方生成的非空审查总结文本，上传后作为 review 主体展示。
- **逐文件评论（Review Comment）**：调用方生成的单条审查意见，包含文件路径、行位置（含代码侧）与评论内容。
- **目标 PR（Target Pull Request）**：由账号+仓库+编号唯一定位，须处于打开状态且位于该 GitHub App 安装与授权范围内。
- **已提交 Review（Submitted Review）**：GitHub 平台上的产物，带 bot 标识，包含整体结论与各文件位置的评论。
- **调用结果（Call Result）**：每次调用返回的结构化结果，为明确成功或明确失败（含原因），供调用方决策。

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**：100% 的有效上传请求在目标 PR 上生成一条已提交 review，整体结论与全部评论内容与调用方输入完全一致（无增删改）。
- **SC-002**：100% 的逐文件评论显示在输入所指定的文件与行位置。
- **SC-003**：100% 的已提交 review 被 GitHub 标记为 bot 身份，无任何冒充人类身份的提交。
- **SC-004**：100% 的失败调用向调用方返回明确、可操作的失败原因；失败调用中 0 次静默吞错。
- **SC-005**：对目标不存在、App 未安装、PR 不存在/已关闭/已合并或载荷无效的调用，0 次产生新 review（0 误提交）。
- **SC-006**：调用结果中 0% 出现半成功状态；每次调用要么整体成功，要么整体失败。
- **SC-007**：任意调用结束后，工具运行环境中 0 条业务数据被持久化。
- **SC-008**：在 GitHub 平台可用且网络正常的前提下，单次上传从调用到返回明确结果在 30 秒内完成。
- **SC-009**：100% 的成功/失败结果可被调用方程序化解析，无需人工查看 GitHub 页面即可作出下一步决策。

## Assumptions

- 唯一使用者是调用方 Agent；工具以 MCP Tool 形式接受结构化的上传调用，无图形界面或人工交互。
- 每次调用显式指定目标账号、仓库与 PR 编号；不存在任何选择或推导目标账号/仓库的机制。
- 审查内容（整体结论与逐条评论）完全由调用方生成；工具只负责提交，不分析代码、不生成或修改任何审查意见。
- 目标仓库已安装并授权该 GitHub App；未安装或授权失效时按失败路径处理并返回明确原因。
- GitHub App 私钥与安装令牌等凭据保存在仓库根目录下的 `private-key` 文件夹（本地受保护位置），并登记在 .gitignore（或等效排除规则）中，绝不进入版本库；令牌为短期有效并及时失效。
- 通过 GitHub App 执行的所有程序化操作，GitHub 平台会标记为 bot 操作；工具不得伪造、隐藏或冒充人类身份。
- 技术背景（仅供计划阶段参考，不构成功能需求或验收约束）：C# / .NET LTS、官方 MCP C# SDK、Octokit / GitHub REST API、GitHub 版本管理。
- 本功能为 v1 唯一功能，MVP 即完整交付；工具只执行"提交 review"一种操作，范围外事项（内容生成、持久化、CI/CD、通知等）一律不做。
- 部分评论失败时的整体原子性以计划阶段可实现性为准；无论实现细节如何，调用方可观察结果必须为整体成功或整体失败，不得出现半成功信号。
- SC-008 的时间指标以 GitHub 平台可用且网络正常为前提。

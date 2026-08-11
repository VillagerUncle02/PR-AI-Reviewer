# Feature Specification: 打包发布（可分发的 MCP Tool 发布包与 Codex 安装）

**Feature Branch**: `002-package-release`

**Created**: 2026-08-11

**Status**: Draft

**Input**: User description: "打包发布"（为已完成的 PR-AI-Reviewer MCP Tool 提供可重复的打包发布流程：产出可分发的发布产物，并给出在 Codex Agent 中安装、配置与验证的完整指南）

## Clarifications

### Session 2026-08-11

- Q: v1 的发布产物需要支持哪些目标形态（操作系统 + 运行时依赖方式）？ → A: 仅 Windows x64 框架依赖；产物不含 .NET 运行时，目标机器需安装 .NET 10 运行时（无需 SDK）。
- Q: 正式发布时，产物是否需要上传到 GitHub Releases 作为存档与分发渠道？ → A: 需要；手动执行发布，git tag、发布说明与校验和资产上传到 GitHub Releases。
- Q: 发布冒烟验收必须用真实 GitHub 目标执行，还是允许用本地测试替身代替？ → A: 每个正式版本发布前，必须用发布产物在真实测试 PR 上完成一次 bot review 上传；测试替身不替代正式发布的真实冒烟。
- Q: 发布产物目录 dist/ 是否纳入版本库？ → A: 不纳入；由 .gitignore 排除，产物仅本地存在，可由任意一次构建再生成。
- Q: 002 首个正式发布版本号？ → A: 1.0.0。
- Q: 发布产物的源码可追溯性要求？ → A: 产物记录构建 commit；release.ps1 校验该 commit 等于远程 main HEAD；CI 状态由发布操作者在 GitHub 页面人工确认（工具不查询 checks）。
- Q: 冒烟失败重试会重复上传 review，如何处理？ → A: 允许重复执行，不清理历史 review，以最近一次成功结果为准。
- Q: 项目级注册示例如何提供？ → A: 仅 README 文档说明 + 占位符示例，不提交 .codex/config.toml。
- Q: 发布中途失败（tag 已推送但 Release 未创建）如何恢复？ → A: 允许补建；tag 存在且无对应 Release 时跳过打 tag、直接创建 Release；绝不删除远端 tag。

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 一条命令生成可分发的发布包 (Priority: P1)

维护者在干净的源码检出上执行发布命令，得到一个包含可执行入口、全部运行所需文件与版本标识的发布产物；该产物不依赖源码目录，不包含任何密钥或敏感配置，可被复制到另一台满足运行时要求的机器上直接运行。

**Why this priority**: 这是"打包发布"的核心价值——让工具脱离源码与开发环境即可运行，也是后续所有安装场景的前提；没有可分发的产物，安装就无从谈起，属于 P1。

**Independent Test**: 在干净的源码检出上执行发布命令得到产物，将产物目录复制到另一个已安装 .NET 10 运行时（无需 SDK）的目标目录并完成一次冒烟上传；可独立验证产物可运行且行为与开发构建一致。

**Acceptance Scenarios**:

1. **Given** 干净的源码检出与明确的语义化版本号，**When** 执行发布命令，**Then** 生成包含可执行入口、运行所需文件与版本标识的发布产物，命令成功结束且退出码为 0。
2. **Given** 发布产物已复制到满足运行时要求的独立目录，**When** 以该产物发起一次冒烟上传，**Then** 工具正常启动并完成上传，目标 PR 上出现带 bot 标识的 review，行为与开发构建一致。
3. **Given** 发布产物已生成，**When** 对其执行敏感内容扫描，**Then** 产物中不包含 GitHub App 私钥、令牌或任何敏感配置。

---

### User Story 2 - 按文档在 Codex 中注册并调用 (Priority: P1)

用户按照安装文档，在 Codex CLI（或等价配置）中注册 `pr-ai-reviewer` MCP 服务，填入三项必需配置（App ID、安装 ID、私钥路径）；重启会话后，Agent 可直接调用 `submit_pr_review`，以 bot 身份把审查结论上传到指定 PR。

**Why this priority**: 发布产物只有能被 Codex Agent 注册调用才算完成闭环；没有可用的安装路径，发布包本身没有价值，属于 P1。

**Independent Test**: 按文档执行注册后通过 `codex mcp list` 确认服务可见；随后在新 Codex 会话中让 Agent 调用 `submit_pr_review` 完成一次真实上传，可独立验证安装链路完整。

**Acceptance Scenarios**:

1. **Given** 发布产物可用且三项必需配置有效，**When** 用户按文档执行注册，**Then** Codex 配置中出现该 MCP 服务，`codex mcp list` 可看到它。
2. **Given** 注册成功后的新 Codex 会话，**When** Agent 调用 `submit_pr_review` 上传有效载荷，**Then** 目标 PR 上出现带 bot 标识的已提交 review，调用方收到明确成功结果。
3. **Given** 配置缺失、非法或私钥路径无效，**When** 启动或调用该服务，**Then** 用户得到明确配置错误提示，且不会向 GitHub 发起任何提交。

---

### User Story 3 - 可追溯的版本发布 (Priority: P2)

维护者为每次发布创建语义化版本号、对应 git tag 与发布说明（含产物、校验和与变更摘要）；用户能够唯一确认自己安装的版本及其来源。

**Why this priority**: 在核心发布与安装闭环之外，版本可追溯能提升可维护性与排错效率；没有它仍可完成安装，属于 P2。

**Independent Test**: 按文档对 vX.Y.Z 执行一次发布，确认 git tag 与 GitHub Release（含发布说明、校验和与产物资产）一一对应，且产物内含可读取的版本标识。

**Acceptance Scenarios**:

1. **Given** 待发布版本号 vX.Y.Z，**When** 执行发布流程，**Then** 创建唯一的 git tag 与 GitHub Release，包含发布说明、校验和与产物资产。
2. **Given** 已安装的发布产物，**When** 用户查看其版本信息，**Then** 可唯一确定对应版本与 GitHub Release。

---

### Edge Cases

- 目标机器未安装 .NET 10 运行时：框架依赖产物无法启动；文档须写明运行时前置要求与安装指引，并明确无需安装 SDK。
- 私钥路径包含空格或非 ASCII 字符：注册命令与配置须能正确处理路径，文档给出示例。
- Codex 已存在同名 MCP 服务：注册前须明确失败或提示先移除/重命名，文档给出处理步骤。
- 发布构建在工作区包含未提交修改或密钥时执行：发布流程须防止敏感内容混入产物；文档建议从干净检出发布。
- 版本号重复或 git tag 已存在：发布流程须明确失败并提示，不得静默覆盖。
- 目标机器处于离线或受限网络环境：框架依赖产物在已安装 .NET 10 运行时后仍可启动；首次调用仅需访问 GitHub API，文档说明网络前提。
- 发布产物误含敏感文件：发布前的敏感扫描须发现并阻断构建，不能仅靠人工检查。
- 多用户/多机器安装：每个安装点各自提供私钥路径与配置，配置不得互相共享或写入版本库。
- 发布产物与开发构建行为不一致：冒烟验收须覆盖同一组成功/失败场景，两者结果一致。

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**：项目 MUST 提供可重复执行的发布构建流程（命令或脚本），在干净的源码检出上生成带语义化版本号的发布产物。
- **FR-002**：发布产物 MUST 包含运行所需的全部文件与单一可执行入口，MUST 不依赖源码目录即可启动；产物 MUST 内含可读取的版本标识，便于用户确认所装版本。
- **FR-003**：发布产物 MUST 不包含 GitHub App 私钥、令牌或任何敏感配置；发布前 MUST 执行敏感内容扫描，发现任何敏感文件时构建 MUST 失败。
- **FR-004**：发布版本号 MUST 遵循语义化版本（SemVer），MUST 与唯一 git tag 一一对应；正式发布 MUST 上传到 GitHub Releases（含发布说明、校验和与产物资产）；重复或冲突版本 MUST 明确失败，MUST NOT 静默覆盖。
- **FR-005**：安装文档 MUST 完整覆盖在 Codex 中注册 MCP 服务的步骤：注册方式（命令行或等价配置）、三项必需配置（App ID、安装 ID、私钥路径）及其含义、注册后的可见性验证（如 `codex mcp list`）。
- **FR-006**：安装文档 MUST 提供安装后验证方法：在新 Codex 会话中通过 `submit_pr_review` 完成一次真实上传，确认目标 PR 出现带 bot 标识的 review 且调用方收到明确结果。
- **FR-007**：文档 MUST 明确说明私钥与敏感文件绝不进入版本库与发布产物，并说明排除规则（.gitignore 或等效机制）与本地保管要求。
- **FR-008**：发布流程 MUST NOT 改变工具的运行行为与职责范围：发布后的工具仍只执行"接收 Agent 的 PR review 内容并上传"，不新增功能、不持久化数据、不执行超出上传职责的操作。
- **FR-009**：发布与版本发布操作 MUST 由维护者手动/本地执行；git tag、GitHub Release、发布说明与校验和等产物按 FR-004/FR-012 生成；自动化 CI/CD 发布明确为范围外。
- **FR-011**：每个正式版本发布前，MUST 使用该版本发布产物在真实 GitHub 测试目标（测试仓库 + 测试 PR）上完成一次 bot review 上传冒烟；冒烟成功是发布的必要条件，本地测试替身不得替代。
- **FR-012**：发布产物 MUST 记录构建所用 git commit（BUILD_INFO）；`release.ps1` MUST 校验该 commit 等于远程默认分支 HEAD，不一致 MUST 拒绝发布；CI 状态由发布操作者人工确认，工具 MUST NOT 查询 checks。
- **FR-013**：冒烟脚本 MUST 支持重复执行；重复执行产生的历史 review 不清理，验收以最近一次成功结果为准。
- **FR-014**：项目级注册示例 MUST 仅以 README 占位符形式提供；MUST NOT 向版本库提交 `.codex/config.toml` 或任何真实 App ID、安装 ID、私钥路径。
- **FR-015**：发布中断恢复 MUST 支持"补建"：git tag 已存在但无对应 GitHub Release 时，`release.ps1` MUST 跳过打 tag 并直接创建 Release；MUST NOT 删除或覆盖远端 tag；tag 与 Release 均存在时 MUST 拒绝。
- **FR-010**：安装文档 MUST 覆盖常见排错场景（配置缺失或非法、私钥路径无效、服务不可见、调用失败等），并提供可操作的处置步骤。

### Key Entities *(include if feature involves data)*

- **发布产物（Release Artifact）**：版本化的可分发包，含可执行入口、运行所需文件、版本标识与校验和。
- **版本标识（Version）**：语义化版本号及对应的 git tag，用于唯一标识一次发布。
- **GitHub Release（发布条目）**：与 git tag 一一对应的发布记录，含发布说明、校验和与产物资产，作为正式分发存档。
- **MCP 注册配置（MCP Server Registration）**：Codex 中该服务的注册条目，含启动方式与三项环境配置（App ID、安装 ID、私钥路径）。
- **安装文档（Installation Guide）**：面向用户的安装、配置、验证与排错说明，随仓库维护。

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**：在干净的源码检出上，发布构建 100% 成功且可重复；同一版本输入得到语义一致的产物与校验和。
- **SC-002**：100% 的正式版本在发布前使用该版本产物在真实测试 PR 上完成一次 bot review 上传冒烟，结果与开发构建一致。
- **SC-003**：发布产物与安装文档中 0 处敏感内容泄露（扫描覆盖私钥文件、令牌等敏感项）。
- **SC-004**：按文档操作，100% 的新安装可在 Codex 新会话中成功调用 `submit_pr_review` 并收到明确结果。
- **SC-005**：每个正式版本有且仅有一个对应 git tag 与 GitHub Release（含发布说明、校验和与产物资产），用户可唯一确定安装版本。
- **SC-006**：首次完成完整安装（含注册、配置与验证上传）在 30 分钟内可完成，且无需向维护者额外咨询。
- **SC-007**：发布后工具职责范围不变：仍只执行 PR review 上传，0 次超出范围的操作。

## Assumptions

- 发布平台默认 Windows；开发机已安装 .NET 10（技术背景，仅供计划阶段参考，不构成功能需求）。
- 发布形态固定为 Windows x64 框架依赖：产物不含 .NET 运行时，目标机器 MUST 已安装 .NET 10 运行时（无需 SDK）（技术背景，仅供计划阶段参考，不构成功能需求）。
- 发布与版本发布由维护者手动/本地执行；不使用 CI/CD 自动发布，与宪法"工具不负责 CI/CD"的边界一致。
- GitHub Releases 是正式发布的必选存档与分发渠道（由维护者手动执行）；它不是 Codex 本地安装的前提。
- GitHub App 私钥始终保存在本机受保护位置（仓库根目录 `private-key` 文件夹），绝不进入版本库与发布产物；安装者通过环境配置指向该路径。
- 单一 GitHub App 安装配置沿用 v1：App ID 与安装 ID 由配置提供，目标仓库须在该安装授权范围内。
- Codex 注册存在两种等价方式（官方 CLI 与配置文件）；具体命令与配置示例属于计划阶段实现细节。
- 正式版本发布前的冒烟验收 MUST 使用真实 GitHub 目标（测试仓库与测试 PR），以发布产物完成一次 bot review 上传；构建成功不能替代行为验收，本地测试替身仅可用于开发期自动化。
- 发布产物输出到仓库根 `dist/<version>/`，该目录由 .gitignore 排除，不进入版本库；产物仅本地留存，可由任意一次构建再生成。
- 首个正式发布版本为 1.0.0；后续版本按 SemVer 递增。
- 发布产物内含 `BUILD_INFO`（构建 commit）；正式发布时须与远程 main HEAD 一致，CI 状态由操作者在 GitHub 页面人工确认。
- 冒烟脚本可重复执行；测试 PR 上积累的冒烟 review 不清理，以最近一次成功记录为准。
- 项目级注册仅通过 README 占位符示例说明，不提交 `.codex/config.toml` 或真实配置。
- 发布中断恢复采用"补建"策略：tag 已推送但 Release 缺失时直接补建 Release，不删除远端 tag。
- 发布冒烟以"从 zip 解压的副本"为执行对象，验证 zip 产物本身完整可用，而非仅验证 dist 源目录。
- 本地可保留多个版本产物目录（dist/1.0.0、dist/1.1.0 等），由维护者按需手动清理；版本升级通过重新注册 Codex 指向新版本完成。
- 本规格只描述"打包、发布、安装"能力，不改变既有 `submit_pr_review` 的输入输出契约与失败语义。

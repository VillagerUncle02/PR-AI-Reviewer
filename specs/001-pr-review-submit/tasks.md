# Tasks: PR Review Submit（PR 审查结果上传）

**Input**: Design documents from `/specs/001-pr-review-submit/`

**Prerequisites**: plan.md（技术栈、项目结构）、spec.md（US1~US3 与优先级）、research.md（技术决策）、data-model.md（领域模型）、contracts/（工具与 GitHub REST 契约）、quickstart.md（验收场景）

**Tests**: spec 的 "User Scenarios & Testing" 为强制章节，quickstart 定义了自动化验证，plan 定义了单元/组件/冒烟三层测试，因此本任务清单**包含测试任务**；新行为按「先写测试（预期失败）→ 再实现」执行，Phase 2 已实现组件（T011/T012/T013）的测试任务为覆盖补齐，不强制先失败。

**Organization**: 任务按用户故事分组，每个故事可独立实现与验证。

## 格式: `[ID] [P?] [Story] Description`

- **[P]**: 可并行（不同文件、无未完成依赖）
- **[Story]**: 所属用户故事（US1/US2/US3）
- 描述中必须包含精确文件路径

## 路径约定

单仓库双项目结构（见 plan.md）：

```text
PrReviewSubmit.sln
src/PrReviewSubmit/                 # MCP stdio server（控制台，net10.0）
tests/PrReviewSubmit.Tests/         # xUnit v3 测试（单元/组件/冒烟）
.github/workflows/ci.yml            # 仓库级 CI（本任务清单新增）
```

## CI/CD 决策说明

本工具是**本地 stdio MCP server**：由 MCP 客户端按需拉起、进程生命周期由客户端管理，没有服务部署、构建发布或运行态编排需求，因此**不需要部署型 CI/CD 流水线**。

但本仓库自身采用 GitHub 分支 + PR 合并工作流（constitution「开发工作流」），拥有完整的自动化测试套件，且 quickstart CHK156 已明确要求「private-key 不入库断言（.gitignore/**CI 检查**）」。因此本任务清单在 Phase 2 增加**仓库级 GitHub Actions**（`ci.yml`）：每次 PR 自动执行 `dotnet build` + `dotnet test` + 私钥排除检查，不包含任何部署/发布步骤。

> 注意：constitution「MUST NOT 执行 CI/CD」约束的是**工具运行期行为**（工具不得替目标仓库执行 CI），与本仓库开发过程的 CI 配置不冲突；本仓库 CI 不会调用工具本身。

---

## Phase 1: Setup（共享基础设施）

**Purpose**: 项目初始化与基础结构

- [X] T001 创建解决方案 PrReviewSubmit.sln 与双项目结构（dotnet new sln/console/xunit）：src/PrReviewSubmit 为 net10.0 控制台 MCP server，tests/PrReviewSubmit.Tests 为 net10.0 测试项目，按 plan.md 的 Project Structure 建立 src/ 与 tests/ 目录
- [X] T002 [P] 添加 NuGet 依赖：src/PrReviewSubmit/PrReviewSubmit.csproj 添加 ModelContextProtocol 2.1.0 与 System.IdentityModel.Tokens.Jwt；tests/PrReviewSubmit.Tests/PrReviewSubmit.Tests.csproj 添加 xUnit v3（Microsoft.Testing.Platform 原生运行器）与 NSubstitute
- [X] T003 [P] 在仓库根目录创建 global.json，固定 .NET 10 SDK 版本（10.0.x），保证本地与 CI 构建一致
- [X] T004 [P] 核验并完善 .gitignore，确保 private-key/ 与 *.pem 永不进入版本库（CHK156；现有条目不足时补充）

---

## Phase 2: Foundational（阻塞性前置条件）

**Purpose**: 所有用户故事依赖的核心基础设施，未完成前不得开始任何用户故事实现

**⚠️ CRITICAL**: 本阶段完成后，用户故事才能开始

- [X] T005 [P] 实现 GitHubAppOptions 于 src/PrReviewSubmit/Configuration/GitHubAppOptions.cs（环境变量绑定 App ID / 安装 ID / 私钥路径；BaseUrl 固定 api.github.com，FR-016）
- [X] T006 [P] 实现 ReviewSubmitRequest 于 src/PrReviewSubmit/Domain/ReviewSubmitRequest.cs（owner/repo/pullNumber/body/comments，字段与校验规则见 data-model.md）
- [X] T007 [P] 实现 ReviewSubmitResult 于 src/PrReviewSubmit/Domain/ReviewSubmitResult.cs（success: reviewId/htmlUrl；error: code/message/httpStatus/details，见 tool-contract.md）
- [X] T008 [P] 实现 ReviewSubmitErrorCode 于 src/PrReviewSubmit/Domain/ReviewSubmitErrorCode.cs（9 个错误码枚举：INVALID_PAYLOAD / CREDENTIALS_INVALID / APP_NOT_INSTALLED / TARGET_NOT_FOUND / PR_NOT_OPEN / REVIEW_UNPROCESSABLE / RATE_LIMITED / NETWORK_ERROR / UNEXPECTED_ERROR）
- [X] T009 [P] 实现 ToolJsonContext 于 src/PrReviewSubmit/Json/ToolJsonContext.cs（工具参数与结果的 JsonSerializerContext，供编译期序列化）
- [X] T010 [P] 实现 IGitHubReviewClient 于 src/PrReviewSubmit/GitHub/IGitHubReviewClient.cs（GetInstallationTokenAsync / GetPullRequestStateAsync / CreateReviewAsync，可替身测试）
- [X] T011 [P] 实现 GitHubAppAuthClient 于 src/PrReviewSubmit/GitHub/GitHubAppAuthClient.cs（PEM → RS256 JWT（iss/iat-60s/exp≤10min）→ POST /app/installations/{id}/access_tokens，repositories 限定目标仓库）
- [X] T012 [P] 实现 GitHubErrorMapper 于 src/PrReviewSubmit/GitHub/GitHubErrorMapper.cs（401/403/404/422/429/5xx/网络 → 错误码；details 脱敏并截断至 2048 字符；Retry-After 透传）
- [X] T013 [P] 实现 ReviewPayloadValidator 于 src/PrReviewSubmit/Validation/ReviewPayloadValidator.cs（FR-002/FR-003：owner/repo trim 后非空、pullNumber ≥ 1、body/comments 按 Unicode 空白 trim 判定、comments 空数组合法/null 非法、提交保持原始内容）
- [X] T014 实现 GitHubReviewClient 于 src/PrReviewSubmit/GitHub/GitHubReviewClient.cs（三请求链路：令牌交换 → GET /pulls/{n} 仅消费 state/merged（FR-004，非 open 或 merged → PR_NOT_OPEN）→ POST create review（event=COMMENT，FR-013）；各请求 HttpClient 超时 10s；无隐式重试（FR-010））
- [X] T015 [P] 创建 GitHub Actions 工作流于 .github/workflows/ci.yml（触发：pull_request 到 main + push 到 main + workflow_dispatch；steps：checkout、setup-dotnet 10.0.x、dotnet restore、dotnet build PrReviewSubmit.sln、dotnet test tests/PrReviewSubmit.Tests、私钥排除检查（git ls-files 中不得出现 private-key/** 或 *.pem，CHK156）；不包含任何部署步骤，冒烟测试默认跳过）

**Checkpoint**: 基础设施就绪 —— 可开始用户故事实现

---

## Phase 3: User Story 1 - 以 Bot 身份用 submit review 上传审查（Priority: P1）🎯 MVP

**Goal**: 调用方 Agent 显式指定 owner/repo/pullNumber，工具以 GitHub App bot 身份将整体结论与逐文件评论作为一条已提交 review（event=COMMENT）上传到打开状态 PR，并返回可程序化解析的成功结果。

**Independent Test**: 通过 in-memory MCP + stub IGitHubReviewClient（open PR + 200 create review）断言返回 status=success 且含 reviewId/htmlUrl；有真实凭据时按 quickstart 场景 A/F 冒烟，PR 上出现 bot 身份 review、内容与输入一致、评论落在指定文件与行。

### Tests for User Story 1（先写、预期失败）⚠️

- [X] T016 [P] [US1] 编写成功路径组件测试于 tests/PrReviewSubmit.Tests/Component/McpToolInvocationTests.cs（in-memory MCP 调用 submit_pr_review，stub 返回 open PR + 200 review，断言 success JSON 含 reviewId/htmlUrl；捕获 stub 收到的 create review 请求，断言 event=COMMENT、body 与 comments 的 path/line/side/body 与输入一致，覆盖 FR-005/FR-013/SC-001/SC-002）
- [X] T017 [P] [US1] 补齐载荷校验单元测试覆盖于 tests/PrReviewSubmit.Tests/Unit/ValidatorTests.cs（合法载荷通过；comments 空数组合法；owner/repo trim 后非空；pullNumber ≥ 1；内容 UTF-8 原样透传；组件已在 T013 实现，本任务为覆盖补齐，新发现缺陷按先失败再实现）

### Implementation for User Story 1

- [X] T018 [P] [US1] 创建 submit_pr_review 工具于 src/PrReviewSubmit/MCP/ReviewSubmitTool.cs（[McpServerToolType]/[McpServerTool]；参数名/类型/必填与 contracts/submit-review.schema.json 一致）
- [X] T019 [P] [US1] 实现 MCP stdio 宿主于 src/PrReviewSubmit/Program.cs（AddMcpServer + WithStdioServerTransport + WithToolsFromAssembly）
- [X] T020 [US1] 实现成功编排于 src/PrReviewSubmit/MCP/ReviewSubmitTool.cs（本地校验 → IGitHubReviewClient 令牌交换 → PR 状态核验 → 单次提交 → ToolJsonContext 序列化成功结果）

**Checkpoint**: User Story 1 可独立测试（stub 测试 + quickstart 场景 A/F）

---

## Phase 4: User Story 2 - 上传失败并返回明确错误（Priority: P1）

**Goal**: 任一失败场景（目标不存在/无权限、App 未安装、PR 已关闭/已合并、载荷无效、凭据失效、限流、网络错误、部分评论被拒等）返回明确、可操作、可程序化解析的失败结果；不静默、不半成功、不产生误提交。

**Independent Test**: 用 stub 分别模拟 closed/merged PR、404/403/422/429、网络超时与启动配置缺失/非法；断言每次返回 code/message（及 httpStatus/details）且 GitHub 侧 0 新 review、0 次提交请求；启动失败时非零退出码 + stderr 明确错误。

### Tests for User Story 2（先写、预期失败）⚠️

- [X] T021 [P] [US2] 补齐错误映射单元测试覆盖于 tests/PrReviewSubmit.Tests/Unit/ErrorMapperTests.cs（401/403/404/422/429/5xx/网络 → 对应错误码；Retry-After 透传；details 脱敏与 2048 字符截断；组件已在 T012 实现，本任务为覆盖补齐）
- [X] T022 [P] [US2] 补齐认证客户端单元测试覆盖于 tests/PrReviewSubmit.Tests/Unit/AppAuthClientTests.cs（JWT claims/iat/exp、PEM 解析、令牌交换成功/401/403/404/429、201 响应缺字段或类型异常 → UNEXPECTED_ERROR；新增：调用间删除私钥或替换为不可解析内容后再次调用 → CREDENTIALS_INVALID 且无 GitHub 请求；替换为有效新私钥 → 下一次调用按新私钥成功、无需重启（2026-08-10 澄清）；组件已在 T011 实现，本任务为覆盖补齐）
- [X] T023 [P] [US2] 编写失败路径组件测试于 tests/PrReviewSubmit.Tests/Component/ReviewSubmitFlowTests.cs（PR closed/merged → PR_NOT_OPEN；404 → TARGET_NOT_FOUND；403 → APP_NOT_INSTALLED；422 → REVIEW_UNPROCESSABLE；空 diff PR + comments → 422 → REVIEW_UNPROCESSABLE（spec 边界）；提交后超时 → NETWORK_ERROR 且提示 review 可能已创建；断言无部分成功）
- [X] T024 [P] [US2] 编写启动配置校验测试于 tests/PrReviewSubmit.Tests/Component/StartupConfigurationTests.cs（FR-015：App ID/安装 ID 缺失或非正整数、私钥路径不存在/不可读/不可解析为 RSA → 非零退出码 + stderr 明确错误；断言无网络请求）

### Implementation for User Story 2

- [X] T025 [P] [US2] 实现失败编排于 src/PrReviewSubmit/MCP/ReviewSubmitTool.cs（先本地校验：INVALID_PAYLOAD 且零 GitHub 请求；客户端各阶段失败统一映射为结构化错误 JSON；无隐式重试）
- [X] T026 [P] [US2] 实现启动配置校验于 src/PrReviewSubmit/Program.cs（FR-015：App ID/安装 ID 须为正整数、私钥路径存在且可读、私钥可解析为 RSA；失败以非零退出码退出并输出 stderr 错误）
- [X] T027 [P] [US2] 添加 FR-016 配置断言测试于 tests/PrReviewSubmit.Tests/Component/GitHubClientConfigurationTests.cs（HttpClient.BaseAddress 恒为 api.github.com、TLS 证书校验开启且不可禁用）

**Checkpoint**: User Story 1 与 User Story 2 均独立可测（成功 + 全失败路径）

---

## Phase 5: User Story 3 - 重复提交由调用方控制（Priority: P2）

**Goal**: 工具不做隐式重试或自动补偿；失败后由调用方修正载荷/条件后再次发起；每次调用相互独立、无共享状态、无令牌缓存。

**Independent Test**: stub 制造一次失败调用后，断言 GitHub 请求计数器保持不变（无自动重试）；随后调用方发起第二次有效调用，断言成功且仅产生一条新 review；连续/并发多次调用互不干扰。

### Tests for User Story 3（先写、预期失败）⚠️

- [X] T028 [P] [US3] 编写无重试行为测试于 tests/PrReviewSubmit.Tests/Component/NoRetryBehaviorTests.cs（失败调用后等待观察：GitHub 请求计数不变、无后台自动请求、无自动补偿）
- [X] T029 [P] [US3] 编写调用方重试测试于 tests/PrReviewSubmit.Tests/Component/RetryFlowTests.cs（修正载荷后的第二次调用成功，且恰好产生一条新 review）

### Implementation for User Story 3

- [X] T030 [P] [US3] 在 README.md 文档化重试/去重语义（工具永不自动重试、不保证幂等；失败后调用方核验 PR 再决定重试；RATE_LIMITED/NETWORK_ERROR 在 details.retryable=true 提示可重试）
- [X] T031 [P] [US3] 核验每次调用独立无状态于 src/PrReviewSubmit/GitHub/GitHubReviewClient.cs（每次调用重新读取私钥、生成新令牌、不缓存、不共享可变状态；替换为有效新私钥后下一次调用即生效、无需重启；进程内并发调用互不影响）

**Checkpoint**: 三个用户故事全部可独立验证

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: 跨用户故事的收尾与质量加固

- [ ] T032 [P] 添加使用文档于 README.md（MCP 客户端注册 JSON、环境变量说明、private-key 路径约定、quickstart 链接）
- [ ] T033 [P] 添加工具契约一致性测试于 tests/PrReviewSubmit.Tests/Component/ToolContractConsistencyTests.cs（断言 MCP 暴露的工具集合恰为 {submit_pr_review}（FR-001 唯一工具），且参数名/类型/必填与 contracts/submit-review.schema.json 一致，CHK142）
- [ ] T034 [P] 添加可选冒烟测试脚手架于 tests/PrReviewSubmit.Tests/Smoke/SmokeTests.cs（未设置 GITHUB_APP_ID / GITHUB_APP_INSTALLATION_ID / GITHUB_PRIVATE_KEY_PATH 时自动跳过；覆盖 quickstart 场景 A/B/C/D）
- [ ] T035 [P] 添加零写入/零输出测试于 tests/PrReviewSubmit.Tests/Component/ZeroWriteTests.cs（SC-007：调用前后工作目录与临时目录无新增文件；运行期除 MCP 协议外无 stdout/stderr 输出）
- [ ] T036 按 quickstart.md 执行端到端验证（dotnet build PrReviewSubmit.sln；dotnet test tests/PrReviewSubmit.Tests；有凭据时手工冒烟场景 A~F；核验 SC-007/SC-008）
- [ ] T037 更新 README.md 项目状态（规划阶段 → 已实现），补充 tasks.md/quickstart.md 链接
- [ ] T038 执行 dotnet format 并清理警告与无用代码于 src/PrReviewSubmit 与 tests/PrReviewSubmit.Tests，确保解决方案无编译警告

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 无依赖，可立即开始
- **Foundational (Phase 2)**: 依赖 Setup 完成 —— **阻塞所有用户故事**
- **User Stories (Phase 3+)**: 全部依赖 Foundational 完成
- **Polish (Final Phase)**: 依赖全部目标用户故事完成

### User Story Dependencies

- **User Story 1 (P1)**: 可在 Foundational 后开始，无其他故事依赖（MVP）
- **User Story 2 (P1)**: 可在 Foundational 后开始；与 US1 共享 src/PrReviewSubmit/MCP/ReviewSubmitTool.cs 与 src/PrReviewSubmit/Program.cs，单线实施时按 US1 → US2 顺序；并行分队时需明确共享文件归属（如 US1 负责工具创建与成功编排、US2 负责失败编排与启动校验，完成后合入）
- **User Story 3 (P2)**: 可在 Foundational 后开始，依赖 GitHubReviewClient 已具备无重试语义，可与其他故事并行

### Within Each User Story

- 新行为测试 MUST 先写并确认失败，再实现；Phase 2 已实现组件（T011/T012/T013）的测试任务为覆盖补齐，不强制先失败
- 模型 → 服务 → 端点/编排 → 集成
- 故事完成前必须通过该故事的 Independent Test

### Parallel Opportunities

- Phase 1：T002/T003/T004 可并行（T001 先建解决方案）
- Phase 2：T005~T013、T015 全部可并行；T014 依赖 T010/T011/T012
- Phase 3：T016/T017 测试并行 → T018/T019 并行 → T020
- Phase 4：T021~T024 测试并行 → T025/T026/T027 并行
- Phase 5：T028/T029 测试并行 → T030/T031 并行
- Phase 6：T032~T035、T038 按文件归属并行（README 由 T032 先行、T037 最后）

---

## Parallel Example: User Story 1

```bash
# 同时启动 User Story 1 的全部测试（预期失败）：
Task: "Write failing component success test in tests/PrReviewSubmit.Tests/Component/McpToolInvocationTests.cs"
Task: "Write failing payload validation unit tests in tests/PrReviewSubmit.Tests/Unit/ValidatorTests.cs"

# 测试失败确认后，同时启动两个独立文件：
Task: "Create submit_pr_review tool in src/PrReviewSubmit/MCP/ReviewSubmitTool.cs"
Task: "Implement MCP stdio host in src/PrReviewSubmit/Program.cs"
```

---

## Implementation Strategy

### MVP First（仅 User Story 1）

1. 完成 Phase 1: Setup（含 global.json 与 .gitignore 核验）
2. 完成 Phase 2: Foundational（含 .github/workflows/ci.yml —— 从第一个用户故事 PR 起，每次 PR 自动 build + test + 私钥排除检查）
3. 完成 Phase 3: User Story 1
4. **STOP and VALIDATE**: 独立验证 User Story 1（stub 测试 + 可选冒烟 A/F）
5. 若具备真实 GitHub App 凭据，完成一次端到端冒烟后即可交付 MVP

### Incremental Delivery

1. Setup + Foundational → 基础设施就绪（CI 已生效）
2. User Story 1 → 独立测试 → MVP
3. User Story 2 → 独立测试（全失败路径）
4. User Story 3 → 独立测试（无重试/调用方重试）
5. Polish → 文档、契约一致性、零写入断言、quickstart 全量验证

### Parallel Team Strategy

1. 团队共同完成 Setup + Foundational
2. Foundational 完成后：
   - 开发者 A: User Story 1（工具 + 宿主 + 成功编排）
   - 开发者 B: User Story 2 测试先行，随后接管失败编排与启动校验（共享文件协调合入）
   - 开发者 C: User Story 3（无重试测试 + 文档/独立性核验）
3. 各故事独立完成并集成验证

---

## Notes

- [P] 任务 = 不同文件、无未完成依赖
- [Story] 标签将任务映射到具体用户故事，保证可追踪
- 每个用户故事必须可独立完成、独立测试
- 测试必须先失败再实现
- 每个任务或逻辑分组完成后提交一次；提交信息遵循 Conventional Commits
- 任一切点处可停下独立验证故事
- CI 仅做仓库级质量门禁（build/test/密钥排除），不含部署；工具运行期仍不执行任何 CI/CD 操作
- 避免：模糊任务、同文件冲突、破坏故事独立性的跨故事依赖

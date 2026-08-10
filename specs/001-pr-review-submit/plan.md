# Implementation Plan: PR Review Submit（PR 审查结果上传）

**Branch**: `001-pr-review-submit` | **Date**: 2026-08-09 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-pr-review-submit/spec.md`

## Summary

本功能交付一个特化的 MCP Tool：AI Agent 完成 PR 代码审查后，调用本工具，将整体结论与逐文件行内评论，以 GitHub App 的 bot 身份、通过 GitHub REST "Create a pull request review"（`event=COMMENT`，即 submit review）一次性上传到显式指定的 `owner/repo/pullNumber`。工具不生成、不修改审查内容，不持久化数据，不执行上传以外的任何 GitHub 操作；成功返回可程序化解析的结构化成功结果，失败返回明确、可操作的结构化错误。

技术方案：基于官方 MCP C# SDK（`ModelContextProtocol` 2.x，stdio 传输）实现单一工具 `submit_pr_review`；GitHub 交互直接使用 REST API（`System.Net.Http`）：先以 App 私钥生成 JWT 换取按目标仓库最小化作用域的短期安装令牌，提交前读取一次目标 PR 状态（PR 不存在/已合并/已关闭直接失败，FR-004），再以单次 POST 提交 review（整体原子：任一评论越界即整体 422 失败，不产生部分 review；不读取文件改动列表，FR-014）。详见 [research.md](research.md)、[data-model.md](data-model.md)、[contracts/](contracts/README.md)。

## Technical Context

**Language/Version**: C# / .NET 10（LTS，官方支持至 2028-11）

**Primary Dependencies**:
- `ModelContextProtocol` 2.1.0（官方 MCP C# SDK；stdio 服务器 + 工具注册）
- `System.IdentityModel.Tokens.Jwt`（GitHub App JWT 签名，RS256）
- `System.Net.Http.Json` / `System.Text.Json`（内置；GitHub REST 调用）
- 测试：`xUnit` v3（含 Microsoft.Testing.Platform 原生运行器）、`NSubstitute`

**Storage**: N/A —— 无数据库、无文件缓存、无任何数据持久化或日志写入（FR-011/SC-007，2026-08-09 spec 修订为"含错误日志"）；仅读取凭据文件（`private-key/`，已被 `.gitignore` 排除）

**Testing**: xUnit v3 + Microsoft.Testing.Platform；分层为 单元（载荷校验、错误映射、JWT 生成）、组件（MCP in-memory 调用工具 + GitHub 客户端替身/Stub 响应，含 PR 状态 open/merged/closed）、冒烟（真实 GitHub App + 测试仓库，手工可选）；另含启动配置校验测试（FR-015：缺配置/非法私钥 → 非零退出码 + stderr 错误）与 BaseUrl/TLS 固定断言（FR-016：HttpClient.BaseAddress 恒为 api.github.com、证书校验开启）（CHK113）；CI 为仓库级 GitHub Actions（`.github/workflows/ci.yml`）：restore/build/test + private-key 不入库断言（CHK156），不含部署步骤，冒烟测试默认跳过（本工具为本地 stdio server，无部署型 CI/CD 需求）

**Target Platform**: 本地 stdio MCP server（控制台应用，`dotnet run` 由 MCP 客户端拉起）；HTTP（Streamable）作为可选部署形态记录但不在 MVP 实现。进程生命周期由客户端管理（拉起→会话→退出即结束，无守护/自重启，CHK160）；启动校验为本地毫秒级操作，远小于典型启动超时（CHK158）；配置错误导致启动失败时宿主若自动重启会反复失败，应检查 stderr 修复配置或调整宿主策略（CHK161）

**Project Type**: MCP Tool（MCP server，向调用方 Agent 暴露唯一工具）

**Performance Goals**: 单次调用从收到请求到返回明确结果 < 30s（SC-008，以 GitHub 可用且网络正常为前提）；三个串行请求各设 HttpClient 超时（建议 10s），总预算 ≤30s（CHK079）

**Constraints**:
- 仅 `COMMENT` 事件，不接受调用方指定事件（FR-013）
- 单次调用仅发送一次 create-review 请求；整体成功或整体失败，无部分成功（FR-009）
- 无隐式重试、无自动补偿（FR-010）；超时与限流映射为明确失败
- 凭据仅来自 `private-key/`；令牌短期有效（GitHub 固定 1h），每次调用按需生成（FR-012）
- 敏感配置（App ID、安装 ID、私钥路径）通过环境变量注入，不硬编码
- 载荷校验在任何 GitHub 请求之前完成（FR-003）；提交前必须读取一次目标 PR 状态，PR 不存在/已合并/已关闭直接失败（FR-004）；不读取文件改动列表（FR-014）
- 不写任何日志、不落盘；诊断信息仅通过调用结果返回（FR-011/SC-007）；运行期除 MCP 协议与调用结果外不产生其他 stdout/stderr 输出（FR-011，CHK162；启动期配置错误的 stderr 输出不属此列，FR-015）
- 错误/诊断信息不含令牌、私钥等敏感内容（FR-011）；提交请求发出后超时按 NETWORK_ERROR 返回并提示 review 可能已创建（不自动重试，FR-010）
- 启动时校验必需配置——App ID/安装 ID 须为正整数、私钥路径须存在且可读、私钥须可解析为 RSA 密钥（CHK174），缺失/非法以非零退出码 + stderr 明确错误立即退出（FR-015）；启动校验仅本地进行，不发网络请求、不做令牌交换预检（FR-015）；配置启动时固化，运行期环境变量变更不生效、需重启（CHK126）；私钥启动时仅校验存在与可解析性、每次调用重新读取并生成短期令牌、不缓存（解析失败 → CREDENTIALS_INVALID，CHK091/CHK102）；私钥轮换无需重启——每次调用重新读取，替换为有效新私钥后下一次调用即生效、旧私钥随即失效；删除或替换为不可解析内容 → CREDENTIALS_INVALID（CHK137，2026-08-10 澄清）
- API 地址固定生产 `https://api.github.com`，不可配置，且必须启用 TLS 证书校验、不得禁用（FR-016）；接口替身仅用于测试（注入 IGitHubReviewClient），不作为产品配置暴露（CHK110）
- 审查内容 UTF-8 原样提交，不做编码转换或规范化（FR-003/FR-005）
- v1 单一安装：installation_id 来自配置，目标仓库须在该安装授权范围内；多安装/多租户范围外（spec Assumptions）
- 同一进程内并发调用相互独立：每次调用各自认证与提交，不共享可变状态、不复用令牌（spec Assumptions，CHK118）；并发共享安装令牌的 GitHub 核心限流配额，高并发由调用方串行化或降低并发（CHK157）
- 目标账号类型（用户级/组织级）不区分，统一按该 GitHub App 安装的授权范围判定（spec Assumptions，CHK152）

**Scale/Scope**: MVP 即完整交付：单功能、单工具、单次上传；无 UI、无多安装编排、无企业版扩展

## Constitution Check

*GATE: 通过。Phase 0 前与 Phase 1 设计后复查结论一致。*

| 原则 | 结论 | 依据 |
|------|------|------|
| 一、单一职责 | PASS | 仅暴露 `submit_pr_review` 一个工具；不生成内容、不持久化；除 FR-001 明示的认证请求与提交前 PR 状态读取外不做其他操作 |
| 二、显式目标与最小作用域 | PASS | 每次调用显式指定 owner/repo/pullNumber；安装令牌通过 `repositories` 参数限定到目标仓库；无推断/默认/通配目标 |
| 三、Bot 身份合规 | PASS | 全程使用 App 安装令牌认证，由 GitHub 天然标记 bot 身份；不伪造、不隐藏 |
| 四、凭据安全 | PASS | 私钥位于 gitignore 的 `private-key/`；JWT 短时（≤10min）、安装令牌 1h 按次生成；环境变量注入；无硬编码 |
| 五、失败透明 | PASS | 所有失败路径映射为结构化错误码+可操作信息；无静默吞错、无半成功状态 |

**技术约束符合性**（宪法"技术约束"章节）：C# / .NET LTS → .NET 10 LTS（PASS）；MCP 服务端基于官方 MCP C# SDK（mcpdotnet）→ `ModelContextProtocol` 2.1.0（PASS）；GitHub 交互基于 Octokit / GitHub REST API → 采用 "GitHub REST API" 分支（PASS，Octokit 因 `DraftPullRequestReviewComment` 不支持 `line`/`side` 被否决，理由与出处见 [research.md](research.md) §5，宪法原文以斜杠并列两者，任一满足即合规）；配置集中定义且敏感配置从代码库外注入 → `GitHubAppOptions` 集中绑定 + 环境变量注入（PASS）；无数据库/消息队列等持久化服务、保持无状态 → 完全满足（PASS）。

**已识别的张力与裁定**：spec（FR-001/FR-004/FR-014，2026-08-09 修订）现已明确允许的 GitHub 操作集合：认证必要请求（安装定位、安装令牌交换）+ 提交前目标 PR 状态读取；禁止读取文件改动列表（FR-014）。本计划认证仅发一次 `POST /app/installations/{id}/access_tokens` 令牌交换（不做安装定位查询，安装 ID 由环境变量配置），状态读取仅消费 `state`/`merged` 字段；与 spec 一致，留痕供评审确认。

**平台行为张力与裁定**：GitHub API 当前对**已合并** PR 仍允许成功创建 review（Web UI 禁止、API 未拦截，见社区讨论 #189039 / cli#5038，2026-03 仍在讨论中）。spec 已裁定（2026-08-09 修订）：提交前读取目标 PR 状态，已合并/已关闭直接失败（FR-004）——"已合并/已关闭必须失败"由此在需求层落实。实现按此执行；状态读取与提交之间存在 TOCTOU 竞态窗口，竞态下若平台仍接受提交（已合并场景），工具按平台结果如实返回，残余风险记录于 [research.md](research.md) §10。

**违规与简化**：无宪法违规；`Complexity Tracking` 无需填写。

*spec 状态门禁（CHK146）*：spec 当前为 Draft；转为正式/合入需满足——全部规划复核清单（Round 1~8，CHK001~181）PASS、宪法合规声明（本文）、评审通过。

*编号与收敛治理（CHK170/CHK171）*：FR/SC 编号采用追加制（新增需求追加新编号如 FR-017，不重排既有编号），避免 plan/contracts 断链；检查收敛标准——连续一轮全部 PASS 且无新增检查点即视为收敛，转入 `$speckit-tasks` 拆分；后续 spec 修订引入新需求时，仅重启受影响范围检查。

## Project Structure

### Documentation (this feature)

```text
specs/001-pr-review-submit/
├── plan.md              # 本文件（$speckit-plan 输出）
├── research.md          # Phase 0 输出（技术选型与依据）
├── data-model.md        # Phase 1 输出（领域模型与校验规则）
├── quickstart.md        # Phase 1 输出（端到端验证指南）
├── checklists/requirements.md  # 规划复核清单（CHK001~181，八轮 $speckit-checklist 输出）
├── contracts/           # Phase 1 输出（MCP 工具契约 + GitHub REST 契约）
│   ├── README.md
│   ├── tool-contract.md
│   ├── submit-review.schema.json
│   └── github-rest.md
└── tasks.md             # Phase 2 输出（$speckit-tasks 生成，本命令不创建）
```

### Source Code (repository root)

```text
PrReviewSubmit.sln
global.json                            # 固定 .NET 10 SDK（10.0.x，本地/CI 一致）
.github/workflows/ci.yml               # 仓库级 CI：build + test + private-key 不入库断言（CHK156），不含部署

src/PrReviewSubmit/                     # 控制台 MCP server（stdio）
├── Program.cs                          # 启动先校验必需配置（FR-015）失败即退出；再构建 Host：AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()
├── Configuration/
│   └── GitHubAppOptions.cs             # App ID / 安装 ID / 私钥路径（环境变量绑定）；API 地址固定 api.github.com（FR-016）
├── MCP/
│   └── ReviewSubmitTool.cs             # [McpServerToolType] 唯一工具 submit_pr_review
├── Domain/
│   ├── ReviewSubmitRequest.cs          # 工具入参（owner/repo/pullNumber/body/comments）
│   ├── ReviewSubmitResult.cs           # 结构化结果（success | error）
│   └── ReviewSubmitErrorCode.cs        # 失败原因枚举
├── Validation/
│   └── ReviewPayloadValidator.cs       # FR-003 本地校验（发起 GitHub 请求前）
├── GitHub/
│   ├── IGitHubReviewClient.cs          # 薄接口：GetInstallationTokenAsync / GetPullRequestStateAsync / CreateReviewAsync（可替身测试）
│   ├── GitHubReviewClient.cs           # HttpClient 实现（JWT→令牌→PR 状态读取→submit review）
│   ├── GitHubAppAuthClient.cs          # PEM→JWT→POST /app/installations/{id}/access_tokens
│   └── GitHubErrorMapper.cs            # 401/403/404/422/429/网络 → 错误码
└── Json/
    └── ToolJsonContext.cs              # JsonSerializerContext（工具参数/结果序列化）

tests/PrReviewSubmit.Tests/
├── Unit/                               # ValidatorTests / ErrorMapperTests / AppAuthClientTests
├── Component/                          # McpToolInvocationTests（in-memory）、ReviewSubmitFlowTests（替身，编排在 ReviewSubmitTool.cs）
└── Smoke/                              # 可选：真实 GitHub App + 测试仓库的端到端冒烟
```

**Structure Decision**: 采用单解决方案双项目（`src/` + `tests/`）的最小结构。工具只有一条业务链路（校验 → 认证 → 单次提交 → 结果），无独立领域层/仓储层需求；双项目即可保证生产代码与测试的清晰边界，避免过度分层。

## Complexity Tracking

> 无宪法违规，无需记录复杂度豁免。

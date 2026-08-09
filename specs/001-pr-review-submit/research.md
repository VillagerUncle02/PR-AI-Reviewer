# Research: PR Review Submit（PR 审查结果上传）

**Created**: 2026-08-09 | **Feature**: 001-pr-review-submit | **Purpose**: Phase 0 调研，消解 Technical Context 中的技术选型问题

**方法**: 官方文档（GitHub REST Docs、MCP C# SDK docs、NuGet、.NET 支持页）+ 社区 issue/讨论交叉验证；关键来源以 URL 标注。

## 1. .NET 版本

- **Decision**: .NET 10（LTS）为目标运行时，官方支持至 2028-11。
- **Rationale**: 截至 2026-08，.NET 10 是唯一"发布未满一年"的 LTS（2025-11 发布，支持至 2028-11-14）；.NET 8 同为 LTS 但支持将于 2026-11-10 结束，新项目不应落在行将 EOL 的版本上。
- **Alternatives considered**: .NET 8 LTS（否决：约 3 个月后 EOL）；.NET 9 STS（否决：非 LTS，宪法要求 LTS）。
- Source: https://learn.microsoft.com/dotnet/core/releases-and-support

## 2. MCP C# SDK

- **Decision**: 使用官方 `ModelContextProtocol` NuGet 包（2.1.0，2026-08-05 发布的稳定版），stdio 传输，控制台宿主；工具以 `[McpServerToolType]` + `[McpServerTool]` 特性注册，`WithToolsFromAssembly()` 自动发现。
- **Rationale**: 宪法要求"官方 MCP C# SDK（mcpdotnet）"；2.x 为现行稳定大版本且兼容 net10.0；stdio 是本地 Agent 调用 MCP 工具的标准形态，无需网络端口与额外托管进程。
- **Alternatives considered**: 1.4.x（上一协议修订版，仍可用但非最新，否决）；`ModelContextProtocol.AspNetCore` + Streamable HTTP（适合远程部署，MVP 不引入，保留为扩展路径）；自实现 MCP 协议（否决，违背宪法）。
- Source: https://www.nuget.org/packages/ModelContextProtocol ; https://csharp.sdk.modelcontextprotocol.io/v1/concepts/getting-started.html

## 3. GitHub App 认证

- **Decision**: 两步认证：(1) 用 App 私钥（PEM）生成 RS256 JWT（claims: `iss`=App ID、`iat`=now-60s、`exp`≤now+10min）；(2) `POST /app/installations/{installation_id}/access_tokens`，请求体以 `repositories` 限定目标仓库，换取 1 小时安装令牌；随后以该令牌作为 `Authorization: Bearer` 调用业务 API。
- **Rationale**: GitHub 官方认证流程；`repositories` 限定使令牌仅能作用于目标仓库，落实"最小作用域"；每次调用按需生成、令牌 1h 自动过期，满足 FR-012"短期令牌及时失效"。安装 ID 来自配置（环境变量）；spec FR-001（2026-08-09 修订）虽允许认证必要请求，本计划仍不做安装查询，保持最小请求面（FR-001）。
- **Alternatives considered**: Octokit SDK 自动管理令牌（引入 Octokit 且仍需安装 ID，令牌作用域不可控，否决）；`GitHubJwt` 第三方库（少写样板但引入额外依赖，否决——.NET 10 自带 `RSA.ImportFromPem` + Jwt 库即可）。
- **Bot 标识**:以安装令牌调用的 REST 请求由 GitHub 记录为该 App 的 bot 用户（如 `{app-slug}[bot]`），create review 响应的 `user` 字段由平台填充；工具无需也不得修改该标识（FR-006/FR-007）。Source: https://docs.github.com/en/apps/creating-github-apps/about-authentication
- **范围与启动**：v1 单一安装（installation_id 来自配置，目标仓库须在该安装授权范围内；多安装/多租户范围外）；必需配置启动时校验、缺失/非法以非零退出码 + stderr 即退出（FR-015）；API 地址固定生产 api.github.com 且必须启用 TLS 证书校验、不得禁用（FR-016）；接口替身仅用于测试（注入 IGitHubReviewClient），不作为产品配置暴露（CHK110）；本机时钟偏差由 JWT `iat` 60s 余量缓解，偏差过大导致 401 → CREDENTIALS_INVALID（CHK105）。
- Source: https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/generating-an-installation-access-token-for-a-github-app

## 4. 提交 review 的接口形态

- **Decision**: 使用 REST `POST /repos/{owner}/{repo}/pulls/{pull_number}/reviews`，请求体 `{ event: "COMMENT", body: <整体结论>, comments: [{ path, line, side, body }] }`；`line`+`side` 是现行定位字段（`position` 已弃用）。
- **Rationale**: 该端点一次调用即创建一个"已提交 review"（含整体结论与全部行内评论），正是 spec 要求的产物；`line`/`side` 与调用方输入的"行位置（含代码侧）"一一对应，无需换算 diff position。
- **Alternatives considered**: 逐条 `POST /pulls/{n}/comments` 后再提交 review（拆成多个请求、产生部分状态，违背 FR-005/FR-009，否决）；GraphQL `addPullRequestReview`（能力等价，但 REST 更贴近 spec 原文"GitHub REST API"，否决）；Octokit.net（见下节）。
- Source: https://docs.github.com/en/rest/pulls/reviews#create-a-review-for-a-pull-request

## 5. Octokit.net vs 直接 REST

- **Decision**: 不引入 Octokit.net，GitHub 交互全部走 REST + `HttpClient`。
- **Rationale**: Octokit.net 14.0.0 的 `DraftPullRequestReviewComment` 仅支持 `Position`（diff 内位置），不支持 `line`/`side`，无法透传调用方输入；其异常模型不如直接读取响应体直观。直接 REST 共三个请求（令牌交换、PR 状态读取、提交 review），依赖面更小，错误体（`message`/`errors`）可完整映射。
- **Alternatives considered**: Octokit 14.0.0（否决：字段能力不足）；Octokit + 自定义 API 请求（否决：绕开 SDK 反而复杂）。
- Source: https://www.nuget.org/packages/Octokit ; https://github.com/octokit/octokit.net/blob/main/Octokit/Models/Request/DraftPullRequestReviewComment.cs

## 6. 原子性与边界

- **Decision**: 单次 create-review 请求即整体事务：任一评论的 path/line 不在 PR file change 范围（diff 外）时，GitHub 返回 422 且不创建任何 review；工具将整次调用判为整体失败并映射明确原因（FR-014/FR-009）。工具不做分块、不做重试。
- **Rationale**: GitHub 在该端点执行请求级校验（社区实证：单条越界评论导致整个 review 请求 422 失败、无部分创建）；评论范围校验以提交结果为准，不读取文件改动列表（FR-014）。已知存在每请求评论数量/单条长度上限与次级限流（422 "spammed"），超限同样整体失败并明确报告。
- **Alternatives considered**: 分批多次提交（否决：产生多条 review，与"整体结论与全部评论一并提交"矛盾）；失败后自动拆批重试（否决：FR-010 禁止隐式重试）。
- Source: https://github.com/actions/github-script/issues/318 ; https://github.com/The-PR-Agent/pr-agent/issues/592

## 7. 错误映射

- **Decision**: 按 HTTP 状态与 PR 状态映射为结构化错误码：401→凭据无效/过期；403→权限不足/App 未安装或撤销；404→目标仓库/PR 不存在或无访问权限（GitHub 对无权限也回 404 防泄露）；PR 状态读取仅当 `state=open` 且 `merged=false` 才继续，否则→ `PR_NOT_OPEN`；422→载荷被拒（评论越界/必填缺失/超限）；429→限流；网络/超时→网络错误；其他→未预期错误。提交请求已发出但响应超时：返回 `NETWORK_ERROR` 并提示调用方该 review 可能已创建（FR-010 不自动重试、工具不保证幂等，调用方核验后决定是否重试）。
- **Rationale**: spec 要求"明确、可操作的失败原因"且"发起请求前先本地校验"；状态码映射是唯一可靠信号（GitHub 不提供逐条评论级错误码，422 响应体 `message` 提供可读细节）。
- **Alternatives considered**: 直接透传 GitHub 响应体文本（细节更丰富但格式不稳定，作为 `details` 保留而非主码，折中采用）。
- Source: GitHub REST Docs（各端点 HTTP status code 表）

## 8. 测试策略

- **Decision**: xUnit v3 + Microsoft.Testing.Platform（.NET 10 推荐组合）；三层测试：单元（载荷校验、错误映射、JWT/PEM）、组件（MCP in-memory 客户端调用工具 + GitHub 客户端经接口替身/Stub 响应模拟 PR 状态 open/merged/closed 与 200/404/403/422/429）、冒烟（真实 App + 测试仓库，可选、需凭据）。
- **Rationale**: MCP SDK 自带 in-memory client/server（Pipe）测试模式，可端到端验证工具注册与参数序列化；GitHub 交互经自建 `IGitHubReviewClient` 接口隔离，单测无需网络；真实冒烟验证 SC-001~003。
- **Alternatives considered**: 仅真实 API 集成测试（脆弱、需凭据、CI 不可用，否决）；Moq 替换 NSubstitute（等价，任选其一；计划固定 NSubstitute）。
- Source: https://deepwiki.com/modelcontextprotocol/csharp-sdk/8.3-testing-infrastructure ; https://xunit.net/docs/getting-started/v3/microsoft-testing-platform

## 9. 调用结果契约

- **Decision**: 工具返回单一 JSON 文本内容块，`status: "success" | "error"`；success 携带 `reviewId`/`htmlUrl`，error 携带 `code`/`message`（及可选 `httpStatus`/`details`）。失败不抛异常，统一走结构化结果。
- **Rationale**: 调用方是 Agent，需要"程序化解析、无需人工看页面"（SC-009）；统一 JSON 结果让成功/失败共用一条解析路径；错误码枚举让 Agent 可据此决策（修正载荷/检查凭据/稍后重试）。
- **Alternatives considered**: 抛 `McpError`（协议层错误，调用方需走错误通道且难以携带结构化细节，否决）；仅返回人类可读文本（不可程序化解析，否决）。
- Source: MCP C# SDK tool 返回值约定（TextContentBlock）；spec SC-009

## 10. 平台行为记录:已合并 / 已关闭 PR 的提交结果

- **Decision**: spec 于 2026-08-09 修订（FR-004/FR-014 + Clarifications）裁定：提交前必须读取一次目标 PR 状态，PR 不存在、已合并或已关闭时直接返回明确失败，不发起 review 提交。错误码：PR 不存在/无权限 → `TARGET_NOT_FOUND`；PR 存在但已合并/已关闭 → `PR_NOT_OPEN`。
- **Rationale**: GitHub API **当前允许**对已合并 PR 成功创建 review（Web UI 禁止、API 未拦截，`gh pr review` 亦受影响），与 spec 边界场景"已合并/已关闭必须失败"冲突；用户已选择在需求层明确禁止（Clarifications："明确禁止；提交前读取目标 PR 状态"），故实现改为提交前 `GET /pulls/{n}` 状态读取。该读取仅消费 `state`/`merged` 字段，不读取文件改动列表（FR-014）。
- **Alternatives considered**: 不做预检、以提交结果为准（否决：与 FR-008/SC-005 的"已合并/已关闭失败"冲突，且平台可能接受已合并 PR）；提交前读取文件改动列表校验评论范围（否决：FR-014 禁止）；对疑似成功响应事后拒绝（否决：review 已创建，无法撤回且不可靠）。
- **竞态说明**：状态读取与提交之间存在 TOCTOU 窗口，PR 可能在此期间被关闭/合并；若平台在竞态下仍接受提交（已合并场景），工具按平台结果如实返回。spec Clarifications（2026-08-09）已确认该处理方式（"按平台结果如实返回；竞态下平台接受即成功"），该残余风险已记录于需求/假设并接受；竞态下平台接受并成功创建的 review 不视为误提交（SC-005）。
- Source: https://github.com/orgs/community/discussions/189039 ; https://github.com/cli/cli/issues/5038 ; https://docs.github.com/en/rest/pulls/reviews#create-a-review-for-a-pull-request

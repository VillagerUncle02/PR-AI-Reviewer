# Quickstart: PR Review Submit 端到端验证指南

**Created**: 2026-08-09 | **Feature**: 001-pr-review-submit | **关联**: [data-model.md](data-model.md)、[contracts/tool-contract.md](contracts/tool-contract.md)、[contracts/github-rest.md](contracts/github-rest.md)

本指南用于验证功能端到端可用（对应 User Story 1/2 与 Success Criteria），不包含实现代码；实现细节见 `tasks.md` 与实现阶段。

## 前置条件

- .NET 10 SDK（`dotnet --version` ≥ 10.0.x）
- 已注册的 GitHub App（权限：Pull requests Read & Write——提交 review 需 Write、状态读取需 Read；已安装到测试仓库）
- 无 API 地址配置项：FR-016 固定生产 api.github.com 且不可配置（验收由自动化测试断言 BaseAddress 恒为 api.github.com、TLS 校验开启，CHK147）
- 私钥位于仓库根 `private-key/` 目录（已被 .gitignore 排除）；实际文件名以本地为准，通过 `GITHUB_PRIVATE_KEY_PATH` 指定
- 私钥文件建议仅当前用户可读（Windows ACL 或 Unix chmod 600），落实 FR-012"本地受保护位置"
- 环境变量：
  - `GITHUB_APP_ID`（App ID）
  - `GITHUB_APP_INSTALLATION_ID`（测试仓库对应安装 ID）
  - `GITHUB_PRIVATE_KEY_PATH`（可选，默认 `private-key/github-app.pem`；若本地私钥文件名不同，用该变量显式指定）
- 测试仓库上一个**打开状态**的 PR，其 file change 包含已知文件路径与行号

## 本地运行

```bash
dotnet build PrReviewSubmit.sln
dotnet run --project src/PrReviewSubmit
```

运行后服务以 MCP stdio 模式等待调用。启动时校验必需配置（App ID/安装 ID 须为正整数、私钥路径须存在且可读、私钥须可解析为 RSA 密钥，CHK174）：缺失/非法以非零退出码退出并在 stderr 给出明确错误（FR-015；启动报错输出属进程输出，不属于持久化日志，FR-011 仅禁落盘；启动校验为本地毫秒级操作，远小于 MCP 客户端典型启动超时；若宿主对启动失败自动重启，配置错误会导致反复拉起——检查 stderr 修复配置，CHK158/CHK161）。按 FR-011/SC-007，工具不写任何日志、不落盘，诊断信息仅通过调用结果返回（stdio 通道仅承载 MCP 协议消息；运行期无其他 stdout/stderr 输出，CHK162）。将可执行文件注册到 MCP 客户端（如 Claude Desktop / Codex）作为 stdio server，客户端配置示例：

```json
{ "mcpServers": { "pr-review-submit": { "command": "dotnet", "args": ["run", "--project", "src/PrReviewSubmit"] } } }
```

## 自动化验证

```bash
dotnet test tests/PrReviewSubmit.Tests
```

预期：全部通过。覆盖：载荷校验（FR-003）、提交前 PR 状态读取（FR-004）、错误映射（FR-008）、单请求原子性模拟（FR-009/FR-014）、无重试（FR-010）、超时歧义提示与错误脱敏（FR-011）、结果契约（SC-009）、MCP 工具注册与调用序列化、状态读取与提交两阶段的 429/网络错误映射（CHK054）、启动期配置校验与启动失败验收（FR-015/US2-S4）、BaseUrl/TLS 固定断言（FR-016）、details 截断与 Retry-After 透传（CHK080/CHK081）、private-key 不入库断言（.gitignore/CI 检查，CHK156）。

## 手工冒烟场景（需真实 GitHub App）

### 场景 A：成功上传（US1 / SC-001~003、SC-007~009）

1. 在 MCP 客户端调用 `submit_pr_review`：
   - `owner`/`repo` = 测试仓库；`pullNumber` = 打开 PR 编号；
   - `body` = 非空整体结论；`comments` 至少 1 条，`path`/`line`/`side`/`body` 均来自该 PR 的真实改动行（如新增行，side=RIGHT）。
2. 预期：
   - 工具返回 `status: success`，含 `reviewId` 与 `htmlUrl`；
   - GitHub PR 页面出现一条 bot 身份 review，整体结论与每条评论内容与输入完全一致，评论落在指定文件与行；
   - 无任何额外 GitHub 产物（无 issue、无重复 review）。
3. 客观核验（不依赖人工看页面）：由测试脚本以只读方式调用 GitHub REST（脚本侧验证，非工具行为）——先用 `GET /repos/{owner}/{repo}/pulls/{pull_number}/reviews/{review_id}` 取 review 级信息（`body`、`user.type=Bot`，SC-003/CHK086），再用 `GET /repos/{owner}/{repo}/pulls/{pull_number}/comments?review_id={review_id}` 取该 review 的逐条评论，逐条比对 `path`/`line`/`side`/`body` 与输入一致（语义一致，SC-001/SC-002，CHK087/CHK181）；结果 JSON 可程序化解析（SC-009，CHK088）。以上均为脚本断言、非人工核对（CHK116）。

### 场景 B：评论越界 → 整体失败（US2 / FR-014、FR-009）

1. 调用同 A，但将某条评论的 `line` 改为 PR diff 范围外的行（或 `path` 改为不存在的文件）。
2. 预期：返回 `status: error`、`code: REVIEW_UNPROCESSABLE`（含 GitHub 422 细节）；GitHub 上无任何新 review 产生；不会部分成功。

### 场景 C：载荷无效 → 本地拒绝（US2 / FR-003）

1. 调用时 `body` 为纯空白或空字符串，或某条评论缺 `path`/`line`/`body`、评论内容为纯空白。
2. 预期：返回 `status: error`、`code: INVALID_PAYLOAD`；工具未向 GitHub 发起任何请求（可在 App 侧验证无请求记录）。

### 场景 D：目标不可提交（US2 / FR-004、FR-008）

1. 对不存在的 PR 编号调用 → 预期：`status: error`、`code: TARGET_NOT_FOUND`；无新 review。
2. 对已关闭或已合并的 PR 调用 → 预期：`status: error`、`code: PR_NOT_OPEN`；工具在提交前状态读取阶段即失败，未发起 review 提交。
3. 0 误提交核验：上述调用前后查询该 PR 的 reviews 列表，数量与内容不变（SC-005）。

> 补充：工具提交前读取一次目标 PR 状态（FR-004），已合并/已关闭直接失败；不读取文件改动列表（FR-014）。状态读取与提交之间存在竞态窗口，若平台在竞态下仍接受提交（已合并场景），按平台结果如实返回（见 [research.md](research.md) §10）。
> 限流（429）与网络错误（含超时歧义）的阶段归属由自动化测试覆盖（状态读取与提交两阶段分别模拟），手工冒烟不强制。

### 场景 E：MCP 层参数/传输异常（CHK005）

1. 调用时参数为畸形 JSON、缺 `required` 字段或字段类型错误。
2. 预期：畸形 JSON/缺 required/类型错误优先由 MCP 框架参数校验在协议层拒绝（调用方收到框架层错误）；若框架放行，工具本地校验兜底返回 `status: error`、`code: INVALID_PAYLOAD`。两种情况均未向 GitHub 发起任何请求；若 MCP 传输层本身中断（连接/反序列化失败），由 MCP 框架按协议报错，不属于工具 JSON 结果契约范围（CHK168/CHK179）。

### 场景 F：仅整体结论、空评论列表（CHK003）

1. 调用 `submit_pr_review`，`body` 为非空整体结论，`comments: []`。
2. 预期：返回 `status: success`，含 `reviewId`/`htmlUrl`；PR 上出现一条仅含整体结论、无逐文件评论的 bot review。

## 验收对照

- US1 → 场景 A/F；US2 → 场景 B/C/D；US3 → 场景 D/E + 自动化测试（工具不重试、调用方再次发起）
- SC-001/002/003 → 场景 A（步骤 3 逐条比对 path/line/side/body 语义一致 + user.type=Bot 断言）
- SC-004/005/006 → 场景 B/C/D/E（0 误提交核验；竞态下平台接受并成功创建的 review 不视为误提交，SC-005）
- SC-007 → 冒烟前后断言运行目录/临时目录无新增文件（含日志、缓存），进程退出无残留（仅凭据文件读取）
- SC-008 → 场景 A 自工具收到调用至返回结果计时 < 30s（前提：GitHub 可用、网络正常；不含 MCP 客户端启动与启动校验（FR-015）时间，CHK127；三请求各设超时，总预算 ≤30s）
- SC-009 → 场景 A~F 返回均可程序化解析（错误响应断言含 code/message，httpStatus/details 出现时类型合法，CHK088）

## 错误码验证映射（CHK117）

| 错误码 | 验证方式 |
|--------|----------|
| INVALID_PAYLOAD | 场景 C/E（本地校验拒绝） |
| CREDENTIALS_INVALID | 自动化测试（私钥运行期失效、JWT/令牌 401）；启动期配置失败属 FR-015 启动失败，不产生本码 |
| APP_NOT_INSTALLED | 自动化测试（认证/状态读取/提交阶段 403、安装不存在 404） |
| TARGET_NOT_FOUND | 场景 D（不存在 PR/仓库，404） |
| PR_NOT_OPEN | 场景 D（已关闭/已合并 PR） |
| REVIEW_UNPROCESSABLE | 场景 B（评论越界 422） |
| RATE_LIMITED | 自动化测试（429 + Retry-After 透传） |
| NETWORK_ERROR | 自动化测试（网络失败/超时、5xx，含提交后超时歧义）；调用方引导：收到后先查 PR reviews 核验是否已创建，已创建则去重、未创建再重试（CHK151） |
| UNEXPECTED_ERROR | 自动化测试（令牌交换 201 缺字段、响应非 JSON/JSON 非对象/缺字段/类型异常，CHK133/CHK148/CHK155） |

## FR 验收映射（CHK149）

| FR | 验收/SC |
|----|---------|
| FR-001 | 自动化测试（唯一工具注册）+ 场景 A（无额外 GitHub 产物） |
| FR-002 | 场景 A（显式指定 owner/repo/pullNumber） |
| FR-003 | 场景 C/F（本地校验拒绝） |
| FR-004 | 场景 D（提交前状态读取） |
| FR-005 | 场景 A（内容原样透传） |
| FR-006 | 场景 A + SC-003（bot 身份） |
| FR-007 | 场景 A（结构化成功结果） |
| FR-008 | 场景 B/C/D + 错误码验证映射表 |
| FR-009 | 场景 B（整体失败无部分成功） |
| FR-010 | US3 + 自动化测试（无隐式重试断言） |
| FR-011 | SC-007（零写入断言）+ 运行期零 stdout/stderr 输出检查 |
| FR-012 | 前置条件 + SC-007（私钥不入库、短期令牌） |
| FR-013 | 场景 A（event=COMMENT 固定） |
| FR-014 | 场景 B（评论越界整体失败，不读文件列表） |
| FR-015 | US2-S4 + 自动化测试（启动校验仅本地、非零退出码） |
| FR-016 | 自动化测试（BaseAddress 恒为 api.github.com、TLS 校验开启，CHK147） |

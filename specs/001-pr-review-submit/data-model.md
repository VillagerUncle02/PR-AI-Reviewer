# Data Model: PR Review Submit

**Created**: 2026-08-09 | **Feature**: 001-pr-review-submit | **关联**: [contracts/tool-contract.md](contracts/tool-contract.md)

> 本工具无持久化（FR-011），以下"数据模型"描述单次调用的领域模型、校验规则与调用状态机，供实现与验证对齐；不存在数据库/存储模型。

## 实体

### 1. 上传请求 ReviewSubmitRequest（MCP 工具入参，单次调用全部输入）

| 字段 | 类型 | 必填 | 校验规则 | 来源 |
|------|------|------|----------|------|
| owner | string | 是 | trim 后非空（纯空白视为缺失，FR-002）；显式指定，无默认/推断 | 调用方 |
| repo | string | 是 | trim 后非空（纯空白视为缺失，FR-002）；显式指定 | 调用方 |
| pullNumber | integer | 是 | ≥ 1 | 调用方 |
| body | string | 是 | 去除首尾空白（Unicode 空白字符，不含零宽空格等格式字符）后非空（FR-003，CHK172） | 调用方（整体结论） |
| comments | ReviewComment[] | 是 | 数组（可为空）；每条见下（FR-003） | 调用方（逐条建议） |

### 2. 逐文件评论 ReviewComment

| 字段 | 类型 | 必填 | 校验规则 | GitHub 对应 |
|------|------|------|----------|-------------|
| path | string | 是 | 非空；PR 内相对路径（越界由 GitHub 422 判定，FR-014） | comments[].path |
| line | integer | 是 | ≥ 1；目标文件行号 | comments[].line |
| side | string | 是 | "RIGHT" \| "LEFT"；RIGHT=新文件侧（新增/上下文行，用新文件行号），LEFT=旧文件侧（删除行，用旧文件行号） | comments[].side |
| body | string | 是 | 去除首尾空白后非空（字符集同 FR-003，CHK172） | comments[].body |

> 说明：`position`（diff 内位置）已弃用，契约固定使用 `line` + `side`（见 [research.md](research.md) §4）；`line` 须落在目标 PR 的 **file change 范围**（spec 术语，即该 PR 引入的 diff 中可评论的行位置，GitHub 以 422 判定，FR-014）。工具不换算、不补充任何字段（FR-005）；trim 仅用于有效性判断，提交保持调用方原始内容，UTF-8 原样透传（FR-003/FR-005）。

### 3. 目标 PR TargetPullRequest

- 由 `owner + repo + pullNumber` 唯一定位；须处于打开状态且位于该 App 安装与授权范围内。
- 提交前工具读取一次 PR 状态（FR-004）：PR 不存在/无权限 → `TARGET_NOT_FOUND`；`state≠open`（含已合并 `merged: true`）→ `PR_NOT_OPEN`，直接失败，不发起 review 提交。
- 不读取 PR 文件改动列表做前置校验（FR-014）；评论是否在 file change 范围内以提交结果为准（422 → `REVIEW_UNPROCESSABLE`）。
- 竞态说明：状态读取与提交之间存在 TOCTOU 窗口，若平台在竞态下仍接受提交（已合并场景），工具按平台结果如实返回；详见 [research.md](research.md) §10。

### 4. 已提交 Review SubmittedReview（GitHub 平台产物）

- 属性（来自 GitHub 响应）：`id`、`html_url`、`state=COMMENTED`、`user`（bot 标识）、`body`、`submitted_at`。
- 工具仅透传响应中的 `id` 与 `html_url` 作为成功结果，不缓存、不存储（FR-011）。

### 5. 调用结果 CallResult（返回调用方的结构化结果）

| 状态 | 字段 | 说明 |
|------|------|------|
| success | status, reviewId, htmlUrl | 对应一条带 bot 标识的已提交 review（FR-007） |
| error | status, code, message, httpStatus?, details? | 明确失败原因，无半成功（FR-008/FR-009） |

错误码全集见 [contracts/tool-contract.md](contracts/tool-contract.md#错误码)。

## 状态转换（单次调用）

```text
Received → Validated → Authenticated → Checked → Submitted → Success
    │          │             │            │         │
    └──────────┴─────────────┴────────────┴─────────┴──→ Error（终态）
```

| 状态 | 触发 | 说明 |
|------|------|------|
| Received | 收到工具调用 | 载荷已到达，未做任何外部请求 |
| Validated | 本地校验通过 | FR-003 全部通过；失败 → Error(INVALID_PAYLOAD)，**未发任何 GitHub 请求** |
| Authenticated | 取得安装令牌 | JWT → access_tokens（限定目标仓库）；失败 → Error(CREDENTIALS_INVALID / APP_NOT_INSTALLED / RATE_LIMITED / NETWORK_ERROR) |
| Checked | PR 状态已核验（FR-004） | GET /pulls/{n} 仅读 state/merged；失败 → Error(CREDENTIALS_INVALID / APP_NOT_INSTALLED / TARGET_NOT_FOUND / PR_NOT_OPEN / RATE_LIMITED / NETWORK_ERROR / UNEXPECTED_ERROR)；仅 state=open 且未合并才继续（draft 不单独拒绝，CHK103） |
| Submitted | 单次 POST create review 成功 | 200 → Success（含 reviewId/htmlUrl）；请求已发出但响应超时 → Error(NETWORK_ERROR)，提示 review 可能已创建（不重试）；失败 → Error(CREDENTIALS_INVALID / APP_NOT_INSTALLED / TARGET_NOT_FOUND / REVIEW_UNPROCESSABLE / RATE_LIMITED / NETWORK_ERROR) |
| Error | 上述任一步失败 | 终态；无重试、无自动补偿（FR-010） |

> 状态仅存在于单次调用的执行上下文中，不落盘；进程内无跨调用状态（FR-011）。
> 启动期必需配置校验（FR-015）在调用状态机之外：配置缺失/非法时进程启动即退出并给出明确错误，不进入任何调用。

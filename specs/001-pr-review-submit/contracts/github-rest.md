# Contract: GitHub REST 集成

**版本**: v3（2026-08-09） | **API 版本头**: `X-GitHub-Api-Version: 2022-11-28`（固定发送） | **关联**: [tool-contract.md](tool-contract.md)

> 共三个请求（均带 `Accept: application/vnd.github+json`）：①认证令牌交换；②提交前读取目标 PR 状态（仅消费 `state`/`merged` 字段）；③单次 create review。读取范围仅限 PR 状态（FR-004），不读取文件改动列表（FR-014）。
> BaseUrl 固定为生产 `https://api.github.com`，不支持配置覆盖，且 MUST 启用 TLS 证书校验、不得禁用（FR-016）；`X-GitHub-Api-Version` 固定发送。依赖假设：`state`/`merged`/`user.type`/`errors` 等响应字段语义以 GitHub 返回为准，缺失或类型变化 → `UNEXPECTED_ERROR` 并需契约升级（CHK098/CHK128）。
> 重定向策略：跟随标准 301/302（HttpClient 默认 `AllowAutoRedirect`，最多 3 跳，保留 Authorization 头）；重定向链异常或最终状态非 2xx 按对应状态映射（CHK120）。
> 404 分阶段区分：认证阶段（安装不存在/仓库不在安装范围）→ `APP_NOT_INSTALLED`；状态读取/提交阶段 → `TARGET_NOT_FOUND`（CHK124）。
> 5xx（502/503/504 等）→ `NETWORK_ERROR`（GitHub 服务端错误，调用方可稍后重试；工具不自动重试，FR-010）（CHK134）。

## 1. 获取安装令牌（认证步骤，FR-006/FR-012）

**请求**

```text
POST https://api.github.com/app/installations/{installation_id}/access_tokens
Authorization: Bearer {JWT}

{
  "repositories": ["{repo}"]
}
```

- JWT：RS256；claims `iss`=App ID、`iat`=now-60s、`exp`≤now+10min。
- `installation_id` 来自配置（环境变量），不做安装查询。
- `repositories` 将令牌作用域限定到目标仓库（平台依赖假设：安装未授权的仓库在令牌交换/调用阶段被拒 403/404，CHK159）。
- 最小权限：App 需 `Pull requests Read & Write`（状态读取需 Read、提交 review 需 Write）；v1 单一安装，授权范围外仓库在令牌交换阶段被拒绝（403/404）→ `APP_NOT_INSTALLED`（CHK095）。
- 本机时钟偏差假设：JWT `iat` 预留 60s 余量；若偏差过大导致 401，按 `CREDENTIALS_INVALID` 返回，调用方校准时钟后重试（CHK105）。

**响应 201**

```json
{ "token": "ghs_...", "expires_at": "2026-08-09T10:00:00Z", "permissions": { } }
```

> 响应 201 但 Content-Type 非 JSON、缺 `token`/`expires_at` 或字段类型异常 → `UNEXPECTED_ERROR`（该阶段未发起任何业务请求，CHK133/CHK144）。

**错误映射**

| 状态 | 映射 code | 说明 |
|------|-----------|------|
| 401 | CREDENTIALS_INVALID | JWT 无效/过期 |
| 403 | APP_NOT_INSTALLED | App 被挂起/无该安装权限 |
| 404 | APP_NOT_INSTALLED | 安装不存在（含目标仓库不在安装范围内） |
| 429 | RATE_LIMITED | 限流（`Retry-After` 透传至 `details`） |
| 网络/超时 | NETWORK_ERROR | - |

## 2. 读取目标 PR 状态（提交前，FR-004）

**请求**

```text
GET https://api.github.com/repos/{owner}/{repo}/pulls/{pull_number}
Authorization: Bearer {installation_token}
```

- 调用时机：载荷本地校验（FR-003）与认证完成后、提交 review 之前。
- 仅消费响应中的 `state` 与 `merged` 字段；其余附带元数据不用于任何校验（FR-014）。`state`/`merged` 为 `null` 或类型异常 → `UNEXPECTED_ERROR`（CHK094）。
- 判定：仅当 `state == "open"` 且 `merged == false` 才继续提交；否则（已合并/已关闭）→ `PR_NOT_OPEN`，**不发起 review 提交**（FR-004）。draft（草稿）PR 不单独拒绝，不读取 `draft` 字段（FR-004，CHK103）。

**响应 200**

```json
{ "number": 42, "state": "open", "merged": false }
```

**错误映射**

| 状态 | 映射 code | 说明 |
|------|-----------|------|
| 401 | CREDENTIALS_INVALID | 令牌无效/过期 |
| 403 | APP_NOT_INSTALLED | 权限不足/App 未安装或撤销 |
| 404 | TARGET_NOT_FOUND | PR 不存在/无访问权限（GitHub 对无权限回 404 防泄露） |
| 200 + state≠open | PR_NOT_OPEN | PR 已合并或已关闭，直接失败（FR-004） |
| 429 | RATE_LIMITED | 限流（`Retry-After` 透传至 `details`） |
| 网络/超时 | NETWORK_ERROR | 状态读取阶段网络失败/超时；未发起任何 review 提交（FR-010） |

> **竞态说明**：状态读取与提交之间存在 TOCTOU 窗口，PR 可能在此期间被关闭/合并；若平台在竞态下仍接受提交（已合并场景），工具按平台结果如实返回。完全消除竞态需平台收紧 API 行为，详见 [research.md](../research.md) §10。
> 响应无法解析（非 JSON、Content-Type 非 application/json、JSON 为非对象类型（数组/字符串/标量）、缺 `state` 或 `merged` 字段、字段为 null/类型异常）→ `UNEXPECTED_ERROR`（附 `details`）；该阶段未发起任何 review 提交（CHK094/CHK122/CHK155）。

## 3. 提交 review（业务步骤，FR-005/FR-007/FR-013/FR-014）

**请求**

```text
POST https://api.github.com/repos/{owner}/{repo}/pulls/{pull_number}/reviews
Authorization: Bearer {installation_token}

{
  "event": "COMMENT",
  "body": "{整体结论}",
  "comments": [
    { "path": "{文件路径}", "line": 42, "side": "RIGHT", "body": "{评论内容}" }
  ]
}
```

- `event` 固定 `COMMENT`（FR-013），不接受调用方指定；成功响应 `state=COMMENTED` 为该事件的平台回执，术语含义不同但一一对应（CHK077）。
- `comments` 原样透传（FR-005）：`path`、`line`、`side`、`body`。
- 单次请求即整体原子：任一评论无效（path/line 不在目标 PR 的 file change 范围内等）→ 422，**整个 review 不创建**（FR-014/FR-009）。
- 空 diff（无文件改动）PR：仅 body 可成功；含评论时评论无法定位到改动位置，422 → `REVIEW_UNPROCESSABLE`（以平台结果为准，CHK135）。

**响应 200**（`state=COMMENTED`，`user` 为 bot）

```json
{
  "id": 123456789,
  "html_url": "https://github.com/owner/repo/pull/42#pullrequestreview-123456789",
  "state": "COMMENTED"
}
```

**错误映射**

| 状态 | 映射 code | 说明 |
|------|-----------|------|
| 401 | CREDENTIALS_INVALID | 令牌无效/过期（含认证成功后中途失效，CHK089） |
| 403 | APP_NOT_INSTALLED | 权限不足/App 未安装或撤销 |
| 404 | TARGET_NOT_FOUND | 仓库或 PR 不存在/无访问权限（GitHub 对无权限回 404 防泄露；含状态读取通过后 PR 被删除/仓库被转移的已接受竞态，CHK090） |
| 422 | REVIEW_UNPROCESSABLE | 载荷被拒：评论越界（FR-014）、路径格式/存在性、数量/长度超限、次级限流（"spammed"）等。必填缺失已在 FR-003 本地校验阶段拦截（INVALID_PAYLOAD），不会到达提交阶段（CHK180） |
| 429 | RATE_LIMITED | 限流（`Retry-After` 透传至 `details`） |
| 网络/超时 | NETWORK_ERROR | 若超时发生在提交请求发出后，review 可能已创建；提示调用方核验，不自动重试（FR-010） |

> 422 响应体 `message`/`errors` 作为 `details` 附带返回，主码固定为 REVIEW_UNPROCESSABLE。
> `errors` 数组含多条时 `details` 透传全部条目（受 2048 字符截断约束，CHK123）。
> 若提交返回 200 但 Content-Type 非 JSON、JSON 为非对象类型、`id` 非正整数或缺失、`html_url` 缺失/非法 → `UNEXPECTED_ERROR`，并提示 review 可能已创建，由调用方核验（FR-007/FR-010，CHK122/CHK153/CHK155）。

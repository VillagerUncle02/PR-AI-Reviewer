# Contract: MCP Tool `submit_pr_review`

**版本**: v3（对应 spec FR-001~FR-016，2026-08-09） | **关联**: [data-model.md](../data-model.md)

## 工具说明

- **工具名**: `submit_pr_review`
- **描述**: 以 GitHub App bot 身份，将整体审查结论与逐文件行内评论（comments 可空，仅整体结论）作为一条已提交 review（event=COMMENT）上传到显式指定的、处于打开状态的 PR（提交前核验状态，FR-004）。
- **传输**: MCP stdio（MVP）；工具结果为单个文本 JSON 内容块。
- **唯一性**: 本工具是服务暴露的唯一 tool（FR-001）。

> **MCP 集成假设**：调用方/MCP 客户端须支持读取单个文本 JSON 内容块并解析；多块输出、非文本内容块或内容块内非 JSON 文本不在本契约范围（CHK129）。

## 输入 JSON Schema（工具参数）

机器可读版本：[submit-review.schema.json](submit-review.schema.json)

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": ["owner", "repo", "pullNumber", "body", "comments"],
  "properties": {
    "owner": { "type": "string", "minLength": 1, "description": "目标账号（用户或组织），trim 后非空" },
    "repo": { "type": "string", "minLength": 1, "description": "目标仓库名，trim 后非空" },
    "pullNumber": { "type": "integer", "minimum": 1, "description": "目标 PR 编号" },
    "body": { "type": "string", "minLength": 1, "description": "整体结论（review body）" },
    "comments": {
      "type": "array",
      "items": {
        "type": "object",
        "additionalProperties": false,
        "required": ["path", "line", "side", "body"],
        "properties": {
          "path": { "type": "string", "minLength": 1 },
          "line": { "type": "integer", "minimum": 1 },
          "side": { "enum": ["RIGHT", "LEFT"] },
          "body": { "type": "string", "minLength": 1, "description": "评论正文（trim 后非空）" }
        }
      }
    }
  }
}
```

> 说明：
> - 单一事实源：[submit-review.schema.json](submit-review.schema.json) 为机器可读输入 schema 的权威版本；本内联 JSON 为文档快照，修订时以 schema 为准并同步（CHK083）。
> - `comments` 允许为空数组（仅上传整体结论）；显式传 `null` 视为结构无效 → `INVALID_PAYLOAD`（区别于缺失与空数组，CHK074）。
> - 同一列表内允许重复 `path+line+side`（同位置多条评论）：原样透传（FR-005），不做去重（CHK073）。
> - `path` 本地仅校验非空；含 `../`、绝对路径等异常格式的规范性由 GitHub 判定（拒绝 → `REVIEW_UNPROCESSABLE`，CHK092）。`pullNumber` 不设本地上限，超出平台可处理范围由状态读取 404 → `TARGET_NOT_FOUND` 判定（CHK093）。
> - trim 仅用于有效性判断，提交时保持调用方原始内容；审查内容按 UTF-8 原样透传，不做编码转换或内容规范化（FR-003/FR-005，CHK075/CHK076）。trim 使用 .NET `string.Trim()` 语义（Unicode 空白字符；零宽空格 U+200B 等格式字符不在裁剪集内，CHK172）；`owner`/`repo` 为 trim 后非空字符串（纯空白视为缺失 → `INVALID_PAYLOAD`），`pullNumber` 为 ≥1 的整数（FR-002，CHK173/CHK177）。
> - 长度上限（评论数、正文长度）由 GitHub 执行，超限以 422 映射（见错误码）。
> - `owner`/`repo` 原样透传，不做大小写归一化；GitHub 对仓库名大小写不敏感，不可解析/无权限统一按 `TARGET_NOT_FOUND` 返回（CHK104）。`owner`/`repo`/`path` 等字符串字段均不设长度上限，格式规范性与存在性由 GitHub 判定（404→`TARGET_NOT_FOUND`、422→`REVIEW_UNPROCESSABLE`，CHK167）。
> - 同一文件可同时含 LEFT 与 RIGHT 评论：分别定位旧/新文件侧行，原样透传（FR-005），合法性由 GitHub 校验（CHK136）。
> - 空 diff（无文件改动）PR：仅整体结论可成功；含评论时评论无法定位，422 → `REVIEW_UNPROCESSABLE`（以平台结果为准，CHK135）。
> - SDK 生成 schema 一致性：实现阶段以 [submit-review.schema.json](submit-review.schema.json) 为参数模型，组件测试断言 SDK 暴露的参数名/类型/必填与契约一致（CHK142）。
> - 类型不符（如 `pullNumber` 传字符串 `"42"`、`side` 传小写 `"right"`）：优先由 MCP 框架参数/schema 校验拒绝（协议层）；框架放行时本地校验兜底 → `INVALID_PAYLOAD`；两种情况均不产生 GitHub 请求（CHK168/CHK169）。

## 输出契约

成功（MCP 工具返回 JSON 文本内容块）：

```json
{ "status": "success", "reviewId": 123456789, "htmlUrl": "https://github.com/owner/repo/pull/42#pullrequestreview-123456789" }
```

失败：

```json
{ "status": "error", "code": "REVIEW_UNPROCESSABLE", "message": "评论不在目标 PR 的 file change 范围内（GitHub 拒绝）", "httpStatus": 422 }
```

> **httpStatus 语义**：为 GitHub HTTP 响应状态码（如 404/422/429），与 MCP 协议层状态无关；本地校验类错误（`INVALID_PAYLOAD` 等）无 httpStatus 或省略（CHK166）。
> **向后兼容**：结果 JSON 允许未来新增字段（调用方应忽略未知字段）；字段删除或重命名属破坏性变更，需契约升级并同步 spec（与 CHK163 错误码演进对齐，CHK175）。

> **错误归属边界**：参数畸形/必填缺失在工具内本地校验并返回 `INVALID_PAYLOAD`（未发任何 GitHub 请求）；工具内部未预期异常返回 `UNEXPECTED_ERROR`；MCP 传输层错误（连接中断、参数 JSON 反序列化失败等）由 MCP 框架按协议报错，不属于工具 JSON 结果契约范围（CHK005）；启动期必需配置校验（FR-015）失败时进程立即以非零码退出并在 stderr 给出明确错误，不进入工具调用，同样不属于本结果契约。错误 `message`/`details` 不含令牌、私钥等敏感内容（FR-011）。

> **消息约定**：`message` 使用简体中文、一句话说明失败原因与调用方可执行动作，长度 ≤512 字符；`message` 为辅助说明，调用方必须以 `code` 分支决策，文本不保证跨版本稳定（CHK164）。`details` 可选，透传 GitHub 原始细节（`message`/`errors` 及 `Retry-After` 等，`errors` 数组透传全部条目），长度上限 2048 字符，超出截断并附省略标记，保持 JSON 可解析（CHK080/CHK097/CHK123）。长度计数单位为 Unicode 字符（非字节），按字符边界截断、不切断多字节序列/代理对（CHK107/CHK154）。透传前过滤令牌、私钥等敏感内容，受 FR-011 禁令约束（CHK109）。可重试性：`details.retryable`（布尔，可选）——`RATE_LIMITED`/`NETWORK_ERROR`（含 5xx）=true，其余=false 或省略；调用方可按错误码约定或该字段分支（CHK139）。

## 错误码

*错误码表为 FR-008 所列失败类别的契约化映射（CHK082）；spec 保持技术中立，失败类别以文字表述，本表给出对应错误码。*
*契约演进：新增、删除或合并错误码，或修订错误语义时，须先经 spec 修订（需求基线）再同步本契约版本；版本映射见文首（CHK132/CHK163）。*
*code ↔ httpStatus 绑定：404→`TARGET_NOT_FOUND`、403→`APP_NOT_INSTALLED`、401→`CREDENTIALS_INVALID`、422→`REVIEW_UNPROCESSABLE`、429→`RATE_LIMITED`；`PR_NOT_OPEN` 由 200+state≠open 判定（httpStatus 附 200 或省略）；`INVALID_PAYLOAD`/`NETWORK_ERROR`/`UNEXPECTED_ERROR` 视场景。实现与测试按此表机器校验（CHK165）。*

| code | 含义 | 触发 | 调用方可采取动作 |
|------|------|------|------------------|
| INVALID_PAYLOAD | 本地校验失败 | body/评论必填缺失或结构无效；**未发任何 GitHub 请求** | 修正载荷后重试 |
| CREDENTIALS_INVALID | 私钥运行期读取/解析失败（如被删除/替换）、JWT 或令牌被拒(401) | 任意请求阶段 | 检查 private-key/ 与配置（FR-011：错误信息不含敏感内容）。注意：启动期配置缺失/非法属 FR-015 启动失败，不产生本错误码（CHK111） |
| APP_NOT_INSTALLED | App 未安装/已撤销/无权限(403) | 认证/状态读取/提交阶段 | 检查 App 安装与仓库授权 |
| TARGET_NOT_FOUND | 仓库/PR 不存在或无访问权限(404) | 状态读取或提交阶段 | 核对 owner/repo/pullNumber（无法区分拼写错误与无权限，平台防枚举，CHK121） |
| PR_NOT_OPEN | PR 存在但已合并/已关闭（提交前状态读取，FR-004） | PR 状态读取阶段 | 重新打开 PR 或更换目标 PR |
| REVIEW_UNPROCESSABLE | GitHub 拒绝载荷(422)：评论越界/缺失/超限等 | 提交阶段 | 修正评论位置或数量后重试 |
| RATE_LIMITED | 限流(429)；`Retry-After` 头（秒数或 HTTP 日期）转换后透传至 `details.retryAfterSeconds` | 任意请求阶段 | 按 `details.retryAfterSeconds`（如有）等待后由调用方重试；并发调用共享安装令牌的 GitHub 核心限流配额，高并发时由调用方串行化或降低并发（CHK141）；工具不自动重试（FR-010） |
| NETWORK_ERROR | 网络失败/超时、GitHub 5xx（502/503/504 等）；提交请求发出后超时可能已创建 review | 任意请求阶段 | 检查网络；若超时发生在提交后，先核验目标 PR 再决定是否重试（FR-010，工具不保证幂等）；5xx 为 GitHub 服务端错误，稍后重试（CHK134） |
| UNEXPECTED_ERROR | 未预期错误；令牌交换 201 响应缺 token/expires_at；状态读取/提交响应非 JSON、JSON 非对象、缺必需字段或类型异常（如 id=0/空串）；提交返回 200 但响应体缺失或结构异常 | 任意阶段 | 报告工具缺陷；若发生在提交后，核验 PR 是否已创建 review（FR-007/FR-010） |

> 任何错误均为整体失败终态；不存在部分成功（FR-009）。工具不自动重试（FR-010）。

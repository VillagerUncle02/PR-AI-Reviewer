# PR 审查记录：PR #39（001-pr-review-submit）

- 时间：2026-08-10
- PR：[#39](https://github.com/VillagerUncle02/PR-AI-Reviewer/pull/39)（main ← 001-pr-review-submit）
- 范围：T001–T038（Phase 1–6 全部任务）
- 审查模式：降级审查（REVIEWER 未配置/子代理不可靠，主循环审查）
- 后续更新：2026-08-11 全量复核（Phase 4–6 追加实现后）

## 门禁与 CI 证据

- 本地门禁：build 0 警告 0 错误；87 个测试（82 通过 + 5 冒烟跳过，凭据未配置）；private-key 排除检查通过；dotnet format 通过
- CI（pull_request，全部 success）：31377737114（0bc07aa）、31378023826（b075f25）、31379051809（1ab8e5b）、31379517854（ab89c27）
- 进程级验证：MCP 会话 stdout 仅 JSON-RPC、stderr 为空（FR-011/CHK162，ZeroWriteTests）

## Findings

| # | 级别 | file:line | 问题 | 状态 |
|---|------|-----------|------|------|
| 1 | 🔴 | src/PrReviewSubmit/GitHub/GitHubReviewClient.cs | create review 载荷的 comments 条目用 `new { c.Path, ... }`，STJ 默认按属性名序列化 → 发送 `Path/Line/Side/Body`（PascalCase），不符合 GitHub REST 契约的 `path/line/side/body`，会导致 422 或评论字段丢失 | 已修复（改为显式小写成员名），并加组件回归测试 |
| 2 | 🔴 | src/PrReviewSubmit/Json/ToolJsonContext.cs | 结果 JSON 中 `code` 枚举默认序列化为数字（如 `5`），而 tool-contract.md 要求字符串错误码（如 `"INVALID_PAYLOAD"`） | 已修复（UseStringEnumConverter=true），并加单元回归测试 |
| 3 | 🔴 | src/PrReviewSubmit/GitHub/GitHubAppAuthClient.cs | CreateJwt 将 using 作用域内 RSA 实例交给 RsaSecurityKey，JWT 签名 provider 缓存复用已释放实例 → 第二次调用 ObjectDisposedException | 已修复（导出 RSA 参数构造 key），StatelessnessTests 回归覆盖 |
| 4 | 🔴 | src/PrReviewSubmit/Program.cs | 运行期 stderr 输出框架 info 日志，违反 FR-011/CHK162 | 已修复（ClearProviders），ZeroWriteTests 进程级回归覆盖 |
| 5 | 🔴 | src/PrReviewSubmit/GitHub/GitHubAppAuthClient.cs + GitHubReviewClient.cs | GitHub REST 请求缺少 User-Agent，真实冒烟返回 403（GitHub 强制要求） | 已修复（GitHubAppOptions.UserAgent 常量 + 所有请求统一添加），单元回归覆盖；真实冒烟 4/4 通过 |

## 宪法合规自查

- 单一职责：仅 upload 一条 review，无其他产出 —— 通过
- 显式目标：owner/repo/pullNumber 必填、无默认/推断 —— 通过
- Bot 身份：安装令牌认证 + 平台固有 bot 标识，未伪造 —— 通过
- 凭据安全：private-key/ 与 *.pem 在 .gitignore + CI 检查，staged 列表已核验无密钥 —— 通过
- 失败透明：结构化 error JSON（code/message/httpStatus/details），无静默、无隐式重试 —— 通过（Phase 4 全失败路径待补测）

## 遗留 TODO

- 真实 GitHub App 冒烟：已用用户提供的凭据执行（4 通过 / 1 跳过），PR #39 上有 bot review 证据；待仓库出现 closed/merged PR 时补跑 PR_NOT_OPEN 子场景
- PR 合并等待人工 Approve；CI 不含 dotnet format（本地门禁已覆盖）

## 结论

PASS（T001–T038 全量复核；真实凭据冒烟为遗留人工项）。

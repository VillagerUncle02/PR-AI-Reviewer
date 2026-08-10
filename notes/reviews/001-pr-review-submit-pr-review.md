# PR 审查记录：PR #39（001-pr-review-submit）

- 时间：2026-08-10
- PR：[#39](https://github.com/VillagerUncle02/PR-AI-Reviewer/pull/39)（main ← 001-pr-review-submit）
- 范围：T001–T020（Phase 1–3），96 个文件，+9553/-11
- 审查模式：降级审查（REVIEWER 未配置/子代理不可靠，主循环审查）

## 门禁与 CI 证据

- 本地门禁：build 0 警告 0 错误；31 个测试全部通过；private-key 排除检查通过；dotnet format 通过
- CI：run 31377737114（pull_request）→ success（restore/build/test/私钥排除）

## Findings

| # | 级别 | file:line | 问题 | 状态 |
|---|------|-----------|------|------|
| 1 | 🔴 | src/PrReviewSubmit/GitHub/GitHubReviewClient.cs | create review 载荷的 comments 条目用 `new { c.Path, ... }`，STJ 默认按属性名序列化 → 发送 `Path/Line/Side/Body`（PascalCase），不符合 GitHub REST 契约的 `path/line/side/body`，会导致 422 或评论字段丢失 | 已修复（改为显式小写成员名），并加组件回归测试 |
| 2 | 🔴 | src/PrReviewSubmit/Json/ToolJsonContext.cs | 结果 JSON 中 `code` 枚举默认序列化为数字（如 `5`），而 tool-contract.md 要求字符串错误码（如 `"INVALID_PAYLOAD"`） | 已修复（UseStringEnumConverter=true），并加单元回归测试 |

## 宪法合规自查

- 单一职责：仅 upload 一条 review，无其他产出 —— 通过
- 显式目标：owner/repo/pullNumber 必填、无默认/推断 —— 通过
- Bot 身份：安装令牌认证 + 平台固有 bot 标识，未伪造 —— 通过
- 凭据安全：private-key/ 与 *.pem 在 .gitignore + CI 检查，staged 列表已核验无密钥 —— 通过
- 失败透明：结构化 error JSON（code/message/httpStatus/details），无静默、无隐式重试 —— 通过（Phase 4 全失败路径待补测）

## 遗留 TODO

- Phase 4（T021–T027）：失败路径测试、失败编排、启动配置校验（FR-015）、FR-016 断言
- Phase 5/6：README 文档、无重试/调用方重试测试、契约一致性测试、冒烟/端到端

## 结论

PASS（基于当前 PR 范围 T001–T020；后续阶段任务会继续追加提交到本分支或链式分支，需在新一轮 PR 审查中复核）。

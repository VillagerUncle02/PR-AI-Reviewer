# Contracts: PR Review Submit

本目录记录本功能对外暴露的接口契约：

- [tool-contract.md](tool-contract.md)：MCP 工具 `submit_pr_review` 的输入/输出 JSON 契约与错误码全集。
- [submit-review.schema.json](submit-review.schema.json)：工具输入的机器可读 JSON Schema（与 tool-contract.md 一致）。
- [github-rest.md](github-rest.md)：GitHub REST 集成契约（认证令牌端点 + create review 端点），字段映射与错误映射。

**约定**：
- 工具只做上传，不生成/修改内容（FR-005）。
- 单次调用整体成功或整体失败，无半成功（FR-009）。
- 失败原因一律以错误码 + 可读信息返回（FR-008）。
- **术语**：用户语境中的"上传审查结果"与本工具"提交 review"（`submit_pr_review`）等价；文档统一使用"提交 review"，spec 保留用户原话（CHK140）。

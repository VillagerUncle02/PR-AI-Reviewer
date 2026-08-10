# Review 001-pr-review-submit - 第 6 轮（真实 GitHub App 冒烟）

- 时间：2026-08-11
- 执行者：主循环 + Backend Architect 子代理（User-Agent 修复）
- 凭据：GITHUB_APP_ID=4525509、GITHUB_APP_INSTALLATION_ID=152380612、
  GITHUB_PRIVATE_KEY_PATH=private-key/vu-s-pr-ai-reviewer.2026-08-09.private-key.pem（相对仓库根）
- 目标：VillagerUncle02/PR-AI-Reviewer PR #39，README.md:17（RIGHT）

## Findings

| # | 级别 | file:line | 问题 | 状态 |
|---|------|-----------|------|------|
| 1 | 🔴 | src/PrReviewSubmit/GitHub/GitHubAppAuthClient.cs + GitHubReviewClient.cs | GitHub REST 请求缺少 User-Agent 头，GitHub 返回 403「Request forbidden by administrative rules… Please make sure your request has a User-Agent header」；令牌交换/PR 状态/create review 全部受影响 | 已修复：新增 `GitHubAppOptions.UserAgent = "PR-AI-Reviewer-MCP/1.0"`，令牌交换与 ApplyHeaders（GET pulls / POST reviews）统一添加；AppAuthClientTests 与 GitHubReviewClientRequestTests 增加断言 |
| 2 | 💭 | tests/.../Smoke/SmokeTests.cs | 冒烟测试自身三处问题：相对密钥路径依赖进程 CWD（testhost 非仓库根）；读回 REST 请求缺 User-Agent 等头；`pulls/comments?review_id=` 不按 review_id 过滤且 `reviews/{id}/comments` 端点缺 line/side 字段 | 已修复：相对路径按仓库根解析；读回请求补 User-Agent/Accept/Api-Version；改为 pulls/comments 全量后按 pull_request_review_id 客户端过滤 |

## 冒烟结果

- 4 通过 / 0 失败 / 1 跳过（Scenario D PR_NOT_OPEN：仓库暂无 closed/merged PR，按设计跳过）
- Scenario A：真实上传成功，REST 读回断言 body 一致、`user.type=Bot`、评论 path/line/side/body 一致
- Scenario B：越界文件评论 → REVIEW_UNPROCESSABLE，reviews 数量不变（无部分成功）
- Scenario C：空白载荷 → INVALID_PAYLOAD，零 GitHub 请求
- Scenario D（不存在 PR）：TARGET_NOT_FOUND，reviews 数量不变
- SC-008：单次上传往返约 3s，远低于 30s 上限
- PR #39 上留有 bot 身份的真实 review/评论（smoke marker），可在 GitHub 页面查看

## 结论

patch is correct（置信度 0.95）。产品端到端真实链路（App JWT → 安装令牌 → PR 状态 → submit review → 读回）已验证通过；剩余人工项仅为「待仓库出现 closed/merged PR 时补跑 Scenario D 子场景」与「PR 合并审批」。

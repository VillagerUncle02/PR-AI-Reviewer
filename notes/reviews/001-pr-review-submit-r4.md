# Review 001-pr-review-submit - 第 4 轮

- 审查范围：T028–T031（Phase 5，US3 无重试/调用方重试/无状态）
- 对比上一轮：r3（T021–T027）
- 审查模式：子代理恢复执行（Backend Architect + Technical Writer，用户确认通讯问题已修复）

## Findings

| # | 级别 | file:line | 问题 | 状态 |
|---|------|-----------|------|------|
| 1 | 🔴 | src/PrReviewSubmit/GitHub/GitHubAppAuthClient.cs | `CreateJwt()` 把 using 作用域内的 RSA 实例直接交给 RsaSecurityKey；CryptoProviderFactory 按密钥材料缓存 AsymmetricSignatureProvider，第二次 CreateJwt（同私钥内容）复用第一次已释放的 RSA（RSABCrypt）→ ObjectDisposedException | 已修复：改为 `RsaSecurityKey(rsa.ExportParameters(true))`，缓存 provider 基于参数自建 RSA；仍每次读私钥、每次新 JWT（FR-012/T031）；StatelessnessTests 提供回归覆盖 |
| 2 | ✅ | tests/.../NoRetryBehaviorTests.cs | 失败调用后 300ms 观察期内 GitHub 请求计数不变（无后台重试/自动补偿） | 通过 |
| 3 | ✅ | tests/.../RetryFlowTests.cs | 调用方修正后第二次调用成功；提交请求恰好 1 次成功（SubmitCalls==2：1 次失败 + 1 次成功），令牌/状态读取各 2 次（调用独立） | 通过 |
| 4 | ✅ | tests/.../StatelessnessTests.cs | 每次调用获取全新安装令牌（token-0/token-1、请求数=2）；并发调用互不影响（2 个不同令牌、请求数=2） | 通过 |
| 5 | ✅ | README.md | 新增「重试与去重语义」小节，覆盖永不自动重试、不保证幂等（超时后先核验 PR）、details.retryable 信号、调用独立无状态 | 通过 |

- 未验证猜测：无
- 运行时自适应：子代理通讯恢复后按 agent-assignments.yml 派发 Backend Architect（T028/T029/T031）与 Technical Writer（T030）；两子代理均完成且未越界 commit/push
- 整体结论：patch is correct（置信度 0.9）——门禁复核（build 0 警告 0 错误 / 79 测试通过 / 私钥排除 / dotnet format）通过
- 收敛检测：正常
- 轮次提醒：正常

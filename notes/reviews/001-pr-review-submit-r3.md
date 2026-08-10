# Review 001-pr-review-submit - 第 3 轮

- 审查范围：T021–T027（Phase 4，US2 失败路径与启动校验）
- 对比上一轮：r2（T016–T020）；新增 3 个生产文件与 5 个测试文件
- 审查模式：降级审查（主循环直接实现并审查）

## Findings

| # | 级别 | file:line | 问题 | 状态 |
|---|------|-----------|------|------|
| 1 | 🟡 | src/PrReviewSubmit/GitHub/GitHubErrorMapper.cs | details 截断计量使用默认转义编码，中文按 \uXXXX 计 6 字符，与契约"按 Unicode 字符计数"不符，导致长中文 message 被整体丢弃 | 已修复：改用 UnsafeRelaxedJsonEscaping 计量（不转义非 ASCII）；测试同步按字符计数断言 |
| 2 | 💭 | src/PrReviewSubmit/GitHub/GitHubErrorMapper.cs | 工具实际返回 JSON 仍由 ToolJsonContext 默认编码转义中文（合法 JSON，仅体积略大）；调用方按解析结果使用，不受影响 | 保持现状，记录 |
| 3 | 💭 | tests/.../StartupConfigurationTests.cs | "私钥不可读"子用例未覆盖（Windows 上难以模拟无读权限）；已覆盖不存在/不可解析为 RSA | 记录，后续可按需补充 |
| 4 | ✅ | src/PrReviewSubmit/Program.cs + StartupConfigurationValidator.cs | FR-015 启动校验（App ID/安装 ID 正整数、私钥存在/可读/可解析 RSA）在进程级测试中验证：非零退出码 + stderr 明确错误 + 快速退出（无网络请求迹象） | 通过 |
| 5 | ✅ | src/PrReviewSubmit/Infrastructure/GitHubHttpClientServiceCollectionExtensions.cs | FR-016：HttpClient.BaseAddress 固定 api.github.com、超时 10s、TLS 默认校验开启且无禁用入口，配置断言测试覆盖 | 通过 |
| 6 | ✅ | tests/.../AppAuthClientTests.cs | 调用间删除私钥 → CREDENTIALS_INVALID 且无新 GitHub 请求；替换为新私钥 → 下一次调用即生效（无需重启） | 通过 |

- 未验证猜测：无
- 运行时自适应：无（default 直接实现）
- 整体结论：patch is correct（置信度 0.9）——门禁（build 0 警告 0 错误 / 75 测试通过 / 私钥排除 / dotnet format）全部通过
- 收敛检测：正常
- 轮次提醒：正常

# 运行时自适应记录：001-pr-review-submit

- 日期：2026-08-10
- 分支：001-pr-review-submit

## 决策记录

1. **子代理降级**：按 agent-assignments.yml 派出的命名子代理多次未执行任务
   （仅返回角色问候语），且派生孙代理后继续失控。按 implement-loop 运行时
   自适应原则降级为 `default`（主循环直接实现），后续尽量以受控方式重试。
2. **.NET 10 SDK**：本机原无 SDK 10，用户手动安装 10.0.302；`global.json`
   固定 10.0.302 + rollForward=latestFeature。
3. **解决方案格式**：.NET 10 SDK 的 `dotnet new sln` 默认生成 `.slnx`，
   与 plan/tasks 要求的 `PrReviewSubmit.sln` 不一致；使用
   `dotnet new sln --format sln` 生成经典格式并删除 `.slnx`。
4. **dotnet 首启 sentinel**：沙箱进程无法写 `C:\Users\ASUS\.dotnet`；
   通过 `DOTNET_CLI_HOME` 重定向到临时目录解决，并写入 scripts/gates.ps1。
5. **CI 现状**：仓库尚无 `.github/workflows/ci.yml`（计划由 T015 创建），
   implement-loop 第 8 步的 CI 等待将在 T015 完成后生效。

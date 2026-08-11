# Review 002-package-release - 第 6 轮（T008 安装验证）

- 审查范围：T008（quickstart 场景 3：Codex 安装验证）
- 对比上一轮：无代码变更 / 新增安装证据
- 验证结果：
  - `codex mcp add pr-ai-reviewer --env ... -- D:\...\dist\1.0.0\PrReviewSubmit.exe` 成功（全局注册，写入用户级 config.toml）
  - `codex mcp list` 显示 pr-ai-reviewer（exe 路径 + 三项 env，enabled）✓
  - 注册到可见耗时约 1 秒（SC-006 ≤30 分钟 ✓；"首次成功调用"以 T006 真实冒烟为证据，同一产物+env 已在 PR #54 完成 bot review 上传）
  - 沙箱备注：写用户级配置需提升权限；CODEX_HOME/HOME 需显式设置
- Findings：无
- 未验证猜测：无
- 运行时自适应：注册使用真实 App ID/安装 ID/私钥绝对路径（均为本机既有配置，未入库）
- 整体结论：patch is correct（置信度 0.95）
- 收敛检测：正常
- 轮次提醒：正常

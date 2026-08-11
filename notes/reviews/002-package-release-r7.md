# Review 002-package-release - 第 7 轮（T009 release.ps1）

- 审查范围：T009（scripts/release.ps1，562 行），Code Reviewer 首轮 FAIL → 修复 → 主循环核实
- 对比上一轮：修复 1 🔴 + 3 🟡 / 回归 0
- Findings：

  | # | 级别 | file:line | 问题 | 状态 |
  |---|------|-----------|------|------|
  | 1 | 🔴 | release.ps1 tag 补建分支 | 补建路径未校验既有 tag 指向 buildCommit（可能发布与产物追溯不符的 tag） | 已修复：Test-TagPointsToBuildCommit，不一致退出 1（严禁删除远端 tag） |
  | 2 | 🟡 | release.ps1 DryRun | DryRun 调用 gh（违反 release-cli.md"不调用 GitHub"） | 已修复：DryRun 跳过全部 gh 调用，输出占位说明 |
  | 3 | 🟡 | release.ps1 交互确认 | Read-Host 失败默认继续（fail-open） | 已修复：取消并退出 1（fail-closed） |
  | 4 | 🟡 | release.ps1 冒烟匹配 | status/状态 success 全文子串匹配过宽 | 已修复：行锚定精确匹配（兼容 "- 状态：success" 真实格式） |
  | 5 | 💭 | 多处 | gh 报错文案判 404 / 未先 fetch / release create 失败补建指引 / 冒烟-产物绑定 / breaking 标记 | 记录（不阻塞；补建指引与 breaking 标记可随 T012 顺手优化） |

- 未验证猜测：无
- 主循环核实：release.ps1 -Version 1.0.0 -DryRun → 退出 1，仅 commit 校验失败（BUILD_INFO=9f0c4ab ≠ origin/main=6dab5d8，预期，待 PR 合并后重新 publish）；DryRun 全程无 gh 调用；冒烟审计正则对 "- 状态：success" 命中、对 successful 不命中（实现 agent 自测 + 主循环确认）
- 运行时自适应：REVIEWER=Code Reviewer；修复由 DevOps Automator 执行
- 整体结论：patch is correct（置信度 0.93）
- 收敛检测：正常
- 轮次提醒：正常

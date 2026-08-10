# CI 反馈记录：001-pr-review-submit

- 时间：2026-08-10
- 分支：001-pr-review-submit @ 0bc07aa
- 工作流：CI（.github/workflows/ci.yml）
- run id：31377737114（event=pull_request，PR #39）
- 结论：success（build + test + private-key 排除检查全部通过）

## 修复提交 CI（b075f25）

- run id：31378023826（event=pull_request，head=b075f25da43093346e4e8548683231d49734e5e5）
- 结论：success（build + test + private-key 排除检查全部通过）

## 运行时自适应

1. CI 工作流尚未合入 main，`gh workflow run ci.yml --ref <branch>` 返回 404
   （workflow 必须存在于默认分支）。改为先开 PR #39，借助
   `pull_request` 触发器运行同一工作流；后续功能分支同理。
2. wait-ci.ps1 的 gh 预检在 Start-Job 子进程中失败（退出码 3），
   改为在主进程直接轮询 `gh run view`；CI 成功。
3. 手工轮询第一次使用了错误硬编码 SHA（`b075f25a2d…` ≠ 实际
   `b075f25da4…`），导致误判 run 未出现；已在第二次轮询中修正为
   读取 `git rev-parse HEAD` 并直接查看 `gh run list`。

# PR #40 AI 审查报告：implement-loop v1.1.0

**审查时间**: 2026-08-11

**PR**: [#40](https://github.com/VillagerUncle02/PR-AI-Reviewer/pull/40) `feature/implement-loop-v1.1.0` → `main`

**Head**: `ba2ebd734c31f879f7cbee6ea82267b46b1f601a`

**改动规模**: 14 个文件，+331 / −54（扩展脚本、文档、技能文件；不涉及 C# 产品代码）

## 改动范围

1. PR 正文 `Closes #` 自动同步：`open-pr.ps1` 改为从 `tasks.md` 收集已完成 `[X]` 任务并按 issue 标题映射真实编号；新增 `sync-pr-closes.ps1` 在阶段提交与 PR 审查前保持正文最新。
2. CI 触发降级路径：前置检查探测 workflow 是否已合入默认分支，未合入时自动降级为"先开 PR、用 pull_request 触发 CI"（修复 `gh workflow run` 404）。
3. `wait-ci.ps1` 认证修复：认证预检改为主进程直调，Start-Job 轮询显式传入 `GH_TOKEN`，并区分「无凭据 / token 无效 / keyring 失效」。
4. 分支切换冲突提示：`prepare-branch.ps1` / `merge-rebase-next.ps1` 检出失败时识别「untracked 文件将被覆盖」并给出处理建议。

## 关键决策

- `Closes` 生成从"手传任务 ID 再截断数字"改为"任务 ID → 真实 issue 号映射"（`Get-TaskIssueMap`），修复了任务号与 issue 号不一致时关错 issue 的隐患。
- 正文 Closes 采用"移除旧行 + 末尾追加最新块"的规范化策略，保证重复执行幂等。
- CI 降级路径与正常路径共用 `wait-ci.ps1`，按"分支 + 当前 HEAD"匹配运行，避免重复维护两套等待逻辑。
- `wait-ci.ps1` 不再信任 Start-Job 子进程的凭据上下文，改为显式传递环境 token，符合"主进程能认证而 job 内失败"的实际场景。
- 公共函数（`Get-TaskIdsFromTasksFile`、`Get-TaskIssueMap`、`Write-UntrackedConflictHint`）下沉到 `common.ps1`，`prepare-branch` / `merge-rebase-next` / `open-pr` 均已确认 dot-source 该文件。

## 门禁与 CI 证据

- **CI**：workflow run [31420165344](https://github.com/VillagerUncle02/PR-AI-Reviewer/actions/runs/31420165344)（event=`pull_request`，head=`ba2ebd7`）状态 completed / success。
- **语法**：对分支版本 8 个 PowerShell 脚本（含新增 `sync-pr-closes.ps1`）做 AST 解析，0 语法错误。
- **实证 1**：`open-pr.ps1 -DryRun -TasksFile specs/001-pr-review-submit/tasks.md` 成功收集 T001–T038 并生成 `Closes #1`–`#38`，退出码 0。
- **实证 2**：`sync-pr-closes.ps1 -PR 40 -TasksFile <001 tasks> -DryRun` 成功生成追加 38 个 Closes 的新正文，退出码 0。
- **PR 说明**：完整列出 4 项修复、验证方式与影响面，标题为 Conventional Commits 格式，符合要求。

## 宪法合规

- 变更仅涉及开发工作流工具（扩展脚本/文档），未改变产品 MCP 工具"只做 PR review 上传"的职责边界。
- 未引入密钥、令牌或敏感数据；`wait-ci.ps1` 仅透传已存在的 `GH_TOKEN`/`GITHUB_TOKEN` 环境变量，不落盘、不硬编码。
- 未新增持久化、缓存或超出上传职责的操作；与宪法"凭据安全""失败透明"无冲突。

## 审查结论

**PASS**（正常路径功能、CI、语法、实证均通过；2 个 🟡 非阻塞改进项 + 3 个 🟢 观察项，建议后续小版本处理）

## Findings

### 🟡 F1：`open-pr.ps1` 对无效 `-TasksFile` 不阻断创建

- **位置**：`open-pr.ps1`（`Get-TaskIdsFromTasksFile` 调用处，约 L56）
- **问题**：`TasksFile` 不存在或不可读时，`Get-TaskIdsFromTasksFile` 仅写错误并返回空数组，`open-pr.ps1` 继续执行并静默创建不带 `Closes` 的 PR——与本 PR 想修复的"漏关 issue"问题同类。
- **修复方向**：`TasksFile` 被提供但无法读取/解析时 `exit 1`，或创建前先校验 `Test-Path`。

### 🟡 F2：Closes 行清理会移除人工/非任务 Closes

- **位置**：`open-pr.ps1` / `sync-pr-closes.ps1` 的 `(?m)^\s*Closes\s+#\d+\s*$` 替换
- **问题**：该正则删除正文中**所有** `Closes #` 行，包括人工补充、与任务无关的 issue；重写后只保留任务映射结果。
- **修复方向**：仅移除脚本生成的 Closes 块（如带可识别标记），或保留映射之外的 Closes 并输出 WARN 供人工确认。

### 🟢 F3：`gh issue list --limit 1000` 存在上限

- **位置**：`common.ps1` `Get-TaskIssueMap`、`check-issues.ps1`
- **问题**：issue 数超过 1000 时映射不完整。
- **建议**：文档注明上限；必要时实现分页循环（注意 `--paginate` 数组拼接问题）。

### 🟢 F4：CompletedOnly 仅匹配小写 `[x]`

- **位置**：`common.ps1` `Get-TaskIdsFromTasksFile`
- **问题**：任务列表使用大写 `[X]` 时 CompletedOnly 漏收集（非 CompletedOnly 分支已支持 `[xX]`）。
- **建议**：两处统一为 `\[[xX]\]`。

### 🟢 F5：`wait-ci.ps1` 认证预检失去硬超时保护

- **位置**：`wait-ci.ps1` 第 0 步
- **问题**：预检改为主进程直调 `gh auth status`，不再受 Start-Job 硬超时保护；极端情况下可能卡住。
- **建议**：接受现状（该命令通常本地快速返回）或对预检同样施加超时。

## 遗留 TODO

- F1 / F2 建议在后续小版本修复；不影响本 PR 四个目标功能的正常路径与验收。

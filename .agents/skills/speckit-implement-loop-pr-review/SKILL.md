---
name: speckit-implement-loop-pr-review
description: AI-review an open PR against spec/plan/tasks/constitution and backfill
  the review comment
compatibility: Requires spec-kit project structure with .specify/ directory
metadata:
  author: github-spec-kit
  source: implement-loop:commands/pr-review.md
---

# AI PR Review（AI 审查 PR，不批准不合并）

## User Input

```text
$ARGUMENTS
```

支持 `--pr <编号>`（缺省自动查找当前分支的 open PR）。

## 复审规则（每一轮都必须执行，包括 FAIL 修复后的重审）

1. **全量复审**：每一轮都重新审查**完整 PR diff 的当前版本**（`gh pr diff <pr>`），而不是只检查上一轮 findings 涉及的代码；上一轮的审查范围不构成跳过理由；
2. **携带完整台账**：若该 PR 已有审查记录，先读 `<REVIEWS_DIR>/<branch>-pr-review*.md` 全部历史记录，把上轮**每条条目（含 🟡 建议与 💭）**逐条列入本轮台账并给出状态：已修复 / 未修复 / 回归 / 过时（注明依据）/ 已采纳 / 不采纳（注明原因）；任何条目不得静默消失；
3. **三重检查**：(a) 上轮每条是否已修复/处置；(b) 修复是否引入回归或新问题；(c) 继续全量查找上轮未覆盖/未发现的代码区域与问题；
4. **建议项由主代理（orchestrator）审阅处置，不强制修复**：采纳（转实现 agent 完成）/ 不采纳（记录原因）/ 核实后升级为必须修复项 / 拿不准时转交实现 agent 自行判断是否修复（结论记入台账）；是否修复由 AI 根据实际情况判断；
5. **PASS 判定（全部满足才算 PASS）**：上轮全部 🔴/🟡 已修复或明确处置；本轮全量复审无新的 🔴/🟡（含回归与上轮遗漏）；全部建议项已由主代理审阅处置；审查记录包含完整台账与"已对完整 diff 复审"的明确结论。
   **禁止**：仅凭"上轮问题看起来已修复"给出 PASS；缺少全量复审证据的 PASS 必须撤回重审。

## Steps

1. 运行 `.specify/extensions/implement-loop/scripts/powershell/load-config.ps1 -Json` 解析配置，取得 `REVIEWER`、`DEVOPS_OPINION`、`REVIEWS_DIR`、`LANGUAGE`、`FEATURE_DIR` 等；
2. 确定 PR：`gh pr view <pr> --repo <owner/repo>`；缺省时 `gh pr list --head <branch> --state open` 查找；检查 `<REVIEWS_DIR>/` 下该 PR 的既有审查记录并全部读取；
3. 审查对象：完整 PR diff（`gh pr diff <pr>`）+ 与 `FEATURE_DIR` 下 spec/plan/tasks 一致性 + 宪法合规（`.specify/memory/constitution.md`）+ 门禁与 CI 证据（`gh pr checks <pr>`）+ PR 说明完整性；按"复审规则"输出上轮条目台账 + 本轮新发现；
4. 执行者：`<REVIEWER>` agent 输出 PASS/FAIL + findings（级别、file:line、修复方向）+ 上轮条目台账，使用配置语言；CI/流水线类 PR 可请 `<DEVOPS_OPINION>` 出具交叉意见（仅意见）；💭 建议项的处置由主代理（orchestrator）审阅并写入记录，拿不准时转交实现 agent 判断是否修复；
5. 写审计文件：`<REVIEWS_DIR>/<branch>-pr-review.md`（改动范围、关键决策、门禁/CI 证据、上轮条目台账、本轮新发现、建议项处置、审查结论、遗留 TODO）；**不得覆盖上轮记录**——每轮追加保留历史，或分文件 `<branch>-pr-review-r<N>.md`；
6. 回填 PR Comment：

```powershell
gh pr comment <pr> --repo <owner/repo> --body-file <REVIEWS_DIR>/<branch>-pr-review.md
```

7. **AI 绝不点 Approve、绝不 merge**；FAIL 的 findings（含主代理决定采纳的建议项与拿不准转交判断的建议项）转给实现 agent 修复（同分支）后重审，直到满足 PASS 判定；建议项不强制修复，由实现 agent 根据实际情况判断。
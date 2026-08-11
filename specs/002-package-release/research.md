# Research: 打包发布（002-package-release）

**日期**: 2026-08-11

## 目标

为已完成的 `submit_pr_review` MCP 工具提供可重复的本地打包、真实冒烟与手动发布流程，并给出 Codex Agent 安装指南；不引入 CI/CD 自动发布，不改变工具运行时职责。

## 决策记录

### D1：发布命令形态 → 仓库内 PowerShell 脚本

- **Decision**: 新增 `scripts/publish.ps1`（产物构建 + 敏感扫描 + 校验和 + zip）与 `scripts/release.ps1`（git tag + GitHub Release）。
- **Rationale**: FR-001 要求"可重复执行"；脚本固化步骤、扫描与失败语义，避免手工命令漂移。
- **Alternatives considered**: 纯文档命令（`dotnet publish` + `gh release create`）→ 步骤易遗漏、不可审计；GitHub Actions 自动发布 → 违反宪法"工具不负责 CI/CD"与用户明确边界。

### D2：发布形态 → Windows x64 框架依赖

- **Decision**: `dotnet publish src/PrReviewSubmit/PrReviewSubmit.csproj -c Release -r win-x64 --self-contained false`。
- **Rationale**: 用户澄清确认（Q1）；产物小，目标机安装 .NET 10 运行时即可，无需 SDK。
- **Alternatives considered**: 自包含（体积大、无需运行时）；多平台（超出当前使用场景）。

### D3：版本标识 → 产物内 VERSION 文件 + 程序集版本

- **Decision**: `publish.ps1 -Version` 参数（默认 `1.0.0`）写入产物根 `VERSION` 文件，并传 `/p:Version=<version>` 给 `dotnet publish` 使文件版本元数据一致。
- **Rationale**: 产物可自证版本（FR-002），可程序化校验，且与 git tag 一一对应。
- **Alternatives considered**: 仅 git tag（产物无法自证）；新增 `--version` 命令行参数（需改产品入口，超出最小改动）。

### D4：校验和 → SHA-256

- **Decision**: `Get-FileHash -Algorithm SHA256` 生成 `<zip>.sha256`（小写十六进制，`<hash>  <filename>` 格式）。
- **Rationale**: 发布资产完整性校验标准做法。
- **Alternatives considered**: MD5/SHA-1（已过时，不安全）。

### D5：敏感扫描 → 文件名 + 内容双重检查

- **Decision**: `publish.ps1` 在打包前扫描产物目录 `dist/<version>/`（源目录由 gates 私钥检查覆盖）：文件名黑名单（`private-key/`、`*.pem`、`*.key`、`*.p12`、`*.pfx`、`.env*`）+ 内容模式（`-----BEGIN ... PRIVATE KEY-----`、`ghp_`、`github_pat_`、`gho_`、`ghs_`），命中任一即失败退出。
- **Rationale**: FR-003/SC-003 要求 0 敏感内容；文件名检查覆盖误复制，内容检查覆盖占位/拼接文件。
- **Alternatives considered**: 仅依赖 .gitignore（不覆盖产物目录）；仅人工检查（不可靠）。

### D6：发布渠道 → 手动 GitHub Releases

- **Decision**: `release.ps1` 前置校验（gates、冒烟、tag 不存在、`gh auth status`）后执行 `git tag v<version>` + `gh release create`（zip + sha256 + 发布说明）；发布说明为 Markdown（标题 + 版本与日期 + 变更分类 + 自上一 tag 的 git log 摘要，可人工编辑）。
- **Rationale**: 用户澄清确认（Q2）；GitHub 存档可追溯，手动执行不违反宪法。
- **Alternatives considered**: 仅本地产物（无公共存档）；CI/CD 自动发布（被排除）。

### D7：冒烟验收 → 直连发布产物的 MCP stdio 真实上传

- **Decision**: 新增 `scripts/smoke-published.ps1`：以 MCP stdio JSON-RPC 启动发布产物 exe，依次 `initialize`、`tools/list`、`tools/call submit_pr_review` 完成一次真实 bot review 上传，并回读校验（内容一致 + `user.type == Bot`）。
- **Rationale**: 用户澄清确认（Q3）；验证的是**发布产物本身**而非源码进程；可重复、不依赖 Codex 会话。
- **Alternatives considered**: 在 Codex 会话中调用（保留为 FR-006 安装验证，但作为发布冒烟不可重复）；`dotnet test` 冒烟（测源码进程，不是产物）。

### D8：首版版本号 → 1.0.0

- **Decision**: 002 首个正式版本默认 `1.0.0`（与产品 v1 一致）。
- **Rationale**: SemVer 起点；后续 PATCH/MINOR/MAJOR 按变更语义递增。

### D9：安装文档 → 全局 + 项目级两种注册方式

- **Decision**: README 新增"发布与安装"章节，覆盖 `codex mcp add`（全局）与 `.codex/config.toml`（项目级）两种等价方式，含三项环境变量语义与验证步骤。
- **Rationale**: FR-005；全局适合单机固定使用，项目级适合随仓库分发配置。

### D10：构建 commit 可追溯 → 产物 BUILD_INFO + release 校验 main HEAD

- **Decision**: `publish.ps1` 在产物根写入 `BUILD_INFO`（含构建 commit）；`release.ps1` 校验其等于远程 main HEAD，不一致拒绝发布。
- **Rationale**: 用户确认（Q4）；SC-005 版本可溯源；避免发布未合入 main 的代码。
- **Alternatives considered**: 仅记录不校验（追溯弱）；查询 GitHub checks 状态（当前 token 无 checks 读取权限，不可实现）。

### D11：冒烟重复执行 → 允许重试、不清理、以最近成功为准

- **Decision**: `smoke-published.ps1` 可重复执行；失败重试产生的新 review 不清理，验收以最近一次成功记录为准。
- **Rationale**: 用户确认（Q5）；测试 PR 无副作用，重试路径简单且保留审计痕迹。
- **Alternatives considered**: 每次清理历史 review（需额外删除权限）；失败后禁止自动重试（过度保守）。

### D12：项目级注册示例 → README 占位符，不提交配置文件

- **Decision**: 项目级注册仅以 README 占位符示例说明，不向版本库提交 `.codex/config.toml`。
- **Rationale**: 用户确认（Q6）；FR-007/014 保证真实 ID 与私钥路径绝不入库。
- **Alternatives considered**: 提交占位符示例文件（增加维护面）；提交真实配置（不可接受）。

### D13：半完成发布恢复 → 允许补建 Release

- **Decision**: `release.ps1` 检测到 tag 已存在但无对应 Release 时，跳过打 tag、直接创建 Release；MUST NOT 删除或覆盖远端 tag；tag 与 Release 均存在时拒绝。
- **Rationale**: 用户确认（Q7）；发布中断（如 push tag 成功、gh release create 失败）后重试可幂等恢复，不被"tag 已存在"卡死。
- **Alternatives considered**: 人工删除远端 tag 后重试（误删风险、操作繁琐）；自动删除远端 tag 重新发布（危险，破坏已引用 tag 的消费者）。
- **附加**：发布操作需要仓库 `contents: write` 权限（push tag 与创建 Release），文档前置声明。

### D14：冒烟对象 → 从 zip 解压的副本

- **Decision**: `smoke-published.ps1` 以"解压 zip 后的副本目录"为执行对象（先解压 zip，再对解压产物启动 MCP stdio 冒烟）。
- **Rationale**: 只验证 `dist/<version>/` 源目录无法发现 zip 打包损坏；对解压副本冒烟才能证明发布资产可用。
- **Alternatives considered**: 直接冒烟 dist 源目录（快但验证不完整）；冒烟后再校验 zip 哈希（不能覆盖内容损坏）。

### D15：发布审计与门禁覆盖 → release 审计文件 + 脚本语法检查

- **Decision**: `release.ps1` 成功后写审计文件 `notes/reviews/<version>-release.md`（版本、commit、tag、Release URL、操作时间）；建议在 `gates.ps1` 增加对 `scripts/*.ps1` 的 PowerShell AST 语法检查。
- **Rationale**: 发布操作可追溯（SC-005/宪法失败透明）；发布脚本语法错误应在门禁阶段暴露而非发布时。
- **Alternatives considered**: 仅靠发布脚本自身报错（暴露时机晚）；不加语法检查（发布可靠性下降）。

### D16：发布脚本 CLI 契约 → 参数表、退出码与 DryRun

- **Decision**: 三个发布脚本统一 CLI 契约（参数表、退出码 `0/1/2`、`-DryRun` 预览、冒烟 env 必填校验、VERSION 内容交叉校验），见 `contracts/release-cli.md`。
- **Rationale**: 发布操作者需要可预期的参数/退出行为；DryRun 降低误操作风险；交叉校验防止"存在但内容不符"的发布。
- **Alternatives considered**: 仅靠脚本内提示（不可预期）；无 DryRun（误操作风险高）。

## 风险与缓解

- 沙箱/离线环境 NuGet 漏洞告警 NU1900：仅告警不阻断；构建产物不受影响。
- 目标机未安装 .NET 10 运行时：文档前置要求 + 启动时明确错误（沿用 FR-015 失败透明）。
- `gh` keyring 默认凭据失效：`release.ps1`/`smoke-published.ps1` 前置 `gh auth status` 检查，失败即退出并提示使用 `GH_TOKEN`。
- 发布后才发现缺陷：禁止覆盖已有 tag/Release，修复后升 PATCH 版本重新发布。

## 结论

所有 NEEDS CLARIFICATION 已消除，设计满足宪法与 002 规格（FR-001~FR-011、SC-001~SC-007）。

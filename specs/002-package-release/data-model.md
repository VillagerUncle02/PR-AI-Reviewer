# Data Model: 打包发布（002-package-release）

**日期**: 2026-08-11

## 实体

### ReleaseVersion（发布版本）

- 字段：`version`（SemVer，如 `1.0.0`）、`tag`（`v<version>`，如 `v1.0.0`）。
- 校验规则：
  - 符合 SemVer（`^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$`），不允许 `v` 前缀进入 `version`；
  - 与 git tag 一一对应：同一 tag 不得重复创建（FR-004）。
- 关系：一个版本对应一个发布产物、一个 GitHub Release、一组冒烟结果。

### ReleaseArtifact（发布产物）

- 字段：`name`（`PrReviewSubmit-<version>-win-x64.zip`）、`layout`（见 contracts/release-artifact.md）、`checksum`（SHA-256）、`versionFile`（`VERSION`）、`buildCommit`（`BUILD_INFO`）。
- 校验规则：
  - 产物目录不依赖源码目录即可启动（FR-002）；
  - 产物内 `VERSION` 与 zip 名、git tag 一致；
  - `BUILD_INFO.commit` 在正式发布时须等于远程 main HEAD（FR-012）；
  - 产物中 0 敏感内容（FR-003）：不得含 `private-key/`、`*.pem`、`*.key`、`*.p12`、`*.pfx`、`.env*` 及私钥/令牌文本模式。
- 状态：`built → scanned → packaged → smoke-passed → released`。

### GitHubRelease（发布条目）

- 字段：`tag`、`title`（`PrReviewSubmit v<version>`）、`notes`（发布说明文件）、`assets`（zip + sha256）。
- 校验规则：与版本 1:1；重复 tag/Release 必须失败（FR-004）；创建过程手动执行（FR-009）。
- 状态：tag 已推送但 Release 未创建时为"半完成"，允许补建（FR-015）；tag 与 Release 均存在时拒绝覆盖。
- 关系：由 `scripts/release.ps1` 创建。

### McpRegistration（MCP 注册配置）

- 字段：`serverName`（`pr-ai-reviewer`）、`command`（发布产物 exe 绝对路径）、`env`（`GITHUB_APP_ID`、`GITHUB_APP_INSTALLATION_ID`、`GITHUB_PRIVATE_KEY_PATH`）、`scope`（global / project）。
- 校验规则：
  - 三项 env 必需且有效，缺失/非法时服务启动即失败（沿用 FR-015）；
  - 私钥路径为本地绝对路径，指向 `private-key/` 下文件，绝不进入版本库（FR-007）；
  - `codex mcp list` 可见（FR-005）。
  - 项目级示例仅以 README 占位符提供，不提交真实配置（FR-014）。
- 关系：引用一个发布产物；密钥路径本地存在性由启动校验负责。

### SmokeResult（冒烟结果）

- 字段：`version`、`scenario`（A：真实上传）、`status`（success/failure）、`reviewId`、`botVerified`（`user.type == Bot`）、`readBackMatched`、`evidence`（PR/comment URL 或日志）。
- 校验规则：每个正式版本发布前必须存在 `status=success` 且 `botVerified=true`、`readBackMatched=true` 的冒烟结果（FR-011/SC-002）。

## 关系总览

- `ReleaseVersion 1 ── 1 ReleaseArtifact`
- `ReleaseVersion 1 ── 1 GitHubRelease`
- `ReleaseArtifact 1 ── 1 checksum(.sha256)`
- `McpRegistration N ── 1 ReleaseArtifact`（多个安装点引用同一产物）
- `ReleaseVersion 1 ── N SmokeResult`（发布前至少一条成功记录）

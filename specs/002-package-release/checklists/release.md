# Release Requirements Quality Checklist: 打包发布（002-package-release）

**Purpose**: 以发布操作者视角，对 002 的产物、安装、冒烟、发布与安全需求做"需求质量"检查（完整性/清晰度/一致性/可测量性/覆盖度），非实现验证。
**Created**: 2026-08-11
**Feature**: [spec.md](../spec.md)
**Depth**: 深度（发布门禁级） | **Audience**: 发布操作者 | **Scope**: 全流程综合

## Requirement Completeness

- [x] CHK001 是否明确规定了发布产物的完整文件清单与目录布局？[Completeness, Spec §FR-002 / Contract release-artifact.md]
- [x] CHK002 是否明确规定了产物命名规则与版本标识文件（VERSION）的存在及内容？[Completeness, Spec §FR-002/FR-004]
- [x] CHK003 是否规定了 SHA-256 校验和的生成格式与校验方式？[Completeness, Spec §FR-004 / Contract release-artifact.md]
- [x] CHK004 是否规定了完整发布流程（产物→敏感扫描→真实冒烟→tag→Release）及各步骤输出？[Completeness, Spec §FR-009 / Contract release-process.md]
- [x] CHK005 是否规定了 git tag 与 GitHub Release 一一对应及重复创建的失败语义？[Completeness, Spec §FR-004]
- [x] CHK006 是否规定了发布说明（release notes）的来源、格式与可编辑性？[Gap, Spec §FR-009]
- [x] CHK007 是否规定 Codex 注册需同时覆盖全局（CLI）与项目级（config.toml）两种方式？[Completeness, Spec §FR-005 / Contract codex-install.md]
- [x] CHK008 是否规定发布前置条件（门禁、真实冒烟、gh 认证、tag 不存在）为可检查清单？[Gap, Contract release-process.md]

## Requirement Clarity

- [x] CHK009 "可重复执行"是否以可验证信号定义（同版本重复构建产物语义一致、校验和可复算）？[Clarity, Spec §FR-001/SC-001]
- [x] CHK010 版本号规则（SemVer、无 v 前缀、与 tag 关系）是否无歧义？[Clarity, Spec §FR-004]
- [x] CHK011 敏感扫描的"敏感项"是否明确到文件名与内容模式级别？[Clarity, Spec §FR-003 / Plan research D5]
- [x] CHK012 "真实冒烟"是否明确目标（测试仓库 + open 测试 PR）、判定（bot 标识 + 回读一致）与时机（每个正式版本发布前）？[Clarity, Spec §FR-011/SC-002]
- [x] CHK013 三项安装配置（App ID、安装 ID、私钥路径）的语义与校验失败行为是否逐项定义？[Clarity, Spec §FR-005]
- [x] CHK014 构建与安装验证时限（构建 ≤5 分钟、首次安装验证 ≤30 分钟）是否明确测量口径？[Measurability, Spec §SC-006 / Plan Technical Context]

## Requirement Consistency

- [x] CHK015 FR-009"手动/本地执行、无 CI/CD"与规格其他章节是否存在自动发布表述冲突？[Consistency, Spec §FR-009]
- [x] CHK016 SC-002 与 FR-011 的冒烟口径是否一致（真实上传为必要条件，测试替身不替代）？[Consistency, Spec §SC-002/FR-011]
- [x] CHK017 Clarifications（Q1–Q3）是否与 Assumptions、FR 无矛盾（框架依赖、GitHub Releases、真实冒烟）？[Consistency, Spec §Clarifications/Assumptions]
- [x] CHK018 contracts 与 spec 的命名、校验和、流程、安装命令是否保持一致？[Consistency, Spec §FR-002~009 / contracts/]
- [x] CHK019 README 安装/发布章节是否要求与 codex-install、release-process 契约一致（实施后核对）？[Consistency, Spec §FR-005/FR-010]

## Acceptance Criteria Quality

- [x] CHK020 SC-001 可重复性是否可客观验证（产物语义一致 + 校验和可复算）？[Measurability, Spec §SC-001]
- [x] CHK021 SC-002 是否以可验证信号定义（status=success、bot 标识、回读一致）？[Measurability, Spec §SC-002]
- [x] CHK022 SC-003 "0 处敏感内容"是否有可执行检查支撑（发布扫描 + 门禁私钥排除）？[Measurability, Spec §SC-003]
- [x] CHK023 SC-005 是否可验证（唯一 tag、Release 资产齐全、版本可溯源）？[Measurability, Spec §SC-005]
- [x] CHK024 SC-007 职责范围不变是否以产物接口可验证（仅 submit_pr_review）？[Measurability, Spec §SC-007 / Contract release-artifact.md]

## Scenario Coverage

- [x] CHK025 是否覆盖首次发布（无既有 tag）与后续版本发布两种场景？[Coverage, Spec §FR-004]
- [x] CHK026 是否覆盖安装失败场景（配置缺失/非法、私钥路径无效、同名 server）？[Coverage, Spec §FR-010 / Edge Cases]
- [x] CHK027 是否覆盖发布中断/失败后的恢复路径（不覆盖既有 tag/Release，修复后升 PATCH）？[Coverage, Contract release-process.md]
- [x] CHK028 是否覆盖离线/受限网络目标机的运行前提与文档说明？[Coverage, Spec §Edge Cases]
- [x] CHK029 是否覆盖多用户/多机器安装的配置隔离要求？[Coverage, Spec §Edge Cases]

## Edge Case Coverage

- [x] CHK030 是否定义私钥路径含空格/非 ASCII 时的引用与文档示例要求？[Edge Case, Spec §Edge Cases]
- [x] CHK031 是否定义 tag/Release 已存在时的失败语义（明确失败、禁止覆盖）？[Edge Case, Spec §FR-004]
- [x] CHK032 是否定义目标机未安装 .NET 10 运行时时的前置要求与提示？[Edge Case, Spec §Edge Cases]
- [x] CHK033 是否定义 gh 凭据失效（keyring 无效但 GH_TOKEN 可用）时的前置检查与提示？[Edge Case, Contract release-process.md / Plan research 风险]
- [x] CHK034 是否定义发布后发现缺陷的版本策略（禁止修改既有 Release，升 PATCH）？[Edge Case, Contract release-process.md]

## Non-Functional Requirements

- [x] CHK035 安全要求是否覆盖密钥不入库/产物、env 仅传路径、扫描命中即失败？[Completeness, Spec §FR-003/FR-007]
- [x] CHK036 发布操作的可观测性与失败透明（审计记录、冒烟证据、明确退出码、无静默/部分成功）是否齐全？[Completeness, Spec §FR-009 / Constitution 失败透明]

## Dependencies & Assumptions

- [x] CHK037 外部依赖（.NET 10 运行时、gh、Codex CLI、GitHub App 安装授权）是否列为前置并文档化？[Dependency, Spec §Assumptions / quickstart.md]
- [x] CHK038 "单一 GitHub App 安装"与"手动发布、不负责 CI/CD"假设是否在所有文档中一致？[Assumption, Spec §Assumptions / Constitution]

## Ambiguities & Conflicts

- [x] CHK039 "框架依赖"是否与任何"零依赖/免安装"表述冲突？[Conflict, Spec §Clarifications/Assumptions]
- [x] CHK040 术语是否统一（发布产物/Release Artifact、版本标识/Version、冒烟验收/Smoke）？[Terminology, Spec/contracts]

## 追溯与重试语义（第二轮补充）

- [x] CHK041 是否明确发布产物记录构建 commit，且 release 校验 commit 等于远程 main HEAD？[Completeness, Spec §FR-012]
- [x] CHK042 是否明确冒烟可重复执行、历史 review 不清理、以最近一次成功为准？[Completeness, Spec §FR-013]
- [x] CHK043 是否明确项目级注册示例仅以 README 占位符提供、不提交配置文件？[Completeness, Spec §FR-014]
- [x] CHK044 是否明确 CI 状态由操作者人工确认、工具不查询 checks？[Clarity, Spec §FR-012]
- [x] CHK045 构建 commit、VERSION、git tag 的可追溯链是否一致（BUILD_INFO/VERSION/tag）？[Consistency, Spec §FR-012/SC-005]

## 发布恢复与产物边界（第三轮补充）

- [x] CHK046 是否定义 tag 已推送但 Release 未创建时的恢复路径（允许补建、不删远端 tag）？[Completeness, Spec §FR-015]
- [x] CHK047 是否声明发布操作者需具备仓库 `contents: write` 权限？[Completeness, Contract release-process.md]
- [x] CHK048 是否明确产物内容边界（不含源码/工程/测试/中间产物）？[Completeness, Contract release-artifact.md]
- [x] CHK049 首次发布（无上一 tag）的发布说明范围是否明确？[Coverage, Contract release-process.md]

## 产物验证与运维闭环（第四轮补充）

- [x] CHK050 是否明确发布冒烟以"从 zip 解压的副本"为执行对象（而非 dist 源目录）？[Completeness, Contract release-artifact.md / research D14]
- [x] CHK051 是否明确安装文档覆盖升级路径（重新注册指向新版本）与卸载方式？[Coverage, Contract codex-install.md]
- [x] CHK052 是否要求发布操作产生审计记录（notes/reviews/<version>-release.md）？[Completeness, Contract release-process.md]
- [x] CHK053 门禁是否覆盖发布脚本的语法/静态检查？[Gap→已补, Plan gates.ps1 扩展]
- [x] CHK054 是否明确本地多版本产物保留/清理策略与升级后的注册更新？[Clarity, Spec §Assumptions / Contract codex-install.md]

## 发布脚本契约（第五轮补充）

- [x] CHK055 发布脚本的参数、默认值与非法输入行为是否文档化（CLI 契约）？[Completeness, Contract release-cli.md]
- [x] CHK056 发布脚本是否定义统一退出码约定（0/1/2）与 `-DryRun` 预览模式？[Completeness, Contract release-cli.md]
- [x] CHK057 是否要求交叉校验 VERSION 内容、zip 名与 tag 版本一致（而非仅存在性）？[Clarity, Contract release-process.md / release-cli.md]
- [x] CHK058 是否明确冒烟必需 env（GITHUB_SMOKE_*）缺失时的失败行为，以及版本回退路径？[Coverage, Contract release-cli.md / codex-install.md]

## 文档一致性审计（第六轮补充）

- [x] CHK059 新增契约文件（release-cli.md）是否同步列入 plan 文档树与 quickstart 引用（引用完整性）？[Consistency, plan.md / quickstart.md]
- [x] CHK060 关键决策（1.0.0、框架依赖、解压副本冒烟、补建恢复、占位符示例）是否在各文档间交叉一致？[Consistency, spec / plan / contracts]

## Notes

- 本清单为"需求质量"单元测试：逐项回答"规格是否写清楚"，不做实现验证。
- 配套文件：`requirements.md`（spec 质量清单，由 $speckit-specify 生成），两者互补。
- 勾选标准：对应要求在 spec/plan/contracts/quickstart 中有明确、无歧义、可验证的表述；缺失项保留未勾选并在审查时标注 [Gap]。
- CHK006、CHK008 已通过 2026-08-11 的 plan 补齐关闭（release notes 格式与发布前置校验已落入 contracts/release-process.md 与 plan.md）。
- 2026-08-11 $speckit-plan checklist 逐项核验：40/40 覆盖；修复 spec §Edge Cases 残留的"自包含"表述（与框架依赖冲突），无遗留 [Gap]。
- 2026-08-11 第二轮检查追加 CHK041–CHK045：追溯（FR-012）、冒烟重试（FR-013）、注册示例占位符（FR-014）已确认并落入 spec/plan/contracts/quickstart。
- 2026-08-11 第三轮检查追加 CHK046–CHK049：半完成发布恢复（FR-015）、发布者权限、产物内容边界、首次发布说明范围已补齐。
- 2026-08-11 第四轮检查追加 CHK050–CHK054：解压副本冒烟、升级/卸载文档、发布审计、门禁语法检查、多版本保留策略已补齐；本轮无新增 clarify 决策（均为合理默认）。
- 2026-08-11 第五轮检查追加 CHK055–CHK058：发布脚本 CLI 契约（参数/退出码/DryRun）、VERSION 交叉校验、冒烟 env 必填、回退路径已补齐；新增契约文件 `contracts/release-cli.md`。
- 2026-08-11 第六轮检查追加 CHK059–CHK060：修复 release-cli.md 在 plan 文档树与 quickstart 引用中的遗漏；关键决策交叉一致。

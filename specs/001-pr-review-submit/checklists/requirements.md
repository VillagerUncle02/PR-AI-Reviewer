# Specification Quality Checklist: PR Review Submit（PR 审查结果上传）

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-09
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- 验证于 2026-08-09，全部条目通过，无需修订。
- 功能需求与成功指标保持技术中立；唯一的技术栈提及位于 Assumptions（用户明确要求记录的技术背景，仅供计划阶段参考），未进入功能需求与成功指标。
- FR-012 描述凭据的来源、登记与时效行为，属可测试的必需行为，未指定具体技术实现。
- 功能需求通过 User Story 验收场景与 Success Criteria 建立了可验证的验收映射，未在 spec 内嵌 checklist。

---

## Plan-Review Checklist（追加）

**Purpose**: 规划阶段对 spec / plan / contracts 的需求质量复查（完整性、清晰性、一致性、可测性、覆盖率）
**Created**: 2026-08-09
**Feature**: [spec.md](../spec.md) | [plan.md](../plan.md) | [contracts/](../contracts/README.md)

## Requirement Completeness

- [x] CHK001 是否 FR-008 列出的全部失败场景都被映射为工具契约中的独立、可操作错误码？[Completeness, Spec §FR-008, Tool Contract 错误码]
- [x] CHK002 令牌获取失败路径（JWT 签名、安装令牌交换）是否有需求级覆盖，而非仅存在于 plan 的项目结构？[Completeness, Gap, Data Model 状态转换]
- [x] CHK003 空评论列表（仅上传整体结论）是否被明确指定为合法场景，而非仅隐含在 schema 允许空数组？[Completeness, Tool Contract 输入 JSON Schema]
- [x] CHK004 GitHub 侧的数量/长度限制拒绝（评论数、正文超限）是否在需求中指定并映射到失败码？[Completeness, Gap, Tool Contract REVIEW_UNPROCESSABLE]
- [x] CHK005 非 GitHub 失败（MCP 传输错误、工具参数畸形、序列化失败）是否在调用结果契约中有明确归属？[Completeness, Gap]

## Requirement Clarity

- [x] CHK006 "可程序化解析的结果"是否用具体字段（status/reviewId/htmlUrl 与 status/code/message）定义？[Clarity, Spec §FR-007, Data Model 调用结果]
- [x] CHK007 "明确、可操作的失败原因"是否对每个错误类别落实为 code + message + 调用方可执行指引？[Clarity, Spec §FR-008, Tool Contract 错误码]
- [x] CHK008 "短期令牌"是否被量化（JWT / 安装令牌生命周期），还是仅存在于 plan 约束中？[Clarity, Spec §FR-012, Plan 约束]
- [x] CHK009 "整体原子性"是否以调用方可观察层面（整体成功或整体失败）定义，避免与实现方式耦合？[Clarity, Spec §FR-009]
- [x] CHK010 评论行位置语义（line + side，RIGHT/LEFT 含义）在 spec、data model 与 schema 之间是否无歧义？[Clarity, Spec §FR-003, Schema side]
- [x] CHK011 SC-008 的 30 秒目标是否定义明确的起止测量点与前提条件？[Clarity, Spec §SC-008, Assumptions]

## Requirement Consistency

- [x] CHK012 FR-004（以提交结果为准、不做只读预检）与 FR-008（已关闭/已合并 PR 失败）是否一致，含已合并 PR 的平台行为差异？[Consistency, Spec §FR-004, §FR-008, Research §10]
- [x] CHK013 FR-001（不得执行提交之外的其他 GitHub 操作）与已记录的令牌交换前置请求是否一致，张力是否已留痕供评审？[Consistency, Spec §FR-001, Plan 张力裁定]
- [x] CHK014 评论字段要求（path/line/side/body、trim 非空）在 FR-003、JSON schema 与 data model 中是否完全一致？[Consistency, Spec §FR-003, Schema, Data Model]
- [x] CHK015 无持久化要求在 FR-011、SC-007 与 plan 的存储约束之间是否一致？[Consistency, Spec §FR-011, Plan 存储]
- [x] CHK016 "仅评论"（COMMENT）事件限制在 spec（FR-013）、工具契约与 GitHub REST 契约之间是否一致？[Consistency, Spec §FR-013, Tool Contract]

## Acceptance Criteria Quality

- [x] CHK017 SC-001（100% 内容保真）能否不依赖人工查看 GitHub 页面而客观验证？[Measurability, Spec §SC-001]
- [x] CHK018 每条 User Story 是否都配有可独立执行、不含实现细节的验收场景？[Acceptance Criteria, Spec §US1/US2/US3]
- [x] CHK019 SC-005 的"0 次误提交"是否为每个失败类别定义了可核验的检查方式？[Measurability, Spec §SC-005]
- [x] CHK020 部分评论失败的验收场景是否在调用方可观察层面明确"无部分成功信号"？[Acceptance Criteria, Spec §US2, FR-009]

## Scenario Coverage

- [x] CHK021 主路径、备选路径（仅整体结论）、异常路径与调用方重试路径四类场景是否都在需求中体现？[Coverage, Spec §US1/US2/US3, Tool Contract]
- [x] CHK022 已合并 PR 的提交结果（平台可能接受）是否在需求中明确裁定，而非仅停留在 research/plan？[Coverage, Research §10, Plan 张力裁定]
- [x] CHK023 重复/并发提交是否被显式处理（如明确声明幂等性不在范围内）？[Coverage, Gap]
- [x] CHK024 "审查 → 修复 → 再审查 → 上传"的调用方工作流是否作为范围边界被记录？[Coverage, Spec Assumptions]

## Edge Case Coverage

- [x] CHK025 评论行边界条件（首/末行、删除行的 LEFT、行不在 diff hunk 内）是否在需求中定义？[Edge Case, Gap, Schema]
- [x] CHK026 纯空白正文/评论内容是否被校验需求显式覆盖？[Edge Case, Data Model 校验规则]
- [x] CHK027 App 安装被撤销、令牌过期、私钥缺失三类情况是否各自映射到独立错误码？[Edge Case, Spec §Edge Cases, Tool Contract]
- [x] CHK028 限流（429）与网络超时是否被指定为"不自动重试"的失败，并附调用方指引？[Edge Case, Spec §FR-010, Tool Contract RATE_LIMITED]

## Non-Functional Requirements

- [x] CHK029 私钥保护要求（位置、.gitignore、不硬编码、短期令牌）是否作为可测试需求陈述？[NFR, Spec §FR-012, Constitution 凭据安全]
- [x] CHK030 配置要求（环境变量注入、缺失/非法配置的失败行为）是否作为需求而非仅 plan 实现细节？[NFR, Gap, Plan 约束]
- [x] CHK031 在无持久化约束下，可观测性/日志边界是否被定义？[NFR, Gap, Spec §FR-011]
- [x] CHK032 性能要求是否显式附带前提（GitHub 可用、网络正常）？[NFR, Spec §SC-008, Assumptions]

## Dependencies & Assumptions

- [x] CHK033 外部依赖（App 已安装、API 可用、bot 标识）是否作为假设记录并带失败映射？[Dependency, Spec Assumptions]
- [x] CHK034 GitHub 平台行为假设（越界评论 422 原子拒绝、已合并 PR 可提交）是否带来源记录供评审？[Dependency, Research §6/§10, Plan 张力裁定]
- [x] CHK035 "GitHub 自动标记 bot 身份"的假设是否与 FR-006 一致并经过验证记录？[Assumption, Spec Assumptions, FR-006]

## Ambiguities & Conflicts

- [x] CHK036 FR-001 与令牌交换前置请求之间的张力是否在 spec 中解决，而非仅存在于 plan？[Conflict, Spec §FR-001, Plan 张力裁定]
- [x] CHK037 "整体原子性"的措辞是否与单次请求实现一致，或需要调用方可观察的定义？[Ambiguity, Spec §FR-009, Plan 摘要]
- [x] CHK038 八个错误码对于全部已记录失败场景是否互斥且穷尽？[Clarity, Tool Contract 错误码]

---

## Plan-Review 验证记录（$speckit-plan 复核，2026-08-09）

> 逐项核对 spec / plan / research / data-model / quickstart / contracts 后记录。PASS = 需求与设计产物已覆盖；PASS(契约级) = spec 未显式陈述，但契约/设计产物明确覆盖；FAIL = 需在 spec 层修订后关闭。核对期间对 quickstart / tool-contract / data-model / research / plan 做了小幅补丁（错误归属边界、side 语义、0 误提交核验、bot 标识来源、零日志写入同步等），见各文件标注的 CHK 编号。注：spec 于 2026-08-09 由用户修订（commit cb25aad + 工作区未提交澄清），本记录基于修订后 spec 核验；同日 spec 再次修订（FR-004 改为提交前读取 PR 状态并直接拒绝已合并/已关闭 PR、新增 FR-014、CHK022 裁定写入需求），本记录与全部规划产物已按 v2 契约同步更新；同日 spec 第三次修订（TOCTOU 竞态处理、提交超时歧义、FR-011 脱敏、FR-004/FR-014 细化），产物与记录已同步。

| CHK | 结论 | 依据 / 说明 |
|-----|------|-------------|
| CHK001 | PASS | tool-contract 错误码表（9 码）覆盖 FR-008 全部失败类别：目标/PR 不存在→TARGET_NOT_FOUND，PR 已合并/已关闭→PR_NOT_OPEN，App→APP_NOT_INSTALLED，凭据→CREDENTIALS_INVALID，载荷→INVALID_PAYLOAD，部分评论/422→REVIEW_UNPROCESSABLE，限流/网络→RATE_LIMITED/NETWORK_ERROR，兜底→UNEXPECTED_ERROR |
| CHK002 | PASS | data-model 状态机 Authenticated 行 + github-rest §1 错误映射 + tool-contract CREDENTIALS_INVALID（含私钥不可解析、JWT/令牌 401） |
| CHK003 | PASS | spec FR-003 + US1 场景 4（2026-08-09 修订）明确空评论列表合法；tool-contract 与 quickstart 场景 F 同步 |
| CHK004 | PASS | REVIEW_UNPROCESSABLE 触发含"数量/长度超限、次级限流（spammed）"；research §6 记录；具体数值由 GitHub 执行，未硬编码进需求 |
| CHK005 | PASS | 本次补丁：tool-contract 新增"错误归属边界"（INVALID_PAYLOAD / UNEXPECTED_ERROR / MCP 协议层）；quickstart 新增场景 E；spec Assumptions（第三次修订）明确 MCP 传输层错误不属工具结果契约范围 |
| CHK006 | PASS | 输出契约定死字段：success{status,reviewId,htmlUrl} / error{status,code,message,httpStatus?,details?}（tool-contract、data-model CallResult） |
| CHK007 | PASS | 错误码表每行含"调用方可采取动作"列（code + message + 指引） |
| CHK008 | PASS | JWT：iat=now-60s、exp≤now+10min；安装令牌 1h（GitHub 固定）——research §3、plan 约束、github-rest §1 量化一致 |
| CHK009 | PASS | FR-009 与边界场景以调用方可观察层面（整体成功/整体失败、无部分成功信号）定义，"实现方式以计划阶段确定"已解耦 |
| CHK010 | PASS | schema 与 quickstart 一致：RIGHT=新文件侧（新增/上下文行）、LEFT=旧文件侧（删除行）；本次补丁将同一语义写入 data-model side 行 |
| CHK011 | PASS | 本次补丁：quickstart SC-008 明确"自工具收到调用至返回结果"计时，前提 GitHub 可用/网络正常，不含客户端启动 |
| CHK012 | PASS | spec FR-004/FR-014（2026-08-09 再次修订）已在需求层解决：提交前读取 PR 状态、已合并/已关闭直接失败；FR-014 保留"不读文件改动列表"；research/plan/data-model/quickstart/github-rest 已同步 |
| CHK013 | PASS | spec FR-001（2026-08-09 修订）已在需求层排除认证必要请求与提交前 PR 状态读取；plan 张力裁定同步并留痕 |
| CHK014 | PASS | FR-003 + Key Entities（行位置含代码侧）、schema、data-model 一致；本次补丁 tool-contract 内联 schema 的 body 补"trim 后非空" |
| CHK015 | PASS | FR-011 / SC-007 / plan Storage N/A / data-model 无持久化声明一致 |
| CHK016 | PASS | FR-013 / tool-contract（event=COMMENT）/ github-rest（event 固定 COMMENT）一致 |
| CHK017 | PASS | 本次补丁：quickstart 场景 A 新增"GET reviews 接口客观比对"（脚本侧只读，非工具行为），不依赖人工看页面 |
| CHK018 | PASS | US1（3 条）/ US2（3 条）/ US3（2 条）均配可独立执行的验收场景，无实现细节 |
| CHK019 | PASS | 本次补丁：quickstart 场景 D 注明"调用前后查询 reviews 列表，数量与内容不变"核验 0 误提交 |
| CHK020 | PASS | US2 场景 3 + quickstart 场景 B 明确"不会部分成功、无部分成功信号" |
| CHK021 | PASS | 主路径（US1）、备选（空评论列表）、异常（US2）、调用方重试（US3）四类均在需求中体现 |
| CHK022 | PASS | spec Clarifications + FR-004 + Assumptions（2026-08-09 修订）明确"提交前读取 PR 状态，已合并/已关闭直接失败、不产生新 review"；research §10、github-rest §2、tool-contract PR_NOT_OPEN 已同步。已关闭 |
| CHK023 | PASS | spec Assumptions（2026-08-09 修订）明确"工具不保证幂等，去重与并发控制由调用方负责"；US3/FR-010 一致 |
| CHK024 | PASS | spec Assumptions 记录"审查→修复→再审查→上传"工作流与范围边界 |
| CHK025 | PASS | line 须落在 diff hunk 内（schema/data-model），范围外→整体 422→REVIEW_UNPROCESSABLE（spec 边界场景）；首/末行等具体边界由 GitHub 判定并映射 |
| CHK026 | PASS | spec 边界场景 + FR-003（2026-08-09 修订）显式覆盖纯空白内容为载荷无效；data-model trim 规则一致 |
| CHK027 | PASS | 撤销/无权限→APP_NOT_INSTALLED；令牌过期/JWT 拒绝→CREDENTIALS_INVALID；私钥缺失/不可解析→CREDENTIALS_INVALID（tool-contract） |
| CHK028 | PASS | RATE_LIMITED/NETWORK_ERROR 行注明"由调用方重试"；FR-010 禁止隐式重试；research §6 无自动补偿；NETWORK_ERROR 含"提交后超时 review 可能已创建"提示（第三次修订） |
| CHK029 | PASS | FR-012 为可测试需求陈述（位置、.gitignore 登记、短期令牌及时失效） |
| CHK030 | PASS(契约级) | 缺失/非法配置→CREDENTIALS_INVALID（tool-contract）；注入方式（环境变量）在 plan/quickstart，宪法要求"环境变量或本地安全存储"；如需将注入方式写入 spec 可经 clarify 补充 |
| CHK031 | PASS | spec FR-011/SC-007（2026-08-09 修订）明确"不允许任何日志写入、诊断仅经调用结果返回"；第三次修订新增"诊断信息不含令牌/私钥等敏感内容"，tool-contract 错误边界与 plan 约束已同步 |
| CHK032 | PASS | SC-008 显式附带前提（GitHub 可用、网络正常），Assumptions 同步记录 |
| CHK033 | PASS | spec Assumptions 记录 App 已安装授权、API 可用、bot 标识三项外部依赖，未满足时映射明确失败 |
| CHK034 | PASS | research §6（422 原子拒绝）与 §10（平台允许已合并 PR 提交 + TOCTOU 竞态说明）均带来源；spec FR-004 裁定提交前状态读取直接拒绝；第三次修订 Clarifications 确认 TOCTOU 竞态按平台结果如实返回 |
| CHK035 | PASS | 本次补丁：research §3 新增 bot 标识平台行为（{app-slug}[bot]）与来源，与 FR-006/FR-007 一致 |
| CHK036 | PASS | spec FR-001（2026-08-09 修订）已在需求层解决（认证必要请求与 PR 状态读取除外）；plan 张力裁定留痕 |
| CHK037 | PASS | "整体原子性"在 spec 中以调用方可观察层面定义（FR-009/边界场景），单次请求为实现方式，二者无冲突 |
| CHK038 | PASS | 现为 9 码（新增 PR_NOT_OPEN），按阶段/HTTP 状态互斥（401→CREDENTIALS_INVALID、403→APP_NOT_INSTALLED、404→TARGET_NOT_FOUND、状态≠open→PR_NOT_OPEN、422→REVIEW_UNPROCESSABLE、429→RATE_LIMITED、网络→NETWORK_ERROR、本地校验→INVALID_PAYLOAD），UNEXPECTED_ERROR 兜底穷尽；互斥穷尽性不变 |

**结论**：38 项中 38 项 PASS（其中 CHK030 为契约级 PASS）。CHK022 已于 spec 层修订关闭，无 FAIL 项。

**勾选更新**：2026-08-09 已按上述验证结论将全部 38 项标记为 `[x]`；CHK022 由用户在 spec 修订后勾选，全部关闭。

---

## Plan-Review Checklist（Round 2 · 继续检查）

**Purpose**: 基于 v2 修订（FR-001/FR-004/FR-011/FR-014、PR_NOT_OPEN、不幂等保证）继续需求质量检查
**Created**: 2026-08-09
**Feature**: [spec.md](../spec.md) | [plan.md](../plan.md) | [data-model.md](../data-model.md) | [contracts/](../contracts/README.md)
**Confirmed**: Round 1（CHK001–CHK038）已于 2026-08-09 全部确认，见上方验证记录

## Requirement Completeness

- [x] CHK039 提交前状态读取（FR-004）允许/禁止的端点边界是否在需求中定义（允许读取 state/merged，禁止文件改动列表等业务数据），避免 FR-004 与 FR-014 界限模糊？[Completeness, Clarity, Spec §FR-004/§FR-014, Gap]
- [x] CHK040 状态读取与提交之间的 TOCTOU 竞态（读取后 PR 被关闭/合并）是否在需求/假设中记录为接受的残余风险？[Completeness, Edge Case, Gap, Research §10]
- [x] CHK041 "PR 不存在/无权限"与"PR 已合并/已关闭"在状态读取阶段的失败映射（TARGET_NOT_FOUND vs PR_NOT_OPEN）是否在需求中定义？[Completeness, Clarity, Data Model TargetPullRequest]
- [x] CHK042 spec 在 2026-08-09 的两次修订（FR-001/FR-004/FR-011/FR-014 等）是否留有变更记录/版本标记，供评审追溯差异？[Traceability, Gap]
- [x] CHK043 提交请求已发出但响应超时（GitHub 可能已创建 review）的场景，是否在需求中与 NETWORK_ERROR 及调用方去重职责建立显式关联？[Completeness, Edge Case, Gap, Tool Contract NETWORK_ERROR]

## Requirement Clarity

- [x] CHK044 FR-004 的"PR 状态"是否定义了具体判定标准（state=open 可提交；merged/closed/不存在失败）？[Clarity, Spec §FR-004, Data Model Checked]
- [x] CHK045 FR-001 修订后的允许操作集合（认证必要请求 + 提交前状态读取）是否在需求中明确为封闭清单？[Clarity, Spec §FR-001]
- [x] CHK046 错误码表中的触发阶段标注（如 TARGET_NOT_FOUND=提交阶段）与 data-model 状态机的阶段归属（Checked 阶段）是否一致？[Consistency, Clarity, Tool Contract 错误码, Data Model 状态转换]
- [x] CHK047 "诊断信息仅通过调用结果返回"是否定义了诊断字段归属（code/message/details）及敏感内容禁令（不泄露令牌/私钥）？[Clarity, Spec §FR-011, Tool Contract]
- [x] CHK048 不幂等保证是否以调用方可理解的方式定义（每次调用独立生成一条 review，重复/并发可能产生多条）？[Clarity, Spec Assumptions]

## Requirement Consistency

- [x] CHK049 FR-004（提交前读状态）与 FR-001（除认证与状态读取外无其他 GitHub 操作）之间是否一致，无隐含操作？[Consistency, Spec §FR-001, §FR-004]
- [x] CHK050 SC-008 的 30 秒目标在新增状态读取请求（认证 + 状态读取 + 提交）后是否仍成立且测量点明确？[Consistency, Measurability, Spec §SC-008]
- [x] CHK051 9 个错误码在 spec 失败类别、data-model 状态机与 tool-contract 错误码表之间是否三方一致？[Consistency, Spec §FR-008, Data Model, Tool Contract]
- [x] CHK052 spec 修订后 plan/research/data-model/quickstart/contracts 是否全部同步，无残留旧表述（如"以提交结果为准、不预检 PR 状态"）？[Consistency, Gap]

## Acceptance Criteria Quality

- [x] CHK053 US2 验收场景 1 是否区分"PR 不存在"（TARGET_NOT_FOUND）与"PR 已合并/已关闭"（PR_NOT_OPEN）的可观察断言？[Acceptance Criteria, Spec §US2-S1]
- [x] CHK054 状态读取阶段失败（404/限流/网络）与提交阶段失败是否各自有独立可执行的验收场景？[Acceptance Criteria, Gap, Data Model 状态转换]
- [x] CHK055 SC-007"0 写入（含日志）"是否定义了可核验方式（如运行环境写入断言）？[Measurability, Spec §SC-007]

## Scenario Coverage

- [x] CHK056 是否覆盖"状态读取阶段 PR 打开、提交阶段 PR 已关闭/已合并"的 TOCTOU 竞态场景及结果归属？[Coverage, Edge Case, Gap, Research §10]
- [x] CHK057 状态读取响应异常（非 JSON、缺 state/merged 字段）是否定义失败归属（UNEXPECTED_ERROR）？[Coverage, Edge Case, Gap]
- [x] CHK058 提交返回 200 但响应体缺失/结构异常（成功判定依赖响应解析）是否定义处理归属？[Coverage, Gap]

## Edge Case Coverage

- [x] CHK059 状态读取 404 与提交 404 是否都明确映射 TARGET_NOT_FOUND？[Edge Case, Consistency, Tool Contract TARGET_NOT_FOUND]
- [x] CHK060 重复/并发调用产生多条 review 是否在需求中明确为预期行为（不幂等）而非缺陷？[Edge Case, Spec Assumptions]
- [x] CHK061 状态读取阶段返回 403（App 无权限/未安装）是否与认证阶段一样映射 APP_NOT_INSTALLED，并在状态机中明确？[Edge Case, Data Model 状态转换, Tool Contract APP_NOT_INSTALLED]

## Non-Functional Requirements

- [x] CHK062 GitHub App 所需最小权限范围（如 Pull requests: Read & Write）是否作为部署/假设要求记录？[NFR, Gap]
- [x] CHK063 环境变量注入方式与配置缺失/非法时的失败行为（CREDENTIALS_INVALID）是否已在需求或契约中明确（Round 1 CHK030 契约级 PASS 的后续）？[NFR, Gap, Tool Contract CREDENTIALS_INVALID]
- [x] CHK064 私钥文件夹的"本地受保护位置"是否具体化为可验证要求（文件系统权限、仅当前用户可读）？[NFR, Clarity, Spec §FR-012]

## Dependencies & Assumptions

- [x] CHK065 TOCTOU 竞态依赖（状态读取与提交非原子）是否在需求/假设中显式记录？[Dependency, Assumption, Research §10]
- [x] CHK066 GitHub PR 状态字段语义（state/merged）的可靠性是否作为外部依赖假设记录？[Assumption, Gap, Data Model Checked]
- [x] CHK067 MCP 传输层错误不属于工具结果契约的边界（CHK005 裁定）是否在需求/契约中明确定界？[Assumption, Tool Contract 错误归属边界]

## Ambiguities & Conflicts

- [x] CHK068 FR-014 禁止"读取文件改动列表"与状态读取响应附带元数据（如 changed_files 计数）之间的边界是否在需求层澄清？[Ambiguity, Spec §FR-014, Plan]
- [x] CHK069 "诊断信息仅通过调用结果返回"是否明确禁止回显 GitHub 错误响应中的敏感内容（令牌/私钥相关）？[Ambiguity, NFR, Gap]
- [x] CHK070 SC-005 的"目标不存在"与 FR-004 状态读取 404→TARGET_NOT_FOUND 的归属是否一致（0 误提交判定覆盖状态读取阶段）？[Conflict, Spec §SC-005, §FR-004]

---

## Plan-Review 验证记录（Round 2 · $speckit-plan 复核，2026-08-09）

> 基于 v2 / 第三次修订 spec 逐项核对 spec / plan / research / data-model / quickstart / contracts。PASS = 已覆盖；PASS(产物级) = 覆盖存在于 plan/research/contracts/quickstart 而非 spec 原文；PASS(契约级) = 覆盖存在于工具契约；PASS(部署级) = 覆盖存在于部署/验证指南；FAIL = 需 spec 层修订。核对期间对 tool-contract / github-rest / data-model / quickstart 做了补丁（错误码阶段标注、UNEXPECTED_ERROR 归属、状态机失败全集、App 最小权限、SC-007 核验方式等），见各文件标注的 CHK 编号。

| CHK | 结论 | 依据 / 说明 |
|-----|------|-------------|
| CHK039 | PASS | spec FR-004/FR-014 界定端点边界：状态读取仅 state/merged，文件改动列表等业务数据禁止读取 |
| CHK040 | PASS | spec Clarifications + Assumptions 记录 TOCTOU 竞态为接受的风险；research §10 同步 |
| CHK041 | PASS | github-rest §2：404→TARGET_NOT_FOUND、200+state≠open/merged→PR_NOT_OPEN；data-model Checked 行、spec FR-004 一致 |
| CHK042 | PASS(产物级) | 追溯记录存在于 plan 张力段落（标注 2026-08-09 修订）、research §3/§10、本清单验证记录头部与 commit cb25aad；spec 未内置 changelog，如需可后续在 spec 顶部加版本标记 |
| CHK043 | PASS | spec 边界场景 + tool-contract NETWORK_ERROR 行 + github-rest §3 均关联"超时可能已创建、调用方核验去重（不幂等）" |
| CHK044 | PASS | spec FR-004 明确 state=open 且未合并才可提交；data-model Checked 行一致 |
| CHK045 | PASS | spec FR-001 为封闭清单（认证必要请求 + 提交前状态读取） |
| CHK046 | PASS | 本次补丁：tool-contract 阶段标注更新（TARGET_NOT_FOUND=状态读取或提交、APP_NOT_INSTALLED=认证/状态读取/提交、CREDENTIALS_INVALID/RATE_LIMITED=任意请求阶段），与 data-model 状态机阶段归属一致 |
| CHK047 | PASS | spec FR-011 + tool-contract 错误归属边界（code/message/details、禁止令牌/私钥） |
| CHK048 | PASS | spec Assumptions 明确每次调用独立生成一条 review、重复/并发可能多条 |
| CHK049 | PASS | FR-001 允许集与 FR-004 状态读取一致，无隐含操作 |
| CHK050 | PASS | quickstart SC-008 测量点明确（自工具收到调用至返回结果、前提 GitHub 可用/网络正常）；新增状态读取为一次 GET，仍在 30s 前提内 |
| CHK051 | PASS | 9 码三方一致：spec FR-008 失败类别 ↔ data-model 状态机（含补丁后 Checked 行失败全集）↔ tool-contract 错误码表 |
| CHK052 | PASS | 全目录扫描："以提交结果为准"仅保留在 FR-014 评论范围与 research §10 被否决备选描述；无"不预检 PR 状态"残留 |
| CHK053 | PASS | spec US2-S1 要求"能区分目标不可提交"；quickstart 场景 D 给出 TARGET_NOT_FOUND vs PR_NOT_OPEN 的可观察断言 |
| CHK054 | PASS | 本次补丁：quickstart 注明 429/网络错误（含超时歧义）由自动化测试覆盖两阶段映射；404/PR_NOT_OPEN/422 分别对应场景 D/B |
| CHK055 | PASS | 本次补丁：quickstart SC-007 定义可核验方式（冒烟前后断言运行/临时目录无新增文件、进程退出无残留） |
| CHK056 | PASS | spec 边界场景 + research §10 覆盖 TOCTOU 场景及结果归属（按平台结果如实返回） |
| CHK057 | PASS | 本次补丁：github-rest §2 定义响应异常（非 JSON/缺 state 或 merged）→ UNEXPECTED_ERROR |
| CHK058 | PASS | 本次补丁：tool-contract UNEXPECTED_ERROR 行 + github-rest §3 定义"提交 200 但响应体异常→UNEXPECTED_ERROR 并提示可能已创建" |
| CHK059 | PASS | github-rest §2 与 §3 的 404 均映射 TARGET_NOT_FOUND |
| CHK060 | PASS | spec Assumptions 明确不幂等为预期行为（非缺陷） |
| CHK061 | PASS | 本次补丁：data-model Checked 行补全失败全集（含 APP_NOT_INSTALLED），与 github-rest §2 403→APP_NOT_INSTALLED 一致 |
| CHK062 | PASS | 本次补丁：quickstart 前置条件明确 Pull requests Read & Write（Write=提交、Read=状态读取） |
| CHK063 | PASS(契约级) | 本次补丁：tool-contract CREDENTIALS_INVALID 触发明确"配置缺失（App ID、安装 ID、私钥路径无效）"；注入方式见 plan/quickstart 环境变量；Round 1 CHK030 的后续已关闭 |
| CHK064 | PASS(部署级) | 本次补丁：quickstart 前置条件补充私钥文件权限建议（仅当前用户可读/ACL/chmod 600）；spec FR-012 保持"本地受保护位置"表述，如需写入 spec 可经 clarify |
| CHK065 | PASS | spec Assumptions 显式记录状态读取与提交非原子的依赖 |
| CHK066 | PASS | spec Assumptions 记录 state/merged 语义以 GitHub 返回为准 |
| CHK067 | PASS | spec Assumptions + tool-contract 错误归属边界明确定界 |
| CHK068 | PASS | spec FR-014 明确状态读取附带元数据不用于任何校验 |
| CHK069 | PASS | spec FR-011 + tool-contract 边界禁止回显敏感内容 |
| CHK070 | PASS | SC-005 的 0 误提交判定覆盖状态读取阶段（404→TARGET_NOT_FOUND，无 review 创建） |

**结论**：Round 2（CHK039–CHK070）32 项全部 PASS（其中 CHK042 为产物级、CHK063 为契约级、CHK064 为部署级），无 FAIL；16 项原未勾选项已按验证结果标记 `[x]`。

---

## Plan-Review Checklist（Round 3 · 继续检查）

**Purpose**: 在第三次修订 spec（v2 契约）基础上继续检查——语义张力、剩余边界、可追溯性与部署/安全细化
**Created**: 2026-08-09
**Feature**: [spec.md](../spec.md) | [plan.md](../plan.md) | [data-model.md](../data-model.md) | [contracts/](../contracts/README.md)
**Confirmed**: Round 2（CHK039–CHK070）已于 2026-08-09 全部确认，见上方验证记录

## Requirement Completeness

- [x] CHK071 单一安装 ID 的约束（installation_id 来自配置，目标仓库必须位于该安装授权范围）是否在需求中明确，且多安装/多租户明确为范围外？[Completeness, Scope, Gap, Spec Assumptions]
- [x] CHK072 可配置 API BaseUrl 的边界（默认生产 api.github.com、仅测试/代理使用、必须 HTTPS）是否在需求或契约中限定？[Completeness, NFR, Security, Gap, Plan 配置]
- [x] CHK073 同一评论列表内重复 path+line+side（同位置多条评论）的处理是否在需求或契约中定义？[Completeness, Edge Case, Gap]
- [x] CHK074 comments 显式传 null（区别于缺失与空数组）是否定义校验归属（INVALID_PAYLOAD）？[Completeness, Edge Case, Gap, Schema]
- [x] CHK075 非 ASCII/Unicode 审查内容（中文、emoji）是否定义编码要求（UTF-8 原样透传）？[Completeness, Edge Case, Gap]

## Requirement Clarity

- [x] CHK076 trim 校验（FR-003）与"不得修改审查内容"（FR-005）的提交语义是否明确——trim 仅用于校验、提交保持调用方原始内容？[Ambiguity, Conflict, Spec §FR-003/§FR-005]
- [x] CHK077 "仅评论事件"（event=COMMENT，FR-013）与成功响应状态（state=COMMENTED）的术语是否在需求与契约中统一？[Clarity, Consistency, Spec §FR-013, github-rest §3]
- [x] CHK078 "file change 范围"（spec）与"diff hunk"（schema/data-model）是否统一为单一术语并定义含义？[Clarity, Consistency, Spec §FR-014, Schema]
- [x] CHK079 SC-008 的 30 秒总预算是否定义认证/状态读取/提交各阶段的超时分配规则？[Clarity, Measurability, Gap, Spec §SC-008]
- [x] CHK080 details 字段是否定义长度/截断策略（如 GitHub 422 的 message/errors 透传上限）？[Clarity, NFR, Gap, github-rest §3]
- [x] CHK081 429 响应的 Retry-After 是否定义透传（details）与调用方重试指引？[Clarity, Gap, Tool Contract RATE_LIMITED]

## Requirement Consistency

- [x] CHK082 FR-008 的文字失败类别与错误码名称（TARGET_NOT_FOUND/PR_NOT_OPEN 等）是否在需求层建立显式引用？[Consistency, Traceability, Gap, Spec §FR-008]
- [x] CHK083 机器可读 schema 与 tool-contract 内联 JSON 是否以单一事实源为准并保持同步？[Consistency, Traceability, Gap]
- [x] CHK084 quickstart 验证场景（A–F）与 US 验收场景/SC 之间是否建立显式编号映射？[Consistency, Traceability, Gap]
- [x] CHK085 工具 description 与 schema/data-model 对空评论列表与 side 语义的说明是否一致？[Consistency, Tool Contract, Schema, Data Model]

## Acceptance Criteria Quality

- [x] CHK086 SC-003 的 bot 标识是否定义客观断言（如响应 user.type=Bot）？[Acceptance Criteria, Measurability, Gap, Spec §SC-003]
- [x] CHK087 SC-002 是否定义每条评论"文件 + 行 + side"的逐条比对断言？[Acceptance Criteria, Measurability, Gap, Spec §SC-002]
- [x] CHK088 SC-009 的"程序化解析"是否覆盖错误响应全部字段（code/message/httpStatus?/details?）的可解析断言？[Acceptance Criteria, Measurability, Gap, Spec §SC-009]

## Scenario Coverage

- [x] CHK089 令牌在调用中途过期（认证成功后、提交阶段 401）是否定义归属（CREDENTIALS_INVALID）与重试指引？[Coverage, Edge Case, Gap, Tool Contract CREDENTIALS_INVALID]
- [x] CHK090 目标 PR 在状态读取后被删除或仓库被转移（提交 404 → TARGET_NOT_FOUND）是否作为竞态场景覆盖？[Coverage, Edge Case, Gap]
- [x] CHK091 配置或私钥文件在两次调用之间变更的生效语义（无状态按调用读取 vs 需重启）是否定义？[Coverage, Clarity, Gap, Plan 配置]

## Edge Case Coverage

- [x] CHK092 path 含 ../ 或绝对路径等异常格式是否定义处理边界（本地仅校验非空、路径规范性以 GitHub 判定为准）？[Edge Case, Clarity, Gap, Schema]
- [x] CHK093 pullNumber 超出合理范围（超大整数）是否定义校验上限或说明由 GitHub 判定？[Edge Case, Gap, Schema]
- [x] CHK094 状态读取返回 200 但 state 或 merged 为 null（字段存在但为空）是否定义失败归属（UNEXPECTED_ERROR）？[Edge Case, Gap, github-rest §2]

## Non-Functional Requirements

- [x] CHK095 安装令牌的最小权限范围（repositories 限定 + 所需权限）是否在需求/部署指南中显式声明并可核验？[NFR, Security, Gap, github-rest §1]
- [x] CHK096 配置缺失/非法时 MCP server 的启动行为（启动即失败 vs 调用时失败）是否在需求中定义？[NFR, Clarity, Gap, Plan 配置]
- [x] CHK097 错误 message 的语言/格式约定（面向 Agent 的可解析文本）是否在契约中明确？[NFR, Clarity, Gap, Tool Contract 输出契约]

## Dependencies & Assumptions

- [x] CHK098 GitHub API 版本头（X-GitHub-Api-Version）是否作为依赖假设记录，并定义响应格式变化时的处理？[Dependency, Assumption, github-rest]
- [x] CHK099 工具契约版本（v2 ↔ FR-001~FR-014）与 spec 修订版本之间的映射是否在文档中可追溯？[Traceability, Assumption, Tool Contract 版本]

## Ambiguities & Conflicts

- [x] CHK100 竞态下"按平台结果如实返回成功"与 SC-005"0 误提交"的边界是否在需求中无歧义（平台接受的成功不属于误提交）？[Conflict, Spec §SC-005, Assumptions]
- [x] CHK101 SC-001"内容与输入完全一致"是否明确为语义一致（非字节级一致），与 FR-005"原样透传"的表述一致？[Ambiguity, Spec §SC-001, §FR-005]

---

## Plan-Review 验证记录（Round 3 · $speckit-plan 复核，2026-08-09）

> 基于第四次修订 spec（FR-001~FR-016、单一安装、UTF-8 原样提交、SC-001/SC-005 细化）逐项核对。PASS = 已覆盖；PASS(契约级) = 覆盖存在于工具/REST 契约；PASS(部署级) = 覆盖存在于部署/验证指南；FAIL = 需 spec 层修订。核对期间对 tool-contract / github-rest / submit-review.schema.json / data-model / plan / quickstart / research 做了补丁（版本映射 v3、术语统一、Retry-After/details/消息约定、启动校验、BaseUrl 固定等），见各文件标注的 CHK 编号。

| CHK | 结论 | 依据 / 说明 |
|-----|------|-------------|
| CHK071 | PASS | spec Assumptions 明确 v1 单一安装：installation_id 来自配置、目标仓库须在授权范围内、多安装/多租户范围外 |
| CHK072 | PASS | spec FR-016 固定生产 api.github.com、不可配置；github-rest 头部与 plan 约束同步 |
| CHK073 | PASS | 本次补丁：tool-contract 输入说明——同位置重复 path+line+side 合法、原样透传（FR-005）、不去重 |
| CHK074 | PASS | 本次补丁：tool-contract 输入说明——comments 显式 null → INVALID_PAYLOAD（区别于缺失与空数组） |
| CHK075 | PASS | spec Assumptions 明确 UTF-8 原样提交；data-model/tool-contract 同步 |
| CHK076 | PASS | spec FR-003 明确"trim 仅用于有效性判断、提交保持原始内容"；data-model/tool-contract 同步 |
| CHK077 | PASS | 本次补丁：github-rest §3 说明 event=COMMENT（请求）与 state=COMMENTED（回执）一一对应 |
| CHK078 | PASS | 本次补丁：统一术语"file change 范围"并定义含义——schema line/path 描述、data-model 说明、github-rest §3 全部同步 |
| CHK079 | PASS | 本次补丁：plan Performance Goals——三请求各设 HttpClient 超时（建议 10s），总预算 ≤30s（SC-008） |
| CHK080 | PASS | 本次补丁：tool-contract 消息约定——details ≤2048 字符、超出截断附省略标记、保持可解析 |
| CHK081 | PASS | 本次补丁：tool-contract RATE_LIMITED 行 + github-rest 429 行——Retry-After 透传至 details.retryAfterSeconds、调用方据此重试 |
| CHK082 | PASS(契约级) | 本次补丁：tool-contract 错误码表头注明"FR-008 失败类别的契约化映射"；spec 保持技术中立（文字类别），错误码归属契约 |
| CHK083 | PASS | 本次补丁：tool-contract 输入说明声明 submit-review.schema.json 为单一事实源、内联 JSON 为文档快照 |
| CHK084 | PASS | 本次补丁：quickstart 验收对照新增显式映射——US1→A/F、US2→B/C/D、US3→D/E+自动化测试 |
| CHK085 | PASS | 本次补丁：tool-contract description 补"(comments 可空)"；空评论与 side 语义在 description/schema/data-model 一致 |
| CHK086 | PASS | 本次补丁：quickstart 场景 A 步骤 3 增加 user.type=Bot 客观断言（SC-003） |
| CHK087 | PASS | 本次补丁：quickstart 场景 A 步骤 3 明确逐条比对 path/line/side/body（SC-002） |
| CHK088 | PASS | 本次补丁：quickstart SC-009 行——错误响应断言含 code/message，httpStatus/details 出现时类型合法 |
| CHK089 | PASS | spec 边界场景（提交阶段 401）+ github-rest §3 401 行 → CREDENTIALS_INVALID |
| CHK090 | PASS | spec 边界场景（提交 404 竞态）+ github-rest §3 404 行 → TARGET_NOT_FOUND，已接受竞态 |
| CHK091 | PASS | 本次补丁：plan 约束——环境变量配置启动时读取（变更需重启生效）；私钥文件按调用读取（解析失败 → CREDENTIALS_INVALID） |
| CHK092 | PASS | 本次补丁：tool-contract 输入说明 + schema path 描述——本地仅校验非空、路径规范性（../、绝对路径等）由 GitHub 判定 |
| CHK093 | PASS | 本次补丁：tool-contract 输入说明——pullNumber 不设本地上限，超出平台范围由状态读取 404 → TARGET_NOT_FOUND 判定 |
| CHK094 | PASS | 本次补丁：github-rest §2——state/merged 为 null 或类型异常 → UNEXPECTED_ERROR |
| CHK095 | PASS | 本次补丁：github-rest §1 最小权限声明（Pull requests Read & Write + repositories 限定）+ quickstart 前置条件 |
| CHK096 | PASS | spec FR-015 明确启动即失败；tool-contract 错误归属边界、plan/quickstart 同步 |
| CHK097 | PASS | 本次补丁：tool-contract 消息约定——简体中文、原因+动作、≤512 字符、Agent 可解析 |
| CHK098 | PASS | 本次补丁：github-rest 头部——X-GitHub-Api-Version 固定发送、响应格式变化→UNEXPECTED_ERROR 并需契约升级 |
| CHK099 | PASS | 本次补丁：tool-contract/github-rest 版本升为 v3，映射"v3 ↔ spec FR-001~FR-016（2026-08-09 第四次修订）"可追溯 |
| CHK100 | PASS | spec SC-005 明确"竞态下平台接受并成功创建的 review 不视为误提交"；research §10/quickstart 同步 |
| CHK101 | PASS | spec SC-001 明确"语义一致（内容原样提交、不承诺渲染层字节级一致）"；quickstart 场景 A 比对措辞同步 |

**结论**：Round 3（CHK071–CHK101）31 项全部 PASS（其中 CHK082 为契约级），无 FAIL；22 项原未勾选项已按验证结果标记 `[x]`。

---

## Plan-Review Checklist（Round 4 · 继续检查）

**Purpose**: 在第四次修订 spec（FR-015/FR-016、契约 v3）基础上继续检查——新增需求语义、消息/输出契约细节、并发与安全边界
**Created**: 2026-08-09
**Feature**: [spec.md](../spec.md) | [plan.md](../plan.md) | [data-model.md](../data-model.md) | [contracts/](../contracts/README.md)
**Confirmed**: Round 3（CHK071–CHK101）已于 2026-08-09 全部确认，见上方验证记录

## Requirement Completeness

- [x] CHK102 私钥读取与解析时机（FR-015 启动时校验 vs plan"每次调用按需生成令牌"）是否在需求中明确：启动仅校验可解析性、调用时重新读取，或启动缓存并说明与无状态的一致性？[Completeness, Conflict, Spec §FR-012/§FR-015, Plan 约束]
- [x] CHK103 draft PR（state=open 但为草稿）是否定义处理（FR-004 仅判 state/merged，draft 不拒绝）？[Completeness, Edge Case, Gap, Spec §FR-004]
- [x] CHK104 owner/repo 大小写与规范化（原样透传 vs 归一化）是否在需求或契约中明确？[Completeness, Edge Case, Gap, Schema]
- [x] CHK105 本机时钟偏差对 JWT 签发的影响（iat/exp 导致的 401 → CREDENTIALS_INVALID）是否作为已知失败/假设记录？[Completeness, Assumption, Gap, github-rest §1]

## Requirement Clarity

- [x] CHK106 FR-015"启动即失败"的退出行为（非零退出码 + stderr 明确错误）是否在需求/契约中统一定义，并明确与 FR-011"零写入"不冲突（stderr 属进程输出而非持久化）？[Clarity, Conflict, Spec §FR-011/§FR-015, Tool Contract 错误归属边界]
- [x] CHK107 message ≤512 字符与 details ≤2048 字符的计数单位（字符 vs 字节，多字节/emoji）是否定义？[Clarity, Gap, Tool Contract 消息约定]
- [x] CHK108 details.retryAfterSeconds 的转换规则（GitHub Retry-After 头为秒数或 HTTP 日期）是否明确？[Clarity, Gap, Tool Contract RATE_LIMITED]
- [x] CHK109 details 透传 GitHub 原始 message/errors 时，是否明确同样受敏感内容禁令约束（FR-011）？[Clarity, NFR, Gap, Spec §FR-011]
- [x] CHK110 FR-016 固定生产地址下，测试经接口替身的边界是否明确（替身属测试范围、不作为产品配置暴露）？[Clarity, Scope, Gap, Spec Assumptions]

## Requirement Consistency

- [x] CHK111 FR-015"配置缺失/非法 → 启动即失败"与错误码表 CREDENTIALS_INVALID 触发"配置缺失"之间是否一致（启动期失败与调用期错误码的归属如何区分）？[Consistency, Spec §FR-015, Tool Contract CREDENTIALS_INVALID]
- [x] CHK112 FR-016（固定生产地址）与 plan 配置中的 API BaseUrl 项是否一致（移除或标注为不可配置）？[Consistency, Spec §FR-016, Plan 配置]
- [x] CHK113 plan 测试策略是否覆盖 FR-015（启动配置校验）与 FR-016（BaseUrl 固定）的验证？[Consistency, Coverage, Plan Testing]
- [x] CHK114 tool-contract 的启动失败行为（非零退出码 + stderr）在 plan/quickstart 中是否同步表述，无"可启动但不可用"残留？[Consistency, Gap, Plan, Quickstart]

## Acceptance Criteria Quality

- [x] CHK115 FR-015 是否有独立验收场景（配置缺失/非法 → 启动失败、明确错误、不进入工具调用）？[Acceptance Criteria, Gap, Spec §FR-015]
- [x] CHK116 SC-002 的评论位置核验是否以脚本断言定义（非人工核对），与 SC-009"无需人工查看 GitHub 页面"一致？[Acceptance Criteria, Measurability, Gap, Spec §SC-002/§SC-009]
- [x] CHK117 每个错误码是否可映射到 quickstart 可执行场景或自动化测试用例（错误码 ↔ 验证场景双向追踪）？[Acceptance Criteria, Traceability, Gap]

## Scenario Coverage

- [x] CHK118 是否覆盖"同一进程内并发多次调用"（各自独立认证、无共享状态）？[Coverage, Gap, Spec §FR-011, Plan 约束]
- [x] CHK119 是否覆盖"启动成功但运行中私钥被删除/替换"（调用时读取失败 → CREDENTIALS_INVALID）？[Coverage, Edge Case, Gap, Tool Contract CREDENTIALS_INVALID]
- [x] CHK120 是否覆盖"提交阶段返回 301/302 重定向"的处理策略（跟随 vs 拒绝）？[Coverage, Edge Case, Gap, github-rest]
- [x] CHK121 是否覆盖"GitHub 对无权限仓库回 404 防泄露"导致的 owner/repo 拼写错误与无权限不可区分（统一 TARGET_NOT_FOUND）的调用方告知？[Coverage, Clarity, Gap, github-rest §2]

## Edge Case Coverage

- [x] CHK122 状态读取或提交返回 200 但 Content-Type 非 JSON 时，是否定义归属（UNEXPECTED_ERROR）？[Edge Case, Gap, github-rest §2/§3]
- [x] CHK123 提交 422 的 errors 数组含多条错误时，details 透传全部还是仅首条，是否定义？[Edge Case, Clarity, Gap, github-rest §3]
- [x] CHK124 同为 404 在不同阶段的区分（认证阶段 → APP_NOT_INSTALLED、状态读取/提交 → TARGET_NOT_FOUND）是否在契约中明确？[Edge Case, Consistency, github-rest §1/§2/§3]

## Non-Functional Requirements

- [x] CHK125 TLS 证书校验是否明确必须启用（不得禁用校验）？[NFR, Security, Gap]
- [x] CHK126 进程运行期配置不可变（配置启动时固化、运行中环境变量变更不生效）是否声明？[NFR, Clarity, Gap, Spec §FR-015]
- [x] CHK127 SC-008 的 30 秒是否明确不含启动校验（FR-015）时间（"从调用到返回"的测量边界）？[NFR, Measurability, Gap, Spec §SC-008/§FR-015]

## Dependencies & Assumptions

- [x] CHK128 GitHub API 响应字段（state/merged/user.type/errors）的稳定性是否作为依赖假设记录，字段变化 → UNEXPECTED_ERROR 并需契约升级？[Dependency, Assumption, github-rest]
- [x] CHK129 MCP 客户端对"单个文本 JSON 内容块"输出格式的集成假设是否记录？[Dependency, Assumption, Tool Contract 传输]

## Ambiguities & Conflicts

- [x] CHK130 "启动即失败"（FR-015）与 MCP stdio 服务器由客户端按需拉起的运行模式是否兼容（配置错误在首次调用时才暴露为启动失败，调用方如何感知）？[Conflict, Ambiguity, Spec §FR-015, Plan 目标平台]
- [x] CHK131 SC-001"语义一致"与 FR-005"原样透传"对首尾空白内容的处理是否明确（trim 仅校验、原样提交后平台渲染不承诺保留空白）？[Ambiguity, Spec §SC-001, §FR-003/§FR-005]
- [x] CHK132 错误码表作为 FR-008 的契约化映射（spec 保持技术中立）——新增错误码时是否需经 spec 修订流程（契约演进与需求基线的一致性）？[Ambiguity, Governance, Gap, Tool Contract 错误码]

---

## Plan-Review 验证记录（Round 4 · $speckit-plan 复核，2026-08-09）

> 基于第五次修订 spec（draft 不拒绝、FR-011 透传细节脱敏、FR-015 非零退出码、FR-016 TLS、私钥按调用读取、并发独立）逐项核对。PASS = 已覆盖；FAIL = 需 spec 层修订。核对期间对 tool-contract / github-rest / data-model / plan / quickstart / research 做了补丁（重定向、Content-Type、字符计数、Retry-After 转换、404 分阶段、错误码归属、测试覆盖等），见各文件标注的 CHK 编号。

| CHK | 结论 | 依据 / 说明 |
|-----|------|-------------|
| CHK102 | PASS | spec Assumptions 明确"启动仅校验存在与可解析性、调用时按需重新读取并生成短期令牌、不缓存"；plan 约束同步 |
| CHK103 | PASS | spec FR-004 + 边界场景明确 draft 不单独拒绝、不读取 draft 字段；github-rest §2 / data-model Checked 行同步 |
| CHK104 | PASS | 本次补丁：tool-contract 输入说明——owner/repo 原样透传、不做大小写归一化、GitHub 大小写不敏感、统一 TARGET_NOT_FOUND |
| CHK105 | PASS | 本次补丁：github-rest §1 + research §3——JWT iat 60s 余量缓解时钟偏差，偏差过大 401 → CREDENTIALS_INVALID |
| CHK106 | PASS | spec FR-015 明确非零退出码 + stderr、与 FR-011 零写入不冲突（stderr 属进程输出）；tool-contract/plan/quickstart 同步 |
| CHK107 | PASS | 本次补丁：tool-contract 消息约定——计数单位为 Unicode 字符（非字节）、emoji 按 1 字符、截断不切断代理对 |
| CHK108 | PASS | 本次补丁：tool-contract RATE_LIMITED 行——Retry-After 为秒数直接透传、为 HTTP 日期转换为距当前秒数，无法解析则省略 |
| CHK109 | PASS | spec FR-011 明确含透传的 GitHub 错误详情；tool-contract 消息约定注明 details 透传前过滤敏感内容 |
| CHK110 | PASS | 本次补丁：plan 约束 + research §3——接口替身仅用于测试（注入 IGitHubReviewClient），不作为产品配置暴露 |
| CHK111 | PASS | 本次补丁：tool-contract CREDENTIALS_INVALID 行移除"配置缺失"触发并注明"启动期配置失败属 FR-015，不产生本码"，归属区分明确 |
| CHK112 | PASS | plan GitHubAppOptions 已标注"API 地址固定 api.github.com（FR-016）"，无 BaseUrl 配置项残留 |
| CHK113 | PASS | 本次补丁：plan Testing——增启动配置校验测试（FR-015）与 BaseUrl/TLS 固定断言（FR-016） |
| CHK114 | PASS | tool-contract/plan/quickstart 统一"非零退出码 + stderr"表述；全目录扫描无"可启动但不可用"残留 |
| CHK115 | PASS | spec US2 场景 4 为独立启动失败验收（非零退出码 + stderr、不进入工具调用）；quickstart 自动化验证同步 |
| CHK116 | PASS | 本次补丁：quickstart 场景 A 步骤 3 注明"均为脚本断言、非人工核对"，与 SC-009 一致 |
| CHK117 | PASS | 本次补丁：quickstart 新增"错误码验证映射"表——9 个错误码 ↔ 场景/自动化测试双向追踪 |
| CHK118 | PASS | spec Assumptions 明确并发调用相互独立（各自认证、无共享状态、不复用令牌）；plan 约束同步 |
| CHK119 | PASS | spec 边界场景（运行期私钥被删除/替换）+ tool-contract CREDENTIALS_INVALID 行（调用时读取/解析失败） |
| CHK120 | PASS | 本次补丁：github-rest 头部——跟随标准 301/302（AllowAutoRedirect、≤3 跳、保留 Authorization），链异常 → 按状态映射 |
| CHK121 | PASS | 本次补丁：tool-contract TARGET_NOT_FOUND 行注明"无法区分拼写错误与无权限（平台防枚举）" |
| CHK122 | PASS | 本次补丁：github-rest §2/§3——200 但 Content-Type 非 JSON 或反序列化失败 → UNEXPECTED_ERROR |
| CHK123 | PASS | 本次补丁：github-rest §3——422 errors 数组透传全部条目（受 2048 字符截断约束） |
| CHK124 | PASS | 本次补丁：github-rest 头部注明 404 分阶段区分——认证阶段→APP_NOT_INSTALLED、状态读取/提交→TARGET_NOT_FOUND |
| CHK125 | PASS | spec FR-016 明确 TLS 证书校验必须启用、不得禁用；github-rest 头部/plan 约束/research §3 同步 |
| CHK126 | PASS | 本次补丁：plan 约束——配置启动时固化，运行期环境变量变更不生效、需重启 |
| CHK127 | PASS | 本次补丁：quickstart SC-008——计时不含 MCP 客户端启动与启动校验（FR-015）时间 |
| CHK128 | PASS | 本次补丁：github-rest 头部——state/merged/user.type/errors 字段稳定性为依赖假设，缺失/类型变化 → UNEXPECTED_ERROR 并需契约升级 |
| CHK129 | PASS | 本次补丁：tool-contract 新增"MCP 集成假设"——单文本 JSON 内容块、解析失败不在契约范围 |
| CHK130 | PASS | spec Clarifications 明确"启动即失败"与 stdio 按需拉起兼容（非零退出码 + stderr，由宿主/调用方环境暴露）；plan 目标平台已注明 |
| CHK131 | PASS | spec SC-001 语义一致 + FR-003/FR-005 trim 仅校验、原样提交，平台渲染不承诺保留空白；data-model/tool-contract 同步 |
| CHK132 | PASS | 本次补丁：tool-contract 错误码表头注明"新增错误码/修订语义须先经 spec 修订再同步契约版本"，演进流程可追溯 |

**结论**：Round 4（CHK102–CHK132）31 项全部 PASS，无 FAIL；21 项原未勾选项已按验证结果标记 `[x]`。

---

## Plan-Review Checklist（Round 5 · 继续检查）

**Purpose**: 在第四次修订 spec / 契约 v3 基础上继续检查——契约缺口（5xx、令牌交换解析）、文档间引用一致性、门禁与治理、并发与安全边界
**Created**: 2026-08-09
**Feature**: [spec.md](../spec.md) | [plan.md](../plan.md) | [data-model.md](../data-model.md) | [contracts/](../contracts/README.md)
**Confirmed**: Round 4（CHK102–CHK132）已于 2026-08-09 全部确认，见上方验证记录

## Requirement Completeness

- [x] CHK133 令牌交换（github-rest §1）返回 201 但响应缺 token/expires_at 时，是否定义失败归属（UNEXPECTED_ERROR）？[Completeness, Gap, github-rest §1]
- [x] CHK134 GitHub API 5xx（502/503/504 等）是否定义失败归属（NETWORK_ERROR vs UNEXPECTED_ERROR）？[Completeness, Edge Case, Gap, github-rest 错误映射]
- [x] CHK135 空 PR（无文件改动）场景是否定义（仅 body 可成功、含评论时 422 → REVIEW_UNPROCESSABLE）？[Completeness, Edge Case, Gap]
- [x] CHK136 同一文件同时含 LEFT 与 RIGHT 评论的组合是否在需求或契约中定义？[Completeness, Edge Case, Gap, Schema side]
- [ ] CHK137 私钥轮换的运维流程（更换私钥后需重启、旧私钥失效）是否作为操作假设记录？[Completeness, Assumption, Gap, Spec §FR-012]

## Requirement Clarity

- [x] CHK138 FR-015 的启动校验是否明确为仅本地校验（不发网络请求、不做令牌交换预检），与"启动即失败"的快速反馈一致？[Clarity, Gap, Spec §FR-015]
- [x] CHK139 错误结果是否定义机器可读的"可重试"提示（如 details.retryable 或按错误码约定），简化调用方分支？[Clarity, Gap, Tool Contract 输出契约]
- [x] CHK140 用户输入"上传审查结果"与需求/契约"提交 review"的术语是否统一？[Clarity, Consistency, Spec Input]
- [x] CHK141 并发调用共享同一安装令牌的 GitHub 核心限流配额，RATE_LIMITED 指引是否考虑并发场景？[Clarity, NFR, Gap, Tool Contract RATE_LIMITED]
- [x] CHK142 MCP 客户端实际暴露的工具 schema（SDK 生成）与契约 schema 是否保证一致（单一事实源或一致性测试断言）？[Clarity, Consistency, Traceability, Gap, Tool Contract 输入 JSON Schema]

## Requirement Consistency

- [x] CHK143 文档内 CHK 交叉引用（如 plan 项目结构标注"CHK001~038"、tool-contract 中 CHK005/CHK074 等）是否随清单增长保持同步，避免过期引用？[Consistency, Traceability, Gap, Plan 项目结构]
- [x] CHK144 github-rest §1 的令牌交换错误映射是否与 §2/§3 的解析失败定义（UNEXPECTED_ERROR）保持一致（§1 缺该定义）？[Consistency, Completeness, github-rest]
- [x] CHK145 spec"第四次修订"与 tool-contract/github-rest 的 v3 版本映射，是否与 data-model/plan/quickstart 的版本标注一致？[Consistency, Traceability, Gap]
- [x] CHK146 spec Status=Draft 是否定义了转为正式/合入状态的门禁（如全部 checklist 项 PASS、宪法合规声明、评审通过）？[Consistency, Governance, Gap]

## Acceptance Criteria Quality

- [x] CHK147 FR-016（固定生产地址、不可配置）是否有可验证的验收方式（如无配置覆盖项、测试经替身完成）？[Acceptance Criteria, Gap, Spec §FR-016]
- [x] CHK148 5xx 与令牌交换解析失败（CHK133/CHK134）是否有对应的可执行验收场景？[Acceptance Criteria, Gap]
- [x] CHK149 每个 FR（含 FR-015/FR-016）是否都有至少一个对应验收场景或 SC（FR → 验收/SC 全覆盖审计）？[Acceptance Criteria, Traceability, Gap]

## Scenario Coverage

- [x] CHK150 是否覆盖"安装后 App 权限在调用期间被移除/降级"（认证通过后提交阶段 403 → APP_NOT_INSTALLED）？[Coverage, Edge Case, Gap]
- [x] CHK151 是否覆盖调用方在 NETWORK_ERROR 后核验 PR、发现 review 已创建再去重的操作流程（quickstart 引导场景）？[Coverage, Gap, Tool Contract NETWORK_ERROR]
- [x] CHK152 目标账号类型（用户级 vs 组织级仓库）是否定义不做区分、统一按安装授权判定？[Coverage, Clarity, Gap, Spec Assumptions]

## Edge Case Coverage

- [x] CHK153 提交返回 200 但 id 为 0 或空字符串（类型异常）是否定义归属（UNEXPECTED_ERROR）？[Edge Case, Gap, github-rest §3]
- [x] CHK154 details 截断是否定义安全边界（按字符截断而非字节，避免切断多字节序列破坏 JSON 可解析性）？[Edge Case, Clarity, Gap, Tool Contract 消息约定]
- [x] CHK155 状态读取或提交返回 200 但 JSON 为非对象类型（数组/字符串等）是否定义归属（UNEXPECTED_ERROR）？[Edge Case, Gap, github-rest §2/§3]

## Non-Functional Requirements

- [x] CHK156 是否定义"private-key 文件夹永不进入版本库"的防护/核验机制（如 CI 断言或 .gitignore 检查）？[NFR, Security, Gap, Spec §FR-012]
- [x] CHK157 并发调用下的稳定性/限流预期是否作为 NFR 定义（或明确由调用方串行化）？[NFR, Gap, Spec §FR-011]
- [x] CHK158 启动校验（FR-015）耗时与 MCP 客户端启动超时/拉起策略之间的关系是否说明？[NFR, Gap, Plan 目标平台]

## Dependencies & Assumptions

- [x] CHK159 安装令牌 repositories 参数的行为（未授权仓库被拒绝）是否作为平台依赖假设记录？[Dependency, Assumption, github-rest §1]
- [x] CHK160 MCP stdio 进程生命周期（由客户端拉起、退出即结束）是否作为部署假设记录？[Dependency, Assumption, Plan 目标平台]

## Ambiguities & Conflicts

- [x] CHK161 FR-015"启动即失败"与 MCP 宿主对工具进程的启动超时/自动重启策略之间是否说明（配置错误可能导致宿主反复重启）？[Conflict, Ambiguity, Gap, Spec §FR-015, Plan 目标平台]
- [x] CHK162 是否明确"运行期除工具调用结果外不产生任何 stdout/stderr 输出"（启动期错误输出与运行期零输出的边界）？[Ambiguity, Clarity, Gap, Spec §FR-011/§FR-015]
- [x] CHK163 错误码演进流程（新增须先经 spec 修订，CHK132）是否同样约束删除/合并错误码？[Ambiguity, Governance, Gap, Tool Contract 错误码]

---

## Plan-Review 验证记录（Round 5 · $speckit-plan 复核，2026-08-09）

> 基于第六次修订 spec（空 diff 边界、运行期 403、FR-011 运行期零 stdout/stderr、FR-015 仅本地校验、账号类型不区分、私钥轮换需重启）逐项核对。PASS = 已覆盖；FAIL = 需 spec 层修订。核对期间对 tool-contract / github-rest / contracts-README / data-model / plan / quickstart 做了补丁（5xx 归属、令牌交换解析失败、版本标注、CHK 引用同步、FR 验收映射等），见各文件标注的 CHK 编号。

| CHK | 结论 | 依据 / 说明 |
|-----|------|-------------|
| CHK133 | PASS | 本次补丁：github-rest §1——201 但缺 token/expires_at 或类型异常 → UNEXPECTED_ERROR |
| CHK134 | PASS | 本次补丁：github-rest 头部 5xx（502/503/504）→ NETWORK_ERROR；tool-contract NETWORK_ERROR 行同步 |
| CHK135 | PASS | spec 边界场景（空 diff：仅 body 可成功、含评论 422）；tool-contract 输入说明 + github-rest §3 同步 |
| CHK136 | PASS | 本次补丁：tool-contract 输入说明——同一文件可同时含 LEFT/RIGHT 评论、原样透传、合法性由 GitHub 校验 |
| CHK137 | PASS | spec Assumptions 明确私钥轮换需重启生效、旧私钥随即失效；plan 约束同步 |
| CHK138 | PASS | spec FR-015 明确启动校验仅本地（不发网络请求、不做令牌交换预检）；plan/quickstart 同步 |
| CHK139 | PASS | 本次补丁：tool-contract 消息约定——details.retryable（布尔，可选），RATE_LIMITED/NETWORK_ERROR=true，其余 false/省略 |
| CHK140 | PASS | 本次补丁：contracts/README 术语约定——"上传审查结果"="提交 review"（submit_pr_review），文档统一术语 |
| CHK141 | PASS | 本次补丁：tool-contract RATE_LIMITED 行——并发共享安装令牌核心限流配额，高并发由调用方串行化/降并发 |
| CHK142 | PASS | 本次补丁：tool-contract 输入说明——以 schema 为参数模型，组件测试断言 SDK 暴露 schema 一致性 |
| CHK143 | PASS | 本次补丁：plan 目录树 CHK001~038 → CHK001~163（五轮）；其余 CHK 具体引用仍有效 |
| CHK144 | PASS | 本次补丁：github-rest §1 补解析失败定义（UNEXPECTED_ERROR），与 §2/§3 一致 |
| CHK145 | PASS | 本次补丁：tool-contract/github-rest 版本标注去除过期序号（"v3（2026-08-09）"），不再绑定修订轮次 |
| CHK146 | PASS | 本次补丁：plan 宪法门禁补 spec 状态门禁——Draft 转正式需全部清单 PASS + 宪法合规声明 + 评审通过 |
| CHK147 | PASS | 本次补丁：quickstart 前置条件注明无 API 地址配置项，验收由自动化测试断言 BaseAddress/TLS |
| CHK148 | PASS | 本次补丁：quickstart 错误码映射表——NETWORK_ERROR 行含 5xx、UNEXPECTED_ERROR 行含令牌交换解析失败 |
| CHK149 | PASS | 本次补丁：quickstart 新增 FR 验收映射表——16 个 FR 全部映射到验收场景/SC/自动化测试 |
| CHK150 | PASS | spec 边界场景（调用期间权限被移除/降级，提交阶段 403）+ github-rest §3 403 行 → APP_NOT_INSTALLED |
| CHK151 | PASS | 本次补丁：quickstart 错误码映射表——NETWORK_ERROR 行含调用方核验去重流程引导 |
| CHK152 | PASS | spec Assumptions 明确账号类型不区分、统一按安装授权判定；plan 约束同步 |
| CHK153 | PASS | 本次补丁：github-rest §3——200 但 id 非正整数/缺失、html_url 缺失/非法 → UNEXPECTED_ERROR |
| CHK154 | PASS | tool-contract 消息约定明确按 Unicode 字符边界截断、不切断多字节序列/代理对、保持 JSON 可解析（上轮已覆盖，本轮措辞加强） |
| CHK155 | PASS | 本次补丁：github-rest §2/§3——JSON 为非对象类型（数组/字符串/标量）→ UNEXPECTED_ERROR |
| CHK156 | PASS | 本次补丁：quickstart 自动化验证补 private-key 不入库断言（.gitignore/CI 检查） |
| CHK157 | PASS | 本次补丁：plan 并发约束——并发共享核心限流配额，高并发由调用方串行化/降并发 |
| CHK158 | PASS | 本次补丁：plan 目标平台——启动校验为本地毫秒级操作，远小于典型启动超时 |
| CHK159 | PASS | 本次补丁：github-rest §1——repositories 参数行为标注为平台依赖假设（未授权仓库被拒 403/404） |
| CHK160 | PASS | 本次补丁：plan 目标平台——进程生命周期由客户端管理（拉起→会话→退出即结束，无守护/自重启） |
| CHK161 | PASS | 本次补丁：plan 目标平台 + quickstart——配置错误导致宿主反复重启的说明（检查 stderr 修复配置或调整宿主策略） |
| CHK162 | PASS | spec FR-011 明确运行期除 MCP 协议与调用结果外零 stdout/stderr；plan 约束/quickstart 同步（启动期错误输出除外，FR-015） |
| CHK163 | PASS | 本次补丁：tool-contract 契约演进——新增、删除或合并错误码均须先经 spec 修订再同步契约版本 |

**结论**：Round 5（CHK133–CHK163）31 项全部 PASS，无 FAIL；25 项原未勾选项已按验证结果标记 `[x]`。

---

## Plan-Review Checklist（Round 6 · 继续检查）

**Purpose**: 对照 Round 1–5 全部已确认记录去重后，仅新增未被覆盖的检查点（其余维度已达收敛，不再重复提出）
**Created**: 2026-08-09
**Feature**: [spec.md](../spec.md) | [plan.md](../plan.md) | [data-model.md](../data-model.md) | [contracts/](../contracts/README.md)
**Confirmed**: Round 5（CHK133–CHK163）已于 2026-08-09 全部确认，见上方验证记录

## Requirement Clarity

- [x] CHK164 是否明确 message 为辅助信息、调用方必须以 code 分支决策（message 文本不保证跨版本稳定）？[Clarity, Gap, Tool Contract 输出契约]
- [x] CHK165 是否定义 code ↔ httpStatus 的绑定一致性规则（如 404→TARGET_NOT_FOUND、422→REVIEW_UNPROCESSABLE），供实现与测试机器校验？[Clarity, Consistency, Gap, Tool Contract 错误码]
- [x] CHK166 是否明确 httpStatus 字段语义为 GitHub HTTP 状态码，与 MCP 协议层状态无关，避免调用方混淆？[Clarity, Gap, Tool Contract 输出契约]

## Requirement Completeness

- [x] CHK167 owner/repo/path 等字符串字段未设长度上限，是否明确处理归属（由 GitHub 判定 → 404/422，或定义本地上限）？[Completeness, Edge Case, Gap, Schema]
- [x] CHK168 MCP 客户端传入类型不符（如 pullNumber 传字符串 "42"）时，是否定义由 MCP 框架拒绝还是本地 INVALID_PAYLOAD？[Completeness, Edge Case, Gap, Tool Contract 输入 JSON Schema]
- [x] CHK169 side 枚举非法值（如小写 "right"）是否定义归属（INVALID_PAYLOAD vs 框架校验）？[Completeness, Edge Case, Gap, Schema]

## Requirement Consistency

- [x] CHK170 FR/SC 编号是否定义追加规则（新增需求追加新编号、不重排既有编号），避免 plan/contracts 断链？[Consistency, Traceability, Governance, Gap]

## Governance

- [x] CHK171 是否定义检查面收敛/退出标准（如连续核验无新发现或全部 PASS 后转入 tasks.md 拆分），避免无限追加检查？[Governance, Gap]

---

## Plan-Review 验证记录（Round 6 · $speckit-plan 复核，2026-08-09）

> 对照 Round 1–5 已确认记录去重后核验；同时同步 spec 第七轮新增边界（owner/repo/path 不设长度上限，CHK167）。PASS = 已覆盖；FAIL = 需 spec 层修订。核对期间对 tool-contract / submit-review.schema.json / plan 做了补丁（message 辅助性、code↔httpStatus 绑定、httpStatus 语义、类型/枚举校验归属、编号与收敛治理），见各文件标注的 CHK 编号。Round 6 完成后检查面收敛（CHK171），可转入 tasks 拆分。

| CHK | 结论 | 依据 / 说明 |
|-----|------|-------------|
| CHK164 | PASS | 本次补丁：tool-contract 消息约定——message 为辅助说明、调用方以 code 分支、文本不保证跨版本稳定 |
| CHK165 | PASS | 本次补丁：tool-contract 错误码表——code↔httpStatus 绑定规则（404/403/401/422/429 → 对应码；PR_NOT_OPEN=200+state≠open），供实现与测试机器校验 |
| CHK166 | PASS | 本次补丁：tool-contract 输出契约——httpStatus 为 GitHub HTTP 状态码、与 MCP 协议层状态无关；本地校验类错误无 httpStatus 或省略 |
| CHK167 | PASS | spec 边界场景（owner/repo/path 不设长度上限、GitHub 判定）→ tool-contract 输入说明 + schema owner/repo/path 描述同步 |
| CHK168 | PASS | 本次补丁：tool-contract 输入说明——类型不符优先由 MCP 框架 schema 校验拒绝，框架放行时本地兜底 INVALID_PAYLOAD，均不产生 GitHub 请求 |
| CHK169 | PASS | 本次补丁：tool-contract 输入说明——side 非法枚举（如小写 right）归属同上（框架校验优先、本地兜底 INVALID_PAYLOAD） |
| CHK170 | PASS | 本次补丁：plan 宪法门禁——FR/SC 编号追加制（新编号如 FR-017、不重排既有编号），避免 plan/contracts 断链 |
| CHK171 | PASS | 本次补丁：plan 宪法门禁——检查收敛标准（连续一轮全部 PASS 且无新增检查点即收敛，转入 $speckit-tasks；spec 修订仅重启受影响范围检查） |

**结论**：Round 6（CHK164–CHK171）8 项全部 PASS，无 FAIL；7 项原未勾选项已按验证结果标记 `[x]`。

---

## 收敛记录（2026-08-09）

对照 Round 1–6 全部已确认核验记录（CHK001–CHK171）逐项去重扫描，本轮未发现新的非重复需求质量检查点；全部 171 项均已确认（`[x]`）。按 CHK171 已确认的收敛标准（连续一轮全部 PASS 且无新增检查点即收敛），本清单检查面收敛，转入 Phase 2（`$speckit-tasks` 生成 tasks.md）。后续 spec 修订仅重启受影响范围的检查，不再无限追加清单轮次。

---

## Plan-Review Checklist（Round 7 · 表述不清/需确认专项）

**Purpose**: 按用户指示，在已确认项基础上仅针对"表述不清、需要进一步确认"的点做定向检查；与 CHK001–CHK171 已确认内容去重，不复述已确认结论
**Created**: 2026-08-09
**Feature**: [spec.md](../spec.md) | [data-model.md](../data-model.md) | [contracts/](../contracts/README.md) | [quickstart.md](../quickstart.md)
**Confirmed**: Round 6（CHK164–CHK171）已于 2026-08-09 全部确认，见上方验证记录

## Requirement Clarity

- [x] CHK172 "去除首尾空白"（FR-003）的空白字符集是否定义（零宽空格 U+200B、NBSP、全角空格等是否视为空白；以 .NET Trim 语义为契约还是显式字符集）？[Clarity, Edge Case, Gap, Spec §FR-003, Data Model 校验规则]

## Ambiguities & Conflicts

- [x] CHK173 owner/repo 是否执行 trim/非空校验（FR-003 的 trim 规则仅覆盖 body 与评论内容；纯空格 owner/repo 是本地拒绝还是交 GitHub 404 → TARGET_NOT_FOUND）？[Ambiguity, Gap, Spec §FR-002/§FR-003, Schema]
- [x] CHK174 FR-015"配置非法"的判定标准是否枚举（App ID/安装 ID 非正整数、私钥路径不存在/不可读、私钥不可解析为 RSA 等），避免"等"字开放解释？[Clarity, Gap, Spec §FR-015, Quickstart 前置条件]
- [x] CHK175 成功/失败结果 JSON 是否定义向后兼容规则（未来可新增字段、调用方忽略未知字段；字段删除/重命名需契约升级），与错误码演进流程（CHK163）对齐？[Governance, Clarity, Gap, Tool Contract 输出契约]

---

## Plan-Review 验证记录（Round 7 · $speckit-plan 复核，2026-08-09）

> 按用户指示做"表述不清/需确认"定向检查，与 CHK001~171 已确认内容去重；同时同步 spec 第八轮细化（FR-002 owner/repo/pullNumber trim 非空、FR-003 Unicode 空白字符集、FR-015 配置校验枚举）。PASS = 已覆盖；FAIL = 需 spec 层修订。核对期间对 data-model / tool-contract / submit-review.schema.json / plan / quickstart 做了补丁，见各文件标注的 CHK 编号。

| CHK | 结论 | 依据 / 说明 |
|-----|------|-------------|
| CHK172 | PASS | spec FR-003 定义空白字符集（Unicode 空白字符，不含零宽空格 U+200B 等格式字符）；data-model 校验规则与 tool-contract 输入说明同步（.NET `string.Trim()` 语义） |
| CHK173 | PASS | spec FR-002 明确 owner/repo/pullNumber 须 trim 后非空、纯空白视为缺失本地拒绝；data-model 实体表、schema owner/repo 描述、tool-contract 输入说明同步 |
| CHK174 | PASS | spec FR-015 枚举配置非法判定标准（App ID/安装 ID 非正整数、私钥路径不存在/不可读、私钥不可解析为 RSA）；plan 约束与 quickstart 本地运行说明同步 |
| CHK175 | PASS | 本次补丁：tool-contract 输出契约——结果 JSON 向后兼容规则（未来可新增字段、调用方忽略未知字段；字段删除/重命名属破坏性变更需契约升级并同步 spec，与 CHK163 对齐） |

**结论**：Round 7（CHK172–CHK175）4 项全部 PASS，无 FAIL；CHK175 已按验证结果标记 `[x]`。清单合计 175 项（Round 1~7）全部确认。

---

## Plan-Review Checklist（Round 8 · 全文件深度检查）

**Purpose**: 按标准对 spec/plan/research/data-model/quickstart/contracts 全部文件做深度检查。功能完整性已确认（FR-001~016、SC-001~009、US1~3、9 个错误码均映射到验收/验证）；以下为跨文件检查发现的"表述不清、需确认"点，与 CHK001~CHK175 已确认内容去重。
**Created**: 2026-08-09
**Feature**: [spec.md](../spec.md) | [plan.md](../plan.md) | [research.md](../research.md) | [data-model.md](../data-model.md) | [quickstart.md](../quickstart.md) | [contracts/](../contracts/README.md)
**Confirmed**: Round 7（CHK172–CHK175）已于 2026-08-09 全部确认，见上方验证记录

## Requirement Clarity

- [x] CHK176 github-rest §2（状态读取）错误映射表中"网络/超时"行的注释"若超时发生在提交请求发出后，review 可能已创建"是否应移至 §3（提交）？状态读取阶段尚未发出提交请求，该注释与阶段不符。 [Clarity, Consistency, github-rest §2/§3]
- [x] CHK177 FR-002 与 tool-contract 输入说明要求"PR 编号（pullNumber）trim 后非空"，但 pullNumber 为 integer，trim/纯空白语义不适用——是否应限定 trim 规则仅适用于字符串字段，pullNumber 校验为 ≥1？ [Clarity, Ambiguity, Spec §FR-002, Tool Contract 输入说明]
- [x] CHK178 tool-contract 内联 JSON schema 快照中 owner/repo 的 description 未同步 submit-review.schema.json 的"trim 后非空"说明——是否按 CHK083 单一事实源原则同步快照？ [Consistency, Tool Contract 输入 JSON Schema]
- [x] CHK179 quickstart 场景 E 将"畸形 JSON、缺 required、字段类型错误"统一预期为工具返回 INVALID_PAYLOAD，但已确认归因（CHK168/CHK005）为"类型不符优先由 MCP 框架拒绝（协议层）、框架放行时本地兜底"——场景 E 预期是否需区分协议层拒绝与本地 INVALID_PAYLOAD？ [Clarity, Consistency, Quickstart 场景 E]
- [x] CHK180 github-rest §3 将"必填缺失"列为提交阶段 422 触发之一，但 FR-003 本地校验已在任何 GitHub 请求前拦截必填缺失（INVALID_PAYLOAD）——两者关系是否需要澄清（哪些缺失仅 GitHub 侧才可能以 422 返回）？ [Clarity, Consistency, github-rest §3, Spec §FR-003]
- [x] CHK181 quickstart 场景 A 的客观核验仅描述 GET /pulls/{n}/reviews/{review_id}，但该端点不返回逐条评论的 path/line/side——验证脚本是否应指明获取评论的端点（如 GET /pulls/{n}/comments?review_id=...），避免误导？ [Completeness, Clarity, Quickstart 场景 A]

---

## Plan-Review 验证记录（Round 8 · $speckit-plan 复核，2026-08-09）

> 全文件深度检查（跨 spec/plan/research/data-model/quickstart/contracts），与 CHK001~175 去重；同时同步 spec 第九轮细化（FR-002：owner/repo 为 trim 后非空字符串、pullNumber 为 ≥1 整数，CHK177）。PASS = 已覆盖；FAIL = 需 spec 层修订。核对期间对 github-rest / tool-contract / quickstart 做了补丁，见各文件标注的 CHK 编号。

| CHK | 结论 | 依据 / 说明 |
|-----|------|-------------|
| CHK176 | PASS | 本次补丁：github-rest §2"网络/超时"行注释改为"状态读取阶段网络失败/超时，未发起任何 review 提交"；"提交后超时可能已创建"注释移至 §3 |
| CHK177 | PASS | spec FR-002 明确 owner/repo 为 trim 后非空字符串、pullNumber 为 ≥1 整数；tool-contract 输入说明同步（trim 仅适用于字符串字段） |
| CHK178 | PASS | 本次补丁：tool-contract 内联 schema 快照 owner/repo description 同步"trim 后非空"，符合 CHK083 单一事实源原则 |
| CHK179 | PASS | 本次补丁：quickstart 场景 E 预期区分"MCP 框架协议层拒绝（类型不符优先）"与"框架放行时本地 INVALID_PAYLOAD"，与 CHK168/CHK005 归因一致 |
| CHK180 | PASS | 本次补丁：github-rest §3 422 行移除"必填缺失"并注明"必填缺失已在 FR-003 本地校验拦截（INVALID_PAYLOAD），不会到达提交阶段"；422 触发改为评论越界/路径格式/数量长度超限/次级限流等 |
| CHK181 | PASS | 本次补丁：quickstart 场景 A 步骤 3 补充评论端点 `GET /pulls/{n}/comments?review_id=...` 逐条比对 path/line/side/body，避免误导 |

**结论**：Round 8（CHK176–CHK181）6 项全部 PASS，无 FAIL；5 项原未勾选项已按验证结果标记 `[x]`。清单合计 181 项（Round 1~8）全部确认。

---

## 收敛确认（2026-08-09 · Round 8 后）

对照 CHK001–CHK181 全部已确认核验记录做去重扫描，本轮未发现新的非重复检查点；Round 8（全文件深度检查）产出的 6 项已全部确认并修复，检查面已穷尽（181/181）。按 CHK171 已确认的收敛标准与 CHK146 状态门禁，本清单作为 Phase 2 门禁就绪，建议转入 `$speckit-tasks` 生成 tasks.md；后续仅因 spec 修订重启受影响范围检查。

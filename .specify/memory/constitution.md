<!--
SYNC IMPACT REPORT
- Version change: template placeholder → 1.0.0 (first creation)
- Modified principles: none (all five principles newly created)
- Added sections: Core Principles (5 principles), Technical Constraints,
  Development Workflow, Governance
- Removed sections: none
- Follow-up TODOs: none — all placeholders resolved
-->

# PR-AI-Reviewer Constitution

## Core Principles

### 一、单一职责（NON-NEGOTIABLE）

本工具 MUST 只做一件事：接收 Agent 提供的 PR review 内容，并通过 GitHub App
渠道上传到 Agent 明确指定的仓库与 PR。本工具 MUST NOT 生成、修改或推断审查
意见；MUST NOT 存储、持久化或缓存任何数据；MUST NOT 执行 CI/CD 或任何超出
上传职责的操作；MUST NOT 扩展任何与上传无关的功能。

**理由**：产品形态是"最小上传接口"。任何附加能力都会扩大攻击面、增加凭据
暴露风险，并使行为难以验证。

### 二、显式目标与最小作用域

每次调用 MUST 显式指定目标 `owner/repo` 以及必要的 PR 标识；MUST NOT 通过
推断、默认值、环境上下文或通配方式选定仓库。权限 MUST 仅覆盖完成 PR review
上传所需的最小 scope；工具 MUST NOT 读取、写入或访问目标之外的任何资源。

**理由**：隐式目标选择可能导致审查内容被上传到错误的仓库，产生越权与数据
泄露风险。

### 三、Bot 身份合规

所有提交 MUST 经 GitHub App 安装认证并以 bot 身份执行。Bot 标识是 GitHub
平台的固有行为；工具 MUST NOT 篡改、隐藏或伪造该标识，MUST NOT 以人类账号
代发操作，MUST NOT 绕过或删除 GitHub 的操作记录。

**理由**：身份真实性是平台信任与审计的基础，伪造身份违反 GitHub 使用条款
并破坏可追溯性。

### 四、凭据安全（NON-NEGOTIABLE）

GitHub App 私钥与安装令牌等密钥 MUST 保存在本地受保护位置，并 MUST 登记在
`.gitignore`（或等效排除规则）中；任何情况下密钥 MUST NOT 进入版本库。密钥
MUST 通过环境变量或本地安全存储注入；MUST 优先使用短期令牌，并确保其及时
失效；MUST NOT 硬编码密钥、令牌或任何敏感配置。

**理由**：密钥泄露等同身份失守，将直接危及目标仓库及其全部安装范围。

### 五、失败透明

上传成功或失败 MUST 向调用方返回明确结果。失败时 MUST 返回清晰、可操作的
错误信息，MUST NOT 静默吞掉错误，MUST NOT 进入不可预期的重试状态；若存在
重试机制，其行为 MUST 被明确界定并文档化。

**理由**：调用方是 Agent，依赖确定性的成功/失败信号来决定下一步；模糊或静默
失败会导致错误重复上传或审查缺失。

## 技术约束

- 语言与框架：MUST 使用 C# / .NET LTS；MCP 服务端 MUST 基于官方 MCP C#
  SDK（mcpdotnet）实现；GitHub 交互 MUST 基于 Octokit / GitHub REST API。
- GitHub App 认证：MUST 使用安装令牌认证；令牌 MUST 经安全渠道获取并短期
  使用，MUST NOT 硬编码。
- 配置管理：可配置项 MUST 集中定义；敏感配置 MUST 从代码库外注入。
- 运行约束：MUST NOT 依赖数据库、消息队列或其他持久化服务；工具 MUST 保持
  无状态，仅完成"请求 → 上传"的转发。

## 开发工作流

- 版本管理：MUST 使用 GitHub 作为版本控制系统；MUST 采用分支开发 + PR 合并
  流程；提交信息 MUST 遵循 Conventional Commits。
- 变更流程：MUST 遵循 spec-kit 规约驱动工作流；代码与规约文档 MUST 同步
  更新；变更 MUST 经确认后方可合入。

## Governance

- 宪法高于其他项目文档与实践；任何冲突 SHOULD 以宪法为准并记录在案。
- 修订流程 MUST 遵循：提案 → 记录变更 → 审批通过 → 更新版本号与修订日期。
- 版本策略：MAJOR 用于治理原则的删除或重定义；MINOR 用于新增原则或实质
  扩充；PATCH 用于澄清与措辞修订。
- 每次修订后 MUST 进行合规自查：所有 PR 与 review 必须声明对宪法的遵守情况。
- 无法确定的信息 MUST 使用 `TODO(字段名): 说明` 标记，并列入同步影响报告。

**Version**: 1.0.0 | **Ratified**: 2026-08-09 | **Last Amended**: 2026-08-09

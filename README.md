# PR-AI-Reviewer

一个**特化的 MCP Tool**：AI Agent 完成代码审查后，调用本工具，以 GitHub App 的 Bot 身份，将审查结论通过 GitHub PR 的 file change 下的 **submit review** 上传到指定仓库的指定 PR，从而完成 AI PR review。

## 职责边界

本工具**只做一件事**：接收 Agent 传来的 PR review 内容，并利用 GitHub App 渠道上传到指定仓库的指定 PR。

- 不生成、不修改审查内容，不分析代码
- 不持久化任何业务数据
- 不负责 CI/CD、构建、部署、通知
- 不操作调用方未指定的仓库或 PR

## 技术栈

- C# / .NET LTS
- 官方 MCP C# SDK（mcpdotnet）
- Octokit / GitHub REST API
- GitHub 版本管理

## 凭据安全

GitHub App 私钥保存在本地 `private-key/` 目录，该目录已被 `.gitignore` 排除，**任何情况下不得提交进版本库**。

## 开发流程

项目遵循 spec-kit 规约驱动工作流（constitution → specify → plan → tasks → implement）。

- 当前功能规格：[specs/001-pr-review-submit/spec.md](specs/001-pr-review-submit/spec.md)

## 项目状态

规划阶段。

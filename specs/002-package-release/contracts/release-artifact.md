# Contract: 发布产物（Release Artifact）

## 命名

- zip：`PrReviewSubmit-<semver>-win-x64.zip`
- 校验和：`PrReviewSubmit-<semver>-win-x64.zip.sha256`
- 版本文件：`VERSION`（纯文本 `1.0.0`，无 `v` 前缀）
- 构建信息：`BUILD_INFO`（`version=1.0.0` + `commit=<40位sha>`，供发布校验与追溯）
- 输出位置：zip 与 sha256 生成到 `dist/` 根目录，与 `dist/<version>/` 产物目录同级。

## 目录布局（dist/<semver>/）

```text
dist/1.0.0/
├── PrReviewSubmit.exe
├── PrReviewSubmit.dll
├── PrReviewSubmit.deps.json
├── PrReviewSubmit.runtimeconfig.json
├── VERSION
├── BUILD_INFO
├── <依赖 dll>
└── (runtimes/ 平台子目录，如存在)
```

## 契约要求

- 单一可执行入口 `PrReviewSubmit.exe`，stdio MCP 协议启动（FR-002）。
- 框架依赖：目标机需 .NET 10 运行时，无需 SDK（Q1）。
- `VERSION` 与 zip 文件名、git tag 完全一致（FR-004）；`BUILD_INFO.commit` 供 `release.ps1` 校验与远程 main HEAD 一致（FR-012）。
- `.sha256` 格式：`<64位小写十六进制>  <文件名>`。
- 产物中 0 敏感内容：无 `private-key/`、`*.pem`、`*.key`、`*.p12`、`*.pfx`、`.env*`，无私钥/令牌文本模式（FR-003）。
- 产物内容边界：仅含运行所需文件（exe / dll / runtimeconfig / deps / 依赖资源 / VERSION / BUILD_INFO）；不得包含源码（.cs/.csproj）、测试工程、obj/bin 中间产物。
- 可重复构建：每次构建前清理旧 `dist/<version>/`，同版本重复构建的产物语义一致、校验和可复算。
- 敏感扫描范围：仅针对产物目录 `dist/<version>/`；源目录的密钥排除由 `scripts/gates.ps1` 私钥检查覆盖。

## 验收方式

1. 解压 zip 到干净的独立目录（目标机已装 .NET 10 运行时）；
2. 启动 `PrReviewSubmit.exe`，完成 MCP `initialize` + `tools/list`；
3. `tools/list` 返回且仅返回 `submit_pr_review`；
4. 校验 `VERSION` 内容与发布版本一致；
5. `Get-FileHash` 结果与 `.sha256` 一致；
6. 发布冒烟（`smoke-published.ps1`）以该解压副本为执行对象，验证 zip 产物完整可用（D14）。

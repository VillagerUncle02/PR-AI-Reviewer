# Contract: 发布流程（Release Process）

## 前置条件（全部满足才可发布）

1. 待发布变更已合入 `main`；
2. 本地全量门禁通过（`scripts/gates.ps1`；`release.ps1` 会再次自动运行）；
3. `scripts/publish.ps1 -Version <v>` 已产出产物并通过敏感扫描；
4. `scripts/smoke-published.ps1 -Version <v>` 真实冒烟成功（bot 标识 + 回读一致）；
5. `gh auth status` 通过；
6. git tag `v<v>` 不存在；或 tag 已存在但无对应 Release（进入补建路径，FR-015）。
7. 执行者具备仓库 `contents: write` 权限（push tag 与创建 Release 所需）。

## 执行步骤（手动）

1. `scripts/publish.ps1 -Version <v>` → 产物 + VERSION + zip + sha256；
2. `scripts/smoke-published.ps1 -Version <v>` → 解压 zip 后对副本真实上传冒烟；
3. `scripts/release.ps1 -Version <v>` →
   - 自动校验：运行 `scripts/gates.ps1`；产物与 VERSION/BUILD_INFO 存在、`BUILD_INFO.commit` 等于远程 main HEAD、sha256 匹配、冒烟审计记录 success、`gh auth status` 通过、tag 不存在，或 tag 已存在但无对应 Release（补建路径）；
   - 生成发布说明（Markdown：标题 + 版本与日期 + 变更分类（修复/新功能/其他）+ 自上一 tag 的 git log 摘要；首次发布无上一 tag 时用仓库全部历史摘要或人工撰写；可人工编辑）；
   - tag 不存在 → `git tag v<v>` + `git push origin v<v>`；tag 已存在且无 Release → 跳过打 tag（FR-015）；
   - `gh release create v<v> <zip> <sha256> --title "PrReviewSubmit v<v>" --notes-file <notes>`；
4. 核对 GitHub Release 页面资产与说明。

发布成功后 `release.ps1` 写审计文件 `notes/reviews/<version>-release.md`（版本、commit、tag、Release URL、操作时间）。

## 失败语义

- 任一前置条件失败：立即退出（非 0），不创建 tag/Release；
- 冒烟可重复执行：重试产生的新 review 不清理，验收以最近一次成功记录为准（FR-013）；
- tag 与 Release 均已存在：明确失败，禁止覆盖（FR-004）；tag 存在但 Release 缺失：按 FR-015 补建，不删除远端 tag；
- 发布后发现问题：修复后升 PATCH 版本重新发布，不修改既有 Release。

## 明确禁止

- 不使用 CI/CD 自动发布（FR-009 / 宪法）；
- 发布脚本不读取、不打包 `private-key/`；
- 不向产物写入任何令牌或环境变量值。

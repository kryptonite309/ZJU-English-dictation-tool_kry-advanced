# v1.3.6 GitHub 发布说明

## 固定资产名称

Release 页面必须上传以下三个文件，名称和大小写保持一致。

- `ZJU-English-dictation-tool_kry-advanced-v1.3.6-full.zip`
- `ZJU-English-dictation-tool_kry-advanced-v1.3.6-upgrade.zip`
- `SHA256SUMS.txt`

不要在文件名末尾重复添加 `.zip`。`SHA256SUMS.txt` 中的文件名必须与 GitHub 页面显示的资产名完全一致。

## 更新仓库源码

将 `repository-files` 中的内容复制到 GitHub 仓库根目录并提交。它包含源码、构建脚本、说明文档、公开词库和 `.gitignore`，不包含用户学习记录。

## 创建 Release

1. 创建标签 `v1.3.6`。
2. 新建同名 Release。
3. 将 `RELEASE_NOTES_v1.3.6.md` 的内容粘贴到 Release 说明。
4. 上传上面的三个固定名称文件。

v1.3.5 及更早版本需要手动升级到本版本。v1.3.6 之后的版本可以继续使用统一的 ASCII 命名规则自动更新。

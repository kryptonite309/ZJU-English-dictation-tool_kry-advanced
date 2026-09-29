# v1.3.5 GitHub 发布说明

## 更新仓库源码

将 `repository-files` 文件夹中的内容复制到 GitHub 仓库根目录并提交。
它包含源码、构建脚本、说明文档、公开词库和 `.gitignore`，不包含任何用户学习记录。

## 创建 Release

1. 在 GitHub 仓库中创建标签 `v1.3.5`。
2. 新建同名 Release，并粘贴 `RELEASE_NOTES_v1.3.5.md` 的内容。
3. 上传下面三个文件。

- `大英默写器-KRY-v1.3.5-完整版.zip`
- `大英默写器-KRY-v1.3.5-升级包.zip`
- `SHA256SUMS.txt`

完整版供第一次安装使用，内含 book2、book3、词性文件与 KRY 默认动态皮肤。

升级包不会携带 `appearance.json`，因此不会覆盖旧用户的外观。升级包中的
`appearance_images` 只补充默认壁纸文件，用户需要将该文件夹合并到原程序目录。

## 发布前检查

- 不要上传 `study_state.json`、`notebook_state.json`、`practice_state.json`。
- 不要上传 `wrong_words.json`、`study_lists`、`backups` 或个人 `appearance.json`。
- Release 页面上传 ZIP 文件，不要把 ZIP 提交进源码仓库。
- 核对 Release 标签和窗口标题均为 `v1.3.5`。

# v1.2.0 GitHub 更新操作

## 一、更新仓库源码

1. 打开项目仓库：<https://github.com/kryptonite309/ZJU-English-dictation-tool_kry-advanced>
2. 点击 `Add file`，选择 `Upload files`。
3. 打开本地 `repository-files` 文件夹，把其中的全部内容拖入上传区域。上传的是文件夹内部内容，不要把 `repository-files` 这一层一起上传。
4. 提交说明填写 `Release v1.2.0`。
5. 选择直接提交到 `main`，点击 `Commit changes`。

上传完成后，仓库首页应能看到新的 `LearningSupport.cs`、`PracticeStore.cs`、`StatisticsForm.cs`、`UpdateService.cs`、`V120Tests.cs`、更新后的其他源码、`README.md` 与 `CHANGELOG.md`。

## 二、创建 v1.2.0 Release

1. 回到仓库首页，打开右侧 `Releases`。
2. 点击 `Draft a new release`。
3. 点击 `Choose a tag`，输入 `v1.2.0` 并创建新标签。
4. Release title 填写 `大英默写器 KRY 增强版 v1.2.0`。
5. 把 `RELEASE_NOTES_v1.2.0.md` 的正文复制到发布说明。
6. 上传以下三个文件：
   - `大英默写器-KRY增强版-v1.2.0-完整包.zip`
   - `大英默写器-KRY增强版-v1.2.0-升级包.zip`
   - `SHA256SUMS.txt`
7. 不要勾选预发布，检查无误后点击 `Publish release`。

升级包文件名必须保留“升级包”三个字，校验文件名必须保持为 `SHA256SUMS.txt`。v1.2.0 的自动更新功能会按这两个条件寻找文件，并在两者都存在且 SHA-256 校验通过时才允许安装。

## 三、文件用途

- 完整包用于首次安装，包含程序、book2、book3、词性映射和说明。
- 升级包用于保留旧版进度，主要替换 `main.exe`。
- `SHA256SUMS.txt` 供用户和程序验证下载完整性。
- `repository-files` 仅用于更新仓库源码，不作为普通用户安装包。


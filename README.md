# 大英默写器 KRY 增强版

基于 [Lucent-Snow 的 ZJU 大英默写器](https://github.com/Lucent-Snow/ZJU-English-dictation-tool) 制作的 Windows 个人增强版，当前版本为 **v1.4.1**。增强版使用 C#、Windows Forms 和 WPF，面向 Windows x64，保留自由练习，并增加长期学习、复习和单词管理功能。本项目与原项目没有官方关联。

**更新修复提示**　v1.4.0 及更早版本的更新器存在附件解析问题，请从 [v1.4.1 发布页](https://github.com/kryptonite309/ZJU-English-dictation-tool_kry-advanced/releases/tag/v1.4.1) 手动下载升级包，关闭程序后合并升级文件。保留原有学习记录和个人设置，完成这一次升级后再使用软件内更新。

## 下载与运行

前往 [GitHub Releases](https://github.com/kryptonite309/ZJU-English-dictation-tool_kry-advanced/releases/latest) 下载完整包，完整解压后运行 `main.exe`。请保持 `main.exe` 和 `data` 文件夹处于同一层，不要直接在压缩包里运行。

程序使用 Windows 的 .NET Framework 4.x 组件。朗读和听写调用系统本机英语语音；缺少英语语音时，可以先关闭朗读或安装语音后再使用听写。

## 主要功能

- 新学、历史列表复习、错题复习和自由练习分别保存进度，支持暂停、有效计时和只读回看。
- 例句填空、听写、普通拼写可以组合并调整顺序，支持词形判定、分阶段提示和自定义可接受答案。
- 错题本、易错本和已掌握单词本独立管理，单词只有手动执行“斩”才进入已掌握。
- 多收藏夹支持新建、重命名和多重归属，默认按 `Ctrl+L` 收藏，自由练习可使用收藏词。
- 历史复习支持手动多选列表，按选择顺序排列并去重；未完成的普通学习列表跨日后可继续或结束。
- 错题复习支持公平抽取和独立的自由模式，题型与答错复现可分别设置。
- 统计看板与学习日历展示学习记录，外部 CSV 可以用于添加词书和单元。
- 主界面使用滚轮选择模块；各场景支持自定义字体、反馈文案、图片或循环视频背景，以及不影响文字显示的面板透明度。
- 新手引导附着在真实操作界面上，支持回放；壁纸库可以复用素材、管理归属和清理未使用文件。
- 提供撤销、手动及自动备份，以及带 SHA-256 校验的 GitHub 更新检查。

功能变化详见 [v1.4.0 更新说明](RELEASE_NOTES_v1.4.0.md)，更新器修复详见 [v1.4.1 更新说明](RELEASE_NOTES_v1.4.1.md)。

## 升级

升级前先完全关闭程序，并备份原来的整个程序文件夹。

已有用户下载升级包后，将其中的 `main.exe` 和 `data/preserve-archived-progress-v1.4.0.flag` 合并到原目录。`data` 是合并，不能先删除旧目录。原有的 `study_state.json`、`notebook_state.json`、`practice_state.json`、词库和个人外观设置均应保留。不要用其他人的学习状态文件替换自己的记录。

v1.4.0 及更早版本请先手动升级，不能依靠旧更新器下载这次修复。v1.4.1 采用以下固定资产名称。

```text
ZJU-English-dictation-tool_kry-advanced-v1.4.1-full.zip
ZJU-English-dictation-tool_kry-advanced-v1.4.1-upgrade.zip
SHA256SUMS.txt
```

完整包内置默认动态主题，升级包只携带程序和升级所需文件，不覆盖已有外观与壁纸。

## 源码构建

在 Windows PowerShell 中进入仓库目录，运行下面的命令。构建脚本使用系统 C# 编译器、Windows Forms、WPF 和 `System.Speech`。

```powershell
.\build.ps1
```

默认输出在上一级 `_build/main.exe`。运行前，需在可执行程序同层放置仓库中的 `data` 目录。源码仓库不保存编译后的程序、动态壁纸或个人学习数据；可直接使用 Release 完整包获取可运行版本。

## 来源与使用说明

感谢 Lucent-Snow 制作并公开原项目，也感谢 [e 志大英默写器](https://eng.zjueva.net/) 及非牛顿流体、BymE 和 E 志者协会的相关工作。

`book2`、`book3` 来源于学校相关英语教材，主要用于校内非商业学习交流。第三方代码、词库和素材的权利归对应权利方，本项目不额外授予这些材料的版权或传播许可。相关说明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。如权利人对内容有疑问，请通过 Issues 联系。

## 反馈

遇到问题时，请尽量附上版本号、操作步骤和截图，说明预期结果与实际结果。欢迎通过 [GitHub Issues](https://github.com/kryptonite309/ZJU-English-dictation-tool_kry-advanced/issues) 反馈，也欢迎提交 Pull Request。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal sealed class ReleaseUpdate
    {
        public string version { get; set; }
        public string tag { get; set; }
        public string notes { get; set; }
        public string zipName { get; set; }
        public string zipUrl { get; set; }
        public string sumsUrl { get; set; }
    }

    internal sealed class UpdateService
    {
        public const string CurrentVersion = "1.2.0";
        private const string ReleasesApi = "https://api.github.com/repos/kryptonite309/ZJU-English-dictation-tool_kry-advanced/releases/latest";
        private readonly string root;
        private readonly BackupService backups;

        public UpdateService(string root, BackupService backups)
        { this.root = root; this.backups = backups; }

        public void CheckAsync(Form owner, bool silentNetworkErrors)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                ReleaseUpdate update = null;
                Exception failure = null;
                try { update = FetchLatest(); }
                catch (Exception error) { failure = error; }
                if (owner.IsDisposed || !owner.IsHandleCreated) return;
                owner.BeginInvoke((Action)delegate
                {
                    if (failure != null)
                    {
                        if (!silentNetworkErrors)
                            MessageBox.Show(owner, "检查更新失败：\n" + failure.Message,
                                "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (update == null)
                    {
                        if (!silentNetworkErrors)
                            MessageBox.Show(owner, "当前已是最新版本 v" + CurrentVersion + "。",
                                "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    string notes = string.IsNullOrWhiteSpace(update.notes) ? "（本次发布没有填写更新说明）"
                        : update.notes.Trim();
                    if (notes.Length > 1800) notes = notes.Substring(0, 1800) + "\n……";
                    DialogResult confirm = MessageBox.Show(owner,
                        "发现新版本 " + update.tag + "。\n\n" + notes
                        + "\n\n确认后将自动下载升级包、校验 SHA256、完整备份、安装并重启。继续吗？",
                        "发现更新", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (confirm == DialogResult.Yes) DownloadAndInstallAsync(owner, update);
                });
            });
        }

        private ReleaseUpdate FetchLatest()
        {
            Dictionary<string, object> release = JsonRequest(ReleasesApi);
            if (release.ContainsKey("draft") && Convert.ToBoolean(release["draft"])) return null;
            if (release.ContainsKey("prerelease") && Convert.ToBoolean(release["prerelease"])) return null;
            string tag = Convert.ToString(release["tag_name"]);
            Version latest;
            if (!Version.TryParse((tag ?? string.Empty).Trim().TrimStart('v', 'V'), out latest))
                throw new InvalidDataException("GitHub Release 的版本号格式无法识别：" + tag);
            Version current = new Version(CurrentVersion);
            if (latest <= current) return null;
            object[] assets = release.ContainsKey("assets") ? release["assets"] as object[] : null;
            if (assets == null) throw new InvalidDataException("新版本没有可下载文件。");
            Dictionary<string, object> zip = assets.Cast<Dictionary<string, object>>()
                .FirstOrDefault(x => Convert.ToString(x["name"]).EndsWith(".zip",
                    StringComparison.OrdinalIgnoreCase)
                    && Convert.ToString(x["name"]).Contains("升级包"));
            Dictionary<string, object> sums = assets.Cast<Dictionary<string, object>>()
                .FirstOrDefault(x => string.Equals(Convert.ToString(x["name"]),
                    "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase));
            if (zip == null || sums == null)
                throw new InvalidDataException("新版本必须同时提供名称含“升级包”的 ZIP 和 SHA256SUMS.txt。");
            return new ReleaseUpdate
            {
                version = latest.ToString(), tag = tag,
                notes = release.ContainsKey("body") ? Convert.ToString(release["body"]) : string.Empty,
                zipName = Convert.ToString(zip["name"]),
                zipUrl = Convert.ToString(zip["browser_download_url"]),
                sumsUrl = Convert.ToString(sums["browser_download_url"])
            };
        }

        private void DownloadAndInstallAsync(Form owner, ReleaseUpdate update)
        {
            MainForm main = owner as MainForm ?? owner.Owner as MainForm;
            if (main != null) main.PrepareForProgramUpdate();
            Form progress = new Form { Text = "正在准备更新", StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(560, 150), ControlBox = false, BackColor = Theme.Background,
                ForeColor = Theme.Text, Font = Theme.UiFont };
            ModernUI.ApplyAppIcon(progress);
            Label status = new Label { Text = "正在下载并校验升级包……", Dock = DockStyle.Top,
                Height = 70, TextAlign = ContentAlignment.MiddleCenter };
            ProgressBar bar = new ProgressBar { Dock = DockStyle.Top, Height = 22,
                Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25 };
            progress.Controls.Add(bar); progress.Controls.Add(status); Theme.Apply(progress);
            progress.Show(owner);
            ThreadPool.QueueUserWorkItem(delegate
            {
                string report = Path.Combine(root, "update-install-report.json");
                try
                {
                    string temporary = Path.Combine(Path.GetTempPath(), "EnglishDictationUpdate_"
                        + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(temporary);
                    string zipPath = Path.Combine(temporary, update.zipName);
                    string sumsPath = Path.Combine(temporary, "SHA256SUMS.txt");
                    Download(update.zipUrl, zipPath);
                    Download(update.sumsUrl, sumsPath);
                    string expected = ExpectedHash(File.ReadAllText(sumsPath, Encoding.UTF8), update.zipName);
                    string actual = FileHash(zipPath);
                    if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("升级包 SHA256 校验失败，安装已停止。");
                    string newExecutable = ExtractExecutable(zipPath, temporary);
                    BackupInfo backup = backups.Create(true, int.MaxValue);
                    if (backup == null || backup.fileCount < 1)
                        throw new IOException("完整备份没有通过检查，安装已停止。");
                    string target = Path.GetFullPath(Application.ExecutablePath);
                    string oldExecutable = Path.Combine(temporary, "main.previous.exe");
                    File.Copy(target, oldExecutable, true);
                    string helper = Path.Combine(temporary, "update-helper.exe");
                    File.Copy(target, helper, true);
                    ProcessStartInfo start = new ProcessStartInfo(helper,
                        "--apply-update " + Quote(newExecutable) + " " + Quote(target) + " "
                        + Quote(oldExecutable) + " " + Process.GetCurrentProcess().Id + " " + Quote(report))
                    { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = temporary };
                    Process.Start(start);
                    owner.BeginInvoke((Action)delegate
                    {
                        progress.Close();
                        Application.Exit();
                    });
                }
                catch (Exception error)
                {
                    owner.BeginInvoke((Action)delegate
                    {
                        progress.Close();
                        MessageBox.Show(owner, "更新没有安装，现有程序和数据保持不变。\n\n" + error.Message,
                            "更新失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
            });
        }

        public static int ApplyUpdate(string source, string target, string previous,
            int waitPid, string report, bool restart = true)
        {
            try
            {
                try { using (Process process = Process.GetProcessById(waitPid)) process.WaitForExit(); }
                catch (ArgumentException) { }
                string beforeHash = FileHash(source);
                string staged = target + ".update.tmp";
                File.Copy(source, staged, true);
                File.Copy(staged, target, true);
                File.Delete(staged);
                if (!string.Equals(beforeHash, FileHash(target), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("安装后文件校验失败。");
                WriteReport(report, true, null);
                if (restart) Process.Start(new ProcessStartInfo(target) { UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(target) });
                return 0;
            }
            catch (Exception error)
            {
                try { if (File.Exists(previous)) File.Copy(previous, target, true); }
                catch { }
                WriteReport(report, false, error.ToString());
                try { if (restart) Process.Start(new ProcessStartInfo(target) { UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(target) }); }
                catch { }
                return 1;
            }
        }

        public static void ShowPriorResult(Form owner, string root)
        {
            string path = Path.Combine(root, "update-install-report.json");
            if (!File.Exists(path)) return;
            try
            {
                Dictionary<string, object> report = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(
                    File.ReadAllText(path, Encoding.UTF8));
                File.Delete(path);
                bool ok = report.ContainsKey("ok") && Convert.ToBoolean(report["ok"]);
                MessageBox.Show(owner, ok ? "更新安装完成，当前版本为 v" + CurrentVersion + "。"
                    : "更新安装失败，已尝试恢复原程序。\n\n" + Convert.ToString(report["error"]),
                    ok ? "更新完成" : "更新失败", MessageBoxButtons.OK,
                    ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
            catch { }
        }

        private static Dictionary<string, object> JsonRequest(string url)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "ZJU-English-dictation-tool-kry-advanced/" + CurrentVersion;
            request.Timeout = 12000; request.ReadWriteTimeout = 12000;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }
                    .Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
        }

        private static void Download(string url, string path)
        {
            using (WebClient client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = "ZJU-English-dictation-tool-kry-advanced/" + CurrentVersion;
                client.DownloadFile(url, path);
            }
        }

        private static string ExpectedHash(string contents, string filename)
        {
            foreach (string line in (contents ?? string.Empty).Split(new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.Trim();
                if (!trimmed.EndsWith(filename, StringComparison.OrdinalIgnoreCase)) continue;
                string hash = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0];
                if (hash.Length == 64 && hash.All(Uri.IsHexDigit)) return hash.ToUpperInvariant();
            }
            throw new InvalidDataException("SHA256SUMS.txt 中没有升级包的有效校验值。");
        }

        private static string ExtractExecutable(string zipPath, string folder)
        {
            string output = Path.Combine(folder, "main.new.exe");
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                ZipArchiveEntry entry = archive.Entries.FirstOrDefault(x =>
                    string.Equals(Path.GetFileName(x.FullName), "main.exe", StringComparison.OrdinalIgnoreCase));
                if (entry == null || entry.Length <= 0) throw new InvalidDataException("升级包内没有 main.exe。");
                using (Stream input = entry.Open())
                using (FileStream target = File.Create(output)) input.CopyTo(target);
            }
            return output;
        }

        private static string FileHash(string path)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream input = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", string.Empty);
        }

        private static string Quote(string value) { return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\""; }
        private static void WriteReport(string path, bool ok, string error)
        {
            try { File.WriteAllText(path, new JavaScriptSerializer().Serialize(new { ok = ok, error = error }),
                new UTF8Encoding(false)); }
            catch { }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal sealed class BackupInfo
    {
        public string path { get; set; }
        public string kind { get; set; }
        public DateTime createdAt { get; set; }
        public int fileCount { get; set; }
    }

    internal sealed class BackupService
    {
        private readonly string root;
        private readonly string backupRoot;
        private readonly object gate = new object();

        public BackupService(string projectRoot)
        {
            root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar);
            backupRoot = Path.Combine(root, "backups");
            if (!Directory.Exists(Path.Combine(root, "data")))
                throw new DirectoryNotFoundException("备份目标不是有效词书项目。");
        }

        public BackupInfo Create(bool manual, int autoKeep)
        {
            lock (gate)
            {
                string kind = manual ? "manual" : "auto";
                string parent = Path.Combine(backupRoot, kind);
                Directory.CreateDirectory(parent);
                string baseName = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string snapshot = Path.Combine(parent, baseName);
                int suffix = 2;
                while (Directory.Exists(snapshot)) snapshot = Path.Combine(parent, baseName + "_" + suffix++);
                Directory.CreateDirectory(snapshot);
                BackupInfo info = new BackupInfo { path = snapshot, kind = kind, createdAt = DateTime.Now };
                try
                {
                    foreach (string source in EnumerateProjectFiles(root))
                    {
                        string relative = source.Substring(root.Length + 1);
                        string destination = Path.Combine(snapshot, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        File.Copy(source, destination);
                        info.fileCount++;
                    }
                    File.WriteAllText(Path.Combine(snapshot, "backup_manifest.json"),
                        new JavaScriptSerializer().Serialize(info), new UTF8Encoding(false));
                }
                catch
                {
                    // An incomplete snapshot is retained for diagnosis, but excluded from the list.
                    throw;
                }
                if (!manual)
                {
                    List<BackupInfo> backups = List("auto");
                    foreach (BackupInfo old in backups.Skip(Math.Max(1, autoKeep)))
                    {
                        DeleteBackupDirectory(old.path, Path.Combine(backupRoot, "auto"));
                    }
                }
                return info;
            }
        }

        public bool AutoDue(int minutes)
        {
            BackupInfo latest = List("auto").FirstOrDefault();
            return latest == null || DateTime.Now - latest.createdAt >= TimeSpan.FromMinutes(Math.Max(1, minutes));
        }

        public List<BackupInfo> List(string kind)
        {
            string folder = Path.Combine(backupRoot, kind);
            if (!Directory.Exists(folder)) return new List<BackupInfo>();
            List<BackupInfo> result = new List<BackupInfo>();
            foreach (string directory in Directory.GetDirectories(folder))
            {
                string manifest = Path.Combine(directory, "backup_manifest.json");
                if (!File.Exists(manifest)) continue;
                try
                {
                    BackupInfo info = new JavaScriptSerializer().Deserialize<BackupInfo>(
                        File.ReadAllText(manifest, Encoding.UTF8));
                    info.path = directory;
                    info.kind = kind;
                    result.Add(info);
                }
                catch { }
            }
            return result.OrderByDescending(x => x.createdAt).ToList();
        }

        public void DeleteManual(string path)
        {
            string parent = Path.GetFullPath(Path.Combine(backupRoot, "manual"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            if (!target.StartsWith(parent, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetDirectoryName(target).TrimEnd(Path.DirectorySeparatorChar),
                    parent.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                || !File.Exists(Path.Combine(target, "backup_manifest.json")))
                throw new InvalidOperationException("只能删除明确选中的完整手动备份。");
            DeleteBackupDirectory(target, Path.Combine(backupRoot, "manual"));
        }

        public void BeginRestore(string snapshot)
        {
            CheckSnapshot(snapshot);
            Create(true, int.MaxValue); // A separate pre-restore safety snapshot is never auto-deleted.
            string helperDirectory = Path.Combine(backupRoot, "restore-helper");
            Directory.CreateDirectory(helperDirectory);
            string helper = Path.Combine(helperDirectory, "main.restore.exe");
            File.Copy(Application.ExecutablePath, helper, true);
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = helper,
                Arguments = "--restore-from \"" + snapshot + "\" --wait-pid " + Process.GetCurrentProcess().Id,
                UseShellExecute = false, CreateNoWindow = true,
                WorkingDirectory = root
            };
            Process.Start(start);
            Application.Exit();
        }

        public void CompleteRestore(string snapshot, int waitPid)
        {
            CheckSnapshot(snapshot);
            try
            {
                using (Process prior = Process.GetProcessById(waitPid)) prior.WaitForExit(120000);
            }
            catch (ArgumentException) { }
            // Ensure the old executable has released its file before replacing it.
            Thread.Sleep(500);
            string extraRoot = Path.Combine(backupRoot, "restore-extra", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            HashSet<string> expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string source in Directory.GetFiles(snapshot, "*", SearchOption.AllDirectories))
            {
                string relative = source.Substring(snapshot.Length + 1);
                if (relative == "backup_manifest.json") continue;
                expected.Add(relative);
            }
            foreach (string current in EnumerateProjectFiles(root))
            {
                string relative = current.Substring(root.Length + 1);
                if (expected.Contains(relative)) continue;
                string destination = Path.Combine(extraRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Move(current, destination);
            }
            foreach (string source in Directory.GetFiles(snapshot, "*", SearchOption.AllDirectories))
            {
                string relative = source.Substring(snapshot.Length + 1);
                if (relative == "backup_manifest.json") continue;
                string destination = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source, destination, true);
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(root, "main.exe"), UseShellExecute = true, WorkingDirectory = root
            });
        }

        private void CheckSnapshot(string snapshot)
        {
            string candidate = Path.GetFullPath(snapshot).TrimEnd(Path.DirectorySeparatorChar);
            string manual = Path.Combine(backupRoot, "manual") + Path.DirectorySeparatorChar;
            string auto = Path.Combine(backupRoot, "auto") + Path.DirectorySeparatorChar;
            if ((!candidate.StartsWith(manual, StringComparison.OrdinalIgnoreCase)
                && !candidate.StartsWith(auto, StringComparison.OrdinalIgnoreCase))
                || !File.Exists(Path.Combine(candidate, "backup_manifest.json")))
                throw new InvalidOperationException("不是有效的项目备份。");
        }

        private IEnumerable<string> EnumerateProjectFiles(string directory)
        {
            foreach (string file in Directory.GetFiles(directory))
            {
                if (IsTransientDevelopmentFile(file)) continue;
                yield return file;
            }
            foreach (string child in Directory.GetDirectories(directory))
            {
                if (string.Equals(Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar),
                    backupRoot, StringComparison.OrdinalIgnoreCase)) continue;
                DirectoryInfo info = new DirectoryInfo(child);
                if (string.Equals(info.Name, ".git", StringComparison.OrdinalIgnoreCase)) continue;
                if (IsTransientDevelopmentDirectory(info)) continue;
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                foreach (string file in EnumerateProjectFiles(child)) yield return file;
            }
        }

        private bool IsTransientDevelopmentDirectory(DirectoryInfo directory)
        {
            string dataRoot = Path.GetFullPath(Path.Combine(root, "data"))
                .TrimEnd(Path.DirectorySeparatorChar);
            string parent = Path.GetFullPath(directory.Parent.FullName)
                .TrimEnd(Path.DirectorySeparatorChar);
            if (!string.Equals(parent, dataRoot, StringComparison.OrdinalIgnoreCase)) return false;
            return directory.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase)
                || string.Equals(directory.Name, "tmp", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsTransientDevelopmentFile(string file)
        {
            string dataRoot = Path.GetFullPath(Path.Combine(root, "data"))
                .TrimEnd(Path.DirectorySeparatorChar);
            string parent = Path.GetFullPath(Path.GetDirectoryName(file))
                .TrimEnd(Path.DirectorySeparatorChar);
            if (!string.Equals(parent, dataRoot, StringComparison.OrdinalIgnoreCase)) return false;
            string name = Path.GetFileName(file);
            return name.StartsWith("_", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("formal-", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("install-v", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("notebook-report-", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("notebook-v", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("study-report-", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("study-v", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("pos-audit-", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("example-audit-", StringComparison.OrdinalIgnoreCase);
        }

        private static void DeleteBackupDirectory(string path, string expectedParent)
        {
            string target = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            string parent = Path.GetFullPath(expectedParent).TrimEnd(Path.DirectorySeparatorChar);
            string actualParent = Path.GetDirectoryName(target).TrimEnd(Path.DirectorySeparatorChar);
            if (!string.Equals(parent, actualParent, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("备份清理目标超出允许范围：" + target);
            if (!Directory.Exists(target)) return;
            ClearDeleteAttributes(target);
            Directory.Delete(target, true);
        }

        private static void ClearDeleteAttributes(string directory)
        {
            FileAttributes attributes = File.GetAttributes(directory);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("备份目录包含链接，拒绝清理：" + directory);
            foreach (string file in Directory.GetFiles(directory))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            foreach (string child in Directory.GetDirectories(directory))
                ClearDeleteAttributes(child);
            File.SetAttributes(directory, attributes & ~FileAttributes.ReadOnly);
        }
    }
}

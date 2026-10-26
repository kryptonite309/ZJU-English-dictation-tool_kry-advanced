using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

public static class UpdateIntegrationProbe
{
    static string Hash(string path)
    {
        using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }
    static object Call(Type type, string name, object instance, params object[] args)
    {
        return type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Static | BindingFlags.Instance).Invoke(instance, args);
    }
    static string ReadProperty(object value, string name)
    { return Convert.ToString(value.GetType().GetProperty(name).GetValue(value, null)); }
    static void Check(bool value, string label, List<string> checks)
    { if (!value) throw new Exception(label); checks.Add("PASS " + label); }

    public static string Run(string probeExe, string root, string previousExe)
    {
        var checks = new List<string>();
        var json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        try
        {
            root = Path.GetFullPath(root);
            if (!File.Exists(Path.Combine(root, "isolated-update-test.flag")))
                throw new Exception("Run only in an explicitly marked disposable test directory.");
            string data = Path.Combine(root, "data"); Directory.CreateDirectory(data);
            var assembly = Assembly.LoadFile(Path.GetFullPath(probeExe));
            var backupType = assembly.GetType("EnglishDictationTool.BackupService", true);
            var backup = Activator.CreateInstance(backupType, new object[] { root });
            var updateType = assembly.GetType("EnglishDictationTool.UpdateService", true);
            var updater = Activator.CreateInstance(updateType, new object[] { root, backup });
            var update = Call(updateType, "FetchLatest", updater);
            Check(update != null, "actual fixed FetchLatest reads the live GitHub Release", checks);
            string tag = ReadProperty(update, "tag"), name = ReadProperty(update, "zipName");
            Check(name.EndsWith("-upgrade.zip", StringComparison.OrdinalIgnoreCase),
                "actual parser selects upgrade ZIP rather than full ZIP", checks);
            string zipPath = Path.Combine(root, name);
            string sums = Path.Combine(root, "SHA256SUMS.txt");
            Call(updateType, "Download", null, ReadProperty(update, "zipUrl"), zipPath);
            Call(updateType, "Download", null, ReadProperty(update, "sumsUrl"), sums);
            string expected = Convert.ToString(Call(updateType, "ExpectedHash", null,
                File.ReadAllText(sums, Encoding.UTF8), name));
            Check(expected == Hash(zipPath), "real downloaded upgrade ZIP matches published SHA-256", checks);
            bool invalidNameRejected = false;
            try { Call(updateType, "ExpectedHash", null, File.ReadAllText(sums), "unlisted-upgrade.zip"); }
            catch (TargetInvocationException e) { invalidNameRejected = e.InnerException is InvalidDataException; }
            Check(invalidNameRejected, "checksum file-name mismatch is rejected", checks);

            string extracted = Convert.ToString(Call(updateType, "ExtractExecutable", null, zipPath, root));
            Check(File.Exists(extracted), "production extractor reads main.exe from the real ZIP", checks);
            string target = Path.Combine(root, "main.exe"), previous = Path.Combine(root, "main.previous.exe");
            File.Copy(previousExe, target, false); File.Copy(previousExe, previous, false);
            string[] privateFiles = { "study_state.json", "notebook_state.json", "practice_state.json", "appearance.json" };
            var before = new Dictionary<string, string>();
            foreach (string file in privateFiles)
            {
                string path = Path.Combine(root, file);
                File.WriteAllText(path, "{\"syntheticFixture\":true,\"keep\":\"" + file + "\"}", new UTF8Encoding(false));
                before[path] = Hash(path);
            }
            string guard = Path.Combine(data, "preserve-archived-progress-v1.4.0.flag");
            File.WriteAllText(guard, "Synthetic progress-preservation marker", new UTF8Encoding(false));
            before[guard] = Hash(guard);
            object snapshot = Call(backupType, "Create", backup, true, 6);
            string snapshotPath = ReadProperty(snapshot, "path");
            foreach (var entry in before)
            {
                string relative = entry.Key.Substring(root.Length + 1);
                Check(Hash(Path.Combine(snapshotPath, relative)) == entry.Value,
                    "production backup preserves " + relative, checks);
            }
            string report = Path.Combine(root, "install-report.json");
            int exit = Convert.ToInt32(Call(updateType, "ApplyUpdate", null,
                extracted, target, previous, -1, report, false));
            Check(exit == 0 && Hash(target) == Hash(extracted), "production installer updates only the isolated executable", checks);
            foreach (var entry in before) Check(Hash(entry.Key) == entry.Value,
                "successful install leaves " + Path.GetFileName(entry.Key) + " unchanged", checks);
            exit = Convert.ToInt32(Call(updateType, "ApplyUpdate", null,
                Path.Combine(root, "does-not-exist.exe"), target, previous, -1, report, false));
            Check(exit == 1 && Hash(target) == Hash(previous), "production installer rolls back on failure", checks);
            foreach (var entry in before) Check(Hash(entry.Key) == entry.Value,
                "rollback leaves " + Path.GetFileName(entry.Key) + " unchanged", checks);
            return json.Serialize(new { ok = true, liveTag = tag, zipName = name,
                downloadedZipSha256 = Hash(zipPath), extractedExecutableSha256 = Hash(extracted),
                probeCurrentVersion = Convert.ToString(updateType.GetField("CurrentVersion").GetRawConstantValue()),
                note = "Fixed production source; only its CurrentVersion constant is lowered in this private QA build to exercise a newer live Release.",
                checks = checks });
        }
        catch (Exception error)
        {
            while (error.InnerException != null) error = error.InnerException;
            return json.Serialize(new { ok = false, checks = checks,
                errorType = error.GetType().FullName, message = error.Message });
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace EnglishDictationTool
{
    internal static class V136Tests
    {
        public static int Run(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            List<string> failures = new List<string>();
            Version version = new Version("1.3.6");
            string fullName = ReleaseAssetNames.FullPackageName(version);
            string upgradeName = ReleaseAssetNames.UpgradePackageName(version);

            object currentVersion = typeof(UpdateService).GetField("CurrentVersion").GetRawConstantValue();
            Version runningVersion;
            if (!Version.TryParse(Convert.ToString(currentVersion), out runningVersion)
                || runningVersion < version) failures.Add("current version compatibility");
            if (fullName != "ZJU-English-dictation-tool_kry-advanced-v1.3.6-full.zip")
                failures.Add("full package naming");
            if (upgradeName != "ZJU-English-dictation-tool_kry-advanced-v1.3.6-upgrade.zip")
                failures.Add("upgrade package naming");
            if (!ReleaseAssetNames.IsUpgradePackage(upgradeName))
                failures.Add("ascii upgrade detection");
            if (!ReleaseAssetNames.IsUpgradePackage("legacy-v1.3.5-升级包.zip"))
                failures.Add("legacy upgrade detection");
            if (ReleaseAssetNames.IsUpgradePackage(fullName)
                || ReleaseAssetNames.IsUpgradePackage(upgradeName + ".zip")
                || ReleaseAssetNames.IsUpgradePackage("Source-code.zip"))
                failures.Add("upgrade false positive");

            string hash = new string('A', 64);
            string sums = new string('B', 64) + "  " + fullName + "\n"
                + hash + "  " + upgradeName + "\n";
            if (UpdateService.ExpectedHash(sums, upgradeName) != hash)
                failures.Add("checksum exact match");
            string starSums = hash.ToLowerInvariant() + " *" + upgradeName + "\n";
            if (UpdateService.ExpectedHash(starSums, upgradeName) != hash)
                failures.Add("checksum star format");
            bool mismatchRejected = false;
            try { UpdateService.ExpectedHash(sums, "different-upgrade.zip"); }
            catch (InvalidDataException) { mismatchRejected = true; }
            if (!mismatchRejected) failures.Add("checksum filename mismatch");

            string releaseJson = "{\"tag_name\":\"v9.9.9\",\"assets\":["
                + "{\"name\":\"" + upgradeName + "\",\"browser_download_url\":\"https://example.invalid/upgrade.zip\"},"
                + "{\"name\":\"SHA256SUMS.txt\",\"browser_download_url\":\"https://example.invalid/SHA256SUMS.txt\"}]}";
            Dictionary<string, object> parsed = new JavaScriptSerializer()
                .Deserialize<Dictionary<string, object>>(releaseJson);
            System.Collections.IEnumerable parsedAssets = parsed["assets"] as System.Collections.IEnumerable;
            int parsedCount = 0;
            if (parsedAssets != null) foreach (object asset in parsedAssets)
                if (asset is Dictionary<string, object>) parsedCount++;
            if (parsedCount != 2) failures.Add("framework JSON asset collection compatibility");

            Dictionary<string, object> report = new Dictionary<string, object>
            {
                { "ok", failures.Count == 0 },
                { "fullPackage", fullName },
                { "upgradePackage", upgradeName },
                { "checksumFile", ReleaseAssetNames.ChecksumFileName },
                { "frameworkAssetsType", parsed["assets"].GetType().FullName },
                { "failures", failures }
            };
            File.WriteAllText(Path.Combine(outputDirectory, "v136-report.json"),
                new JavaScriptSerializer().Serialize(report), new UTF8Encoding(false));
            return failures.Count == 0 ? 0 : 6;
        }
    }
}

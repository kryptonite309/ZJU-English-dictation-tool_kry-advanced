using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace EnglishDictationTool
{
    internal sealed class PronunciationSettings
    {
        public bool enabled { get; set; }
        public bool automatic { get; set; }
        public string voice { get; set; }
        public int rate { get; set; }
        public int volume { get; set; }
        public int replayKey { get; set; }

        public static PronunciationSettings Defaults()
        {
            return new PronunciationSettings
            {
                enabled = true, automatic = true, voice = string.Empty,
                rate = 0, volume = 100, replayKey = (int)(Keys.Control | Keys.R)
            };
        }
    }

    internal sealed class PronunciationStore
    {
        private readonly string path;
        public PronunciationSettings Settings { get; private set; }

        public PronunciationStore(string projectRoot)
        {
            path = Path.Combine(projectRoot, "pronunciation.json");
            Settings = File.Exists(path)
                ? new JavaScriptSerializer().Deserialize<PronunciationSettings>(
                    File.ReadAllText(path, Encoding.UTF8))
                : PronunciationSettings.Defaults();
            if (Settings == null) throw new InvalidDataException("朗读设置文件无效。请恢复备份或移走 pronunciation.json。");
            Validate(Settings);
        }

        public void Save(PronunciationSettings settings)
        {
            Validate(settings);
            string json = new JavaScriptSerializer().Serialize(settings);
            File.WriteAllText(path, json, new UTF8Encoding(false));
            Settings = settings;
        }

        private static void Validate(PronunciationSettings settings)
        {
            if (settings == null || settings.rate < -10 || settings.rate > 10
                || settings.volume < 0 || settings.volume > 100
                || settings.replayKey == 0)
                throw new InvalidDataException("朗读设置超出允许范围。");
            if (settings.voice == null) settings.voice = string.Empty;
        }
    }

    internal sealed class WordPronouncer : IDisposable
    {
        private readonly PronunciationSettings settings;
        private readonly List<string> englishVoices;
        private SpeechSynthesizer synthesizer;

        public WordPronouncer(PronunciationSettings pronunciationSettings)
        {
            settings = pronunciationSettings;
            englishVoices = GetEnglishVoices();
        }

        public static List<string> GetEnglishVoices()
        {
            try
            {
                using (SpeechSynthesizer available = new SpeechSynthesizer())
                    return available.GetInstalledVoices()
                        .Where(x => x.Enabled && x.VoiceInfo.Culture != null
                            && x.VoiceInfo.Culture.TwoLetterISOLanguageName == "en")
                        .Select(x => x.VoiceInfo.Name).Distinct().ToList();
            }
            catch { return new List<string>(); }
        }

        public bool Speak(string word, out string error)
        {
            error = null;
            if (!settings.enabled) return false;
            if (englishVoices.Count == 0)
            {
                error = "系统没有可用的英语语音；请先在 Windows 中安装英语语音。";
                return false;
            }
            string text = (word ?? string.Empty).Split('/')[0].Trim();
            if (text.Length == 0) return false;
            try
            {
                if (synthesizer == null)
                {
                    synthesizer = new SpeechSynthesizer();
                    synthesizer.SetOutputToDefaultAudioDevice();
                }
                synthesizer.SpeakAsyncCancelAll();
                string selected = englishVoices.Contains(settings.voice)
                    ? settings.voice : englishVoices[0];
                synthesizer.SelectVoice(selected);
                synthesizer.Rate = settings.rate;
                synthesizer.Volume = settings.volume;
                synthesizer.SpeakAsync(text);
                return true;
            }
            catch (Exception exception)
            {
                error = "无法播放英语朗读：" + exception.Message;
                if (synthesizer != null)
                {
                    try { synthesizer.Dispose(); }
                    catch { }
                    synthesizer = null;
                }
                return false;
            }
        }

        public void Stop()
        {
            if (synthesizer == null) return;
            try { synthesizer.SpeakAsyncCancelAll(); }
            catch { }
        }

        public void Dispose()
        {
            if (synthesizer == null) return;
            Stop();
            synthesizer.Dispose();
            synthesizer = null;
        }
    }
}

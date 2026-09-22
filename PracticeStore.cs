using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace EnglishDictationTool
{
    internal sealed class PracticeAttempt
    {
        public string sessionId { get; set; }
        public string word { get; set; }
        public string mode { get; set; }
        public int taskIndex { get; set; }
        public DateTime answeredAt { get; set; }
        public bool correct { get; set; }
        public bool retry { get; set; }
    }

    internal sealed class PracticeSessionState
    {
        public string id { get; set; }
        public DateTime createdAt { get; set; }
        public bool active { get; set; }
        public bool paused { get; set; }
        public PracticeEngineSnapshot engine { get; set; }
        public string input { get; set; }
        public int revealStage { get; set; }
        public bool dictationMeaningShown { get; set; }
        public long activeMilliseconds { get; set; }
    }

    internal sealed class PracticeState
    {
        public int version { get; set; }
        public PracticeSessionState current { get; set; }
        public List<PracticeAttempt> attempts { get; set; }
        public List<PracticeSessionState> completed { get; set; }
    }

    internal sealed class PracticeStore
    {
        private readonly string path;
        private PracticeState state;

        public PracticeStore(string root)
        {
            path = Path.Combine(root, "practice_state.json");
            if (File.Exists(path))
            {
                state = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }
                    .Deserialize<PracticeState>(File.ReadAllText(path, Encoding.UTF8));
                if (state == null || state.version != 1)
                    throw new InvalidDataException("自由练习进度文件无效，未覆盖原文件。");
            }
            else state = new PracticeState { version = 1, attempts = new List<PracticeAttempt>(),
                completed = new List<PracticeSessionState>() };
            if (state.attempts == null) state.attempts = new List<PracticeAttempt>();
            if (state.completed == null) state.completed = new List<PracticeSessionState>();
        }

        public PracticeSessionState Current { get { return state.current; } }
        public IList<PracticeAttempt> Attempts { get { return state.attempts.AsReadOnly(); } }
        public IList<PracticeSessionState> Completed { get { return state.completed.AsReadOnly(); } }

        public void Begin(GameEngine engine)
        {
            if (state.current != null && state.current.active) FinishCurrent();
            state.current = new PracticeSessionState
            {
                id = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6),
                createdAt = DateTime.Now, active = true, paused = false,
                engine = engine.ExportState(), input = string.Empty
            };
            Save();
        }

        public void SavePosition(GameEngine engine, string input, int revealStage,
            bool dictationMeaningShown, bool paused)
        {
            if (state.current == null || !state.current.active) return;
            state.current.engine = engine.ExportState();
            state.current.input = input ?? string.Empty;
            state.current.revealStage = Math.Max(0, revealStage);
            state.current.dictationMeaningShown = dictationMeaningShown;
            state.current.paused = paused;
            Save();
        }

        public void AddActiveMilliseconds(long milliseconds)
        {
            if (state.current == null || !state.current.active || milliseconds <= 0) return;
            state.current.activeMilliseconds = Math.Max(0, state.current.activeMilliseconds + milliseconds);
            Save();
        }

        public void RecordAttempt(WordEntry word, string mode, int taskIndex, bool correct,
            DateTime answeredAt)
        {
            if (state.current == null || !state.current.active) return;
            bool retry = state.attempts.Any(x => x.sessionId == state.current.id
                && x.taskIndex == taskIndex);
            state.attempts.Add(new PracticeAttempt
            {
                sessionId = state.current.id,
                word = GameEngine.CleanEnglish(word), mode = mode,
                taskIndex = taskIndex, correct = correct, retry = retry,
                answeredAt = answeredAt
            });
            Save();
        }

        public void Resume(GameEngine engine)
        {
            if (state.current == null || !state.current.active || state.current.engine == null)
                throw new InvalidOperationException("没有可继续的自由练习。");
            engine.RestoreState(state.current.engine);
        }

        public void SetPaused(bool paused)
        {
            if (state.current == null || !state.current.active) return;
            state.current.paused = paused;
            Save();
        }

        public void FinishCurrent()
        {
            if (state.current == null || !state.current.active) return;
            state.current.active = false;
            state.current.paused = false;
            state.completed.Add(state.current);
            if (state.completed.Count > 500)
                state.completed.RemoveRange(0, state.completed.Count - 500);
            Save();
        }

        private void Save()
        {
            string temporary = path + ".tmp";
            string json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(state);
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace GearMastery
{
    /// <summary>
    /// The mod's log: <c>Documents\Mount and Blade II Bannerlord\Configs\GearMastery\gear_mastery.log</c>,
    /// one timestamped line per event. Trimmed to its newest half past ~1 MB. Best-effort by design: a failed
    /// write must never cost gameplay, so every call swallows its own errors.
    /// </summary>
    internal static class ModLog
    {
        private const long TrimAtBytes = 1_000_000;
        private static readonly object Gate = new object();
        private static readonly HashSet<string> Once = new HashSet<string>();

        // MyDocuments follows a redirected (e.g. OneDrive) Documents folder, as the game itself does.
        public static string Folder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Mount and Blade II Bannerlord", "Configs", "GearMastery");

        public static string FilePath => Path.Combine(Folder, "gear_mastery.log");

        public static void Info(string area, string message) => Write(area, message);

        public static void Error(string area, string message, Exception e) => Write(area, "ERROR " + message + " - " + e);

        /// <summary>For code that runs inside the game's own methods (every hit, every stat refresh): log the first
        /// failure of each site with its stack, then stay quiet.</summary>
        public static void ErrorOnce(string area, Exception e)
        {
            lock (Gate)
            {
                if (!Once.Add(area)) return;
            }
            Write(area, "ERROR (first of possibly many, vanilla behaviour used) - " + e);
            try { TaleWorlds.Library.Debug.Print("[GearMastery] " + area + " failed: " + e.Message); } catch { }
        }

        private static void Write(string area, string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Folder);
                    File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss") + " [" + area + "] " + message + Environment.NewLine);
                    TrimIfHuge();
                }
            }
            catch { /* the log is a luxury, the game is not */ }
        }

        private static void TrimIfHuge()
        {
            var info = new FileInfo(FilePath);
            if (!info.Exists || info.Length <= TrimAtBytes) return;
            var text = File.ReadAllText(FilePath);
            var cut = text.IndexOf('\n', text.Length / 2);
            if (cut < 0) return;
            File.WriteAllText(FilePath, "(older lines trimmed)" + Environment.NewLine + text.Substring(cut + 1));
        }
    }
}

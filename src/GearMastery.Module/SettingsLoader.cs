using System;
using System.IO;
using GearMastery.Core;

namespace GearMastery
{
    /// <summary>
    /// <c>Documents\Mount and Blade II Bannerlord\Configs\GearMastery\settings.json</c>: written with the defaults when
    /// missing, read at every game start (so an edit applies on the next load, no restart). Bad values fall back to
    /// their default with a <c>[settings]</c> log line; an unreadable file means all defaults (the file is left alone).
    /// </summary>
    internal static class SettingsLoader
    {
        public static string FilePath => Path.Combine(ModLog.Folder, SettingsFile.FileName);

        public static GearMasterySettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    Directory.CreateDirectory(ModLog.Folder);
                    File.WriteAllText(FilePath, SettingsFile.Write(new GearMasterySettings()));
                    ModLog.Info("settings", "no settings file - wrote the defaults to " + FilePath);
                }

                var result = SettingsFile.Read(File.ReadAllText(FilePath));
                if (result.Error != null)
                {
                    ModLog.Info("settings", "could not read " + FilePath + " (" + result.Error + ") - ALL DEFAULTS used");
                    return result.Settings;
                }
                foreach (var issue in result.Issues)
                    ModLog.Info("settings", issue);
                ModLog.Info("settings", "loaded " + result.FromFile + " value(s) from " + FilePath
                    + (result.Missing.Count > 0 ? "; defaults for missing: " + string.Join(", ", result.Missing) : ""));
                return result.Settings;
            }
            catch (Exception e)
            {
                ModLog.Error("settings", "settings file failed - ALL DEFAULTS used", e);
                return new GearMasterySettings();
            }
        }
    }
}

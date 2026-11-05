using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GearMastery.Core
{
    /// <summary>One tunable key of settings.json: its name, the words above it, its valid range and how it maps onto
    /// <see cref="GearMasterySettings"/>.</summary>
    public sealed class SettingDef
    {
        internal SettingDef(string key, string description, SettingType type, double min, double max,
            Func<GearMasterySettings, double> get, Action<GearMasterySettings, double> set)
        {
            Key = key;
            Description = description;
            Type = type;
            Min = min;
            Max = max;
            Get = get;
            Set = set;
        }

        public string Key { get; }
        public string Description { get; }
        public SettingType Type { get; }
        public double Min { get; }
        public double Max { get; }
        internal Func<GearMasterySettings, double> Get { get; }
        internal Action<GearMasterySettings, double> Set { get; }
    }

    public enum SettingType
    {
        Number,
        WholeNumber,
        Bool,
    }

    /// <summary>What <see cref="SettingsFile.Read"/> made of a settings.json text.</summary>
    public sealed class SettingsReadResult
    {
        internal SettingsReadResult(GearMasterySettings settings) => Settings = settings;

        /// <summary>Always usable: file values where valid, defaults everywhere else.</summary>
        public GearMasterySettings Settings { get; }

        /// <summary>Null when the text parsed as JSON; otherwise why not (then every value is the default).</summary>
        public string? Error { get; internal set; }

        /// <summary>One line per problem (invalid value, unknown key, duplicate) - each falls back to the default.</summary>
        public List<string> Issues { get; } = new List<string>();

        /// <summary>Settings absent from the file (they take the default - normal after an update adds a key).</summary>
        public List<string> Missing { get; } = new List<string>();

        /// <summary>How many settings took their value from the file.</summary>
        public int FromFile { get; internal set; }
    }

    /// <summary>
    /// settings.json, as text - pure, no disk. A flat JSON object of numbers and booleans, // comments allowed (the file
    /// we write explains every key above it). Dependency-free on purpose: the reader is a small JSON parser, so Core
    /// stays free of game and Newtonsoft references. Every value is checked against its range; anything wrong falls
    /// back to the default with a line in <see cref="SettingsReadResult.Issues"/>, never an exception.
    /// </summary>
    public static class SettingsFile
    {
        public const string FileName = "settings.json";

        private const string Nl = "\r\n";

        // Reductions stop at 90%: a 100% swing / speed loss would freeze an agent.
        private const double MaxReduction = 0.9;
        private const double MaxBonus = 5.0;

        public static readonly IReadOnlyList<SettingDef> All = new List<SettingDef>
        {
            Num("WeaponSlope", "Requirement per item tier for melee and throwing weapons (R = slope x tier, rounded to 5). Vanilla bows sit at about 12 x tier.", 0, 50, s => s.WeaponSlope, (s, v) => s.WeaponSlope = (float)v),
            Num("ShieldSlope", "Requirement per item tier for shields (One Handed).", 0, 50, s => s.ShieldSlope, (s, v) => s.ShieldSlope = (float)v),
            Num("ArmourSlope", "Requirement per item tier for armour (Athletics).", 0, 50, s => s.ArmourSlope, (s, v) => s.ArmourSlope = (float)v),
            Whole("WeaponCap", "Highest requirement a weapon or shield can ask.", 0, 330, s => s.WeaponCap, (s, v) => s.WeaponCap = (int)v),
            Whole("ArmourCap", "Highest Athletics an armour piece can ask.", 0, 330, s => s.ArmourCap, (s, v) => s.ArmourCap = (int)v),
            Num("PenaltyExponent", "Penalty = max x shortfall^exponent. 0.5 bites early on a moderate gap, 1 = linear.", 0.05, 5, s => s.PenaltyExponent, (s, v) => s.PenaltyExponent = (float)v),
            Num("MaxWeaponDamagePenalty", "Melee / thrown weapon damage lost at full shortfall (0.15 = -15%).", 0, MaxReduction, s => s.MaxWeaponDamagePenalty, (s, v) => s.MaxWeaponDamagePenalty = (float)v),
            Num("MaxSwingSpeedPenalty", "Swing and thrust speed lost at full shortfall (0.10 = -10%).", 0, MaxReduction, s => s.MaxSwingSpeedPenalty, (s, v) => s.MaxSwingSpeedPenalty = (float)v),
            Num("MaxHandlingPenalty", "Weapon handling lost at full shortfall.", 0, MaxReduction, s => s.MaxHandlingPenalty, (s, v) => s.MaxHandlingPenalty = (float)v),
            Num("MaxShieldDamageTakenBonus", "Extra damage the shield itself takes at full shortfall (0.25 = +25%).", 0, MaxBonus, s => s.MaxShieldDamageTakenBonus, (s, v) => s.MaxShieldDamageTakenBonus = (float)v),
            Num("MaxArmourSpeedPenalty", "Foot speed lost when ALL worn armour is at full shortfall (shared by armour weight).", 0, MaxReduction, s => s.MaxArmourSpeedPenalty, (s, v) => s.MaxArmourSpeedPenalty = (float)v),
            Num("MaxRangedDamagePenalty", "Bow / crossbow / sling missile damage lost at full shortfall.", 0, MaxReduction, s => s.MaxRangedDamagePenalty, (s, v) => s.MaxRangedDamagePenalty = (float)v),
            Num("MaxReloadSpeedPenalty", "Reload and draw speed lost at full shortfall.", 0, MaxReduction, s => s.MaxReloadSpeedPenalty, (s, v) => s.MaxReloadSpeedPenalty = (float)v),
            Num("MaxInaccuracyBonus", "Extra inaccuracy at full shortfall (0.30 = +30%).", 0, MaxBonus, s => s.MaxInaccuracyBonus, (s, v) => s.MaxInaccuracyBonus = (float)v),
            Num("MaxHorseSpeedPenalty", "Horse speed lost at full Riding shortfall.", 0, MaxReduction, s => s.MaxHorseSpeedPenalty, (s, v) => s.MaxHorseSpeedPenalty = (float)v),
            Num("MaxHorseManeuverPenalty", "Horse maneuver lost at full Riding shortfall.", 0, MaxReduction, s => s.MaxHorseManeuverPenalty, (s, v) => s.MaxHorseManeuverPenalty = (float)v),
            Flag("HeroesOnly", "true = only heroes (you, companions, lords) pay penalties; troops fight as in vanilla.", s => s.HeroesOnly, (s, v) => s.HeroesOnly = v),
            Flag("DumpRequirementsCsv", "true = write item_requirements.csv (every item's requirement) next to this file at game start.", s => s.DumpRequirementsCsv, (s, v) => s.DumpRequirementsCsv = v),
        };

        private static SettingDef Num(string key, string text, double min, double max, Func<GearMasterySettings, float> get, Action<GearMasterySettings, double> set) =>
            new SettingDef(key, text, SettingType.Number, min, max, s => get(s), set);

        private static SettingDef Whole(string key, string text, double min, double max, Func<GearMasterySettings, int> get, Action<GearMasterySettings, double> set) =>
            new SettingDef(key, text, SettingType.WholeNumber, min, max, s => get(s), set);

        private static SettingDef Flag(string key, string text, Func<GearMasterySettings, bool> get, Action<GearMasterySettings, bool> set) =>
            new SettingDef(key, text, SettingType.Bool, 0, 1, s => get(s) ? 1 : 0, (s, v) => set(s, v != 0));

        // ------------------------------------------------------------------ write

        /// <summary>The commented file for these settings (defaults: <c>new GearMasterySettings()</c>).</summary>
        public static string Write(GearMasterySettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var defaults = new GearMasterySettings();
            var sb = new StringBuilder(4096);
            sb.Append("// Gear Mastery settings. Edit, save, then load a save or start a battle: the file is read at every game start.").Append(Nl);
            sb.Append("// A value that is missing, out of range or not a number falls back to its default (see gear_mastery.log).").Append(Nl);
            sb.Append("// Delete this file to get a fresh one with the defaults.").Append(Nl);
            sb.Append('{').Append(Nl);
            for (int i = 0; i < All.Count; i++)
            {
                var def = All[i];
                sb.Append("  // ").Append(def.Description).Append(Nl);
                sb.Append("  //   default ").Append(Format(def, def.Get(defaults)));
                if (def.Type != SettingType.Bool)
                    sb.Append(", range ").Append(Format(def, def.Min)).Append(" to ").Append(Format(def, def.Max));
                sb.Append(Nl);
                sb.Append("  \"").Append(def.Key).Append("\": ").Append(Format(def, def.Get(settings)));
                if (i < All.Count - 1) sb.Append(',');
                sb.Append(Nl);
                if (i < All.Count - 1) sb.Append(Nl);
            }
            sb.Append('}').Append(Nl);
            return sb.ToString();
        }

        private static string Format(SettingDef def, double value) => def.Type switch
        {
            SettingType.Bool => value != 0 ? "true" : "false",
            SettingType.WholeNumber => ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture),
            _ => ((decimal)Math.Round(value, 4)).ToString("0.####", CultureInfo.InvariantCulture),
        };

        // ------------------------------------------------------------------ read

        /// <summary>Settings from a settings.json text. Never throws; see <see cref="SettingsReadResult"/>.</summary>
        public static SettingsReadResult Read(string? text)
        {
            var result = new SettingsReadResult(new GearMasterySettings());
            Dictionary<string, object?> obj;
            try
            {
                obj = new JsonReader(text ?? "").ReadRootObject(result.Issues);
            }
            catch (FormatException e)
            {
                result.Error = e.Message;
                foreach (var def in All) result.Missing.Add(def.Key);
                return result;
            }

            var byKey = new Dictionary<string, SettingDef>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in All) byKey[def.Key] = def;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in obj)
            {
                if (!byKey.TryGetValue(pair.Key, out var def))
                {
                    result.Issues.Add("\"" + pair.Key + "\" is not a setting - ignored (a typo?)");
                    continue;
                }
                seen.Add(def.Key);
                if (TryValue(def, pair.Value, out double value, out string? why))
                {
                    def.Set(result.Settings, value);
                    result.FromFile++;
                }
                else
                {
                    result.Issues.Add(def.Key + ": " + why + " - default " + Format(def, def.Get(new GearMasterySettings())) + " used");
                }
            }

            foreach (var def in All)
                if (!seen.Contains(def.Key)) result.Missing.Add(def.Key);
            return result;
        }

        private static bool TryValue(SettingDef def, object? raw, out double value, out string? why)
        {
            value = 0;
            why = null;
            if (def.Type == SettingType.Bool)
            {
                if (raw is bool b)
                {
                    value = b ? 1 : 0;
                    return true;
                }
                why = Describe(raw) + " is not true / false";
                return false;
            }
            if (!(raw is double d))
            {
                why = Describe(raw) + " is not a number";
                return false;
            }
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                why = "not a finite number";
                return false;
            }
            if (def.Type == SettingType.WholeNumber && d != Math.Floor(d))
            {
                why = Describe(raw) + " is not a whole number";
                return false;
            }
            if (d < def.Min || d > def.Max)
            {
                why = Describe(raw) + " is outside " + Format(def, def.Min) + " to " + Format(def, def.Max);
                return false;
            }
            value = d;
            return true;
        }

        private static string Describe(object? raw) => raw switch
        {
            null => "null",
            string s => "\"" + s + "\"",
            bool b => b ? "true" : "false",
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            _ => "a list / object",
        };

        /// <summary>
        /// A small JSON reader: objects, arrays, strings, numbers, true / false / null; // and /* */ comments and trailing
        /// commas tolerated (hand-edited files). Throws <see cref="FormatException"/> with the line on bad syntax.
        /// </summary>
        private sealed class JsonReader
        {
            private readonly string _s;
            private int _i;

            public JsonReader(string s) => _s = s;

            public Dictionary<string, object?> ReadRootObject(List<string> issues)
            {
                SkipWs();
                if (Peek() != '{') throw Fail("the file must be one { ... } object");
                var obj = ReadObject(issues);
                SkipWs();
                if (_i < _s.Length) throw Fail("unexpected text after the closing }");
                return obj;
            }

            private Dictionary<string, object?> ReadObject(List<string>? issues)
            {
                var obj = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                _i++; // {
                SkipWs();
                if (Peek() == '}') { _i++; return obj; }
                while (true)
                {
                    SkipWs();
                    if (Peek() == '}') { _i++; return obj; } // trailing comma
                    if (Peek() != '"') throw Fail("expected a \"key\"");
                    string key = ReadString();
                    SkipWs();
                    if (Peek() != ':') throw Fail("expected : after \"" + key + "\"");
                    _i++;
                    var value = ReadValue();
                    if (obj.ContainsKey(key)) issues?.Add("\"" + key + "\" appears twice - the last one wins");
                    obj[key] = value;
                    SkipWs();
                    char c = Peek();
                    if (c == ',') { _i++; continue; }
                    if (c == '}') { _i++; return obj; }
                    throw Fail("expected , or } after the value of \"" + key + "\"");
                }
            }

            private List<object?> ReadArray()
            {
                var list = new List<object?>();
                _i++; // [
                while (true)
                {
                    SkipWs();
                    if (Peek() == ']') { _i++; return list; }
                    list.Add(ReadValue());
                    SkipWs();
                    char c = Peek();
                    if (c == ',') { _i++; continue; }
                    if (c == ']') { _i++; return list; }
                    throw Fail("expected , or ] in a list");
                }
            }

            private object? ReadValue()
            {
                SkipWs();
                char c = Peek();
                if (c == '{') return ReadObject(null);
                if (c == '[') return ReadArray();
                if (c == '"') return ReadString();
                if (c == '-' || c == '+' || c == '.' || (c >= '0' && c <= '9')) return ReadNumber();
                if (Word("true")) return true;
                if (Word("false")) return false;
                if (Word("null")) return null;
                throw Fail(c == '\0' ? "the file ends too early" : "unexpected '" + c + "'");
            }

            private bool Word(string w)
            {
                if (string.CompareOrdinal(_s, _i, w, 0, w.Length) != 0) return false;
                int end = _i + w.Length;
                if (end < _s.Length && char.IsLetterOrDigit(_s[end])) return false;
                _i = end;
                return true;
            }

            private double ReadNumber()
            {
                int start = _i;
                while (_i < _s.Length && "+-.eE0123456789".IndexOf(_s[_i]) >= 0) _i++;
                string token = _s.Substring(start, _i - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    throw Fail("\"" + token + "\" is not a number (use a dot for decimals: 0.15)");
                return d;
            }

            private string ReadString()
            {
                var sb = new StringBuilder();
                _i++; // "
                while (true)
                {
                    if (_i >= _s.Length) throw Fail("a \"text\" is never closed");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c == '\n') throw Fail("a \"text\" is never closed");
                    if (c != '\\') { sb.Append(c); continue; }
                    if (_i >= _s.Length) throw Fail("a \"text\" is never closed");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (_i + 4 > _s.Length || !int.TryParse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                throw Fail("bad \\u escape");
                            sb.Append((char)code);
                            _i += 4;
                            break;
                        default: sb.Append(e); break; // \" \\ \/
                    }
                }
            }

            private void SkipWs()
            {
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (char.IsWhiteSpace(c) || c == '﻿') { _i++; continue; }
                    if (c == '/' && _i + 1 < _s.Length && _s[_i + 1] == '/')
                    {
                        while (_i < _s.Length && _s[_i] != '\n') _i++;
                        continue;
                    }
                    if (c == '/' && _i + 1 < _s.Length && _s[_i + 1] == '*')
                    {
                        int end = _s.IndexOf("*/", _i + 2, StringComparison.Ordinal);
                        if (end < 0) throw Fail("a /* comment is never closed");
                        _i = end + 2;
                        continue;
                    }
                    return;
                }
            }

            private char Peek() => _i < _s.Length ? _s[_i] : '\0';

            private FormatException Fail(string what)
            {
                int line = 1;
                for (int k = 0; k < _i && k < _s.Length; k++) if (_s[k] == '\n') line++;
                return new FormatException("line " + line + ": " + what);
            }
        }
    }
}

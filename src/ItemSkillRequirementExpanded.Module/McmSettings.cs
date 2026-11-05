using System;
using System.Linq;
using System.Runtime.CompilerServices;
using ItemSkillRequirementExpanded.Core;

namespace ItemSkillRequirementExpanded
{
    /// <summary>
    /// The Mod Configuration Menu page (MCM v5, module Bannerlord.MBOptionScreen) - a SOFT dependency.
    ///
    /// Built with MCM's fluent builder, not an AttributeGlobalSettings subclass: the game loads our DLL with
    /// Assembly.GetTypes(), and any type whose base type or field type lives in a missing MCMv5.dll would stop the whole
    /// mod from loading for players without MCM (trax_combat_enhancements' CLAUDE.md, "MCM is truly optional"). So MCM
    /// types appear ONLY inside method bodies here, and every callback MCM gets is an <c>Action&lt;object&gt;</c> instance
    /// method or a plain Func/Action - never a lambda whose parameter is an MCM type (the compiler caches those in a
    /// static field typed on MCM).
    ///
    /// The page reads and writes <see cref="ItemRequirements.Settings"/> directly through ProxyRefs (the Core
    /// <see cref="SettingsMenu"/> list is the mapping), so a change applies at once: penalties at the next stat refresh
    /// or hit, the tooltip at its next draw, requirements after the item table is rebuilt (done on every such change).
    /// MCM saves the values itself (json2, Configs\ModSettings\Global\ItemSkillRequirementExpanded). Without MCM the mod runs on the
    /// defaults.
    /// </summary>
    internal static class McmSettings
    {
        private const string McmAssemblyName = "MCMv5";

        private static bool _done;
        private static object? _registered; // the FluentGlobalSettings, typed object on purpose (kept alive)

        public static bool IsRegistered => _registered != null;

        /// <summary>Main menu and game start. Registers once; logs once when MCM is absent or fails.</summary>
        public static void TryRegister(string when)
        {
            if (_done) return;
            bool loaded = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => string.Equals(a.GetName().Name, McmAssemblyName, StringComparison.OrdinalIgnoreCase));
            if (!loaded)
            {
                _done = true;
                ModLog.Info("mcm", "Mod Configuration Menu (MCMv5) not loaded - running on the default settings");
                return;
            }
            try
            {
                if (Register())
                {
                    _done = true;
                    ModLog.Info("mcm", "settings page registered (" + when + "): " + SettingsMenu.All.Count + " settings; values now "
                        + Describe(ItemRequirements.Settings));
                }
                else
                {
                    ModLog.Info("mcm", "MCM not ready yet (" + when + ") - defaults for now, will try again at game start");
                    if (when == "game start") _done = true;
                }
            }
            catch (Exception e)
            {
                _done = true;
                ModLog.Error("mcm", "settings page failed - running on the default settings", e);
            }
        }

        /// <summary>Every setting that differs from the default, for the log.</summary>
        internal static string Describe(SkillRequirementSettings settings)
        {
            var defaults = new SkillRequirementSettings();
            var changed = SettingsMenu.All
                .Where(s => Math.Abs(s.Get(settings) - s.Get(defaults)) > 1e-6)
                .Select(s => s.Id + "=" + s.Get(settings).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
                .ToList();
            return changed.Count == 0 ? "all defaults" : string.Join(", ", changed);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Register()
        {
            var builder = MCM.Abstractions.FluentBuilder.BaseSettingsBuilder.Create(SettingsMenu.PageId, SettingsMenu.PageName);
            if (builder == null) return false;
            builder = builder
                .SetFolderName(SettingsMenu.FolderName)
                .SetFormat("json2");
            foreach (var group in SettingsMenu.Groups)
                builder = builder.CreateGroup(group.Name, (Action<object>)new GroupFiller(group).Fill);
            var settings = builder.BuildAsGlobal();
            settings.Register(); // loads the saved values through the ProxyRefs into ItemRequirements.Settings
            _registered = settings;
            return true;
        }

        private sealed class GroupFiller
        {
            private readonly MenuGroup _group;

            public GroupFiller(MenuGroup group) => _group = group;

            public void Fill(object groupBuilder)
            {
                var g = (MCM.Abstractions.FluentBuilder.ISettingsPropertyGroupBuilder)groupBuilder;
                g.SetGroupOrder(_group.Order);
                int order = 0;
                foreach (var s in SettingsMenu.InGroup(_group))
                {
                    var p = new PropertyFiller(s, order++);
                    switch (s.Kind)
                    {
                        case MenuKind.Bool:
                            g.AddBool(s.Id, s.Label, new MCM.Common.ProxyRef<bool>(p.GetBool, p.SetBool), (Action<object>)p.Configure);
                            break;
                        case MenuKind.Int:
                            g.AddInteger(s.Id, s.Label, (int)s.Min, (int)s.Max,
                                new MCM.Common.ProxyRef<int>(p.GetInt, p.SetInt), (Action<object>)p.Configure);
                            break;
                        default:
                            g.AddFloatingInteger(s.Id, s.Label, (float)s.Min, (float)s.Max,
                                new MCM.Common.ProxyRef<float>(p.GetFloat, p.SetFloat), (Action<object>)p.Configure);
                            break;
                    }
                }
            }
        }

        /// <summary>One setting's live getter / setter (on <see cref="ItemRequirements.Settings"/>) and its MCM options.</summary>
        private sealed class PropertyFiller
        {
            private readonly MenuSetting _s;
            private readonly int _order;

            public PropertyFiller(MenuSetting s, int order)
            {
                _s = s;
                _order = order;
            }

            public bool GetBool() => _s.Get(ItemRequirements.Settings) != 0;
            public int GetInt() => (int)Math.Round(_s.Get(ItemRequirements.Settings));
            public float GetFloat() => (float)_s.Get(ItemRequirements.Settings);

            public void SetBool(bool value) => Set(value ? 1 : 0);
            public void SetInt(int value) => Set(value);
            public void SetFloat(float value) => Set(value);

            private void Set(double value)
            {
                try
                {
                    double before = _s.Get(ItemRequirements.Settings);
                    if (!_s.Set(ItemRequirements.Settings, value)) return;
                    if (_s.AffectsRequirements) ItemRequirements.Reset(); // rebuilt lazily with the new table
                    ModLog.Info("settings", _s.Id + ": " + Text(before) + " -> " + Text(_s.Get(ItemRequirements.Settings)) + " (MCM)");
                }
                catch (Exception e)
                {
                    ModLog.Error("mcm", "setting " + _s.Id, e);
                }
            }

            private static string Text(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

            public void Configure(object propertyBuilder)
            {
                switch (_s.Kind)
                {
                    case MenuKind.Bool:
                        ((MCM.Abstractions.FluentBuilder.Models.ISettingsPropertyBoolBuilder)propertyBuilder)
                            .SetOrder(_order).SetRequireRestart(false).SetHintText(_s.Hint);
                        break;
                    case MenuKind.Int:
                        var i = (MCM.Abstractions.FluentBuilder.Models.ISettingsPropertyIntegerBuilder)propertyBuilder;
                        i.SetOrder(_order).SetRequireRestart(false).SetHintText(_s.Hint);
                        i.AddValueFormat(_s.Format ?? "0");
                        break;
                    default:
                        var f = (MCM.Abstractions.FluentBuilder.Models.ISettingsPropertyFloatingIntegerBuilder)propertyBuilder;
                        f.SetOrder(_order).SetRequireRestart(false).SetHintText(_s.Hint);
                        f.AddValueFormat(_s.Format ?? "0");
                        break;
                }
            }
        }
    }
}

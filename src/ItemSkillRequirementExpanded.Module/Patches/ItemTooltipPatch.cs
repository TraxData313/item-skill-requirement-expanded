using System;
using System.Collections.Generic;
using ItemSkillRequirementExpanded.Core;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ItemSkillRequirementExpanded.Patches
{
    /// <summary>
    /// The inventory item tooltip (ItemMenuVM), round 2 (step 9):
    /// - "Requires: &lt;Skill&gt; &lt;R&gt; (yours &lt;n&gt;)" red / green for every item we cover, per usage tab - only
    ///   when the who-toggles cover the hero (else the whole tooltip stays vanilla).
    /// - When the character is short AND the who-toggles cover him: vanilla's own stat rows carry the value after the
    ///   penalty with the breakdown ("Swing Damage: 37 (99-62) Cut", horse "Speed: 39 (52-13)"), computed by
    ///   Core <see cref="TooltipStats"/> from the same functions the battle models use. In comparison mode each column
    ///   uses its own item's penalty and the green / red better-worse colours compare the AFTER values.
    /// - A compact red "Unskilled: ..." line only for penalties without a stat row (shield damage taken, armour foot
    ///   speed), or as a fallback when no stat row of a short item could be rewritten.
    ///
    /// Vanilla (1.4.8) - RefreshItemTooltips(item, compared, usage) clears the lists, runs SetGeneralComponentTooltip
    /// (weight, then AddSkillRequirement for target + compared when the target has a RelevantSkill and either item has
    /// Difficulty &gt; 0), then the armour / weapon / horse section. So:
    /// - Prefix on RefreshItemTooltips: remember the usage tab (SetGeneralComponentTooltip runs BEFORE
    ///   SetWeaponComponentTooltip stores AlternativeUsageIndex).
    /// - Prefix on AddSkillRequirement: for an item we cover with R &gt; 0, draw our line and skip vanilla's.
    /// - Postfix on SetGeneralComponentTooltip: our requirement line when vanilla did not draw one, then the compact
    ///   line (shield / armour). Both lists get a row (blank on the side without one) so the columns stay aligned.
    /// - Postfix on RefreshItemTooltips: rewrite the stat rows (found by vanilla's own label TextObjects), recolour
    ///   compared rows by after-penalty values; fallback compact line.
    /// Character = ItemMenuVM._character (the hero whose equipment is open); skill = GetSkillValue, as vanilla, plus the
    /// same culture bonus (<see cref="CultureMatch"/>, v1.2.0) everywhere a skill meets R.
    /// </summary>
    internal static class ItemTooltipPatch
    {
        // Private ItemMenuVM fields. Set in InitFieldRefs (from Apply, inside the caller's try/catch) - NOT in field
        // initialisers: a failing static constructor would throw TypeInitializationException from the patch methods,
        // outside their own try/catch, i.e. inside the game's tooltip code.
        private static AccessTools.FieldRef<ItemMenuVM, BasicCharacterObject>? CharacterRef;
        private static AccessTools.FieldRef<ItemMenuVM, ItemVM>? TargetRef;
        private static AccessTools.FieldRef<ItemMenuVM, ItemVM>? ComparedRef;

        // Vanilla's stat-row labels (private readonly TextObject fields of ItemMenuVM) -> our row kind. A row is found
        // by its DefinitionLabel == label.ToString(), so it follows the game's language.
        private enum Row { SwingSpeed, SwingDamage, ThrustSpeed, ThrustDamage, Handling, Damage, Accuracy, Speed, Maneuver }

        private static readonly (string Field, Row Row)[] LabelFields =
        {
            ("_swingSpeedText", Row.SwingSpeed),
            ("_swingDamageText", Row.SwingDamage),
            ("_thrustSpeedText", Row.ThrustSpeed),
            ("_thrustDamageText", Row.ThrustDamage),
            ("_handlingText", Row.Handling),
            ("_damageText", Row.Damage),
            ("_accuracyText", Row.Accuracy),
            ("_speedText", Row.Speed),
            ("_maneuverText", Row.Maneuver),
        };

        private static List<(AccessTools.FieldRef<ItemMenuVM, TextObject> Ref, Row Row)>? _labelRefs;

        private static void InitFieldRefs()
        {
            if (CharacterRef != null && TargetRef != null && ComparedRef != null) return;
            CharacterRef = AccessTools.FieldRefAccess<ItemMenuVM, BasicCharacterObject>("_character");
            TargetRef = AccessTools.FieldRefAccess<ItemMenuVM, ItemVM>("_targetItem");
            ComparedRef = AccessTools.FieldRefAccess<ItemMenuVM, ItemVM>("_comparedItem");
        }

        private static void InitLabelRefs()
        {
            if (_labelRefs != null) return;
            var refs = new List<(AccessTools.FieldRef<ItemMenuVM, TextObject>, Row)>(LabelFields.Length);
            foreach (var (field, row) in LabelFields)
                refs.Add((AccessTools.FieldRefAccess<ItemMenuVM, TextObject>(field), row));
            _labelRefs = refs;
        }

        /// <summary>Vanilla's own text and id (ItemMenuVM._requiresText), so it localises like the native line.</summary>
        private static TextObject RequiresText() => new TextObject("{=154a34f8caccfc833238cc89d38861e8}Requires: ");

        public static void ApplyRequirementLine(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(ItemMenuVM), "AddSkillRequirement",
                    new[] { typeof(ItemVM), typeof(MBBindingList<ItemMenuTooltipPropertyVM>), typeof(bool) })
                ?? throw new MissingMethodException("ItemMenuVM.AddSkillRequirement(ItemVM, MBBindingList<ItemMenuTooltipPropertyVM>, bool) not found");
            InitFieldRefs();
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(ItemTooltipPatch), nameof(AddSkillRequirementPrefix)));
        }

        /// <summary>Remembers the usage tab being drawn: RefreshItemTooltips(item, compared, alternativeUsageIndex) runs
        /// SetGeneralComponentTooltip (our lines) BEFORE SetWeaponComponentTooltip stores the index in
        /// AlternativeUsageIndex, so the public property can still hold the previous item's tab at that point.</summary>
        public static void ApplyUsageIndex(Harmony harmony)
        {
            var target = RefreshItemTooltipsMethod();
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(ItemTooltipPatch), nameof(RefreshItemTooltipsPrefix)));
        }

        /// <summary>The stat rows after the penalty (postfix on RefreshItemTooltips, when every row exists).</summary>
        public static void ApplyStatRows(Harmony harmony)
        {
            var target = RefreshItemTooltipsMethod();
            InitFieldRefs();
            InitLabelRefs(); // a renamed label field fails here (logged by the caller), not in every tooltip
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(ItemTooltipPatch), nameof(RefreshItemTooltipsPostfix)));
        }

        private static System.Reflection.MethodInfo RefreshItemTooltipsMethod() =>
            AccessTools.Method(typeof(ItemMenuVM), "RefreshItemTooltips", new[] { typeof(ItemVM), typeof(ItemVM), typeof(int) })
            ?? throw new MissingMethodException("ItemMenuVM.RefreshItemTooltips(ItemVM, ItemVM, int) not found");

        // Usage tab of the tooltip being built (UI thread only). 0 = primary usage, vanilla's default.
        private static int _usageIndex;

        private static void RefreshItemTooltipsPrefix(int alternativeUsageIndex) => _usageIndex = alternativeUsageIndex;

        public static void ApplyGeneralTooltip(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(ItemMenuVM), "SetGeneralComponentTooltip", Type.EmptyTypes)
                ?? throw new MissingMethodException("ItemMenuVM.SetGeneralComponentTooltip() not found");
            InitFieldRefs(); // a renamed field fails here (logged by the caller), not in every tooltip
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(ItemTooltipPatch), nameof(SetGeneralComponentTooltipPostfix)));
        }

        private static bool AddSkillRequirementPrefix(ItemMenuVM __instance, ItemVM itemVm,
            MBBindingList<ItemMenuTooltipPropertyVM> itemProperties, bool isComparison)
        {
            try
            {
                var req = Covered(itemVm);
                if (req == null || itemProperties == null || CharacterRef == null) return true;
                var character = CharacterRef(__instance);
                if (!Applies(character)) return true; // who-toggle off: vanilla's line (and vanilla's lock colour)
                itemProperties.Add(RequirementLine(req, SkillOf(__instance, req, isComparison), character, isComparison));
                return false;
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("ItemTooltipPatch.AddSkillRequirement", e);
                return true;
            }
        }

        private static void SetGeneralComponentTooltipPostfix(ItemMenuVM __instance)
        {
            try
            {
                if (CharacterRef == null || TargetRef == null || ComparedRef == null) return;
                var target = TargetRef(__instance);
                var targetItem = target?.ItemRosterElement.EquipmentElement.Item;
                if (targetItem == null) return;
                var compared = ComparedRef(__instance);
                bool comparing = __instance.IsComparing && compared?.ItemRosterElement.EquipmentElement.Item != null;
                var comparedItem = comparing ? compared!.ItemRosterElement.EquipmentElement.Item : null;
                var character = CharacterRef(__instance);
                if (!Applies(character)) return; // who-toggle off: the tooltip stays vanilla

                var targetReq = Covered(target);
                var comparedReq = comparing ? Covered(compared) : null;
                if (targetReq == null && comparedReq == null) return;

                // Same condition as vanilla's call to AddSkillRequirement (our prefix already drew those lines).
                bool vanillaDrew = targetItem.RelevantSkill != null
                    && (targetItem.Difficulty > 0 || (comparing && comparedItem!.Difficulty > 0));
                if (!vanillaDrew)
                {
                    __instance.TargetItemProperties.Add(targetReq != null ? RequirementLine(targetReq, SkillOf(__instance, targetReq, false), character, false) : Blank());
                    if (comparing)
                        __instance.ComparedItemProperties.Add(comparedReq != null ? RequirementLine(comparedReq, SkillOf(__instance, comparedReq, true), character, true) : Blank());
                }

                // Compact line only for what has no stat row: shield damage taken, armour foot speed.
                string? targetPenalty = targetReq != null && HasNoStatRow(targetReq.Kind)
                    ? PenaltyText(targetReq, SkillOf(__instance, targetReq, false), character, false) : null;
                string? comparedPenalty = comparedReq != null && HasNoStatRow(comparedReq.Kind)
                    ? PenaltyText(comparedReq, SkillOf(__instance, comparedReq, true), character, false) : null;
                AddCompactLines(__instance, comparing, targetPenalty, comparedPenalty);
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("ItemTooltipPatch.SetGeneralComponentTooltip", e);
            }
        }

        /// <summary>Does the mod apply to the hero whose inventory is open? No character (a trade screen without one):
        /// yes, the requirement is shown green as plain information. Otherwise the who-toggles decide; an uncovered
        /// hero sees vanilla's tooltip, matching vanilla's lock in <see cref="CanUseItemPatch"/>.</summary>
        private static bool Applies(BasicCharacterObject? character) => character == null || AgentGear.CharacterPays(character);

        private static bool HasNoStatRow(SkillKind kind) => kind == SkillKind.Shield || kind == SkillKind.Armour;

        private static void AddCompactLines(ItemMenuVM vm, bool comparing, string? targetPenalty, string? comparedPenalty)
        {
            if (targetPenalty == null && comparedPenalty == null) return;
            vm.TargetItemProperties.Add(targetPenalty != null
                ? new ItemMenuTooltipPropertyVM(UnskilledText().ToString(), targetPenalty, 0, UIColors.NegativeIndicator)
                : Blank());
            if (comparing)
                vm.ComparedItemProperties.Add(comparedPenalty != null
                    ? new ItemMenuTooltipPropertyVM("", comparedPenalty, 0, UIColors.NegativeIndicator)
                    : Blank());
        }

        // ---- stat rows after the penalty ----

        /// <summary>One column of the tooltip: its item, the usage shown, and the character's shortfall on it.</summary>
        private sealed class Side
        {
            public EquipmentElement Element;
            public ItemRequirement? Req;
            public int UsageIndex = -1;      // index into item.Weapons, -1 for horses / armour
            public bool Thrown;              // the usage shown is a thrown one (javelin thrown)
            public float Shortfall;          // 0 = vanilla values (not covered, met, or who-toggle off)
            public int Rewritten;            // stat rows rewritten for this side
            public bool Short => Req != null && Shortfall > 0f;
        }

        private static void RefreshItemTooltipsPostfix(ItemMenuVM __instance, int alternativeUsageIndex)
        {
            try
            {
                if (CharacterRef == null || TargetRef == null || ComparedRef == null || _labelRefs == null) return;
                var targetVm = TargetRef(__instance);
                var targetEl = targetVm?.ItemRosterElement.EquipmentElement ?? EquipmentElement.Invalid;
                if (targetEl.Item == null) return;
                var comparedVm = ComparedRef(__instance);
                bool comparing = __instance.IsComparing && comparedVm?.ItemRosterElement.EquipmentElement.Item != null;
                var character = CharacterRef(__instance);
                if (character == null || !AgentGear.CharacterPays(character)) return; // who-toggle off: vanilla rows

                var target = MakeSide(targetEl, alternativeUsageIndex, character);
                Side? compared = null;
                if (comparing)
                {
                    var comparedEl = comparedVm!.ItemRosterElement.EquipmentElement;
                    compared = MakeSide(comparedEl, ComparedUsageIndex(targetEl, alternativeUsageIndex, comparedEl), character);
                }
                if (!target.Short && (compared == null || !compared.Short)) return;

                var settings = ItemRequirements.Settings;
                var targetRows = __instance.TargetItemProperties;
                var comparedRows = __instance.ComparedItemProperties;
                // Vanilla adds every stat row to both lists in step (compared rows have definition " "); anything else
                // (dev-mode market lines) breaks the pairing, and then the compared column is left alone.
                bool paired = compared != null && comparedRows != null && comparedRows.Count == targetRows.Count;

                var labels = new Dictionary<string, Row>();
                foreach (var (fieldRef, row) in _labelRefs)
                {
                    var text = fieldRef(__instance)?.ToString();
                    if (!string.IsNullOrEmpty(text) && !labels.ContainsKey(text!)) labels[text!] = row;
                }

                for (int i = 0; i < targetRows.Count; i++)
                {
                    var tRow = targetRows[i];
                    if (tRow?.DefinitionLabel == null || !labels.TryGetValue(tRow.DefinitionLabel, out var row)) continue;
                    var cRow = paired ? comparedRows![i] : null;

                    var tValue = StatValue(target, row, settings, compared, out int tBase, out int tAfter);
                    int? cBase = null, cAfter = null;
                    if (cRow != null && StatValue(compared!, row, settings, target, out int cb, out int ca) is TooltipStat)
                    {
                        cBase = cb;
                        cAfter = ca;
                    }

                    bool tChanged = tValue != null && tAfter != tBase && Rewrite(tRow, tBase, tAfter);
                    bool cChanged = cRow != null && cBase.HasValue && cAfter != cBase && Rewrite(cRow, cBase.Value, cAfter!.Value);
                    if (tChanged) target.Rewritten++;
                    if (cChanged) compared!.Rewritten++;
                    if (!tChanged && !cChanged) continue;

                    if (cRow != null && tValue != null && cBase.HasValue)
                    {
                        // Vanilla's better / worse colours, on the after-penalty values (higher is better for all these rows).
                        int result = TooltipStats.Compare(tChanged ? tAfter : tBase, cChanged ? cAfter!.Value : cBase.Value);
                        tRow.TextColor = ComparisonColor(result, false);
                        cRow.TextColor = ComparisonColor(result, true);
                    }
                    else if (tChanged && cRow == null)
                    {
                        tRow.TextColor = UIColors.NegativeIndicator; // single tooltip: a penalised row reads red
                    }
                }

                // Fallback: short on a weapon / launcher / horse but no row could be rewritten -> the compact line.
                string? tLine = target.Short && target.Rewritten == 0 && !HasNoStatRow(target.Req!.Kind)
                    ? PenaltyText(target.Req, SkillOfSide(target), character, target.Thrown) : null;
                string? cLine = compared != null && compared.Short && compared.Rewritten == 0 && !HasNoStatRow(compared.Req!.Kind)
                    ? PenaltyText(compared.Req, SkillOfSide(compared), character, compared.Thrown) : null;
                if (tLine != null || cLine != null)
                    AddCompactLines(__instance, compared != null && comparedRows != null && comparedRows.Count == targetRows.Count, tLine, cLine);
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("ItemTooltipPatch.RefreshItemTooltips", e);
            }
        }

        private static Side MakeSide(EquipmentElement element, int usageIndex, BasicCharacterObject character)
        {
            var side = new Side { Element = element };
            var item = element.Item;
            var weapons = item?.Weapons;
            if (weapons != null && weapons.Count > 0)
                side.UsageIndex = usageIndex >= 0 && usageIndex < weapons.Count ? usageIndex : 0;
            var req = ItemRequirements.Get(item);
            if (req == null || !req.HasRequirement) return side;
            side.Req = req;
            var usage = side.UsageIndex >= 0 ? weapons![side.UsageIndex] : null;
            side.Thrown = CombatPenalties.IsThrownUsage(req.Kind, usage != null && usage.IsRangedWeapon);
            var skill = SkillOfSide(side);
            side.Shortfall = skill == null ? 0f
                : Requirements.Shortfall(req.Value, CultureMatch.Effective(character, req, ItemRequirements.GetSkill(character, skill)));
            return side;
        }

        private static SkillObject SkillOfSide(Side side) =>
            side.UsageIndex >= 0 ? side.Req!.SkillForUsage(side.UsageIndex) : side.Req!.Skill;

        /// <summary>The compared item's usage shown beside the target's tab - vanilla's GetComparedWeapon pairing.</summary>
        private static int ComparedUsageIndex(EquipmentElement target, int targetUsage, EquipmentElement compared)
        {
            var tWeapons = target.Item?.Weapons;
            if (compared.Item?.Weapons == null || compared.Item.Weapons.Count == 0) return -1;
            if (tWeapons == null || targetUsage < 0 || targetUsage >= tWeapons.Count) return 0;
            return ItemHelper.IsWeaponComparableWithUsage(compared.Item, tWeapons[targetUsage].WeaponDescriptionId, out int index) ? index : 0;
        }

        /// <summary>
        /// The row's stat for this side, its vanilla number and the number after the penalty (equal when unpenalised).
        /// Null when the row means nothing for this side (e.g. a shield's swing speed, an item we do not cover).
        /// </summary>
        /// An item we do not cover at all (only possible on the compared side) is read like the other side's family
        /// (<paramref name="like"/>) with no penalty, so the colours still compare like with like.
        private static TooltipStat? StatValue(Side side, Row row, SkillRequirementSettings settings, Side? like, out int value, out int after)
        {
            value = after = 0;
            var kindSide = side.Req != null ? side : like;
            if (kindSide?.Req == null || side.UsageIndex < 0 && kindSide.Req.Kind != SkillKind.Mount) return null;
            var stat = StatFor(kindSide.Req.Kind, kindSide.Thrown, row);
            if (stat == null) return null;
            value = BaseValue(side, stat.Value);
            float m = side.Req == null ? 1f : TooltipStats.Multiplier(side.Req.Kind, side.Thrown, stat.Value, side.Shortfall, settings, side.Req.DisplayedTier);
            after = TooltipStats.After(stat.Value, value, m);
            return stat;
        }

        private static TooltipStat? StatFor(SkillKind kind, bool thrown, Row row)
        {
            switch (kind)
            {
                case SkillKind.Weapon when thrown:
                    return row == Row.Damage ? TooltipStat.ThrownDamage : row == Row.Accuracy ? TooltipStat.Accuracy : (TooltipStat?)null;
                case SkillKind.Weapon:
                    return row switch
                    {
                        Row.SwingSpeed => TooltipStat.SwingSpeed,
                        Row.SwingDamage => TooltipStat.SwingDamage,
                        Row.ThrustSpeed => TooltipStat.ThrustSpeed,
                        Row.ThrustDamage => TooltipStat.ThrustDamage,
                        Row.Handling => TooltipStat.Handling,
                        _ => (TooltipStat?)null,
                    };
                case SkillKind.Ranged:
                    return row switch
                    {
                        Row.SwingSpeed => TooltipStat.LauncherSpeed,
                        Row.Damage => TooltipStat.LauncherDamage,
                        Row.Accuracy => TooltipStat.Accuracy,
                        _ => (TooltipStat?)null,
                    };
                case SkillKind.Mount:
                    return row == Row.Speed ? TooltipStat.MountSpeed : row == Row.Maneuver ? TooltipStat.MountManeuver : (TooltipStat?)null;
                default:
                    return null;
            }
        }

        /// <summary>The same numbers vanilla's rows show (ItemMenuVM.SetWeaponComponentTooltip / SetHorseComponentTooltip).</summary>
        private static int BaseValue(Side side, TooltipStat stat)
        {
            var el = side.Element;
            int u = side.UsageIndex;
            switch (stat)
            {
                case TooltipStat.SwingSpeed:
                case TooltipStat.LauncherSpeed: return el.GetModifiedSwingSpeedForUsage(u);
                case TooltipStat.SwingDamage: return el.GetModifiedSwingDamageForUsage(u);
                case TooltipStat.ThrustSpeed: return el.GetModifiedThrustSpeedForUsage(u);
                case TooltipStat.ThrustDamage:
                case TooltipStat.LauncherDamage: return el.GetModifiedThrustDamageForUsage(u);
                case TooltipStat.ThrownDamage: return el.GetModifiedMissileDamageForUsage(u);
                case TooltipStat.Handling: return el.GetModifiedHandlingForUsage(u);
                case TooltipStat.Accuracy: return el.Item.GetWeaponWithUsageIndex(u).Accuracy;
                case TooltipStat.MountSpeed: return el.GetModifiedMountSpeed(in EquipmentElement.Invalid);
                case TooltipStat.MountManeuver: return el.GetModifiedMountManeuver(in EquipmentElement.Invalid);
                default: return 0;
            }
        }

        /// <summary>"99 Cut" -> "37 (99-62) Cut". False (row untouched) when the row's text does not hold the number.</summary>
        private static bool Rewrite(ItemMenuTooltipPropertyVM row, int value, int after)
        {
            var text = TooltipStats.InsertBreakdown(row.ValueLabel, value, TooltipStats.Breakdown(after, value));
            if (text == null) return false;
            row.ValueLabel = text;
            return true;
        }

        /// <summary>Vanilla's GetColorFromComparison; "equal" is the default (uncoloured) text colour.</summary>
        private static Color ComparisonColor(int result, bool isCompared)
        {
            if (result == 0) return new Color(0f, 0f, 0f, 0f); // TooltipProperty's default = no colour
            bool good = isCompared ? result < 0 : result > 0;
            return good ? UIColors.PositiveIndicator : UIColors.NegativeIndicator;
        }

        // ---- requirement line ----

        /// <summary>Our requirement for this tooltip item, only when it asks something (R &gt; 0).</summary>
        private static ItemRequirement? Covered(ItemVM? itemVm)
        {
            var item = itemVm?.ItemRosterElement.EquipmentElement.Item;
            var req = ItemRequirements.Get(item);
            return req != null && req.HasRequirement && req.Skill != null ? req : null;
        }

        /// <summary>
        /// The skill for the usage tab on show: the target item's tab index; for the compared item, its usage of the same
        /// kind (WeaponDescriptionId, as vanilla pairs them in GetComparedWeapon), else its primary skill.
        /// </summary>
        private static SkillObject SkillOf(ItemMenuVM vm, ItemRequirement req, bool compared)
        {
            if (req.UsageCount == 0) return req.Skill;
            if (!compared) return req.SkillForUsage(_usageIndex);
            var targetItem = TargetRef?.Invoke(vm)?.ItemRosterElement.EquipmentElement.Item;
            var weapons = targetItem?.Weapons;
            var usage = weapons != null && _usageIndex >= 0 && _usageIndex < weapons.Count ? weapons[_usageIndex] : null;
            return req.SkillFor(usage);
        }

        /// <summary>Vanilla's look: "Requires: " (target side only) + "&lt;Skill&gt; &lt;R&gt; (yours &lt;n&gt;)", or with the
        /// same culture bonus "(yours 150 +35 Battanian)"; green when met (effective skill) or no character, red when short.</summary>
        private static ItemMenuTooltipPropertyVM RequirementLine(ItemRequirement req, SkillObject skill, BasicCharacterObject? character, bool isComparison)
        {
            string definition = isComparison ? "" : RequiresText().ToString();
            if (character == null)
                return new ItemMenuTooltipPropertyVM(definition, skill.Name + " " + req.Value, 0, UIColors.PositiveIndicator);
            int yours = ItemRequirements.GetSkill(character, skill);
            // Same culture bonus: the colour follows the effective skill; the text shows raw + the bonus
            // ("yours 150 +35 Battanian"), the bonus rounded down so a red line never adds up to R.
            double effective = CultureMatch.Effective(character, req, yours);
            bool met = !TooltipPenalties.IsShort(req.Value, effective);
            var text = new TextObject("{=isre_requires_yours}{SKILL} {VALUE} (yours {YOURS})");
            text.SetTextVariable("SKILL", skill.Name);
            text.SetTextVariable("VALUE", req.Value);
            text.SetTextVariable("YOURS", CultureBonus.YoursText(yours, effective, CultureMatch.Adjective(req)));
            return new ItemMenuTooltipPropertyVM(definition, text.ToString(), 0, met ? UIColors.PositiveIndicator : UIColors.NegativeIndicator);
        }

        private static ItemMenuTooltipPropertyVM Blank() => new ItemMenuTooltipPropertyVM("", "", 0);

        // ---- compact penalty line ----

        /// <summary>"+25% shield damage taken", or null when not short, not covered by the who-toggles, or nothing
        /// rounds above 0%.</summary>
        private static string? PenaltyText(ItemRequirement req, SkillObject skillObject, BasicCharacterObject? character, bool thrownUsage)
        {
            if (character == null || !AgentGear.CharacterPays(character)) return null;
            double skill = CultureMatch.Effective(character, req, ItemRequirements.GetSkill(character, skillObject));
            if (!TooltipPenalties.IsShort(req.Value, skill)) return null;
            var parts = TooltipPenalties.For(req.Kind, Requirements.Shortfall(req.Value, skill), ItemRequirements.Settings, thrownUsage, req.DisplayedTier);
            if (parts.Count == 0) return null;
            var texts = new List<string>(parts.Count);
            foreach (var part in parts)
            {
                var t = PartText(part.Stat);
                t.SetTextVariable("VALUE", part.SignedPercent);
                texts.Add(t.ToString());
            }
            return string.Join(", ", texts);
        }

        private static TextObject UnskilledText() => new TextObject("{=isre_unskilled}Unskilled: ");

        // New TextObject each time: SetTextVariable mutates it.
        private static TextObject PartText(PenaltyStat stat) => stat switch
        {
            PenaltyStat.Damage => new TextObject("{=isre_pen_damage}{VALUE}% damage"),
            PenaltyStat.SwingSpeed => new TextObject("{=isre_pen_swing}{VALUE}% swing speed"),
            PenaltyStat.Handling => new TextObject("{=isre_pen_handling}{VALUE}% handling"),
            PenaltyStat.ShieldDamageTaken => new TextObject("{=isre_pen_shield}{VALUE}% shield damage taken"),
            PenaltyStat.FootSpeedUpTo => new TextObject("{=isre_pen_armour}up to {VALUE}% foot speed (shared by armour weight)"),
            PenaltyStat.ReloadSpeed => new TextObject("{=isre_pen_reload}{VALUE}% reload and draw speed"),
            PenaltyStat.Inaccuracy => new TextObject("{=isre_pen_aim}{VALUE}% inaccuracy"),
            PenaltyStat.HorseSpeed => new TextObject("{=isre_pen_horse_speed}{VALUE}% speed"),
            PenaltyStat.HorseManeuver => new TextObject("{=isre_pen_horse_maneuver}{VALUE}% maneuver"),
            _ => new TextObject("{VALUE}%"),
        };
    }
}

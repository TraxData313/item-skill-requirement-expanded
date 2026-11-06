using System;
using System.Collections.Generic;
using HarmonyLib;
using ItemSkillRequirementExpanded.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Inventory;

namespace ItemSkillRequirementExpanded.Patches
{
    /// <summary>
    /// Orange inventory rows for items above the selected hero's skill (v1.1.0). No UIExtenderEx, no prefab change:
    /// vanilla's <c>InventoryItemTupleWidget.UpdateEquipmentTypeState</c> picks the row's brush (red
    /// <c>Inventory.Tuple.CharacterCantUse.*</c> when the character can't use it, <c>CantUseInSet</c> in the civilian /
    /// stealth set, else the default). A postfix swaps the DEFAULT one for our orange brush
    /// (<c>module\GUI\Brushes\ItemSkillRequirementExpanded.xml</c>, a copy of vanilla's red tuple brush in orange), so
    /// vanilla's red always wins and vanilla resets the brush itself whenever the row stops being ours.
    ///
    /// The widget knows only its <c>ItemID</c> (SPItemVM.StringId = item id + modifier id); the item and the character
    /// come from the open <see cref="SPInventoryVM"/> (its lists, its <c>_currentCharacter</c>), captured in
    /// <c>UpdateRightCharacter</c> - which vanilla runs on open, on every character switch and after equipping. That
    /// also clears our per-row cache and re-runs the brush choice on every live row (vanilla's setters only re-run it
    /// when a bound value CHANGES, so a switch between two heroes who can both use an item would not). Skills cannot
    /// change while the inventory is open; a new inventory = a new VM = a fresh cache.
    /// </summary>
    internal static class InventoryHighlightPatch
    {
        private const string BrushLeft = "ItemSkillRequirementExpanded.Tuple.AboveSkill.Left";
        private const string BrushRight = "ItemSkillRequirementExpanded.Tuple.AboveSkill.Right";

        private static readonly AccessTools.FieldRef<SPInventoryVM, CharacterObject> CurrentCharacter =
            AccessTools.FieldRefAccess<SPInventoryVM, CharacterObject>("_currentCharacter");

        private static Action<InventoryItemTupleWidget>? _update;
        private static WeakReference<SPInventoryVM>? _inventory;
        private static readonly Dictionary<string, bool> Cache = new Dictionary<string, bool>();
        private static int _cacheVersion = -1; // ItemRequirements.SettingsVersion the Cache was filled under
        private static readonly HashSet<InventoryItemTupleWidget> Live = new HashSet<InventoryItemTupleWidget>();
        private static bool _brushMissingLogged;

        public static void Apply(Harmony harmony)
        {
            var update = AccessTools.Method(typeof(InventoryItemTupleWidget), "UpdateEquipmentTypeState")
                ?? throw new MissingMethodException("InventoryItemTupleWidget.UpdateEquipmentTypeState not found");
            var connected = AccessTools.Method(typeof(InventoryItemTupleWidget), "OnConnectedToRoot")
                ?? throw new MissingMethodException("InventoryItemTupleWidget.OnConnectedToRoot not found");
            var disconnected = AccessTools.Method(typeof(InventoryItemTupleWidget), "OnDisconnectedFromRoot")
                ?? throw new MissingMethodException("InventoryItemTupleWidget.OnDisconnectedFromRoot not found");
            var itemId = AccessTools.PropertySetter(typeof(InventoryItemTupleWidget), nameof(InventoryItemTupleWidget.ItemID))
                ?? throw new MissingMethodException("InventoryItemTupleWidget.ItemID setter not found");
            var rightCharacter = AccessTools.Method(typeof(SPInventoryVM), "UpdateRightCharacter")
                ?? throw new MissingMethodException("SPInventoryVM.UpdateRightCharacter not found");

            _update = AccessTools.MethodDelegate<Action<InventoryItemTupleWidget>>(update);

            harmony.Patch(update, postfix: new HarmonyMethod(typeof(InventoryHighlightPatch), nameof(UpdatePostfix)));
            harmony.Patch(connected, postfix: new HarmonyMethod(typeof(InventoryHighlightPatch), nameof(ConnectedPostfix)));
            harmony.Patch(disconnected, postfix: new HarmonyMethod(typeof(InventoryHighlightPatch), nameof(DisconnectedPostfix)));
            harmony.Patch(itemId, postfix: new HarmonyMethod(typeof(InventoryHighlightPatch), nameof(ItemIdPostfix)));
            harmony.Patch(rightCharacter,
                prefix: new HarmonyMethod(typeof(InventoryHighlightPatch), nameof(RightCharacterPrefix)),
                postfix: new HarmonyMethod(typeof(InventoryHighlightPatch), nameof(RightCharacterPostfix)));
        }

        // ---- the VM side: which inventory, which character; cache and refresh

        private static void RightCharacterPrefix(SPInventoryVM __instance)
        {
            try
            {
                _inventory = new WeakReference<SPInventoryVM>(__instance);
                Cache.Clear();
            }
            catch (Exception e) { ModLog.ErrorOnce("InventoryHighlight.capture", e); }
        }

        private static void RightCharacterPostfix()
        {
            try
            {
                if (Live.Count == 0) return;
                foreach (var widget in new List<InventoryItemTupleWidget>(Live)) Refresh(widget);
            }
            catch (Exception e) { ModLog.ErrorOnce("InventoryHighlight.refresh", e); }
        }

        // ---- the widget side

        private static void ConnectedPostfix(InventoryItemTupleWidget __instance)
        {
            try
            {
                Live.Add(__instance);
                Refresh(__instance);
            }
            catch (Exception e) { ModLog.ErrorOnce("InventoryHighlight.connect", e); }
        }

        private static void DisconnectedPostfix(InventoryItemTupleWidget __instance) => Live.Remove(__instance);

        // Bound after the values that trigger vanilla's brush choice (prefab attribute order), so re-run it once known.
        private static void ItemIdPostfix(InventoryItemTupleWidget __instance)
        {
            try
            {
                if (Live.Contains(__instance)) Refresh(__instance);
            }
            catch (Exception e) { ModLog.ErrorOnce("InventoryHighlight.itemId", e); }
        }

        /// <summary>Re-runs vanilla's brush choice (and so our postfix) when the widget is fully built and on screen.</summary>
        private static void Refresh(InventoryItemTupleWidget widget)
        {
            if (_update == null || widget.MainContainer == null || widget.EquipButton == null || widget.DefaultBrush == null) return;
            if (widget.ScreenWidget == null) return;
            _update(widget);
        }

        private static void UpdatePostfix(InventoryItemTupleWidget __instance)
        {
            try
            {
                var main = __instance.MainContainer;
                var defaultBrush = __instance.DefaultBrush;
                if (main?.Brush == null || defaultBrush == null || __instance.ScreenWidget == null) return;
                // Vanilla left the row in its default look (not red, not "not for this set").
                bool vanillaDefault = main.Brush.IsCloneRelated(defaultBrush) && __instance.IsEquipable && __instance.CanCharacterUseItem;
                if (!vanillaDefault || !ItemRequirements.Settings.HighlightItemsAboveSkill) return;
                if (!IsAboveSkill(__instance.ItemID, __instance.IsRightSide)) return;

                var brush = __instance.Context?.GetBrush(__instance.IsRightSide ? BrushRight : BrushLeft);
                if (brush == null)
                {
                    if (!_brushMissingLogged)
                    {
                        _brushMissingLogged = true;
                        ModLog.Info("inventory", "orange row brush not found (GUI\\Brushes\\ItemSkillRequirementExpanded.xml missing?) - no highlight");
                    }
                    return;
                }
                main.Brush = brush;
            }
            catch (Exception e) { ModLog.ErrorOnce("InventoryHighlight.update", e); }
        }

        // ---- the decision

        private static bool IsAboveSkill(string? itemId, bool rightSide)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            SPInventoryVM? vm = null;
            if (_inventory == null || !_inventory.TryGetTarget(out vm) || vm == null) return false;
            // A settings change (MCM: bonus, toggles, requirements) since the rows were cached: decide afresh.
            int version = ItemRequirements.SettingsVersion;
            if (version != _cacheVersion)
            {
                Cache.Clear();
                _cacheVersion = version;
            }
            if (Cache.TryGetValue(itemId!, out var cached)) return cached;

            var item = FindItem(rightSide ? vm.RightItemListVM : vm.LeftItemListVM, itemId!)
                ?? FindItem(rightSide ? vm.LeftItemListVM : vm.RightItemListVM, itemId!);
            if (item == null) return false; // not in the lists yet: not cached, asked again on the next refresh

            bool result = false;
            var character = CurrentCharacter(vm);
            var req = ItemRequirements.Get(item);
            if (character != null && req != null && req.HasRequirement)
            {
                // Multi-usage weapons: the PRIMARY usage's skill (the tab vanilla's tooltip opens on).
                var skill = req.SkillFor(null);
                result = InventoryHighlight.AboveSkill(ItemRequirements.Settings, AgentGear.CharacterPays(character),
                    vanillaDefaultLook: true, req.Value, CultureMatch.Effective(character, req, ItemRequirements.GetSkill(character, skill)));
            }
            Cache[itemId!] = result;
            return result;
        }

        private static ItemObject? FindItem(MBBindingList<SPItemVM>? list, string itemId)
        {
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
            {
                var vm = list[i];
                if (vm != null && vm.StringId == itemId) return vm.ItemRosterElement.EquipmentElement.Item;
            }
            return null;
        }
    }
}

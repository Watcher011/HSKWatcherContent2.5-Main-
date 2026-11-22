using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace HSK.Watcher.Content
{
    // ==================== КЛАССЫ ДЛЯ КОМПОНЕНТОВ ====================
    public class CompProperties_ReinforcementTimer : CompProperties
    {
        public CompProperties_ReinforcementTimer()
        {
            this.compClass = typeof(CompReinforcementTimer);
        }
    }

    public class CompReinforcementTimer : ThingComp
    {
        private int expiryTick = -1;
        private int durationDays = 0;
        private bool isMercenary = false;

        public void SetExpiryTick(int tick)
        {
            expiryTick = tick;
        }

        public int GetExpiryTick() => expiryTick;

        public void SetDurationDays(int days)
        {
            durationDays = days;
        }

        public void SetIsMercenary(bool value)
        {
            isMercenary = value;
        }

        public bool IsMercenary() => isMercenary;

        public bool IsActive()
        {
            return isMercenary && expiryTick > 0 && Find.TickManager.TicksGame < expiryTick;
        }

        public bool IsExpired()
        {
            return isMercenary && expiryTick > 0 && Find.TickManager.TicksGame >= expiryTick;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref expiryTick, "expiryTick", -1);
            Scribe_Values.Look(ref durationDays, "durationDays", 0);
            Scribe_Values.Look(ref isMercenary, "isMercenary", false);
        }

        public override string CompInspectStringExtra()
        {
            if (isMercenary && expiryTick > 0)
            {
                int ticksLeft = expiryTick - Find.TickManager.TicksGame;
                if (ticksLeft > 0)
                {
                    string timeLeft = ticksLeft.ToStringTicksToPeriod();
                    return "Наёмник (осталось: " + timeLeft + ")";
                }
                else
                {
                    return "Контракт истёк";
                }
            }
            return base.CompInspectStringExtra();
        }
    }

    public class CompProperties_CallReinforcements : CompProperties
    {
        public CompProperties_CallReinforcements()
        {
            this.compClass = typeof(CompCallReinforcements);
        }
    }

    public class CompCallReinforcements : ThingComp
    {
        private Map Map => this.parent.Map;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (Map != null && this.parent.Faction == Faction.OfPlayer)
            {
                var powerComp = this.parent.GetComp<CompPowerTrader>();
                if (powerComp == null || powerComp.PowerOn)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "Вызвать подкрепления",
                        defaultDesc = "Вызвать наёмников на помощь",
                        icon = ContentFinder<Texture2D>.Get("UI/Commands/CallReinforcements", false) ?? TexCommand.Attack,
                        action = delegate
                        {
                            Find.WindowStack.Add(new Dialog_CallReinforcements(Map));
                        }
                    };
                }
            }
        }
    }

    // ==================== HARMONY ПАТЧИ ====================
    [StaticConstructorOnStartup]
    public static class HarmonyPatcher
    {
        static HarmonyPatcher()
        {
            try
            {
                var harmony = new Harmony("HSK.Watcher.Content");

                // Применяем патчи вручную
                PatchApparelTracker(harmony);
                PatchEquipmentTracker(harmony);
                PatchFloatMenu(harmony);
                PatchGearTab(harmony);
                PatchGizmos(harmony);
                PatchThingDef(harmony);
                PatchInspectString(harmony);
                PatchOtherMethods(harmony);

                //Log.Message("[HSK.Watcher] Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
            //    Log.Error($"[HSK.Watcher] Failed to apply Harmony patches: {ex}");
            }
        }

        private static void PatchApparelTracker(Harmony harmony)
        {
            try
            {
                var originalMethod = typeof(Pawn_ApparelTracker).GetMethod("TryDrop",
                    new Type[] { typeof(Apparel), typeof(Apparel).MakeByRefType(), typeof(IntVec3), typeof(bool) });

                if (originalMethod != null)
                {
                    var prefixMethod = typeof(Pawn_ApparelTracker_TryDrop_Patch).GetMethod("Prefix");
                    if (prefixMethod != null)
                    {
                        harmony.Patch(originalMethod, prefix: new HarmonyMethod(prefixMethod));
                        //Log.Message("[HSK.Watcher] ApparelTracker.TryDrop patched");
                    }
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error patching ApparelTracker: {ex}");
            }
        }

        private static void PatchEquipmentTracker(Harmony harmony)
        {
            try
            {
                var originalMethod = typeof(Pawn_EquipmentTracker).GetMethod("TryDropEquipment",
                    new Type[] { typeof(ThingWithComps), typeof(ThingWithComps).MakeByRefType(), typeof(IntVec3), typeof(bool) });

                if (originalMethod != null)
                {
                    var prefixMethod = typeof(Pawn_EquipmentTracker_TryDropEquipment_Patch).GetMethod("Prefix");
                    if (prefixMethod != null)
                    {
                        harmony.Patch(originalMethod, prefix: new HarmonyMethod(prefixMethod));
                        //Log.Message("[HSK.Watcher] EquipmentTracker.TryDropEquipment patched");
                    }
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error patching EquipmentTracker: {ex}");
            }
        }

        private static void PatchFloatMenu(Harmony harmony)
        {
            try
            {
                var originalMethod = typeof(FloatMenuMakerMap).GetMethod("AddHumanlikeOrders",
                    new Type[] { typeof(Vector3), typeof(Pawn), typeof(List<FloatMenuOption>) });

                if (originalMethod != null)
                {
                    var postfixMethod = typeof(FloatMenuMakerMap_AddHumanlikeOrders_Patch).GetMethod("Postfix");
                    if (postfixMethod != null)
                    {
                        harmony.Patch(originalMethod, postfix: new HarmonyMethod(postfixMethod));
                        //Log.Message("[HSK.Watcher] FloatMenuMakerMap.AddHumanlikeOrders patched");
                    }
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error patching FloatMenuMakerMap: {ex}");
            }
        }

        private static void PatchGearTab(Harmony harmony)
        {
            try
            {
                var interfaceDropMethod = typeof(ITab_Pawn_Gear).GetMethod("InterfaceDrop",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (interfaceDropMethod != null)
                {
                    var prefixMethod = typeof(ITab_Pawn_Gear_InterfaceDrop_Patch).GetMethod("Prefix");
                    if (prefixMethod != null)
                    {
                        harmony.Patch(interfaceDropMethod, prefix: new HarmonyMethod(prefixMethod));
                        //Log.Message("[HSK.Watcher] ITab_Pawn_Gear.InterfaceDrop patched");
                    }
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error patching ITab_Pawn_Gear: {ex}");
            }
        }

        private static void PatchGizmos(Harmony harmony)
        {
            try
            {
                var originalMethod = typeof(Pawn).GetMethod("GetGizmos");
                if (originalMethod != null)
                {
                    var postfixMethod = typeof(Pawn_GetGizmos_Patch).GetMethod("Postfix");
                    if (postfixMethod != null)
                    {
                        harmony.Patch(originalMethod, postfix: new HarmonyMethod(postfixMethod));
                        //Log.Message("[HSK.Watcher] Pawn.GetGizmos patched");
                    }
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error patching Pawn.GetGizmos: {ex}");
            }
        }

        private static void PatchThingDef(Harmony harmony)
        {
            try
            {
                var originalMethod = typeof(ThingDef).GetMethod("PostLoad");
                if (originalMethod != null)
                {
                    var postfixMethod = typeof(ThingDef_PostLoad_Patch).GetMethod("Postfix");
                    if (postfixMethod != null)
                    {
                        harmony.Patch(originalMethod, postfix: new HarmonyMethod(postfixMethod));
                        //Log.Message("[HSK.Watcher] ThingDef.PostLoad patched");
                    }
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error patching ThingDef.PostLoad: {ex}");
            }
        }

        private static void PatchInspectString(Harmony harmony)
        {
            try
            {
                var originalMethod = typeof(Pawn).GetMethod("GetInspectString");
                if (originalMethod != null)
                {
                    var postfixMethod = typeof(Pawn_GetInspectString_Patch).GetMethod("Postfix");
                    if (postfixMethod != null)
                    {
                        harmony.Patch(originalMethod, postfix: new HarmonyMethod(postfixMethod));
                        //Log.Message("[HSK.Watcher] Pawn.GetInspectString patched");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[HSK.Watcher] Error patching Pawn.GetInspectString: {ex}");
            }
        }

        private static void PatchOtherMethods(Harmony harmony)
        {
            try
            {
                // Патч для SpawnSetup
                var spawnSetupMethod = typeof(Pawn).GetMethod("SpawnSetup",
                    new Type[] { typeof(Map), typeof(bool) });
                if (spawnSetupMethod != null)
                {
                    var postfixMethod = typeof(Pawn_SpawnSetup_Patch).GetMethod("Postfix");
                    if (postfixMethod != null)
                    {
                        harmony.Patch(spawnSetupMethod, postfix: new HarmonyMethod(postfixMethod));
                        //Log.Message("[HSK.Watcher] Pawn.SpawnSetup patched");
                    }
                }

                // Патч для LoadGame
                var loadGameMethod = typeof(Game).GetMethod("LoadGame");
                if (loadGameMethod != null)
                {
                    var postfixMethod = typeof(Game_LoadGame_Patch).GetMethod("Postfix");
                    if (postfixMethod != null)
                    {
                        harmony.Patch(loadGameMethod, postfix: new HarmonyMethod(postfixMethod));
                        //Log.Message("[HSK.Watcher] Game.LoadGame patched");
                    }
                }

                //Log.Message("[HSK.Watcher] Other patches applied");
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error patching other methods: {ex}");
            }
        }
    }

    // ==================== ОСНОВНЫЕ ПАТЧИ ====================
    public static class Pawn_ApparelTracker_TryDrop_Patch
    {
        public static bool Prefix(Pawn_ApparelTracker __instance, Apparel ap, ref Apparel resultingAp, ref IntVec3 pos, ref bool forbid)
        {
            try
            {
                if (__instance?.pawn != null && ap != null)
                {
                    var timerComp = __instance.pawn.GetComp<CompReinforcementTimer>();
                    if (timerComp != null && timerComp.IsActive())
                    {
                        if (Current.ProgramState == ProgramState.Playing &&
                            __instance.pawn.Faction == Faction.OfPlayer)
                        {
                            Messages.Message("Снаряжение наёмников заблокировано до окончания контракта.",
                                __instance.pawn, MessageTypeDefOf.RejectInput);
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error in apparel patch: {ex}");
            }

            return true;
        }
    }

    public static class Pawn_EquipmentTracker_TryDropEquipment_Patch
    {
        public static bool Prefix(Pawn_EquipmentTracker __instance, ThingWithComps eq, ref ThingWithComps resultingEq, ref IntVec3 pos, ref bool forbid)
        {
            try
            {
                if (__instance?.pawn != null && eq != null)
                {
                    var timerComp = __instance.pawn.GetComp<CompReinforcementTimer>();
                    if (timerComp != null && timerComp.IsActive())
                    {
                        if (Current.ProgramState == ProgramState.Playing &&
                            __instance.pawn.Faction == Faction.OfPlayer)
                        {
                            Messages.Message("Снаряжение наёмников заблокировано до окончания контракта.",
                                __instance.pawn, MessageTypeDefOf.RejectInput);
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error in equipment patch: {ex}");
            }

            return true;
        }
    }

    public static class FloatMenuMakerMap_AddHumanlikeOrders_Patch
    {
        public static void Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
            try
            {
                if (opts == null || pawn == null)
                    return;

                var timerComp = pawn.GetComp<CompReinforcementTimer>();
                if (timerComp != null && timerComp.IsActive())
                {
                    for (int i = opts.Count - 1; i >= 0; i--)
                    {
                        var option = opts[i];
                        if (option != null)
                        {
                            string label = option.Label?.ToLower() ?? "";
                            if (label.Contains("strip") ||
                                label.Contains("снять") ||
                                label.Contains("drop equipment"))
                            {
                                opts.RemoveAt(i);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error in float menu patch: {ex}");
            }
        }
    }

    public static class ITab_Pawn_Gear_InterfaceDrop_Patch
    {
        public static bool Prefix(ITab_Pawn_Gear __instance, Thing t)
        {
            try
            {
                var pawnField = AccessTools.Field(typeof(ITab_Pawn_Gear), "SelPawnForGear");
                if (pawnField == null) return true;

                var pawn = pawnField.GetValue(__instance) as Pawn;
                if (pawn == null) return true;

                var timerComp = pawn.GetComp<CompReinforcementTimer>();
                if (timerComp != null && timerComp.IsActive())
                {
                    Messages.Message("Нельзя снять снаряжение с наёмника.",
                        MessageTypeDefOf.RejectInput);
                    return false;
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error in gear tab drop patch: {ex}");
            }

            return true;
        }
    }

    public static class Pawn_GetGizmos_Patch
    {
        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            try
            {
                if (__instance == null || __result == null)
                    return;

                var timerComp = __instance.GetComp<CompReinforcementTimer>();
                if (timerComp != null && timerComp.IsActive())
                {
                    var gizmos = new List<Gizmo>(__result);

                    for (int i = gizmos.Count - 1; i >= 0; i--)
                    {
                        var gizmo = gizmos[i];
                        if (gizmo is Command command)
                        {
                            string label = command.defaultLabel?.ToLower() ?? "";
                            string desc = command.defaultDesc?.ToLower() ?? "";

                            if (label.Contains("strip") ||
                                label.Contains("снять") ||
                                desc.Contains("strip") ||
                                desc.Contains("снять"))
                            {
                                gizmos.RemoveAt(i);
                            }
                        }
                    }

                    __result = gizmos;
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error in gizmos patch: {ex}");
            }
        }
    }

    public static class ThingDef_PostLoad_Patch
    {
        public static void Postfix(ThingDef __instance)
        {
            try
            {
                if (__instance.defName == "CommsConsole" ||
                    (__instance.building != null && __instance.building.buildingTags != null &&
                     __instance.building.buildingTags.Contains("CommsConsole")))
                {
                    if (__instance.comps == null)
                    {
                        __instance.comps = new List<CompProperties>();
                    }

                    bool alreadyHasComp = false;
                    for (int i = 0; i < __instance.comps.Count; i++)
                    {
                        var comp = __instance.comps[i];
                        if (comp != null && comp.compClass == typeof(CompCallReinforcements))
                        {
                            alreadyHasComp = true;
                            break;
                        }
                    }

                    if (!alreadyHasComp)
                    {
                        __instance.comps.Add(new CompProperties_CallReinforcements());
                        //Log.Message("[HSK.Watcher] Компонент вызова подкреплений добавлен к консоли связи");
                    }
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error in ThingDef patch: {ex}");
            }
        }
    }

    public static class Pawn_GetInspectString_Patch
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            try
            {
                var timerComp = __instance.GetComp<CompReinforcementTimer>();
                if (timerComp != null && timerComp.IsActive())
                {
                    if (!string.IsNullOrEmpty(__result))
                        __result += "\n";

                    __result += "Наёмник (снаряжение заблокировано)";

                    int ticksLeft = timerComp.GetExpiryTick() - Find.TickManager.TicksGame;
                    if (ticksLeft > 0)
                    {
                        string timeLeft = ticksLeft.ToStringTicksToPeriod();
                        __result += $"\nКонтракт истекает через: {timeLeft}";
                    }
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error in inspect string patch: {ex}");
            }
        }
    }

    public static class Pawn_SpawnSetup_Patch
    {
        public static void Postfix(Pawn __instance, Map map, bool respawningAfterLoad)
        {
            try
            {
                if (__instance == null || map == null) return;

                var timerComp = __instance.GetComp<CompReinforcementTimer>();
                if (timerComp != null && timerComp.IsMercenary())
                {
                    var manager = map.GetComponent<ReinforcementManager>();
                    if (manager == null)
                    {
                        manager = new ReinforcementManager(map);
                        map.components.Add(manager);
                    }

                    manager.RegisterMercenary(__instance);
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error in SpawnSetup patch: {ex}");
            }
        }
    }

    public static class Game_LoadGame_Patch
    {
        public static void Postfix()
        {
            try
            {
                //Log.Message("[HSK.Watcher] Game loaded, checking mercenaries...");

                foreach (Map map in Find.Maps)
                {
                    var manager = map.GetComponent<ReinforcementManager>();
                    if (manager == null)
                    {
                        manager = new ReinforcementManager(map);
                        map.components.Add(manager);
                    }
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error in LoadGame patch: {ex}");
            }
        }
    }

    // ==================== ДИАЛОГОВОЕ ОКНО ====================
    public class Dialog_CallReinforcements : Window
    {
        private readonly Map map;
        private const int DurationDays = 1;
        private int selectedPackage = 0;
        private const int SmallSilverCost = 2000;
        private const int LargeSilverCost = 5000;
        private const int SmallSoldiers = 2;
        private const int LargeSoldiers = 5;

        public override Vector2 InitialSize => new Vector2(500f, 400f);

        public Dialog_CallReinforcements(Map map)
        {
            this.map = map;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
            this.closeOnAccept = false;
            this.closeOnCancel = true;
            this.doCloseX = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            try
            {
                float silverCount = GetSilverCount();
                bool canAffordSmall = silverCount >= SmallSilverCost;
                bool canAffordLarge = silverCount >= LargeSilverCost;
                bool canAffordSelected = selectedPackage == 0 ? canAffordSmall : canAffordLarge;

                float y = 0f;

                Text.Font = GameFont.Medium;
                Rect titleRect = new Rect(0f, y, inRect.width, 35f);
                Widgets.Label(titleRect, "Вызов подкреплений");
                Text.Font = GameFont.Small;
                y += 40f;

                string descText = $"Вызовите наёмников на помощь колонии. Контракт: {DurationDays} день.\nСтоимость: {SmallSilverCost} серебра (малый отряд) или {LargeSilverCost} (большой отряд)";
                Rect descRect = new Rect(0f, y, inRect.width, 60f);
                Widgets.Label(descRect, descText);
                y += 70f;

                Text.Font = GameFont.Medium;
                Rect selectRect = new Rect(0f, y, inRect.width, 30f);
                Widgets.Label(selectRect, "Выберите пакет:");
                Text.Font = GameFont.Small;
                y += 40f;

                Rect smallRect = new Rect(20f, y, inRect.width - 40f, 60f);
                bool smallHover = Mouse.IsOver(smallRect);
                if (selectedPackage == 0)
                {
                    Widgets.DrawHighlightSelected(smallRect);
                }
                else if (smallHover)
                {
                    Widgets.DrawHighlight(smallRect);
                }

                string smallText = $"<b>Малый отряд</b>\n{SmallSoldiers} бойцов - {SmallSilverCost} серебра";
                if (!canAffordSmall) smallText += "\n<color=red>Недостаточно серебра</color>";

                Widgets.Label(smallRect, smallText);
                if (Widgets.ButtonInvisible(smallRect))
                {
                    selectedPackage = 0;
                }
                y += 70f;

                Rect largeRect = new Rect(20f, y, inRect.width - 40f, 60f);
                bool largeHover = Mouse.IsOver(largeRect);
                if (selectedPackage == 1)
                {
                    Widgets.DrawHighlightSelected(largeRect);
                }
                else if (largeHover)
                {
                    Widgets.DrawHighlight(largeRect);
                }

                string largeText = $"<b>Большой отряд</b>\n{LargeSoldiers} бойцов - {LargeSilverCost} серебра";
                if (!canAffordLarge) largeText += "\n<color=red>Недостаточно серебра</color>";

                Widgets.Label(largeRect, largeText);
                if (Widgets.ButtonInvisible(largeRect))
                {
                    selectedPackage = 1;
                }
                y += 80f;

                Rect buttonRect = new Rect(inRect.width / 2 - 100f, y, 200f, 40f);
                if (!canAffordSelected)
                {
                    GUI.color = Color.gray;
                }

                if (Widgets.ButtonText(buttonRect, "Вызвать подкрепления") && canAffordSelected)
                {
                    TryCallReinforcements();
                }

                GUI.color = Color.white;

                y += 50f;

                Rect cancelRect = new Rect(inRect.width / 2 - 100f, y, 200f, 30f);
                if (Widgets.ButtonText(cancelRect, "Отмена"))
                {
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                //Log.Error("[HSK.Watcher] Error in DoWindowContents: " + ex);
            }
        }

        private float GetSilverCount()
        {
            try
            {
                if (map == null)
                {
                    //Log.Error("[HSK.Watcher] Карта не найдена");
                    return 0f;
                }

                float silver = 0f;

                if (map.resourceCounter != null)
                {
                    silver = map.resourceCounter.GetCount(ThingDefOf.Silver);
                }

                if (silver <= 0)
                {
                    var silverThings = map.listerThings?.ThingsOfDef(ThingDefOf.Silver);
                    if (silverThings != null)
                    {
                        foreach (var thing in silverThings)
                        {
                            if (thing.Spawned)
                                silver += thing.stackCount;
                        }
                    }
                }

                //Log.Message($"[HSK.Watcher] Всего серебра на карте: {silver}");
                return silver;
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error in GetSilverCount: {ex}");
                return 0f;
            }
        }

        private bool RemoveSilver(int amount)
        {
            try
            {
                if (amount <= 0) return true;

                //Log.Message($"[HSK.Watcher] Пытаемся списать {amount} серебра");

                int removed = 0;
                int attempts = 0;
                const int maxAttempts = 10000;

                while (removed < amount && attempts < maxAttempts)
                {
                    attempts++;

                    var silverThings = map.listerThings.ThingsOfDef(ThingDefOf.Silver);
                    if (silverThings == null || silverThings.Count == 0)
                    {
                        //Log.Error($"[HSK.Watcher] Нет серебра для списания");
                        break;
                    }

                    bool removedThisCycle = false;

                    var silverList = new List<Thing>();
                    foreach (var thing in silverThings)
                    {
                        if (thing.Spawned && thing.stackCount > 0)
                            silverList.Add(thing);
                    }

                    foreach (var thing in silverList)
                    {
                        if (removed >= amount) break;

                        if (thing.stackCount > 0)
                        {
                            thing.stackCount--;
                            removed++;
                            removedThisCycle = true;

                            if (thing.stackCount <= 0)
                            {
                                thing.Destroy();
                            }

                            if (removed % 100 == 0)
                            {
                                //Log.Message($"[HSK.Watcher] Списано {removed}/{amount} серебра");
                            }
                            break;
                        }
                    }

                    if (!removedThisCycle)
                    {
                        //Log.Error($"[HSK.Watcher] Не удалось найти серебро для списания на попытке {attempts}");
                        break;
                    }
                }

                bool success = removed >= amount;
                //Log.Message($"[HSK.Watcher] Списание завершено. Списано: {removed}/{amount}. Успех: {success}");

                return success;
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Ошибка при списании серебра: {ex}");
                return false;
            }
        }

        private void ReturnSilver(int amount)
        {
            try
            {
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = Mathf.Min(amount, 999);

                IntVec3 spawnCell;
                if (DropCellFinder.TryFindDropSpotNear(map.Center, map, out spawnCell, false, false))
                {
                    GenSpawn.Spawn(silver, spawnCell, map, WipeMode.Vanish);
                    //Log.Message($"[HSK.Watcher] Возвращено {amount} серебра");
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Ошибка при возврате серебра: {ex}");
            }
        }

        private void TryCallReinforcements()
        {
            try
            {
                int silverCost = selectedPackage == 0 ? SmallSilverCost : LargeSilverCost;
                int soldierCount = selectedPackage == 0 ? SmallSoldiers : LargeSoldiers;

                float silverCount = GetSilverCount();
                //Log.Message($"[HSK.Watcher] Попытка вызова подкреплений. Нужно серебра: {silverCost}, есть: {silverCount}");

                if (silverCount < silverCost)
                {
                    Messages.Message($"Недостаточно серебра. Нужно: {silverCost}, есть: {silverCount}", MessageTypeDefOf.RejectInput);
                    return;
                }

                //Log.Message($"[HSK.Watcher] Пробуем списать {silverCost} серебра...");
                if (!RemoveSilver(silverCost))
                {
                    Messages.Message("Ошибка при оплате. Проверьте логи.", MessageTypeDefOf.RejectInput);
                    return;
                }

                //Log.Message($"[HSK.Watcher] Оплата успешна. Создаем {soldierCount} наемников...");

                List<Pawn> mercenaries = CreateMercenaries(soldierCount);

                if (mercenaries == null || mercenaries.Count == 0)
                {
                    Messages.Message("Не удалось создать наемников", MessageTypeDefOf.RejectInput);
                    ReturnSilver(silverCost);
                    return;
                }

                //Log.Message($"[HSK.Watcher] Успешно создано {mercenaries.Count} наемников. Спавним...");

                SpawnReinforcementsOnBase(mercenaries);

                Messages.Message($"Наёмники прибыли в колонию", MessageTypeDefOf.PositiveEvent);
                this.Close();
            }
            catch (Exception ex)
            {
                //Log.Error("[HSK.Watcher] Error in TryCallReinforcements: " + ex);
                Messages.Message("Ошибка вызова подкреплений", MessageTypeDefOf.RejectInput);
            }
        }

        private List<Pawn> CreateMercenaries(int count)
        {
            List<Pawn> mercenaries = new List<Pawn>();

            if (count <= 0)
            {
                //Log.Error("[HSK.Watcher] Запрошено 0 наемников");
                return mercenaries;
            }

            //Log.Message($"[HSK.Watcher] Начинаем создание {count} наемников...");

            for (int i = 0; i < count; i++)
            {
                try
                {
                    string[] syndicateKinds = new string[]
                    {
                        "SyndicateProspector",
                        "SyndicateStriker",
                        "SyndicateHunter",
                        "SyndicateHawkeye",
                        "SyndicateDevastatorHeavy",
                        "SyndicateAnnihilator",
                        "SyndicateBloodborne"
                    };

                    PawnKindDef mercKind = null;
                    for (int j = 0; j < syndicateKinds.Length; j++)
                    {
                        mercKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(syndicateKinds[j]);
                        if (mercKind != null) break;
                    }

                    if (mercKind == null)
                    {
                        //Log.Warning("[HSK.Watcher] Не найден деф синдиката, используем пирата");
                        mercKind = PawnKindDefOf.Pirate;
                    }

                    PawnGenerationRequest request = new PawnGenerationRequest(
                        kind: mercKind,
                        faction: Faction.OfPlayer,
                        context: PawnGenerationContext.NonPlayer,
                        tile: -1,
                        forceGenerateNewPawn: true,
                        allowDead: false,
                        allowDowned: false,
                        canGeneratePawnRelations: false,
                        mustBeCapableOfViolence: true,
                        colonistRelationChanceFactor: 0f,
                        forceAddFreeWarmLayerIfNeeded: false,
                        allowGay: true,
                        allowPregnant: false,
                        allowFood: true,
                        allowAddictions: true,
                        inhabitant: false,
                        certainlyBeenInCryptosleep: false,
                        forceRedressWorldPawnIfFormerColonist: false,
                        worldPawnFactionDoesntMatter: false,
                        biocodeWeaponChance: 0f,
                        fixedBiologicalAge: Rand.Range(25, 45),
                        fixedChronologicalAge: null,
                        fixedGender: null,
                        fixedLastName: null
                    );

                    Pawn mercenary = PawnGenerator.GeneratePawn(request);

                    if (mercenary == null)
                  {
                    //    Log.Error("[HSK.Watcher] Не удалось создать наемника");
                        continue;
                    }

                    var timerComp = new CompReinforcementTimer();
                    mercenary.AllComps.Add(timerComp);
                    timerComp.parent = mercenary;
                    timerComp.SetExpiryTick(Find.TickManager.TicksGame + (DurationDays * 60000));
                    timerComp.SetDurationDays(DurationDays);
                    timerComp.SetIsMercenary(true);

                    mercenaries.Add(mercenary);
                    //Log.Message($"[HSK.Watcher] Создан наемник {i + 1}/{count}: {mercenary.Name}");
                }
                catch (Exception ex)
                {
                    //Log.Error($"[HSK.Watcher] Ошибка создания наемника {i + 1}: {ex}");
                }
            }

            //Log.Message($"[HSK.Watcher] Всего создано наемников: {mercenaries.Count} из {count}");
            return mercenaries;
        }

        private void SpawnReinforcementsOnBase(List<Pawn> mercenaries)
        {
            if (mercenaries == null || mercenaries.Count == 0)
                return;

            try
            {
                IntVec3 spawnPoint = FindSafeBaseSpawnSpot();

                if (!spawnPoint.IsValid)
                {
                    //Log.Error("[HSK.Watcher] Не удалось найти точку спавна на базе");
                    spawnPoint = new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);
                }

                //Log.Message($"[HSK.Watcher] Наемники появятся на базе в точке: {spawnPoint}");

                for (int i = 0; i < mercenaries.Count; i++)
                {
                    Pawn mercenary = mercenaries[i];
                    if (mercenary != null)
                    {
                        try
                        {
                            IntVec3 spawnCell = FindSpawnCellOnBase(spawnPoint, i);

                            GenSpawn.Spawn(mercenary, spawnCell, map, WipeMode.Vanish);

                            CreateArrivalEffects(spawnCell);

                            ApplyArrivalShock(mercenary);

                            //Log.Message($"[HSK.Watcher] Наемник {mercenary.Name} появился в клетке {spawnCell}");
                        }
                        catch (Exception ex)
                        {
                            //Log.Error($"[HSK.Watcher] Ошибка спавна наемника {mercenary.Name}: {ex}");
                        }
                    }
                }

                Find.LetterStack.ReceiveLetter("Прибытие подкреплений",
                    $"{mercenaries.Count} наёмников прибыли в колонию. Они будут помогать вам в течение {DurationDays} дня.",
                    LetterDefOf.PositiveEvent,
                    new TargetInfo(spawnPoint, map));
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Ошибка при спавне наемников: {ex}");

                IntVec3 fallbackCell = new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);
                foreach (var mercenary in mercenaries)
                {
                    if (mercenary != null)
                    {
                        GenSpawn.Spawn(mercenary, fallbackCell, map, WipeMode.Vanish);
                    }
                }
            }
        }

        private IntVec3 FindSafeBaseSpawnSpot()
        {
            try
            {
                List<Building> colonyBuildings = new List<Building>();
                if (map.listerBuildings != null)
                {
                    List<Building> allBuildings = map.listerBuildings.allBuildingsColonist;
                    for (int i = 0; i < allBuildings.Count; i++)
                    {
                        Building building = allBuildings[i];
                        if (building != null && building.Spawned && building.def.passability != Traversability.Impassable)
                        {
                            colonyBuildings.Add(building);
                        }
                    }
                }

                if (colonyBuildings.Count > 0)
                {
                    Building randomBuilding = colonyBuildings.RandomElement();

                    for (int radius = 1; radius <= 3; radius++)
                    {
                        foreach (IntVec3 cell in GenRadial.RadialCellsAround(randomBuilding.Position, radius, true))
                        {
                            if (cell.InBounds(map) &&
                                cell.Standable(map) &&
                                !cell.Fogged(map) &&
                                !cell.Roofed(map) &&
                                cell.GetEdifice(map) == null &&
                                cell.GetFirstItem(map) == null &&
                                map.reachability.CanReachColony(cell))
                            {
                                return cell;
                            }
                        }
                    }
                }

                CellRect baseArea = CellRect.WholeMap(map).ContractedBy(10);

                for (int i = 0; i < 30; i++)
                {
                    IntVec3 randomCell = new IntVec3(
                        Rand.Range(baseArea.minX, baseArea.maxX),
                        0,
                        Rand.Range(baseArea.minZ, baseArea.maxZ)
                    );

                    if (randomCell.InBounds(map) &&
                        randomCell.Standable(map) &&
                        !randomCell.Fogged(map) &&
                        !randomCell.Roofed(map) &&
                        randomCell.GetEdifice(map) == null &&
                        map.reachability.CanReachColony(randomCell))
                    {
                        return randomCell;
                    }
                }

                return new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error finding safe base spawn spot: {ex}");
                return new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);
            }
        }

        private IntVec3 FindSpawnCellOnBase(IntVec3 center, int index)
        {
            float angle = (360f / 8) * index;
            float radius = 2f;

            float rad = angle * Mathf.Deg2Rad;
            int x = Mathf.RoundToInt(center.x + Mathf.Cos(rad) * radius);
            int z = Mathf.RoundToInt(center.z + Mathf.Sin(rad) * radius);
            IntVec3 targetPos = new IntVec3(x, 0, z);

            if (targetPos.InBounds(map) && targetPos.Standable(map) && !targetPos.Fogged(map))
            {
                return targetPos;
            }

            for (int r = 0; r <= 3; r++)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, r, true))
                {
                    if (cell.InBounds(map) && cell.Standable(map) && !cell.Fogged(map))
                    {
                        return cell;
                    }
                }
            }

            return center;
        }

        private void CreateArrivalEffects(IntVec3 cell)
        {
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    FleckMaker.ThrowSmoke(cell.ToVector3Shifted(), map, 1.5f);
                }

                FleckMaker.ThrowMicroSparks(cell.ToVector3Shifted(), map);
                FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(), map, 1f);
                SoundDefOf.PsychicPulseGlobal.PlayOneShot(new TargetInfo(cell, map));
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Ошибка создания эффектов прибытия: {ex}");
            }
        }

        private void ApplyArrivalShock(Pawn pawn)
        {
            try
            {
                if (pawn.health != null && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
                {
                    pawn.stances.SetStance(new Stance_Cooldown(60, null, null));
                    MoteMaker.ThrowText(pawn.DrawPos, map, "Прибыл!", Color.green, 2f);
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Ошибка применения шока прибытия: {ex}");
            }
        }
    }

    // ==================== ИНИЦИАЛИЗАТОР ====================
    [StaticConstructorOnStartup]
    public static class ReinforcementInitializer
    {
        static ReinforcementInitializer()
        {
            try
            {
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    try
                    {
                        if (Current.Game == null || Current.ProgramState != ProgramState.Playing)
                            return;

                        ThingDef commsConsole = DefDatabase<ThingDef>.GetNamedSilentFail("CommsConsole");
                        if (commsConsole != null && commsConsole.comps != null)
                        {
                            bool hasComp = false;
                            for (int i = 0; i < commsConsole.comps.Count; i++)
                            {
                                if (commsConsole.comps[i].compClass == typeof(CompCallReinforcements))
                                {
                                    hasComp = true;
                                    break;
                                }
                            }

                            if (!hasComp)
                            {
                                commsConsole.comps.Add(new CompProperties_CallReinforcements());
                                //Log.Message("[HSK.Watcher] Компонент добавлен к консоли связи через инициализатор");
                            }
                        }

                        foreach (Map map in Find.Maps)
                        {
                            if (map.GetComponent<ReinforcementManager>() == null)
                            {
                                map.components.Add(new ReinforcementManager(map));
                                //Log.Message($"[HSK.Watcher] Добавлен ReinforcementManager к карте {map}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        //Log.Error($"[HSK.Watcher] Error in deferred initialization: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Error in static constructor: {ex}");
            }
        }
    }

    // ==================== МЕНЕДЖЕР ПОДКРЕПЛЕНИЙ ====================
    public class ReinforcementManager : MapComponent
    {
        private HashSet<Pawn> activeMercenaries = new HashSet<Pawn>();
        private List<MercenaryCorpseData> mercenaryCorpses = new List<MercenaryCorpseData>();
        private int lastCheckTick = -99999;
        private const int CHECK_INTERVAL = 1200;
        private const int CORPSE_CLEANUP_DELAY = 12000;
        private int cleanupDelayTicks = 5000;

        public ReinforcementManager(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - lastCheckTick > CHECK_INTERVAL)
            {
                lastCheckTick = currentTick;
                CheckActiveMercenaries();
                CleanupExpiredCorpses();
            }
        }

        public void RegisterMercenary(Pawn mercenary)
        {
            if (mercenary != null && mercenary.SpawnedOrAnyParentSpawned)
            {
                var timerComp = mercenary.GetComp<CompReinforcementTimer>();
                if (timerComp != null)
                {
                    timerComp.SetIsMercenary(true);
                    activeMercenaries.Add(mercenary);
                    //Log.Message($"[HSK.Watcher] Наемник зарегистрирован: {mercenary.Name}");
                }
            }
        }

        private void CheckActiveMercenaries()
        {
            List<Pawn> mercenariesToCheck = new List<Pawn>(activeMercenaries);

            foreach (Pawn mercenary in mercenariesToCheck)
            {
                if (mercenary == null || mercenary.Destroyed)
                {
                    activeMercenaries.Remove(mercenary);
                    continue;
                }

                if (mercenary.Dead || mercenary.Downed)
                {
                    HandleMercenaryDeath(mercenary);
                    continue;
                }

                var timerComp = mercenary.GetComp<CompReinforcementTimer>();
                if (timerComp != null && timerComp.IsExpired())
                {
                    DespawnMercenary(mercenary);
                }
                else if (timerComp != null && timerComp.IsActive())
                {
                    int ticksLeft = timerComp.GetExpiryTick() - Find.TickManager.TicksGame;
                    if (ticksLeft > 0 && ticksLeft <= 25000)
                    {
                        if (ticksLeft % 10000 == 0 && mercenary.IsColonistPlayerControlled)
                        {
                            Messages.Message($"{mercenary.Name} скоро завершит контракт (осталось: {ticksLeft.ToStringTicksToPeriod()})",
                                mercenary, MessageTypeDefOf.NeutralEvent);
                        }
                    }
                }
            }
        }

        private void HandleMercenaryDeath(Pawn mercenary)
        {
            try
            {
                //Log.Message($"[HSK.Watcher] Обработка смерти наёмника: {mercenary.Name}");

                Corpse corpse = FindMercenaryCorpse(mercenary);
                if (corpse != null && corpse.Spawned)
                {
                    mercenaryCorpses.Add(new MercenaryCorpseData
                    {
                        corpse = corpse,
                        expiryTick = Find.TickManager.TicksGame + CORPSE_CLEANUP_DELAY,
                        mercenaryName = mercenary.Name?.ToStringShort ?? "Наёмник"
                    });

                    //Log.Message($"[HSK.Watcher] Труп наёмника добавлен в список очистки: {mercenary.Name}");
                }
                else
                {
                    SafeDestroyPawn(mercenary);
                }

                activeMercenaries.Remove(mercenary);
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Ошибка обработки смерти наёмника: {ex}");
                activeMercenaries.Remove(mercenary);
            }
        }

        private Corpse FindMercenaryCorpse(Pawn mercenary)
        {
            try
            {
                if (map == null || !mercenary.Dead) return null;

                List<Thing> things = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
                foreach (Thing thing in things)
                {
                    Corpse corpse = thing as Corpse;
                    if (corpse != null && corpse.InnerPawn == mercenary)
                    {
                        return corpse;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Ошибка поиска трупа: {ex}");
                return null;
            }
        }

        private void CleanupExpiredCorpses()
        {
            int currentTick = Find.TickManager.TicksGame;

            for (int i = mercenaryCorpses.Count - 1; i >= 0; i--)
            {
                MercenaryCorpseData data = mercenaryCorpses[i];

                if (data.corpse == null || data.corpse.Destroyed)
                {
                    mercenaryCorpses.RemoveAt(i);
                    continue;
                }

                if (currentTick >= data.expiryTick)
                {
                    DestroyMercenaryCorpse(data);
                    mercenaryCorpses.RemoveAt(i);
                }
            }
        }

        private void DestroyMercenaryCorpse(MercenaryCorpseData data)
        {
            try
            {
                Corpse corpse = data.corpse;
                if (corpse == null || corpse.Destroyed) return;

                if (corpse.Spawned)
                {
                    CreateDisappearEffects(corpse.Position);
                }

                DestroyCorpseAndEquipment(corpse);

                //Log.Message($"[HSK.Watcher] Труп наёмника уничтожен: {data.mercenaryName}");
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Ошибка уничтожения трупа: {ex}");
            }
        }

        private void CreateDisappearEffects(IntVec3 position)
        {
            try
            {
                if (!position.IsValid || map == null) return;

                for (int i = 0; i < 3; i++)
                {
                    Vector3 pos = position.ToVector3Shifted() + new Vector3(Rand.Range(-0.3f, 0.3f), 0, Rand.Range(-0.3f, 0.3f));
                    FleckMaker.ThrowSmoke(pos, map, 1.5f);
                }

                FleckMaker.ThrowMicroSparks(position.ToVector3Shifted(), map);

                SoundDefOf.PsychicPulseGlobal.PlayOneShot(new TargetInfo(position, map));
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Ошибка создания эффектов: {ex}");
            }
        }

        private void DestroyCorpseAndEquipment(Corpse corpse)
        {
            try
            {
                if (corpse == null) return;

                IntVec3 position = corpse.Position;
                Map map = corpse.Map;

                corpse.Destroy();

                if (map != null && position.IsValid)
                {
                    List<Thing> things = map.thingGrid.ThingsListAt(position);
                    foreach (Thing thing in things)
                    {
                        if (IsMercenaryEquipment(thing))
                        {
                            thing.Destroy();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Ошибка уничтожения снаряжения: {ex}");
            }
        }

        private bool IsMercenaryEquipment(Thing thing)
        {
            if (thing is Apparel || thing is ThingWithComps)
            {
                CompQuality quality = thing.TryGetComp<CompQuality>();
                if (quality != null)
                {
                    return quality.Quality >= QualityCategory.Normal;
                }
                return true;
            }
            return false;
        }

        private void DespawnMercenary(Pawn mercenary)
        {
            try
            {
                if (mercenary == null || mercenary.Dead || mercenary.Destroyed)
                {
                    activeMercenaries.Remove(mercenary);
                    return;
                }

                //Log.Message($"[HSK.Watcher] Деспавн наёмника: {mercenary.Name}");

                if (mercenary.Spawned && mercenary.IsColonistPlayerControlled)
                {
                    Messages.Message($"{mercenary.Name} завершил контракт и покидает колонию",
                        mercenary, MessageTypeDefOf.NeutralEvent);
                }

                StripAndDestroyEquipment(mercenary);

                if (mercenary.Spawned)
                {
                    IntVec3 exitSpot;
                    if (TryFindExitSpot(mercenary, out exitSpot))
                    {
                        Job exitJob = JobMaker.MakeJob(JobDefOf.Goto, exitSpot);
                        exitJob.exitMapOnArrival = true;
                        exitJob.failIfCantJoinOrCreateCaravan = true;

                        mercenary.jobs.StartJob(exitJob, JobCondition.None, null, false, true);

                        ScheduleMercenaryExitCheck(mercenary);
                    }
                    else
                    {
                        SafeDespawnPawn(mercenary);
                    }
                }
                else
                {
                    SafeDestroyPawn(mercenary);
                }

                activeMercenaries.Remove(mercenary);
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Ошибка деспавна наёмника: {ex}");
                activeMercenaries.Remove(mercenary);
            }
        }

        private void ScheduleMercenaryExitCheck(Pawn mercenary)
        {
            var scheduler = Current.Game.GetComponent<MercenaryScheduler>();
            if (scheduler == null)
            {
                scheduler = new MercenaryScheduler();
                Current.Game.components.Add(scheduler);
            }

            scheduler.ScheduleCheck(mercenary, cleanupDelayTicks);
        }

        private bool TryFindExitSpot(Pawn pawn, out IntVec3 exitSpot)
        {
            exitSpot = IntVec3.Invalid;

            try
            {
                if (map == null || pawn == null) return false;

                return RCellFinder.TryFindBestExitSpot(pawn, out exitSpot, TraverseMode.ByPawn);
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Ошибка поиска выхода: {ex}");
                return false;
            }
        }

        public void CheckMercenaryExit(Pawn mercenary)
        {
            try
            {
                if (mercenary == null || mercenary.Destroyed) return;

                if (mercenary.Spawned)
                {
                    //Log.Message($"[HSK.Watcher] Наёмник {mercenary.Name} не смог выйти, принудительный деспавн");
                    SafeDespawnPawn(mercenary);
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Ошибка проверки выхода: {ex}");
            }
        }

        private void StripAndDestroyEquipment(Pawn pawn)
        {
            try
            {
                if (pawn == null || pawn.Dead) return;

                if (pawn.apparel != null)
                {
                    List<Apparel> wornApparel = new List<Apparel>(pawn.apparel.WornApparel);
                    foreach (Apparel apparel in wornApparel)
                    {
                        if (pawn.apparel.TryDrop(apparel, out Apparel droppedApparel, pawn.Position, true))
                        {
                            if (droppedApparel != null && droppedApparel.Spawned)
                            {
                                CreateDisappearEffects(droppedApparel.Position);
                                droppedApparel.Destroy();
                            }
                        }
                    }
                }

                if (pawn.equipment != null)
                {
                    List<ThingWithComps> equipment = new List<ThingWithComps>(pawn.equipment.AllEquipmentListForReading);
                    foreach (ThingWithComps eq in equipment)
                    {
                        if (pawn.equipment.TryDropEquipment(eq, out ThingWithComps droppedEq, pawn.Position, true))
                        {
                            if (droppedEq != null && droppedEq.Spawned)
                            {
                                CreateDisappearEffects(droppedEq.Position);
                                droppedEq.Destroy();
                            }
                        }
                    }
                }

                if (pawn.inventory != null)
                {
                    List<Thing> inventory = new List<Thing>(pawn.inventory.innerContainer);
                    foreach (Thing item in inventory)
                    {
                        if (pawn.inventory.innerContainer.TryDrop(item, item.Position, map, ThingPlaceMode.Near, out Thing droppedItem))
                        {
                            if (droppedItem != null && droppedItem.Spawned)
                            {
                                CreateDisappearEffects(droppedItem.Position);
                                droppedItem.Destroy();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Ошибка снятия снаряжения: {ex}");
            }
        }

        private void SafeDespawnPawn(Pawn pawn)
        {
            try
            {
                if (pawn == null || pawn.Destroyed) return;

                if (pawn.Spawned)
                {
                    CreateDisappearEffects(pawn.Position);
                    pawn.DeSpawn();
                }

                pawn.Destroy();
                //Log.Message($"[HSK.Watcher] Наёмник деспавнен: {pawn.Name}");
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Ошибка деспавна павна: {ex}");
            }
        }

        private void SafeDestroyPawn(Pawn pawn)
        {
            try
            {
                if (pawn == null || pawn.Destroyed) return;

                pawn.Destroy();
                //Log.Message($"[HSK.Watcher] Наёмник уничтожен: {pawn.Name}");
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK.Watcher] Ошибка уничтожения павна: {ex}");
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref activeMercenaries, "activeMercenaries", LookMode.Reference);
            Scribe_Collections.Look(ref mercenaryCorpses, "mercenaryCorpses", LookMode.Deep);
        }

        private class MercenaryCorpseData : IExposable
        {
            public Corpse corpse;
            public int expiryTick;
            public string mercenaryName;

            public void ExposeData()
            {
                Scribe_References.Look(ref corpse, "corpse");
                Scribe_Values.Look(ref expiryTick, "expiryTick");
                Scribe_Values.Look(ref mercenaryName, "mercenaryName");
            }
        }
    }

    // ==================== КОМПОНЕНТ ДЛЯ ОТЛОЖЕННЫХ ПРОВЕРОК ====================
    public class MercenaryScheduler : GameComponent
    {
        private struct ScheduledCheck
        {
            public Pawn mercenary;
            public int checkTick;
        }

        private List<ScheduledCheck> scheduledChecks = new List<ScheduledCheck>();

        public MercenaryScheduler() : base()
        {
        }

        public MercenaryScheduler(Game game) : base()
        {
        }

        public void ScheduleCheck(Pawn mercenary, int delayTicks)
        {
            scheduledChecks.Add(new ScheduledCheck
            {
                mercenary = mercenary,
                checkTick = Find.TickManager.TicksGame + delayTicks
            });
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            int currentTick = Find.TickManager.TicksGame;

            for (int i = scheduledChecks.Count - 1; i >= 0; i--)
            {
                var check = scheduledChecks[i];

                if (check.mercenary == null || check.mercenary.Destroyed)
                {
                    scheduledChecks.RemoveAt(i);
                    continue;
                }

                if (currentTick >= check.checkTick)
                {
                    Map map = check.mercenary.Map;
                    if (map != null)
                    {
                        var manager = map.GetComponent<ReinforcementManager>();
                        if (manager != null)
                        {
                            manager.CheckMercenaryExit(check.mercenary);
                        }
                    }

                    scheduledChecks.RemoveAt(i);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }
    }
}
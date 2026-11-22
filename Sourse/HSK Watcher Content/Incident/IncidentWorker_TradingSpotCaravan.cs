using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HSK.Watcher.Content.Incident
{
    [StaticConstructorOnStartup]
    public static class TradingSpotCaravanMod
    {
        public static bool IsEnabled = false; // По умолчанию выключено

        static TradingSpotCaravanMod()
        {
            Log.Message("[TradingSpotCaravan] Initializing mod...");

            try
            {
                var harmony = new Harmony("com.hsk.watcher.tradingspotcaravan");

                PatchCanFireNowSub(harmony);
                PatchTryExecuteWorker(harmony);
                PatchIncidentWorker(harmony);

                // Загружаем настройки
                LoadSettings();

                Log.Message($"[TradingSpotCaravan] Mod is {(IsEnabled ? "ENABLED" : "DISABLED")}");
            }
            catch (Exception ex)
            {
                Log.Error($"[TradingSpotCaravan] Exception during initialization: {ex}");
            }
        }

        public static void LoadSettings()
        {
            try
            {
                var settings = new ModSettings_TradingSpotCaravan();
                if (settings != null)
                {
                    IsEnabled = settings.IsEnabled;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[TradingSpotCaravan] Could not load settings: {ex.Message}");
            }
        }

        public static void SaveSettings()
        {
            try
            {
                var settings = new ModSettings_TradingSpotCaravan();
                settings.IsEnabled = IsEnabled;
                settings.Write();
            }
            catch (Exception ex)
            {
                Log.Warning($"[TradingSpotCaravan] Could not save settings: {ex.Message}");
            }
        }

        private static void PatchCanFireNowSub(Harmony harmony)
        {
            try
            {
                var traderType = typeof(IncidentWorker_TraderCaravanArrival);
                var method = traderType.GetMethod("CanFireNowSub", BindingFlags.Instance | BindingFlags.NonPublic);

                if (method != null)
                {
                    var postfix = typeof(Patch_CanFireNowSub).GetMethod("Postfix");
                    harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[TradingSpotCaravan] Failed to patch CanFireNowSub: {ex.Message}");
            }
        }

        private static void PatchTryExecuteWorker(Harmony harmony)
        {
            try
            {
                var traderType = typeof(IncidentWorker_TraderCaravanArrival);
                var method = traderType.GetMethod("TryExecuteWorker", BindingFlags.Instance | BindingFlags.NonPublic);

                if (method != null)
                {
                    var prefix = typeof(Patch_TryExecuteWorker).GetMethod("Prefix");
                    harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                }
                else
                {
                    method = traderType.GetMethod("TryExecute", BindingFlags.Instance | BindingFlags.Public);
                    if (method != null)
                    {
                        var prefix = typeof(Patch_TryExecute).GetMethod("Prefix");
                        harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[TradingSpotCaravan] Failed to patch TryExecuteWorker: {ex.Message}");
            }
        }

        private static void PatchIncidentWorker(Harmony harmony)
        {
            try
            {
                var baseType = typeof(IncidentWorker);
                var method = baseType.GetMethod("TryExecute", BindingFlags.Instance | BindingFlags.Public);

                if (method != null)
                {
                    var prefix = typeof(Patch_IncidentWorker_TryExecute).GetMethod("Prefix");
                    harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[TradingSpotCaravan] Could not patch base IncidentWorker: {ex.Message}");
            }
        }

        public static bool HasRequiredBuilding(Map map)
        {
            if (map == null) return false;

            var tradingSpotDef = ThingDef.Named("TradingSpot");
            if (tradingSpotDef != null)
            {
                if (map.listerBuildings.AllBuildingsColonistOfDef(tradingSpotDef).Any())
                {
                    return true;
                }
            }

            var commsConsoleDef = ThingDef.Named("CommsConsole");
            if (commsConsoleDef != null)
            {
                if (map.listerBuildings.AllBuildingsColonistOfDef(commsConsoleDef).Any())
                {
                    return true;
                }
            }

            return false;
        }
    }

    // Класс для сохранения настроек
    public class ModSettings_TradingSpotCaravan : ModSettings
    {
        public bool IsEnabled = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref IsEnabled, "IsEnabled", false);
        }
    }

    // Класс мода для отображения в меню настроек
    public class Mod_TradingSpotCaravan : Mod
    {
        public Mod_TradingSpotCaravan(ModContentPack content) : base(content)
        {
            var settings = GetSettings<ModSettings_TradingSpotCaravan>();
            TradingSpotCaravanMod.IsEnabled = settings.IsEnabled;
        }

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            var settings = GetSettings<ModSettings_TradingSpotCaravan>();
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            bool newValue = settings.IsEnabled;
            listing.CheckboxLabeled(
                "TradingSpotCaravan_EnableMod".Translate(),
                ref newValue,
                "TradingSpotCaravan_EnableModDesc".Translate()
            );

            if (newValue != settings.IsEnabled)
            {
                settings.IsEnabled = newValue;
                TradingSpotCaravanMod.IsEnabled = newValue;
                TradingSpotCaravanMod.SaveSettings();
                Log.Message($"[TradingSpotCaravan] Mod setting changed to: {newValue}");
            }

            listing.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "TradingSpotCaravan_SettingsCategory".Translate();
        }
    }

    // Патчи с проверкой IsEnabled
    public static class Patch_CanFireNowSub
    {
        public static void Postfix(ref IncidentParms parms, ref bool __result)
        {
            if (!TradingSpotCaravanMod.IsEnabled) return;
            if (!__result) return;

            Map map = parms.target as Map;
            if (map == null) return;

            bool hasBuilding = TradingSpotCaravanMod.HasRequiredBuilding(map);

            if (!hasBuilding)
            {
                __result = false;
            }
        }
    }

    public static class Patch_TryExecuteWorker
    {
        public static bool Prefix(ref IncidentParms parms, ref bool __result)
        {
            if (!TradingSpotCaravanMod.IsEnabled) return true;

            Map map = parms.target as Map;
            if (map == null) return true;

            bool hasBuilding = TradingSpotCaravanMod.HasRequiredBuilding(map);

            if (!hasBuilding)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    public static class Patch_TryExecute
    {
        public static bool Prefix(IncidentParms parms, ref bool __result)
        {
            if (!TradingSpotCaravanMod.IsEnabled) return true;

            if (parms.target is Map map)
            {
                var worker = parms.target as IncidentWorker;
                if (worker is IncidentWorker_TraderCaravanArrival)
                {
                    bool hasBuilding = TradingSpotCaravanMod.HasRequiredBuilding(map);

                    if (!hasBuilding)
                    {
                        __result = false;
                        return false;
                    }
                }
            }

            return true;
        }
    }

    public static class Patch_IncidentWorker_TryExecute
    {
        public static bool Prefix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
        {
            if (!TradingSpotCaravanMod.IsEnabled) return true;
            if (!(__instance is IncidentWorker_TraderCaravanArrival)) return true;

            if (parms.target is Map map)
            {
                bool hasBuilding = TradingSpotCaravanMod.HasRequiredBuilding(map);

                if (!hasBuilding)
                {
                    __result = false;
                    return false;
                }
            }

            return true;
        }
    }

    [DefOf]
    public static class TradingSpotCaravanDefOf
    {
        public static ThingDef TradingSpot;
        public static ThingDef CommsConsole;
    }
}
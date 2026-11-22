using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace HSK.Watcher.Content
{
    // Основной класс мода
    public class HSKWatcherBugProofMod : Mod
    {
        public static bool debugMode = false; // Отключаем отладочный режим
        public static HashSet<string> protectedFloorDefNames = new HashSet<string>();

        public HSKWatcherBugProofMod(ModContentPack content) : base(content)
        {
            try
            {
                // Инициализация защищённых полов
                protectedFloorDefNames.Add("HSK_CeramicTile");
                protectedFloorDefNames.Add("HSK_SterileTile");
                protectedFloorDefNames.Add("HSK_ConcreteTile");

                var harmony = new Harmony("HSK.Watcher.BugProof");

                // Патчим инспекционную строку
                harmony.Patch(
                    original: AccessTools.Method(typeof(Thing), "GetInspectString"),
                    postfix: new HarmonyMethod(typeof(InspectStringPatch), "Postfix")
                );

                // Патчим инфестации
                TryPatchInfestationMethod(harmony);
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK BugProof] Ошибка при загрузке мода: {ex}");
            }
        }

        private static void TryPatchInfestationMethod(Harmony harmony)
        {
            try
            {
                // Патчим основной метод инфестаций
                var incidentType = typeof(IncidentWorker_Infestation);
                var tryExecuteMethod = AccessTools.Method(incidentType, "TryExecuteWorker");

                if (tryExecuteMethod != null)
                {
                    harmony.Patch(tryExecuteMethod,
                        prefix: new HarmonyMethod(typeof(InfestationPatch), "TryExecuteWorker_Prefix"));
                }
            }
            catch (Exception ex)
            {
                //Log.Warning($"[HSK BugProof] Ошибка при патчинге инфестаций: {ex.Message}");
            }
        }

        public static bool IsFloorThing(Thing thing)
        {
            try
            {
                return thing != null &&
                       thing.def != null &&
                       thing.def.category == ThingCategory.Building &&
                       (thing.def.defName.Contains("Tile") || thing.def.defName.Contains("Floor") || thing.def.defName.Contains("HSK_"));
            }
            catch
            {
                return false;
            }
        }

        public static void SendProtectedMessage(IntVec3 cell, Map map, string floorName)
        {
            try
            {
                if (map == null) return;

                // Получаем сообщение
                string messageText = TranslationHelper.GetRandomPositiveProtectionMessage(floorName, cell.x, cell.z);

                // Создаем и отправляем сообщение
                Message message = new Message(messageText, MessageTypeDefOf.PositiveEvent, new LookTargets(cell, map));
                Messages.Message(message);
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK BugProof] Ошибка при отправке письма: {ex}");
            }
        }

        public static bool IsCellProtected(IntVec3 cell, Map map, out Thing protectedFloor)
        {
            protectedFloor = null;

            try
            {
                if (map == null) return false;

                var things = cell.GetThingList(map);
                foreach (var thing in things)
                {
                    if (thing == null || thing.def == null) continue;

                    if (IsFloorThing(thing) && protectedFloorDefNames.Contains(thing.def.defName))
                    {
                        protectedFloor = thing;
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                //Log.Warning($"[HSK BugProof] Ошибка при проверке клетки: {ex.Message}");
                return false;
            }
        }
    }

    // Класс помощника переводов
    public static class TranslationHelper
    {
        private static System.Random random = new System.Random();

        public static string GetRandomProtectionMessage(string floorName, int x, int z)
        {
            try
            {
                int choice = random.Next(1, 6);
                string key = $"HSKBugProof_ProtectionMessage{(choice > 1 ? choice.ToString() : "")}";
                return key.Translate(floorName, x, z);
            }
            catch
            {
                return $"Пол {floorName} защищён от насекомых на ({x}, {z})";
            }
        }

        public static string GetRandomPositiveProtectionMessage(string floorName, int x, int z)
        {
            try
            {
                int choice = random.Next(1, 6);
                string key = $"HSKBugProof_ProtectionMessage{(choice > 1 ? choice.ToString() : "")}_Positive";
                return key.Translate(floorName, x, z);
            }
            catch
            {
                return $"✅ Защита {floorName} активирована! Координаты ({x}, {z}) безопасны.";
            }
        }

        public static string GetRandomProtectionDesc()
        {
            try
            {
                int choice = random.Next(1, 6);
                string key = $"HSKBugProof_FloorProtection{(choice > 1 ? choice.ToString() : "")}";
                return key.Translate();
            }
            catch
            {
                return "Защищено от насекомых";
            }
        }

        public static string GetRandomAdditionalDesc()
        {
            if (random.Next(0, 100) < 30)
            {
                try
                {
                    int descChoice = random.Next(1, 6);
                    string descKey = $"HSKBugProof_FloorProtectionDesc{(descChoice > 1 ? descChoice.ToString() : "")}";
                    return "\n" + descKey.Translate();
                }
                catch
                {
                    return "";
                }
            }
            return "";
        }
    }

    // Патч для инспекционной строки
    [HarmonyPatch(typeof(Thing), "GetInspectString")]
    public static class InspectStringPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Thing __instance, ref string __result)
        {
            try
            {
                if (__instance == null || !__instance.Spawned || __instance.def == null)
                    return;

                if (HSKWatcherBugProofMod.IsFloorThing(__instance))
                {
                    if (HSKWatcherBugProofMod.protectedFloorDefNames.Contains(__instance.def.defName))
                    {
                        string protectionText = TranslationHelper.GetRandomProtectionDesc();
                        protectionText += TranslationHelper.GetRandomAdditionalDesc();

                        if (string.IsNullOrEmpty(__result))
                            __result = protectionText;
                        else
                            __result += "\n" + protectionText;
                    }
                }
            }
            catch { }
        }
    }

    // Патч для инфестаций
    public static class InfestationPatch
    {
        // Префикс для TryExecuteWorker
        [HarmonyPrefix]
        [HarmonyPatch(typeof(IncidentWorker_Infestation), "TryExecuteWorker")]
        public static bool TryExecuteWorker_Prefix(IncidentWorker_Infestation __instance, IncidentParms parms, ref bool __result)
        {
            try
            {
                if (parms == null || parms.target == null || !(parms.target is Map map))
                {
                    return true;
                }

                // Получаем метод TryFindCell через рефлексию
                MethodInfo tryFindCellMethod = typeof(IncidentWorker_Infestation).GetMethod("TryFindCell",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(Map), typeof(int).MakeByRefType(), typeof(int).MakeByRefType() },
                    null);

                if (tryFindCellMethod == null)
                {
                    return true;
                }

                object[] parameters = new object[] { map, null, null };
                bool foundCell = (bool)tryFindCellMethod.Invoke(__instance, parameters);

                if (!foundCell)
                {
                    return true;
                }

                int x = (int)parameters[1];
                int z = (int)parameters[2];
                IntVec3 cell = new IntVec3(x, 0, z);

                // Проверяем защищённость
                Thing protectedFloor;
                if (HSKWatcherBugProofMod.IsCellProtected(cell, map, out protectedFloor))
                {
                    string floorName = protectedFloor?.def.label ?? "Защищённый пол";
                    HSKWatcherBugProofMod.SendProtectedMessage(cell, map, floorName);

                    // Отменяем инфестацию
                    __result = false;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                //Log.Error($"[HSK BugProof] Ошибка в TryExecuteWorker_Prefix: {ex}");
                return true;
            }
        }
    }
}
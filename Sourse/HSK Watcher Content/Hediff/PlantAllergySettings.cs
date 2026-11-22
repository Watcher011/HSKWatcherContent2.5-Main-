using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSK.Watcher.Content
{
    // ==================== ГЛОБАЛЬНЫЕ НАСТРОЙКИ (Def) ====================
    public class PlantAllergyGlobalSettings : Def
    {
        public float radius = 4.9f;
        public int checkInterval = 250;
        public bool onlyMature = true;
        public bool affectAnimals = false;
        public bool affectColonists = true;
        public bool affectPrisoners = true;
        public bool affectSlaves = true;
        public bool affectVisitors = false;
        public float maxSeverity = 1.0f;
        public float fixedSeverity = 0.25f;
        public bool useFixedMode = false;
        public float severityPerTick = 0.001f; // НОВОЕ: сколько добавляется за один тик
        public float decayPerTick = 0.002f;    // НОВОЕ: сколько убывает за один тик
        public List<string> protectiveHediffNames = new List<string>();

        [Unsaved(false)]
        public List<HediffDef> protectiveHediffs = new List<HediffDef>();

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            protectiveHediffs.Clear();
            if (protectiveHediffNames != null)
            {
                foreach (var name in protectiveHediffNames)
                {
                    var hd = DefDatabase<HediffDef>.GetNamedSilentFail(name);
                    if (hd != null)
                        protectiveHediffs.Add(hd);
                }
            }
        }
    }

    // ==================== КОМПОНЕНТ РАСТЕНИЯ ====================
    public class CompProperties_PlantAllergySource : CompProperties
    {
        public HediffDef hediffDef;
        public float severityContribution = 0.05f;
        public bool onlyMature = true;
        public float radius = -1f;
        public float maxSeverity = -1f;
        public float severityPerTick = -1f;    // НОВОЕ: переопределение для растения
        public float decayPerTick = -1f;       // НОВОЕ: переопределение для растения

        public CompProperties_PlantAllergySource()
        {
            compClass = typeof(CompPlantAllergySource);
        }
    }

    public class CompPlantAllergySource : ThingComp
    {
        public CompProperties_PlantAllergySource Props =>
            (CompProperties_PlantAllergySource)props;
    }

    // ==================== MAP COMPONENT ====================
    public class PlantAllergyMapComponent : MapComponent
    {
        private int tickCounter;
        private PlantAllergyGlobalSettings settings;
        private HashSet<HediffDef> trackedHediffs = new HashSet<HediffDef>();
        private Dictionary<Pawn, Dictionary<HediffDef, float>> pawnSeverities = new Dictionary<Pawn, Dictionary<HediffDef, float>>();

        public PlantAllergyMapComponent(Map map) : base(map) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            ResolveSettings();
        }

        private void ResolveSettings()
        {
            settings = DefDatabase<PlantAllergyGlobalSettings>.AllDefs.FirstOrDefault();
            if (settings == null)
            {
                Log.Warning("[HSK.Watcher] PlantAllergyGlobalSettings не найден. Система отключена.");
                return;
            }
            settings.ResolveReferences();

            trackedHediffs.Clear();
            foreach (var thingDef in DefDatabase<ThingDef>.AllDefs)
            {
                var comp = thingDef.GetCompProperties<CompProperties_PlantAllergySource>();
                if (comp?.hediffDef != null)
                    trackedHediffs.Add(comp.hediffDef);
            }

            if (trackedHediffs.Count == 0)
                Log.Warning("[HSK.Watcher] Ни одно растение не имеет CompPlantAllergySource.");
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (settings == null || trackedHediffs.Count == 0) return;

            tickCounter++;
            if (tickCounter < settings.checkInterval) return;
            tickCounter = 0;

            // Очищаем умерших павнов
            var toRemove = new List<Pawn>();
            foreach (var pawn in pawnSeverities.Keys)
            {
                if (pawn == null || pawn.Dead || !pawn.Spawned)
                    toRemove.Add(pawn);
            }
            foreach (var pawn in toRemove)
                pawnSeverities.Remove(pawn);

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (!IsValidPawn(pawn)) continue;

                if (HasProtection(pawn))
                {
                    ReduceAllTracked(pawn);
                    continue;
                }

                // Получаем словарь для этого павна
                if (!pawnSeverities.ContainsKey(pawn))
                    pawnSeverities[pawn] = new Dictionary<HediffDef, float>();

                var pawnDict = pawnSeverities[pawn];

                // Рассчитываем новые значения
                var newSeverities = CalculateSeverities(pawn);

                // Обновляем накопленные значения
                foreach (var hediffDef in trackedHediffs)
                {
                    if (newSeverities.ContainsKey(hediffDef))
                    {
                        // Есть источник аллергии - добавляем
                        float addAmount = newSeverities[hediffDef];

                        if (!pawnDict.ContainsKey(hediffDef))
                            pawnDict[hediffDef] = 0f;

                        pawnDict[hediffDef] += addAmount;

                        // Получаем максимальное значение для этого хеддифа
                        float maxSev = GetMaxSeverityForHediff(hediffDef);
                        pawnDict[hediffDef] = Mathf.Min(pawnDict[hediffDef], maxSev);
                    }
                    else
                    {
                        // Нет источника - убываем
                        if (pawnDict.ContainsKey(hediffDef))
                        {
                            float decay = GetDecayForHediff(hediffDef);
                            pawnDict[hediffDef] -= decay;

                            if (pawnDict[hediffDef] <= 0.001f)
                            {
                                pawnDict.Remove(hediffDef);
                                // Удаляем хеддиф у павна
                                var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                                if (hediff != null)
                                    pawn.health.RemoveHediff(hediff);
                            }
                        }
                    }
                }

                // Применяем хеддифы
                foreach (var kv in pawnDict)
                {
                    ApplyAllergy(pawn, kv.Key, kv.Value);
                }
            }
        }

        private float GetMaxSeverityForHediff(HediffDef hediffDef)
        {
            float maxSev = settings.maxSeverity;
            foreach (var thingDef in DefDatabase<ThingDef>.AllDefs)
            {
                var comp = thingDef.GetCompProperties<CompProperties_PlantAllergySource>();
                if (comp?.hediffDef == hediffDef && comp.maxSeverity > 0)
                {
                    maxSev = comp.maxSeverity;
                    break;
                }
            }
            return maxSev;
        }

        private float GetSeverityPerTickForHediff(HediffDef hediffDef)
        {
            float sevPerTick = settings.severityPerTick;
            foreach (var thingDef in DefDatabase<ThingDef>.AllDefs)
            {
                var comp = thingDef.GetCompProperties<CompProperties_PlantAllergySource>();
                if (comp?.hediffDef == hediffDef && comp.severityPerTick > 0)
                {
                    sevPerTick = comp.severityPerTick;
                    break;
                }
            }
            return sevPerTick;
        }

        private float GetDecayForHediff(HediffDef hediffDef)
        {
            float decay = settings.decayPerTick;
            foreach (var thingDef in DefDatabase<ThingDef>.AllDefs)
            {
                var comp = thingDef.GetCompProperties<CompProperties_PlantAllergySource>();
                if (comp?.hediffDef == hediffDef && comp.decayPerTick > 0)
                {
                    decay = comp.decayPerTick;
                    break;
                }
            }
            return decay;
        }

        private bool IsValidPawn(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.health == null) return false;

            if (pawn.IsColonistPlayerControlled && settings.affectColonists) return true;
            if (pawn.IsPrisonerOfColony && settings.affectPrisoners) return true;
            if (pawn.IsSlaveOfColony && settings.affectSlaves) return true;
            if (pawn.Faction != null && !pawn.Faction.IsPlayer
                && !pawn.HostileTo(Faction.OfPlayer) && settings.affectVisitors) return true;
            if (!pawn.RaceProps.Humanlike && settings.affectAnimals) return true;

            return false;
        }

        private bool HasProtection(Pawn pawn)
        {
            if (settings.protectiveHediffs.NullOrEmpty()) return false;
            foreach (var hd in settings.protectiveHediffs)
                if (pawn.health.hediffSet.HasHediff(hd))
                    return true;
            return false;
        }

        private Dictionary<HediffDef, float> CalculateSeverities(Pawn pawn)
        {
            var result = new Dictionary<HediffDef, float>();

            float checkRadius = settings.radius;
            int radiusInt = Mathf.CeilToInt(checkRadius);
            var grid = map.thingGrid;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, radiusInt, true))
            {
                if (!cell.InBounds(map)) continue;

                var things = grid.ThingsListAt(cell);
                for (int i = 0; i < things.Count; i++)
                {
                    if (!(things[i] is Plant plant)) continue;
                    if (plant.LeaflessNow) continue;

                    var compProps = plant.def.GetCompProperties<CompProperties_PlantAllergySource>();
                    if (compProps?.hediffDef == null) continue;

                    float plantRadius = compProps.radius > 0 ? compProps.radius : settings.radius;
                    float plantRadiusSq = plantRadius * plantRadius;

                    if ((plant.Position - pawn.Position).LengthHorizontalSquared > plantRadiusSq) continue;

                    bool plantOnlyMature = compProps.onlyMature;
                    if (plantOnlyMature && plant.LifeStage != PlantLifeStage.Mature) continue;

                    // Возвращаем не финальную severity, а количество, которое нужно добавить
                    if (settings.useFixedMode)
                    {
                        if (!result.ContainsKey(compProps.hediffDef))
                            result[compProps.hediffDef] = 0f;
                        result[compProps.hediffDef] += settings.fixedSeverity;
                    }
                    else
                    {
                        if (!result.ContainsKey(compProps.hediffDef))
                            result[compProps.hediffDef] = 0f;
                        result[compProps.hediffDef] += compProps.severityContribution;
                    }
                }
            }

            // Умножаем на severityPerTick для плавного накопления
            var finalResult = new Dictionary<HediffDef, float>();
            foreach (var kv in result)
            {
                float sevPerTick = GetSeverityPerTickForHediff(kv.Key);
                finalResult[kv.Key] = kv.Value * sevPerTick;
            }

            return finalResult;
        }

        private void ApplyAllergy(Pawn pawn, HediffDef hediffDef, float severity)
        {
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff != null)
            {
                hediff.Severity = severity;
            }
            else
            {
                if (severity > 0.001f)
                {
                    hediff = HediffMaker.MakeHediff(hediffDef, pawn);
                    hediff.Severity = severity;
                    pawn.health.AddHediff(hediff);
                }
            }
        }

        private void ReduceAllergy(Pawn pawn, HediffDef hediffDef)
        {
            if (pawnSeverities.ContainsKey(pawn))
            {
                var pawnDict = pawnSeverities[pawn];
                if (pawnDict.ContainsKey(hediffDef))
                {
                    float decay = GetDecayForHediff(hediffDef);
                    pawnDict[hediffDef] -= decay;

                    if (pawnDict[hediffDef] <= 0.001f)
                    {
                        pawnDict.Remove(hediffDef);
                        var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                        if (hediff != null)
                            pawn.health.RemoveHediff(hediff);
                    }
                    else
                    {
                        var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                        if (hediff != null)
                            hediff.Severity = pawnDict[hediffDef];
                    }
                }
            }
        }

        private void ReduceAllTracked(Pawn pawn)
        {
            foreach (var hediffDef in trackedHediffs)
                ReduceAllergy(pawn, hediffDef);
        }
    }

    // ==================== HARMONY ====================
    [HarmonyPatch(typeof(Map), nameof(Map.FinalizeInit))]
    public static class PlantAllergy_Map_FinalizeInit_Patch
    {
        public static void Postfix(Map __instance)
        {
            if (__instance.GetComponent<PlantAllergyMapComponent>() == null)
                __instance.components.Add(new PlantAllergyMapComponent(__instance));
        }
    }
}
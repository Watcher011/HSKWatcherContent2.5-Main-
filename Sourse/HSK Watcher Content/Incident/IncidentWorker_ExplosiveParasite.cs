using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace HSK.Watcher.Content.Incident
{
    [StaticConstructorOnStartup]
    public static class ExplosiveParasiteHarmony
    {
        static ExplosiveParasiteHarmony()
        {
            Harmony harmony = new Harmony("HSK.Watcher.ExplosiveParasite");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(Pawn), "PostApplyDamage")]
    public static class Pawn_PostApplyDamage_Patch
    {
        public static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
        {
            if (__instance == null) return;
            if (!__instance.RaceProps.Animal) return;
            if (totalDamageDealt <= 0f) return;

            Hediff explosiveHediff = __instance.health.hediffSet.GetFirstHediffOfDef(
                DefDatabase<HediffDef>.GetNamed("ExplosiveParasite", false));

            if (explosiveHediff != null)
            {
                HediffComp_ExplosiveOnDamage comp = explosiveHediff.TryGetComp<HediffComp_ExplosiveOnDamage>();
                comp?.TriggerExplosion();
            }
        }
    }

    public class HediffCompProperties_ExplosiveOnDamage : HediffCompProperties
    {
        public float explosionRadius = 3.9f;
        public DamageDef explosionDamageType;
        public int explosionDamage = 10;
        public float chanceToExplode = 1.0f;
        public GasType postExplosionGasType = GasType.ToxGas;
        public float postExplosionGasRadius = 4f;
        public string letterLabelKey;
        public string letterTextKey;
        public LetterDef letterDef;

        public HediffCompProperties_ExplosiveOnDamage()
        {
            compClass = typeof(HediffComp_ExplosiveOnDamage);
        }
    }

    public class HediffComp_ExplosiveOnDamage : HediffComp
    {
        private bool hasExploded = false;
        private int ticksSinceLastMote = 0;

        public HediffCompProperties_ExplosiveOnDamage Props => (HediffCompProperties_ExplosiveOnDamage)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || Pawn.Map == null) return;

            ticksSinceLastMote++;
            if (ticksSinceLastMote >= 120)
            {
                ticksSinceLastMote = 0;
                ThrowParasiteMotes();
            }
        }

        private void ThrowParasiteMotes()
        {
            if (Pawn == null || Pawn.Map == null) return;

            Vector3 drawPos = Pawn.DrawPos;
            drawPos.y = Altitudes.AltitudeFor(AltitudeLayer.MoteOverhead);

            FleckMaker.ThrowDustPuffThick(drawPos, Pawn.Map, 0.8f, new Color(0.3f, 0.7f, 0.2f));

            if (Rand.Chance(0.3f))
            {
                FleckMaker.ThrowSmoke(drawPos, Pawn.Map, 0.5f);
            }
        }

        public void TriggerExplosion()
        {
            if (hasExploded) return;
            if (!Rand.Chance(Props.chanceToExplode)) return;

            hasExploded = true;
            DoExplosion();
        }

        private void DoExplosion()
        {
            if (Pawn == null || Pawn.Destroyed) return;

            IntVec3 center = Pawn.Position;
            Map map = Pawn.Map;
            if (map == null) return;

            Pawn.Destroy(DestroyMode.Vanish);

            GenExplosion.DoExplosion(
                center: center,
                map: map,
                radius: Props.explosionRadius,
                damType: Props.explosionDamageType ?? DamageDefOf.Bomb,
                instigator: null,
                damAmount: Props.explosionDamage,
                armorPenetration: -1f,
                explosionSound: null,
                weapon: null,
                projectile: null,
                intendedTarget: null,
                postExplosionSpawnThingDef: null,
                postExplosionSpawnChance: 0f,
                postExplosionSpawnThingCount: 1,
                postExplosionGasType: Props.postExplosionGasType,
                applyDamageToExplosionCellsNeighbors: false,
                preExplosionSpawnThingDef: null,
                preExplosionSpawnChance: 0f,
                preExplosionSpawnThingCount: 1,
                chanceToStartFire: 0f,
                damageFalloff: false,
                direction: null,
                ignoredThings: null,
                affectedAngle: null,
                doVisualEffects: true,
                propagationSpeed: 1f,
                excludeRadius: 0f,
                doSoundEffects: true,
                postExplosionSpawnThingDefWater: null,
                screenShakeFactor: 1f
            );

            if (!Props.letterLabelKey.NullOrEmpty())
            {
                string label = Props.letterLabelKey.Translate(Pawn.LabelShort);
                string text = Props.letterTextKey.Translate(Pawn.LabelShort);
                Find.LetterStack.ReceiveLetter(
                    label,
                    text,
                    Props.letterDef ?? LetterDefOf.NegativeEvent,
                    new LookTargets(center, map)
                );
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref hasExploded, "hasExploded", false);
            Scribe_Values.Look(ref ticksSinceLastMote, "ticksSinceLastMote", 0);
        }
    }

    public class IncidentWorker_ExplosiveParasiteRaid : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return TryFindAnimalKind((Map)parms.target, out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!TryFindAnimalKind(map, out PawnKindDef animalKind))
                return false;

            int animalCount = Mathf.Max(1, Mathf.RoundToInt(parms.points / animalKind.combatPower));
            animalCount = Mathf.Clamp(animalCount, 1, 15);

            if (!CellFinder.TryFindRandomEdgeCellWith((IntVec3 c) => map.reachability.CanReachColony(c), map, CellFinder.EdgeRoadChance_Neutral, out IntVec3 spawnCell))
            {
                spawnCell = CellFinder.RandomEdgeCell(map);
            }

            HediffDef parasiteDef = DefDatabase<HediffDef>.GetNamed("ExplosiveParasite", false);

            if (parasiteDef == null)
            {
                Log.Warning("[HSK.Watcher] HediffDef 'ExplosiveParasite' not found!");
                return false;
            }

            // Получаем враждебную фракцию
            Faction enemyFaction = Find.FactionManager.FirstFactionOfDef(FactionDefOf.Insect);
            if (enemyFaction == null)
            {
                enemyFaction = Find.FactionManager.FirstFactionOfDef(FactionDefOf.Mechanoid);
            }
            if (enemyFaction == null)
            {
                enemyFaction = FactionUtility.DefaultFactionFrom(FactionDefOf.Insect);
            }

            List<Pawn> spawnedAnimals = new List<Pawn>();
            for (int i = 0; i < animalCount; i++)
            {
                IntVec3 loc = CellFinder.RandomClosewalkCellNear(spawnCell, map, 10);

                PawnGenerationRequest request = new PawnGenerationRequest(
                    kind: animalKind,
                    faction: enemyFaction,
                    context: PawnGenerationContext.NonPlayer,
                    tile: map.Tile,
                    forceGenerateNewPawn: true,
                    allowDead: false,
                    allowDowned: false,
                    canGeneratePawnRelations: false,
                    mustBeCapableOfViolence: true
                );

                Pawn animal = PawnGenerator.GeneratePawn(request);
                GenSpawn.Spawn(animal, loc, map, Rot4.Random);

                // Взрывной паразит
                Hediff parasite = HediffMaker.MakeHediff(parasiteDef, animal);
                animal.health.AddHediff(parasite);

                spawnedAnimals.Add(animal);
            }

            // Создаём Lord для целенаправленной атаки на колонию
            if (spawnedAnimals.Count > 0 && enemyFaction != null)
            {
                LordJob_AssaultColony lordJob = new LordJob_AssaultColony(
                    enemyFaction,
                    canKidnap: false,
                    canTimeoutOrFlee: false,
                    sappers: false,
                    useAvoidGridSmart: false,
                    canSteal: false
                );
                LordMaker.MakeNewLord(enemyFaction, lordJob, map, spawnedAnimals);
            }

            SendStandardLetter(
                "HSK_ExplosiveParasiteRaid_LetterLabel".Translate(),
                "HSK_ExplosiveParasiteRaid_LetterText".Translate(),
                LetterDefOf.ThreatBig,
                parms,
                new LookTargets(spawnedAnimals)
            );
            return true;
        }

        private bool TryFindAnimalKind(Map map, out PawnKindDef kind)
        {
            List<PawnKindDef> validKinds = new List<PawnKindDef>();
            foreach (PawnKindDef pk in DefDatabase<PawnKindDef>.AllDefs)
            {
                if (!pk.RaceProps.Animal) continue;
                if (pk.RaceProps.Insect) continue;
                if (pk.RaceProps.baseBodySize < 0.3f) continue;
                if (pk.combatPower <= 0) continue;
                if (map.Biome.AllWildAnimals.Contains(pk))
                {
                    validKinds.Add(pk);
                }
            }

            if (validKinds.Count == 0)
            {
                foreach (PawnKindDef pk in DefDatabase<PawnKindDef>.AllDefs)
                {
                    if (!pk.RaceProps.Animal) continue;
                    if (pk.RaceProps.Insect) continue;
                    if (pk.combatPower > 0)
                    {
                        validKinds.Add(pk);
                    }
                }
            }

            if (validKinds.Count == 0)
            {
                kind = null;
                return false;
            }

            kind = validKinds.RandomElementByWeight(k => k.combatPower);
            return true;
        }
    }
}
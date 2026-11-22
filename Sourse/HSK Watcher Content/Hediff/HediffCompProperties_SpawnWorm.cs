using HarmonyLib;
using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace HSK.Watcher.Content
{
    [StaticConstructorOnStartup]
    public static class GutWormsSpawnPatcher
    {
        static GutWormsSpawnPatcher()
        {
            try
            {
                var harmony = new Harmony("HSK.Watcher.Content.GutWorms");
                var original = AccessTools.Method(typeof(Hediff), nameof(Hediff.Tick));
                var postfix = AccessTools.Method(typeof(Hediff_Tick_Patch), nameof(Hediff_Tick_Patch.Postfix));
                harmony.Patch(original, postfix: new HarmonyMethod(postfix));
                Log.Message("[GutWormsSpawn] Patch applied.");
            }
            catch (Exception ex)
            {
                Log.Error($"[GutWormsSpawn] {ex}");
            }
        }
    }

    public static class Hediff_Tick_Patch
    {
        private const int CheckInterval = 250;
        private const float MtbDays = 2f;

        public static void Postfix(Hediff __instance)
        {
            if (!(__instance is HediffWithComps)) return;
            if (__instance.def?.defName != "GutWorms") return;

            Pawn pawn = __instance.pawn;
            if (pawn == null || pawn.Dead || pawn.Map == null) return;
            if (!pawn.IsHashIntervalTick(CheckInterval)) return;
            if (!Rand.MTBEventOccurs(MtbDays, 60000f, CheckInterval)) return;

            // Спавним прямо на клетке пешки
            IntVec3 spawnCell = pawn.Position;

            if (!spawnCell.IsValid || !spawnCell.Walkable(pawn.Map))
            {
                // Если клетка пешки непроходима, ищем соседнюю
                spawnCell = CellFinder.RandomClosewalkCellNear(
                    pawn.Position,
                    pawn.Map,
                    1
                );
            }

            if (!spawnCell.IsValid) return;

            PawnKindDef wormKind = DefDatabase<PawnKindDef>.GetNamed("Worm", false);
            if (wormKind == null)
            {
                Log.Warning("[GutWormsSpawn] PawnKindDef 'Worm' not found.");
                return;
            }

            // Враждебная фракция
            Faction hostileFaction = Find.FactionManager.FirstFactionOfDef(FactionDefOf.Insect)
                ?? Find.FactionManager.RandomEnemyFaction()
                ?? FactionUtility.DefaultFactionFrom(FactionDefOf.Insect);

            Pawn worm = PawnGenerator.GeneratePawn(
                new PawnGenerationRequest(
                    wormKind,
                    hostileFaction,
                    PawnGenerationContext.NonPlayer,
                    pawn.Map.Tile,
                    forceGenerateNewPawn: true,
                    canGeneratePawnRelations: false,
                    colonistRelationChanceFactor: 0f
                )
            );

            if (worm == null) return;

            GenSpawn.Spawn(worm, spawnCell, pawn.Map);

            // Принудительная атака на хозяина
            if (worm.mindState != null)
            {
                worm.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter);
            }

            if (worm.jobs != null)
            {
                Job attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, pawn);
                attackJob.killIncappedTarget = true;
                worm.jobs.StartJob(attackJob, JobCondition.InterruptForced);
            }

            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "worm!", 2f);
            Log.Message($"[GutWormsSpawn] Worm spawned at {spawnCell} on {pawn.Name}");
        }
    }
}
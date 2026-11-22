using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.Sound;

//namespace HSK.Watcher.Content.Incident
//{
//    // ============================================
//    // ИНЦИДЕНТ: Спавн Ocellus
//    // ============================================
//    public class IncidentWorker_OcellusAttack : IncidentWorker
//    {
//        public static readonly string ParasiteHediffDefName = "OcellusBrainParasite";
//        public static readonly string OcellusPawnKindDefName = "Ocellus";
//        public static readonly string InsectFactionDefName = "Insect";

//        protected override bool CanFireNowSub(IncidentParms parms)
//        {
//            Map map = (Map)parms.target;
//            return map.mapPawns.FreeColonists.Any(c => c.Spawned && !c.Dead);
//        }

//        protected override bool TryExecuteWorker(IncidentParms parms)
//        {
//            Map map = (Map)parms.target;

//            IntVec3 spawnCell = FindFarthestSpawnCell(map);
//            if (!spawnCell.IsValid)
//            {
//                Log.Warning("[Ocellus] Could not find valid spawn cell!");
//                return false;
//            }

//            Faction insectFaction = Find.FactionManager.FirstFactionOfDef(
//                FactionDef.Named(InsectFactionDefName)
//            );
//            if (insectFaction == null)
//            {
//                Log.Warning("[Ocellus] Insect faction not found!");
//                return false;
//            }

//            PawnKindDef ocellusKind = DefDatabase<PawnKindDef>.GetNamed(OcellusPawnKindDefName, false);
//            if (ocellusKind == null)
//            {
//                Log.Warning($"[Ocellus] PawnKindDef '{OcellusPawnKindDefName}' not found!");
//                return false;
//            }

//            Pawn ocellus = PawnGenerator.GeneratePawn(ocellusKind, insectFaction);
//            GenSpawn.Spawn(ocellus, spawnCell, map);

//            // Регистрируем в трекере
//            MapComponent_OcellusTracker tracker = map.GetComponent<MapComponent_OcellusTracker>();
//            if (tracker == null)
//            {
//                tracker = new MapComponent_OcellusTracker(map);
//                map.components.Add(tracker);
//            }
//            tracker.RegisterOcellus(ocellus);

//            SendStandardLetter(
//                "LetterLabelOcellusAttack".Translate(),
//                "LetterOcellusAttack".Translate(),
//                LetterDefOf.ThreatSmall,
//                parms,
//                ocellus
//            );

//            return true;
//        }

//        private IntVec3 FindFarthestSpawnCell(Map map)
//        {
//            List<IntVec3> edgeCells = new List<IntVec3>();
//            foreach (IntVec3 cell in CellRect.WholeMap(map).EdgeCells)
//            {
//                if (cell.Walkable(map) && !cell.Fogged(map))
//                    edgeCells.Add(cell);
//            }

//            if (edgeCells.Count == 0) return IntVec3.Invalid;

//            List<Pawn> colonists = map.mapPawns.FreeColonists.ToList();
//            colonists.RemoveAll(c => !c.Spawned);

//            if (colonists.Count == 0)
//                return edgeCells[Rand.Range(0, edgeCells.Count)];

//            IntVec3 bestCell = IntVec3.Invalid;
//            float bestDist = -1f;

//            for (int i = 0; i < edgeCells.Count; i++)
//            {
//                float minDist = float.MaxValue;
//                for (int j = 0; j < colonists.Count; j++)
//                {
//                    float d = edgeCells[i].DistanceToSquared(colonists[j].Position);
//                    if (d < minDist) minDist = d;
//                }

//                if (minDist > bestDist)
//                {
//                    bestDist = minDist;
//                    bestCell = edgeCells[i];
//                }
//            }

//            return bestCell;
//        }
//    }

//    // ============================================
//    // MAP COMPONENT: Полная логика Ocellus
//    // ============================================
//    public class MapComponent_OcellusTracker : MapComponent
//    {
//        private List<OcellusData> ocellusList = new List<OcellusData>();
//        private List<Pawn> infectedColonists = new List<Pawn>();

//        public MapComponent_OcellusTracker(Map map) : base(map) { }

//        public void RegisterOcellus(Pawn ocellus)
//        {
//            ocellusList.Add(new OcellusData
//            {
//                Ocellus = ocellus,
//                Target = null,
//                AttackStartTick = Find.TickManager.TicksGame,
//                LastPosition = ocellus.Position,
//                StuckTicks = 0,
//                LastJobTick = 0
//            });
//            Log.Message($"[Ocellus] Registered {ocellus.ThingID} at {ocellus.Position}");
//        }

//        public override void MapComponentTick()
//        {
//            if (Find.TickManager.TicksGame % 30 != 0) return;

//            for (int i = ocellusList.Count - 1; i >= 0; i--)
//            {
//                var data = ocellusList[i];

//                if (data.Ocellus == null || data.Ocellus.Destroyed || data.Ocellus.Dead)
//                {
//                    ocellusList.RemoveAt(i);
//                    continue;
//                }

//                // Проверка застревания
//                if (data.Ocellus.Position == data.LastPosition)
//                {
//                    data.StuckTicks += 30;
//                }
//                else
//                {
//                    data.StuckTicks = 0;
//                    data.LastPosition = data.Ocellus.Position;
//                }

//                // Телепортация если застрял
//                if (data.StuckTicks > 300 && data.Target != null)
//                {
//                    IntVec3 newPos = CellFinder.RandomClosewalkCellNear(
//                        data.Target.Position, map, 3, c => c.Walkable(map));

//                    if (newPos.IsValid)
//                    {
//                        data.Ocellus.Position = newPos;
//                        data.StuckTicks = 0;
//                        Log.Message($"[Ocellus] Teleported to {newPos}");
//                    }
//                }

//                // Находим/обновляем цель
//                if (data.Target == null || data.Target.Dead || !data.Target.Spawned)
//                {
//                    data.Target = FindNearestColonist(data.Ocellus);
//                    if (data.Target == null)
//                    {
//                        DisappearOcellus(data.Ocellus);
//                        ocellusList.RemoveAt(i);
//                        continue;
//                    }
//                }

//                // Проверяем расстояние до цели
//                float dist = data.Ocellus.Position.DistanceTo(data.Target.Position);

//                // ВНЕДРЕНИЕ: вплотную (0.5f)
//                if (dist <= 0.5f)
//                {
//                    Log.Message($"[Ocellus] Adjacent to {data.Target.NameShortColored}, injecting!");
//                    InjectAndDisappear(data.Ocellus, data.Target);
//                    ocellusList.RemoveAt(i);
//                    continue;
//                }

//                // Даём команду двигаться
//                int currentTick = Find.TickManager.TicksGame;
//                if (currentTick - data.LastJobTick > 60)
//                {
//                    data.LastJobTick = currentTick;
//                    ForceMoveToTarget(data.Ocellus, data.Target);
//                }

//                // Таймаут 30 минут
//                if (currentTick - data.AttackStartTick > 75000)
//                {
//                    Log.Message($"[Ocellus] Timeout, disappearing.");
//                    DisappearOcellus(data.Ocellus);
//                    ocellusList.RemoveAt(i);
//                }
//            }

//            UpdateInfectedColonists();
//        }

//        private void ForceMoveToTarget(Pawn ocellus, Pawn target)
//        {
//            if (ocellus == null || target == null) return;
//            if (ocellus.Downed || !ocellus.Spawned) return;

//            ocellus.jobs.ClearQueuedJobs();

//            Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, target.Position);
//            gotoJob.expiryInterval = 120;
//            gotoJob.checkOverrideOnExpire = false;
//            gotoJob.locomotionUrgency = LocomotionUrgency.Sprint;
//            gotoJob.canBashDoors = true;
//            gotoJob.canBashFences = true;
//            gotoJob.ignoreDesignations = true;

//            ocellus.jobs.StartJob(gotoJob, JobCondition.InterruptForced);
//        }

//        private Pawn FindNearestColonist(Pawn ocellus)
//        {
//            List<Pawn> colonists = ocellus.Map.mapPawns.FreeColonists.ToList();
//            Pawn best = null;
//            float bestDist = float.MaxValue;

//            for (int i = 0; i < colonists.Count; i++)
//            {
//                Pawn c = colonists[i];
//                if (!c.Spawned || c.Dead || c.Downed) continue;

//                float d = ocellus.Position.DistanceToSquared(c.Position);
//                if (d < bestDist)
//                {
//                    bestDist = d;
//                    best = c;
//                }
//            }
//            return best;
//        }

//        private void InjectAndDisappear(Pawn ocellus, Pawn target)
//        {
//            HediffDef parasiteDef = DefDatabase<HediffDef>.GetNamed(
//                IncidentWorker_OcellusAttack.ParasiteHediffDefName, false);

//            if (parasiteDef != null)
//            {
//                Hediff hediff = HediffMaker.MakeHediff(parasiteDef, target);
//                hediff.Severity = 1.0f;

//                BodyPartRecord brain = target.health.hediffSet.GetBrain();
//                if (brain != null)
//                    target.health.AddHediff(hediff, brain);
//                else
//                    target.health.AddHediff(hediff);

//                FleckMaker.Static(target.Position, target.Map, FleckDefOf.ExplosionFlash, 2f);
//            }

//            TurnColonistHostile(target);

//            FleckMaker.Static(ocellus.Position, ocellus.Map, FleckDefOf.ExplosionFlash, 2f);
//            ocellus.Destroy();
//        }

//        private void TurnColonistHostile(Pawn colonist)
//        {
//            if (colonist == null || colonist.Dead) return;

//            Faction insectFaction = Find.FactionManager.FirstFactionOfDef(
//                FactionDef.Named(IncidentWorker_OcellusAttack.InsectFactionDefName)
//            );
//            if (insectFaction == null) return;

//            string colonistName = colonist.Name.ToStringShort;

//            colonist.jobs.EndCurrentJob(JobCondition.InterruptForced);
//            colonist.jobs.ClearQueuedJobs();
//            colonist.SetFaction(insectFaction);

//            if (colonist.mindState != null)
//            {
//                colonist.mindState.mentalStateHandler.TryStartMentalState(
//                    MentalStateDefOf.Berserk,
//                    "Ocellus parasite",
//                    forceWake: true,
//                    causedByMood: false
//                );
//            }

//            FleckMaker.Static(colonist.Position, colonist.Map, FleckDefOf.ExplosionFlash, 3f);

//            Find.LetterStack.ReceiveLetter(
//                "Ocellus Infection!",
//                $"{colonistName} has been taken over by the Ocellus parasite! They are now hostile and attacking everyone!",
//                LetterDefOf.ThreatBig,
//                colonist
//            );

//            Log.Message($"[Ocellus] {colonistName} turned hostile");
//        }

//        private void UpdateInfectedColonists()
//        {
//            for (int i = infectedColonists.Count - 1; i >= 0; i--)
//            {
//                Pawn infected = infectedColonists[i];

//                if (infected == null || infected.Dead || !infected.Spawned)
//                {
//                    infectedColonists.RemoveAt(i);
//                    continue;
//                }

//                Hediff hediff = infected.health.hediffSet.GetFirstHediffOfDef(
//                    DefDatabase<HediffDef>.GetNamed(IncidentWorker_OcellusAttack.ParasiteHediffDefName, false)
//                );

//                if (hediff == null)
//                {
//                    infectedColonists.RemoveAt(i);
//                    continue;
//                }

//                if (infected.mindState?.mentalStateHandler?.CurStateDef != MentalStateDefOf.Berserk)
//                {
//                    if (!infected.Downed && infected.Awake())
//                    {
//                        infected.mindState.mentalStateHandler.TryStartMentalState(
//                            MentalStateDefOf.Berserk,
//                            "Ocellus parasite",
//                            causedByMood: false
//                        );
//                    }
//                }

//                if (infected.CurJobDef == JobDefOf.Wait_Wander ||
//                    infected.CurJobDef == JobDefOf.GotoWander ||
//                    infected.CurJobDef == null)
//                {
//                    Pawn enemy = FindNearestEnemy(infected);
//                    if (enemy != null)
//                    {
//                        Job attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, enemy);
//                        attackJob.expiryInterval = 999999;
//                        infected.jobs.StartJob(attackJob, JobCondition.InterruptForced);
//                    }
//                }
//            }
//        }

//        private Pawn FindNearestEnemy(Pawn searcher)
//        {
//            List<Pawn> allPawns = searcher.Map.mapPawns.AllPawnsSpawned.ToList();
//            Pawn best = null;
//            float bestDist = float.MaxValue;

//            for (int i = 0; i < allPawns.Count; i++)
//            {
//                Pawn p = allPawns[i];
//                if (p == searcher) continue;
//                if (p.Dead || !p.Spawned) continue;
//                if (!p.HostileTo(searcher)) continue;

//                float d = searcher.Position.DistanceToSquared(p.Position);
//                if (d < bestDist)
//                {
//                    bestDist = d;
//                    best = p;
//                }
//            }
//            return best;
//        }

//        private void DisappearOcellus(Pawn ocellus)
//        {
//            FleckMaker.Static(ocellus.Position, ocellus.Map, FleckDefOf.ExplosionFlash, 2f);
//            ocellus.Destroy();
//        }

//        public override void ExposeData()
//        {
//            base.ExposeData();
//            Scribe_Collections.Look(ref ocellusList, "ocellusList", LookMode.Deep);
//            Scribe_Collections.Look(ref infectedColonists, "infectedColonists", LookMode.Reference);
//        }
//    }

//    public class OcellusData : IExposable
//    {
//        public Pawn Ocellus;
//        public Pawn Target;
//        public int AttackStartTick;
//        public IntVec3 LastPosition;
//        public int StuckTicks;
//        public int LastJobTick;

//        public void ExposeData()
//        {
//            Scribe_References.Look(ref Ocellus, "ocellus");
//            Scribe_References.Look(ref Target, "target");
//            Scribe_Values.Look(ref AttackStartTick, "attackStartTick", 0);
//            Scribe_Values.Look(ref LastPosition, "lastPosition");
//            Scribe_Values.Look(ref StuckTicks, "stuckTicks", 0);
//            Scribe_Values.Look(ref LastJobTick, "lastJobTick", 0);
//        }
//    }
//}
namespace HSK.Watcher.Content.Incident
{
    // ============================================
    // ИНЦИДЕНТ: Спавн Ocellus
    // ============================================
    public class IncidentWorker_OcellusAttack : IncidentWorker
    {
        public static readonly string ParasiteHediffDefName = "OcellusBrainParasite";
        public static readonly string OcellusPawnKindDefName = "Ocellus";
        public static readonly string InsectFactionDefName = "Insect";

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            return map.mapPawns.FreeColonists.Any(c => c.Spawned && !c.Dead);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;

            IntVec3 spawnCell = FindFarthestSpawnCell(map);
            if (!spawnCell.IsValid)
            {
                Log.Warning("[Ocellus] Could not find valid spawn cell!");
                return false;
            }

            Faction insectFaction = Find.FactionManager.FirstFactionOfDef(
                FactionDef.Named(InsectFactionDefName)
            );
            if (insectFaction == null)
            {
                Log.Warning("[Ocellus] Insect faction not found!");
                return false;
            }

            PawnKindDef ocellusKind = DefDatabase<PawnKindDef>.GetNamed(OcellusPawnKindDefName, false);
            if (ocellusKind == null)
            {
                Log.Warning($"[Ocellus] PawnKindDef '{OcellusPawnKindDefName}' not found!");
                return false;
            }

            Pawn ocellus = PawnGenerator.GeneratePawn(ocellusKind, insectFaction);
            GenSpawn.Spawn(ocellus, spawnCell, map);

            MapComponent_OcellusTracker tracker = map.GetComponent<MapComponent_OcellusTracker>();
            if (tracker == null)
            {
                tracker = new MapComponent_OcellusTracker(map);
                map.components.Add(tracker);
            }
            tracker.RegisterOcellus(ocellus);

            SendStandardLetter(
                "LetterLabelOcellusAttack".Translate(),
                "LetterOcellusAttack".Translate(),
                LetterDefOf.ThreatSmall,
                parms,
                ocellus
            );

            return true;
        }

        private IntVec3 FindFarthestSpawnCell(Map map)
        {
            List<IntVec3> edgeCells = new List<IntVec3>();
            foreach (IntVec3 cell in CellRect.WholeMap(map).EdgeCells)
            {
                if (cell.Walkable(map) && !cell.Fogged(map))
                    edgeCells.Add(cell);
            }

            if (edgeCells.Count == 0) return IntVec3.Invalid;

            List<Pawn> colonists = map.mapPawns.FreeColonists.ToList();
            colonists.RemoveAll(c => !c.Spawned);

            if (colonists.Count == 0)
                return edgeCells[Rand.Range(0, edgeCells.Count)];

            IntVec3 bestCell = IntVec3.Invalid;
            float bestDist = -1f;

            for (int i = 0; i < edgeCells.Count; i++)
            {
                float minDist = float.MaxValue;
                for (int j = 0; j < colonists.Count; j++)
                {
                    float d = edgeCells[i].DistanceToSquared(colonists[j].Position);
                    if (d < minDist) minDist = d;
                }

                if (minDist > bestDist)
                {
                    bestDist = minDist;
                    bestCell = edgeCells[i];
                }
            }

            return bestCell;
        }
    }

    // ============================================
    // MAP COMPONENT: Полная логика Ocellus
    // ============================================
    public class MapComponent_OcellusTracker : MapComponent
    {
        private List<OcellusData> ocellusList = new List<OcellusData>();
        private List<Pawn> infectedColonists = new List<Pawn>();

        public MapComponent_OcellusTracker(Map map) : base(map) { }

        public void RegisterOcellus(Pawn ocellus)
        {
            ocellusList.Add(new OcellusData
            {
                Ocellus = ocellus,
                Target = null,
                AttackStartTick = Find.TickManager.TicksGame,
                LastPosition = ocellus.Position,
                StuckTicks = 0,
                LastJobTick = 0
            });
            Log.Message($"[Ocellus] Registered {ocellus.ThingID} at {ocellus.Position}");
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 30 != 0) return;

            for (int i = ocellusList.Count - 1; i >= 0; i--)
            {
                var data = ocellusList[i];

                if (data.Ocellus == null || data.Ocellus.Destroyed || data.Ocellus.Dead)
                {
                    ocellusList.RemoveAt(i);
                    continue;
                }

                // Проверка застревания
                if (data.Ocellus.Position == data.LastPosition)
                {
                    data.StuckTicks += 30;
                }
                else
                {
                    data.StuckTicks = 0;
                    data.LastPosition = data.Ocellus.Position;
                }

                // Телепортация если застрял
                if (data.StuckTicks > 300 && data.Target != null)
                {
                    IntVec3 newPos = CellFinder.RandomClosewalkCellNear(
                        data.Target.Position, map, 3, c => c.Walkable(map));

                    if (newPos.IsValid)
                    {
                        data.Ocellus.Position = newPos;
                        data.StuckTicks = 0;
                        Log.Message($"[Ocellus] Teleported to {newPos}");
                    }
                }

                // Находим/обновляем цель
                if (data.Target == null || data.Target.Dead || !data.Target.Spawned)
                {
                    data.Target = FindNearestColonist(data.Ocellus);
                    if (data.Target == null)
                    {
                        DisappearOcellus(data.Ocellus);
                        ocellusList.RemoveAt(i);
                        continue;
                    }
                }

                // Проверяем расстояние до цели
                float dist = data.Ocellus.Position.DistanceTo(data.Target.Position);

                // ВНЕДРЕНИЕ: вплотную (0.5f)
                if (dist <= 0.5f)
                {
                    Log.Message($"[Ocellus] Adjacent to {data.Target.NameShortColored}, injecting!");
                    InjectAndDisappear(data.Ocellus, data.Target);
                    ocellusList.RemoveAt(i);
                    continue;
                }

                // Даём команду двигаться
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - data.LastJobTick > 60)
                {
                    data.LastJobTick = currentTick;
                    ForceMoveToTarget(data.Ocellus, data.Target);
                }

                // Таймаут 30 минут
                if (currentTick - data.AttackStartTick > 75000)
                {
                    Log.Message($"[Ocellus] Timeout, disappearing.");
                    DisappearOcellus(data.Ocellus);
                    ocellusList.RemoveAt(i);
                }
            }

            UpdateInfectedColonists();
        }

        private void ForceMoveToTarget(Pawn ocellus, Pawn target)
        {
            if (ocellus == null || target == null) return;
            if (ocellus.Downed || !ocellus.Spawned) return;

            ocellus.jobs.ClearQueuedJobs();

            Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, target.Position);
            gotoJob.expiryInterval = 120;
            gotoJob.checkOverrideOnExpire = false;
            gotoJob.locomotionUrgency = LocomotionUrgency.Sprint;
            gotoJob.canBashDoors = true;
            gotoJob.canBashFences = true;
            gotoJob.ignoreDesignations = true;

            ocellus.jobs.StartJob(gotoJob, JobCondition.InterruptForced);
        }

        private Pawn FindNearestColonist(Pawn ocellus)
        {
            List<Pawn> colonists = ocellus.Map.mapPawns.FreeColonists.ToList();
            Pawn best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn c = colonists[i];
                if (!c.Spawned || c.Dead || c.Downed) continue;

                float d = ocellus.Position.DistanceToSquared(c.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }
            return best;
        }

        private void InjectAndDisappear(Pawn ocellus, Pawn target)
        {
            HediffDef parasiteDef = DefDatabase<HediffDef>.GetNamed(
                IncidentWorker_OcellusAttack.ParasiteHediffDefName, false);

            if (parasiteDef != null)
            {
                Hediff hediff = HediffMaker.MakeHediff(parasiteDef, target);
                hediff.Severity = 1.0f;

                BodyPartRecord brain = target.health.hediffSet.GetBrain();
                if (brain != null)
                    target.health.AddHediff(hediff, brain);
                else
                    target.health.AddHediff(hediff);

                FleckMaker.Static(target.Position, target.Map, FleckDefOf.ExplosionFlash, 2f);
            }

            TurnColonistHostile(target);

            FleckMaker.Static(ocellus.Position, ocellus.Map, FleckDefOf.ExplosionFlash, 2f);
            ocellus.Destroy();
        }

        private void TurnColonistHostile(Pawn colonist)
        {
            if (colonist == null || colonist.Dead) return;

            Faction insectFaction = Find.FactionManager.FirstFactionOfDef(
                FactionDef.Named(IncidentWorker_OcellusAttack.InsectFactionDefName)
            );
            if (insectFaction == null) return;

            string colonistName = colonist.Name.ToStringShort;

            colonist.jobs.EndCurrentJob(JobCondition.InterruptForced);
            colonist.jobs.ClearQueuedJobs();
            colonist.SetFaction(insectFaction);

            if (colonist.mindState != null)
            {
                colonist.mindState.mentalStateHandler.TryStartMentalState(
                    MentalStateDefOf.Berserk,
                    "Ocellus parasite",
                    forceWake: true,
                    causedByMood: false
                );
            }

            FleckMaker.Static(colonist.Position, colonist.Map, FleckDefOf.ExplosionFlash, 3f);

            Find.LetterStack.ReceiveLetter(
                "Ocellus Infection!",
                $"{colonistName} has been taken over by the Ocellus parasite! They are now hostile and attacking everyone!",
                LetterDefOf.ThreatBig,
                colonist
            );

            Log.Message($"[Ocellus] {colonistName} turned hostile");
        }

        private void UpdateInfectedColonists()
        {
            for (int i = infectedColonists.Count - 1; i >= 0; i--)
            {
                Pawn infected = infectedColonists[i];

                if (infected == null || infected.Dead || !infected.Spawned)
                {
                    infectedColonists.RemoveAt(i);
                    continue;
                }

                Hediff hediff = infected.health.hediffSet.GetFirstHediffOfDef(
                    DefDatabase<HediffDef>.GetNamed(IncidentWorker_OcellusAttack.ParasiteHediffDefName, false)
                );

                if (hediff == null)
                {
                    infectedColonists.RemoveAt(i);
                    continue;
                }

                if (infected.mindState?.mentalStateHandler?.CurStateDef != MentalStateDefOf.Berserk)
                {
                    if (!infected.Downed && infected.Awake())
                    {
                        infected.mindState.mentalStateHandler.TryStartMentalState(
                            MentalStateDefOf.Berserk,
                            "Ocellus parasite",
                            causedByMood: false
                        );
                    }
                }

                if (infected.CurJobDef == JobDefOf.Wait_Wander ||
                    infected.CurJobDef == JobDefOf.GotoWander ||
                    infected.CurJobDef == null)
                {
                    Pawn enemy = FindNearestEnemy(infected);
                    if (enemy != null)
                    {
                        Job attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, enemy);
                        attackJob.expiryInterval = 999999;
                        infected.jobs.StartJob(attackJob, JobCondition.InterruptForced);
                    }
                }
            }
        }

        private Pawn FindNearestEnemy(Pawn searcher)
        {
            List<Pawn> allPawns = searcher.Map.mapPawns.AllPawnsSpawned.ToList();
            Pawn best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn p = allPawns[i];
                if (p == searcher) continue;
                if (p.Dead || !p.Spawned) continue;
                if (!p.HostileTo(searcher)) continue;

                float d = searcher.Position.DistanceToSquared(p.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }
            return best;
        }

        private void DisappearOcellus(Pawn ocellus)
        {
            FleckMaker.Static(ocellus.Position, ocellus.Map, FleckDefOf.ExplosionFlash, 2f);
            ocellus.Destroy();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref ocellusList, "ocellusList", LookMode.Deep);
            Scribe_Collections.Look(ref infectedColonists, "infectedColonists", LookMode.Reference);
        }
    }

    public class OcellusData : IExposable
    {
        public Pawn Ocellus;
        public Pawn Target;
        public int AttackStartTick;
        public IntVec3 LastPosition;
        public int StuckTicks;
        public int LastJobTick;

        public void ExposeData()
        {
            Scribe_References.Look(ref Ocellus, "ocellus");
            Scribe_References.Look(ref Target, "target");
            Scribe_Values.Look(ref AttackStartTick, "attackStartTick", 0);
            Scribe_Values.Look(ref LastPosition, "lastPosition");
            Scribe_Values.Look(ref StuckTicks, "stuckTicks", 0);
            Scribe_Values.Look(ref LastJobTick, "lastJobTick", 0);
        }
    }
}

namespace HSK.Watcher.Content
{
    // ============================================
    // HEDIFF COMP: Спавн Ocellus при смерти
    // ============================================
    public class HediffCompProperties_SpawnOcellusOnDeath : HediffCompProperties
    {
        public string pawnKind = "Ocellus";
        public string factionDefName = "Insect";
        public SoundDef spawnSound;
        public string letterLabel;
        public string letterText;
        public LetterDef letterDef;

        public HediffCompProperties_SpawnOcellusOnDeath()
        {
            compClass = typeof(HediffComp_SpawnOcellusOnDeath);
        }
    }

    public class HediffComp_SpawnOcellusOnDeath : HediffComp
    {
        public HediffCompProperties_SpawnOcellusOnDeath Props => (HediffCompProperties_SpawnOcellusOnDeath)props;

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            if (Pawn == null || Pawn.Corpse == null || Pawn.Corpse.Destroyed) return;

            Map map = Pawn.Corpse.Map;
            if (map == null) return;

            // Находим фракцию инсектицидов
            Faction faction = Find.FactionManager.FirstFactionOfDef(
                DefDatabase<FactionDef>.GetNamed(Props.factionDefName, false));

            if (faction == null)
            {
                // Если нет — создаём временную или берём первую враждебную
                faction = Find.FactionManager.AllFactions.FirstOrDefault(f =>
                    !f.IsPlayer && !f.defeated && f.def.defName.Contains("Insect"));
            }

            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamed(Props.pawnKind, false);
            if (kindDef == null)
            {
                Log.Warning($"[Ocellus] PawnKindDef '{Props.pawnKind}' not found!");
                return;
            }

            // Спавним Ocellus из глазного гнезда (рядом с трупом)
            IntVec3 spawnCell = CellFinder.RandomClosewalkCellNear(
                Pawn.Corpse.Position, map, 2, c => c.Walkable(map));

            if (!spawnCell.IsValid)
            {
                spawnCell = Pawn.Corpse.Position;
            }

            Pawn ocellus = PawnGenerator.GeneratePawn(kindDef, faction);
            GenSpawn.Spawn(ocellus, spawnCell, map);

            // Эффект
            if (Props.spawnSound != null)
                Props.spawnSound.PlayOneShot(SoundInfo.InMap(ocellus));

            FleckMaker.Static(ocellus.Position, map, FleckDefOf.ExplosionFlash, 2f);

            // Письмо
            if (!string.IsNullOrEmpty(Props.letterLabel))
            {
                Find.LetterStack.ReceiveLetter(
                    Props.letterLabel,
                    string.Format(Props.letterText, Pawn.NameShortColored),
                    Props.letterDef ?? LetterDefOf.ThreatSmall,
                    ocellus
                );
            }

            Log.Message($"[Ocellus] Spawned from corpse of {Pawn.NameShortColored} at {spawnCell}");

            // Регистрируем в трекере для охоты
            var tracker = map.GetComponent<Incident.MapComponent_OcellusTracker>();
            if (tracker != null)
            {
                tracker.RegisterOcellus(ocellus);
            }
        }
    }
}
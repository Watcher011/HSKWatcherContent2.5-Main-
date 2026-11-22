using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace HSK.Watcher.Content.Headcrab
{
    // ============================================
    // RENDER NODE PROPERTIES: Хедкраб на голове
    // ============================================
    public class PawnRenderNodeProperties_Headcrab : PawnRenderNodeProperties
    {
        public PawnRenderNodeProperties_Headcrab()
        {
            workerClass = typeof(PawnRenderNodeWorker_Headcrab);
        }
    }

    // ============================================
    // RENDER NODE WORKER: Отрисовка хедкраба
    // ============================================
    public class PawnRenderNodeWorker_Headcrab : PawnRenderNodeWorker
    {
        private static readonly BodyPartGroupDef HeadGroupDef = DefDatabase<BodyPartGroupDef>.GetNamed("Head", false);
        private static readonly BodyPartGroupDef FullHeadGroupDef = DefDatabase<BodyPartGroupDef>.GetNamed("FullHead", false);
        private static readonly BodyPartGroupDef UpperHeadGroupDef = DefDatabase<BodyPartGroupDef>.GetNamed("UpperHead", false);

        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
                return false;

            if (parms.pawn.Dead)
                return false;

            Hediff headcrabHediff = parms.pawn.health.hediffSet.GetFirstHediffOfDef(
                DefDatabase<HediffDef>.GetNamed("HeadcrabParasite", false));
            if (headcrabHediff == null)
                return false;

            if (parms.pawn.apparel?.WornApparel != null)
            {
                foreach (var apparel in parms.pawn.apparel.WornApparel)
                {
                    if (CoversHead(apparel.def))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool CoversHead(ThingDef apparelDef)
        {
            if (apparelDef.apparel?.bodyPartGroups == null) return false;

            foreach (var group in apparelDef.apparel.bodyPartGroups)
            {
                if (group == HeadGroupDef || group == FullHeadGroupDef || group == UpperHeadGroupDef)
                    return true;
            }
            return false;
        }

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 baseOffset = base.OffsetFor(node, parms, out pivot);

            if (parms.pawn.style?.nextHairDef != null)
            {
                baseOffset.y += 0.05f;
            }

            if (parms.pawn.style?.beardDef != null)
            {
                baseOffset.y += 0.03f;
            }

            return baseOffset;
        }
    }

    // ============================================
    // HEDIFF COMP: Спавн Хедкраба при смерти
    // ============================================
    public class HediffCompProperties_SpawnHeadcrabOnDeath : HediffCompProperties
    {
        public string pawnKind = "Headcrab";
        public string factionDefName = "Insect";
        public SoundDef spawnSound;
        public string letterLabel;
        public string letterText;
        public LetterDef letterDef;

        public HediffCompProperties_SpawnHeadcrabOnDeath()
        {
            compClass = typeof(HediffComp_SpawnHeadcrabOnDeath);
        }
    }

    public class HediffComp_SpawnHeadcrabOnDeath : HediffComp
    {
        public HediffCompProperties_SpawnHeadcrabOnDeath Props => (HediffCompProperties_SpawnHeadcrabOnDeath)props;

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            if (Pawn == null || Pawn.Corpse == null || Pawn.Corpse.Destroyed) return;

            Map map = Pawn.Corpse.Map;
            if (map == null) return;

            Faction faction = Find.FactionManager.FirstFactionOfDef(
                DefDatabase<FactionDef>.GetNamed(Props.factionDefName, false));

            if (faction == null)
            {
                faction = Find.FactionManager.AllFactions.FirstOrDefault(f =>
                    !f.IsPlayer && !f.defeated && f.def.defName.Contains("Insect"));
            }

            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamed(Props.pawnKind, false);
            if (kindDef == null)
            {
                Log.Warning($"[Headcrab] PawnKindDef '{Props.pawnKind}' not found!");
                return;
            }

            IntVec3 spawnCell = CellFinder.RandomClosewalkCellNear(
                Pawn.Corpse.Position, map, 2, c => c.Walkable(map));

            if (!spawnCell.IsValid)
            {
                spawnCell = Pawn.Corpse.Position;
            }

            Pawn headcrab = PawnGenerator.GeneratePawn(kindDef, faction);
            GenSpawn.Spawn(headcrab, spawnCell, map);

            if (Props.spawnSound != null)
                Props.spawnSound.PlayOneShot(SoundInfo.InMap(headcrab));

            FleckMaker.Static(headcrab.Position, map, FleckDefOf.ExplosionFlash, 2f);

            if (!string.IsNullOrEmpty(Props.letterLabel))
            {
                Find.LetterStack.ReceiveLetter(
                    Props.letterLabel,
                    string.Format(Props.letterText, Pawn.NameShortColored),
                    Props.letterDef ?? LetterDefOf.ThreatSmall,
                    headcrab
                );
            }

            var tracker = map.GetComponent<MapComponent_HeadcrabTracker>();
            if (tracker != null)
            {
                tracker.RegisterHeadcrab(headcrab);
            }
        }
    }

    // ============================================
    // HEDIFF COMP: Запрет шлемов для заражённых
    // ============================================
    public class HediffCompProperties_PreventHeadgear : HediffCompProperties
    {
        public HediffCompProperties_PreventHeadgear()
        {
            compClass = typeof(HediffComp_PreventHeadgear);
        }
    }

    public class HediffComp_PreventHeadgear : HediffComp
    {
        private static readonly BodyPartGroupDef HeadGroupDef = DefDatabase<BodyPartGroupDef>.GetNamed("Head", false);
        private static readonly BodyPartGroupDef FullHeadGroupDef = DefDatabase<BodyPartGroupDef>.GetNamed("FullHead", false);
        private static readonly BodyPartGroupDef UpperHeadGroupDef = DefDatabase<BodyPartGroupDef>.GetNamed("UpperHead", false);

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (!Pawn.IsHashIntervalTick(60)) return;
            if (Pawn.apparel == null) return;

            List<Apparel> toRemove = new List<Apparel>();
            foreach (var apparel in Pawn.apparel.WornApparel)
            {
                if (CoversHead(apparel.def))
                {
                    toRemove.Add(apparel);
                }
            }

            foreach (var apparel in toRemove)
            {
                if (Pawn.apparel.TryDrop(apparel, out Apparel droppedApparel, Pawn.Position))
                {
                    Messages.Message(
                        $"{Pawn.NameShortColored} dropped {apparel.Label} — the Headcrab parasite prevents wearing headgear!",
                        Pawn, MessageTypeDefOf.NegativeEvent);
                }
            }
        }

        private bool CoversHead(ThingDef apparelDef)
        {
            if (apparelDef.apparel?.bodyPartGroups == null) return false;

            foreach (var group in apparelDef.apparel.bodyPartGroups)
            {
                if (group == HeadGroupDef || group == FullHeadGroupDef || group == UpperHeadGroupDef)
                    return true;
            }
            return false;
        }
    }

    // ============================================
    // HEDIFF COMP: Запрет оружия для заражённых
    // ============================================
    public class HediffCompProperties_PreventWeapons : HediffCompProperties
    {
        public HediffCompProperties_PreventWeapons()
        {
            compClass = typeof(HediffComp_PreventWeapons);
        }
    }

    public class HediffComp_PreventWeapons : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (!Pawn.IsHashIntervalTick(30)) return;

            if (Pawn.equipment?.Primary != null)
            {
                ThingWithComps weapon = Pawn.equipment.Primary;
                Pawn.equipment.TryDropEquipment(weapon, out ThingWithComps droppedWeapon, Pawn.Position);
                if (droppedWeapon != null)
                {
                    Messages.Message(
                        $"{Pawn.NameShortColored} dropped {weapon.Label} — the Headcrab parasite controls only melee instincts!",
                        Pawn, MessageTypeDefOf.NegativeEvent);
                }
            }

            if (Pawn.CurJobDef == JobDefOf.Equip)
            {
                Pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }
    }

    // ============================================
    // INCIDENT: Спавн Хедкраба
    // ============================================
    public class IncidentWorker_HeadcrabAttack : IncidentWorker
    {
        public static readonly string HeadcrabParasiteHediffDefName = "HeadcrabParasite";
        public static readonly string HeadcrabPawnKindDefName = "Headcrab";
        public static readonly string InsectFactionDefName = "Insect";

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            return map.mapPawns.AllPawnsSpawned.Any(p => p.RaceProps.Humanlike && p.Spawned && !p.Dead);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;

            IntVec3 spawnCell = FindFarthestSpawnCell(map);
            if (!spawnCell.IsValid)
            {
                Log.Warning("[Headcrab] Could not find valid spawn cell!");
                return false;
            }

            Faction insectFaction = Find.FactionManager.FirstFactionOfDef(
                FactionDef.Named(InsectFactionDefName));
            if (insectFaction == null)
            {
                Log.Warning("[Headcrab] Insect faction not found!");
                return false;
            }

            PawnKindDef headcrabKind = DefDatabase<PawnKindDef>.GetNamed(HeadcrabPawnKindDefName, false);
            if (headcrabKind == null)
            {
                Log.Warning($"[Headcrab] PawnKindDef '{HeadcrabPawnKindDefName}' not found!");
                return false;
            }

            Pawn headcrab = PawnGenerator.GeneratePawn(headcrabKind, insectFaction);
            GenSpawn.Spawn(headcrab, spawnCell, map);

            MapComponent_HeadcrabTracker tracker = map.GetComponent<MapComponent_HeadcrabTracker>();
            if (tracker == null)
            {
                tracker = new MapComponent_HeadcrabTracker(map);
                map.components.Add(tracker);
            }
            tracker.RegisterHeadcrab(headcrab);

            SendStandardLetter(
                "LetterLabelHeadcrabAttack".Translate(),
                "LetterHeadcrabAttack".Translate(),
                LetterDefOf.ThreatSmall,
                parms,
                headcrab
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

            List<Pawn> humanoids = map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Humanlike && p.Spawned).ToList();

            if (humanoids.Count == 0)
                return edgeCells[Rand.Range(0, edgeCells.Count)];

            IntVec3 bestCell = IntVec3.Invalid;
            float bestDist = -1f;

            for (int i = 0; i < edgeCells.Count; i++)
            {
                float minDist = float.MaxValue;
                for (int j = 0; j < humanoids.Count; j++)
                {
                    float d = edgeCells[i].DistanceToSquared(humanoids[j].Position);
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
    // MAP COMPONENT: Полная логика Хедкраба
    // ============================================
    public class MapComponent_HeadcrabTracker : MapComponent
    {
        private List<HeadcrabData> headcrabList = new List<HeadcrabData>();
        private List<Pawn> infectedHumanoids = new List<Pawn>();

        public static readonly string HeadcrabDefName = "Headcrab";

        private const int TargetReevaluationTicks = 180;
        private const int AttackTimeout = 75000;

        private const float LeapWarmupSeconds = 2.5f;
        private const float LeapWarmupTicks = 150f;
        private const float HeadgearProtectionChance = 0.99f;

        // === NEW: Параметры побега ===
        private const int FleeSearchRadius = 50;
        private const int FleeDurationTicks = 300; // 5 секунд бегства
        private const int MaxFailedAttempts = 3; // Макс неудачных попыток заражения

        private FleckDef _cachedHeartbeatDef;
        private FleckDef HeartbeatDef
        {
            get
            {
                if (_cachedHeartbeatDef == null)
                {
                    _cachedHeartbeatDef = DefDatabase<FleckDef>.GetNamed("Heartbeat", false);
                    if (_cachedHeartbeatDef == null)
                        _cachedHeartbeatDef = FleckDefOf.ExplosionFlash;
                }
                return _cachedHeartbeatDef;
            }
        }

        public MapComponent_HeadcrabTracker(Map map) : base(map) { }

        public void RegisterHeadcrab(Pawn headcrab)
        {
            if (headcrab == null || headcrab.def.defName != HeadcrabDefName) return;
            if (headcrabList.Any(h => h.Headcrab == headcrab)) return;

            headcrabList.Add(new HeadcrabData
            {
                Headcrab = headcrab,
                Target = null,
                AttackStartTick = Find.TickManager.TicksGame,
                LastPosition = headcrab.Position,
                StuckTicks = 0,
                LastJobTick = 0,
                LastTargetReevalTick = 0,
                IsLeaping = false,
                LeapStartTick = 0,
                LeapTarget = null,
                FailedAttempts = 0,
                IsFleeing = false,
                FleeEndTick = 0
            });
        }

        private void AutoRegisterWildHeadcrabs()
        {
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.def.defName == HeadcrabDefName && !headcrabList.Any(h => h.Headcrab == pawn))
                {
                    RegisterHeadcrab(pawn);
                }
            }
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                AutoRegisterWildHeadcrabs();
            }

            if (Find.TickManager.TicksGame % 30 != 0) return;

            int currentTick = Find.TickManager.TicksGame;

            for (int i = headcrabList.Count - 1; i >= 0; i--)
            {
                var data = headcrabList[i];

                if (data.Headcrab == null || data.Headcrab.Destroyed || data.Headcrab.Dead)
                {
                    headcrabList.RemoveAt(i);
                    continue;
                }

                // === NEW: Проверяем, закончилось ли бегство ===
                if (data.IsFleeing)
                {
                    if (currentTick >= data.FleeEndTick)
                    {
                        data.IsFleeing = false;
                        data.Target = null; // Сброс цели после бегства
                    }
                    else
                    {
                        // Продолжаем бежать
                        continue;
                    }
                }

                if (data.Headcrab.Position == data.LastPosition)
                {
                    data.StuckTicks += 30;
                }
                else
                {
                    data.StuckTicks = 0;
                    data.LastPosition = data.Headcrab.Position;
                }

                bool needNewTarget = data.Target == null ||
                                     data.Target.Dead ||
                                     !data.Target.Spawned ||
                                     (currentTick - data.LastTargetReevalTick > TargetReevaluationTicks);

                if (needNewTarget)
                {
                    Pawn newTarget = FindNearestHumanoid(data.Headcrab);

                    if (newTarget != null && newTarget != data.Target)
                    {
                        data.Target = newTarget;
                        data.LastTargetReevalTick = currentTick;
                        data.LastJobTick = 0;
                        data.IsLeaping = false;
                        data.LeapTarget = null;
                    }
                    else if (newTarget == null && data.Target != null)
                    {
                        newTarget = FindNearestAccessibleHumanoid(data.Headcrab);
                        if (newTarget != null)
                        {
                            data.Target = newTarget;
                            data.LastTargetReevalTick = currentTick;
                            data.LastJobTick = 0;
                            data.IsLeaping = false;
                            data.LeapTarget = null;
                        }
                    }
                }

                if (data.Target == null)
                {
                    DisappearHeadcrab(data.Headcrab);
                    headcrabList.RemoveAt(i);
                    continue;
                }

                float dist = data.Headcrab.Position.DistanceTo(data.Target.Position);

                if (dist <= 1.5f)
                {
                    if (!data.IsLeaping)
                    {
                        data.IsLeaping = true;
                        data.LeapStartTick = currentTick;
                        data.LeapTarget = data.Target;

                        FleckMaker.Static(data.Headcrab.Position, data.Headcrab.Map, HeartbeatDef, 1.5f);

                        Messages.Message(
                            "HeadcrabLeapWarning".Translate(data.Headcrab.LabelShort, data.Target.NameShortColored),
                            data.Target, MessageTypeDefOf.ThreatSmall);

                        continue;
                    }

                    float leapProgress = (currentTick - data.LeapStartTick) / LeapWarmupTicks;

                    if (leapProgress >= 1.0f)
                    {
                        TryAttachAndInfect(data, i);
                        continue;
                    }
                    else
                    {
                        if ((currentTick - data.LeapStartTick) % 30 == 0)
                        {
                            FleckMaker.Static(data.Headcrab.Position, data.Headcrab.Map, HeartbeatDef, 1.0f + leapProgress);
                        }
                        continue;
                    }
                }
                else
                {
                    if (data.IsLeaping)
                    {
                        data.IsLeaping = false;
                        data.LeapTarget = null;
                    }
                }

                if (currentTick - data.LastJobTick > 60)
                {
                    data.LastJobTick = currentTick;
                    ForceMoveToTarget(data.Headcrab, data.Target);
                }

                if (currentTick - data.AttackStartTick > AttackTimeout)
                {
                    DisappearHeadcrab(data.Headcrab);
                    headcrabList.RemoveAt(i);
                }
            }

            UpdateInfectedHumanoids();
        }

        // === NEW: Перегруженный метод с индексом для возможности удаления ===
        private void TryAttachAndInfect(HeadcrabData data, int listIndex)
        {
            Pawn headcrab = data.Headcrab;
            Pawn target = data.Target;

            if (headcrab == null || target == null || target.Dead || !target.Spawned)
            {
                DisappearHeadcrab(headcrab);
                headcrabList.RemoveAt(listIndex);
                return;
            }

            if (HasHeadgear(target))
            {
                float roll = Rand.Value;
                if (roll < HeadgearProtectionChance)
                {
                    // Заблокировано шлемом
                    Messages.Message(
                        "HeadcrabBlockedByHelmet".Translate(target.NameShortColored),
                        target, MessageTypeDefOf.PositiveEvent);

                    FleckMaker.Static(target.Position, target.Map, FleckDefOf.ExplosionFlash, 1f);

                    data.FailedAttempts++;

                    if (data.FailedAttempts >= MaxFailedAttempts)
                    {
                        // Слишком много неудач — убегаем искать новую цель
                        StartFleeing(data);
                    }
                    else
                    {
                        // Сбрасываем прыжок, ищем новую цель
                        data.IsLeaping = false;
                        data.LeapTarget = null;
                        data.Target = null; // Принудительно ищем новую
                    }
                    return;
                }
                else
                {
                    Messages.Message(
                        "HeadcrabBreaksHelmet".Translate(target.NameShortColored),
                        target, MessageTypeDefOf.NegativeEvent);
                }
            }

            // Успешное заражение
            AttachAndDisappear(headcrab, target);
            headcrabList.RemoveAt(listIndex);
        }

        // === NEW: Начать бегство ===
        private void StartFleeing(HeadcrabData data)
        {
            data.IsFleeing = true;
            data.FleeEndTick = Find.TickManager.TicksGame + FleeDurationTicks;
            data.FailedAttempts = 0;
            data.IsLeaping = false;
            data.LeapTarget = null;

            Pawn headcrab = data.Headcrab;
            Pawn currentTarget = data.Target;

            // Ищем точку для бегства — подальше от текущей цели
            IntVec3 fleeDestination = IntVec3.Invalid;

            if (currentTarget != null && currentTarget.Spawned && !currentTarget.Dead)
            {
                // Бежим в противоположном направлении от цели
                IntVec3 direction = headcrab.Position - currentTarget.Position;
                if (direction.x != 0 || direction.z != 0)
                {
                    // Нормализуем направление и умножаем на радиус поиска
                    float length = Mathf.Sqrt(direction.x * direction.x + direction.z * direction.z);
                    IntVec3 normalized = new IntVec3(
                        Mathf.RoundToInt(direction.x / length * FleeSearchRadius),
                        0,
                        Mathf.RoundToInt(direction.z / length * FleeSearchRadius)
                    );
                    fleeDestination = headcrab.Position + normalized;
                }
            }

            // Если не получилось — случайная точка в радиусе
            if (!fleeDestination.IsValid || !fleeDestination.InBounds(headcrab.Map))
            {
                fleeDestination = headcrab.Position + new IntVec3(
                    Rand.Range(-FleeSearchRadius, FleeSearchRadius),
                    0,
                    Rand.Range(-FleeSearchRadius, FleeSearchRadius)
                );
            }

            // Убеждаемся, что точка в пределах карты и проходима
            if (fleeDestination.InBounds(headcrab.Map))
            {
                fleeDestination = CellFinder.RandomClosewalkCellNear(
                    fleeDestination, headcrab.Map, 5, c => c.Walkable(headcrab.Map));
            }
            else
            {
                fleeDestination = CellFinder.RandomClosewalkCellNear(
                    headcrab.Position, headcrab.Map, FleeSearchRadius, c => c.Walkable(headcrab.Map));
            }

            if (fleeDestination.IsValid)
            {
                headcrab.jobs.ClearQueuedJobs();

                Job fleeJob = JobMaker.MakeJob(JobDefOf.Goto, fleeDestination);
                fleeJob.expiryInterval = FleeDurationTicks;
                fleeJob.checkOverrideOnExpire = false;
                fleeJob.locomotionUrgency = LocomotionUrgency.Sprint;
                fleeJob.canBashDoors = true;
                fleeJob.canBashFences = true;

                headcrab.jobs.StartJob(fleeJob, JobCondition.InterruptForced);

                Messages.Message(
                    "HeadcrabFleeing".Translate(headcrab.LabelShort),
                    headcrab, MessageTypeDefOf.NeutralEvent);
            }
        }

        private bool HasHeadgear(Pawn pawn)
        {
            if (pawn.apparel?.WornApparel == null) return false;

            foreach (var apparel in pawn.apparel.WornApparel)
            {
                if (apparel.def.apparel?.bodyPartGroups != null)
                {
                    foreach (var group in apparel.def.apparel.bodyPartGroups)
                    {
                        if (group.defName == "Head" ||
                            group.defName == "FullHead" ||
                            group.defName == "UpperHead" ||
                            group.defName == "Eyes")
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private void ForceMoveToTarget(Pawn headcrab, Pawn target)
        {
            if (headcrab == null || target == null) return;
            if (headcrab.Downed || !headcrab.Spawned) return;

            headcrab.jobs.ClearQueuedJobs();

            Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, target.Position);
            gotoJob.expiryInterval = 120;
            gotoJob.checkOverrideOnExpire = false;
            gotoJob.locomotionUrgency = LocomotionUrgency.Sprint;
            gotoJob.canBashDoors = true;
            gotoJob.canBashFences = true;
            gotoJob.ignoreDesignations = true;

            headcrab.jobs.StartJob(gotoJob, JobCondition.InterruptForced);
        }

        private Pawn FindNearestHumanoid(Pawn headcrab)
        {
            List<Pawn> allHumanoids = headcrab.Map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Humanlike && !p.Dead && p.Spawned && !p.Downed)
                .ToList();

            allHumanoids.RemoveAll(p => p.health.hediffSet.GetFirstHediffOfDef(
                DefDatabase<HediffDef>.GetNamed(IncidentWorker_HeadcrabAttack.HeadcrabParasiteHediffDefName, false)) != null);

            Pawn best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < allHumanoids.Count; i++)
            {
                Pawn h = allHumanoids[i];
                float d = headcrab.Position.DistanceToSquared(h.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = h;
                }
            }
            return best;
        }

        private Pawn FindNearestAccessibleHumanoid(Pawn headcrab)
        {
            List<Pawn> allHumanoids = headcrab.Map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Humanlike && !p.Dead && p.Spawned && !p.Downed)
                .ToList();

            allHumanoids.RemoveAll(p => p.health.hediffSet.GetFirstHediffOfDef(
                DefDatabase<HediffDef>.GetNamed(IncidentWorker_HeadcrabAttack.HeadcrabParasiteHediffDefName, false)) != null);

            Pawn best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < allHumanoids.Count; i++)
            {
                Pawn h = allHumanoids[i];

                if (!headcrab.CanReach(h, PathEndMode.Touch, Danger.Deadly, canBashDoors: true))
                    continue;

                float d = headcrab.Position.DistanceToSquared(h.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = h;
                }
            }
            return best;
        }

        private void AttachAndDisappear(Pawn headcrab, Pawn target)
        {
            HediffDef parasiteDef = DefDatabase<HediffDef>.GetNamed(
                IncidentWorker_HeadcrabAttack.HeadcrabParasiteHediffDefName, false);

            if (parasiteDef != null)
            {
                Hediff hediff = HediffMaker.MakeHediff(parasiteDef, target);
                hediff.Severity = 1.0f;

                BodyPartRecord head = target.health.hediffSet.GetBodyPartRecord(BodyPartDefOf.Head);
                if (head != null)
                    target.health.AddHediff(hediff, head);
                else
                    target.health.AddHediff(hediff);

                FleckMaker.Static(target.Position, target.Map, FleckDefOf.ExplosionFlash, 2f);
            }

            TurnHumanoidHostile(target);

            FleckMaker.Static(headcrab.Position, headcrab.Map, FleckDefOf.ExplosionFlash, 2f);
            headcrab.Destroy();
        }

        private void TurnHumanoidHostile(Pawn humanoid)
        {
            if (humanoid == null || humanoid.Dead) return;

            Faction insectFaction = Find.FactionManager.FirstFactionOfDef(
                FactionDef.Named(IncidentWorker_HeadcrabAttack.InsectFactionDefName));
            if (insectFaction == null) return;

            string humanoidName = humanoid.Name?.ToStringShort ?? humanoid.LabelShort;

            humanoid.jobs.EndCurrentJob(JobCondition.InterruptForced);
            humanoid.jobs.ClearQueuedJobs();
            humanoid.SetFaction(insectFaction);

            if (humanoid.equipment?.Primary != null)
            {
                humanoid.equipment.TryDropEquipment(humanoid.equipment.Primary, out ThingWithComps dropped, humanoid.Position);
            }

            if (humanoid.mindState != null)
            {
                humanoid.mindState.mentalStateHandler.TryStartMentalState(
                    MentalStateDefOf.Berserk,
                    "Headcrab parasite",
                    forceWake: true,
                    causedByMood: false);
            }

            FleckMaker.Static(humanoid.Position, humanoid.Map, FleckDefOf.ExplosionFlash, 3f);

            Find.LetterStack.ReceiveLetter(
                "HeadcrabInfectionLabel".Translate(humanoidName),
                "HeadcrabInfectionText".Translate(humanoidName),
                LetterDefOf.ThreatBig,
                humanoid);

            if (!infectedHumanoids.Contains(humanoid))
            {
                infectedHumanoids.Add(humanoid);
            }
        }

        private void UpdateInfectedHumanoids()
        {
            for (int i = infectedHumanoids.Count - 1; i >= 0; i--)
            {
                Pawn infected = infectedHumanoids[i];

                if (infected == null || infected.Dead || !infected.Spawned)
                {
                    infectedHumanoids.RemoveAt(i);
                    continue;
                }

                if (infected.Downed)
                {
                    continue;
                }

                Hediff hediff = infected.health.hediffSet.GetFirstHediffOfDef(
                    DefDatabase<HediffDef>.GetNamed(IncidentWorker_HeadcrabAttack.HeadcrabParasiteHediffDefName, false));

                if (hediff == null)
                {
                    infectedHumanoids.RemoveAt(i);
                    continue;
                }

                if (infected.mindState?.mentalStateHandler?.CurStateDef != MentalStateDefOf.Berserk)
                {
                    if (!infected.Downed && infected.Awake())
                    {
                        infected.mindState.mentalStateHandler.TryStartMentalState(
                            MentalStateDefOf.Berserk,
                            "Headcrab parasite",
                            causedByMood: false);
                    }
                }

                if (infected.CurJobDef == JobDefOf.Wait_Wander ||
                    infected.CurJobDef == JobDefOf.GotoWander ||
                    infected.CurJobDef == null ||
                    (infected.CurJobDef != JobDefOf.AttackMelee && infected.CurJobDef != JobDefOf.Goto))
                {
                    if (infected.equipment?.Primary != null)
                    {
                        infected.equipment.TryDropEquipment(infected.equipment.Primary, out ThingWithComps dropped, infected.Position);
                    }

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

        private void DisappearHeadcrab(Pawn headcrab)
        {
            FleckMaker.Static(headcrab.Position, headcrab.Map, FleckDefOf.ExplosionFlash, 2f);
            headcrab.Destroy();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref headcrabList, "headcrabList", LookMode.Deep);
            Scribe_Collections.Look(ref infectedHumanoids, "infectedHumanoids", LookMode.Reference);
        }
    }

    // ============================================
    // DATA: Данные хедкраба (обновлено)
    // ============================================
    public class HeadcrabData : IExposable
    {
        public Pawn Headcrab;
        public Pawn Target;
        public int AttackStartTick;
        public IntVec3 LastPosition;
        public int StuckTicks;
        public int LastJobTick;
        public int LastTargetReevalTick;

        public bool IsLeaping;
        public int LeapStartTick;
        public Pawn LeapTarget;

        // === NEW: Данные бегства ===
        public int FailedAttempts;
        public bool IsFleeing;
        public int FleeEndTick;

        public void ExposeData()
        {
            Scribe_References.Look(ref Headcrab, "headcrab");
            Scribe_References.Look(ref Target, "target");
            Scribe_Values.Look(ref AttackStartTick, "attackStartTick", 0);
            Scribe_Values.Look(ref LastPosition, "lastPosition");
            Scribe_Values.Look(ref StuckTicks, "stuckTicks", 0);
            Scribe_Values.Look(ref LastJobTick, "lastJobTick", 0);
            Scribe_Values.Look(ref LastTargetReevalTick, "lastTargetReevalTick", 0);

            Scribe_Values.Look(ref IsLeaping, "isLeaping", false);
            Scribe_Values.Look(ref LeapStartTick, "leapStartTick", 0);
            Scribe_References.Look(ref LeapTarget, "leapTarget");

            // === NEW ===
            Scribe_Values.Look(ref FailedAttempts, "failedAttempts", 0);
            Scribe_Values.Look(ref IsFleeing, "isFleeing", false);
            Scribe_Values.Look(ref FleeEndTick, "fleeEndTick", 0);
        }
    }
}
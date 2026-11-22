using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

/*namespace HSK.Watcher.Content.Incident
{
    public class IncidentWorker_WildHunt : IncidentWorker
    {
        private const int MinDays = 220;
        private const int AttackDuration = 2; // дня
        private const int MaxBarghestCount = 50; // Максимальное количество Barghest на карте

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms.target as Map ?? Find.CurrentMap;

            // Проверяем, что прошло достаточно дней
            if (GenDate.DaysPassed < MinDays)
                return false;

            // Создаем письмо с событием
            Find.LetterStack.ReceiveLetter("HSK_WildHunt_LetterLabel".Translate(),
                "HSK_WildHunt_LetterText".Translate(), LetterDefOf.NegativeEvent);

            // Запускаем атаку Barghest
            StartBarghestAttack(map, parms.points);

            // Запускаем таймер для завершения события через 2 дня
            StartTimerForCompletion(map);

            return true;
        }

        private void StartBarghestAttack(Map map, float points = -1f)
        {
            if (points <= 0f)
                points = CalculateRaidForce(map);

            // Ищем PawnKindDef для Barghest
            PawnKindDef barghestKindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail("Barghest");
            if (barghestKindDef == null)
                return;

            // Проверяем текущее количество Barghest
            int currentBarghestCount = GetCurrentBarghestCount(map);
            if (currentBarghestCount >= MaxBarghestCount)
                return;

            // Создаем кастомную группу только из Barghest, но не более лимита
            List<Pawn> barghests = new List<Pawn>();
            float pointsLeft = points;

            while (pointsLeft > barghestKindDef.combatPower && currentBarghestCount < MaxBarghestCount)
            {
                Pawn barghest = CreateBarghest(barghestKindDef, map);
                barghests.Add(barghest);
                pointsLeft -= barghestKindDef.combatPower;
                currentBarghestCount++;
            }

            // Спавним на краю карты
            foreach (Pawn barghest in barghests)
            {
                IntVec3 spawnCell = FindEdgeCell(map);
                if (spawnCell.IsValid)
                {
                    GenSpawn.Spawn(barghest, spawnCell, map);
                    ForceAttackNearestTarget(barghest, map);
                }
            }
        }

        private int GetCurrentBarghestCount(Map map)
        {
            return map.mapPawns.AllPawnsSpawned
                .Count(p => p.kindDef?.defName == "Barghest");
        }

        private Pawn CreateBarghest(PawnKindDef barghestKindDef, Map map)
        {
            Pawn barghest = PawnGenerator.GeneratePawn(barghestKindDef, null);
            barghest.Name = new NameSingle("Barghest");
            barghest.SetFactionDirect(null);
            
            // Делаем перманентным манхантером, чтобы атаковал всех
            barghest.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent);
            
            // Добавляем Scaria для агрессивного поведения
            barghest.health.AddHediff(HediffDefOf.Scaria);
            
            return barghest;
        }

        private void ForceAttackNearestTarget(Pawn barghest, Map map)
        {
            // Ищем ближайшее живое существо для атаки
            Pawn nearestTarget = (Pawn)GenClosest.ClosestThing_Global(barghest.Position, map.mapPawns.AllPawnsSpawned, 999f, 
                (Thing p) => 
                {
                    if (p == barghest || !(p is Pawn pawn))
                        return false;
                    
                    // Атакуем ВСЁ живое: людей, животных, нейтральных существ
                    // Исключаем только других Barghest и механикоидов
                    return pawn.kindDef?.defName != "Barghest" && 
                           !pawn.RaceProps.IsMechanoid &&
                           !pawn.Dead;
                });

            if (nearestTarget != null)
            {
                barghest.mindState.enemyTarget = nearestTarget;
            }
        }

        private IntVec3 FindEdgeCell(Map map)
        {
            List<IntVec3> edgeCells = new List<IntVec3>();

            foreach (IntVec3 cell in map.AllCells)
            {
                if (cell.OnEdge(map) && cell.Standable(map) && !cell.Fogged(map))
                {
                    edgeCells.Add(cell);
                }
            }

            if (edgeCells.Count > 0)
            {
                return edgeCells.RandomElement();
            }

            return CellFinder.RandomCell(map);
        }

        private float CalculateRaidForce(Map map)
        {
            return StorytellerUtility.DefaultThreatPointsNow(map) * 0.7f;
        }

        private void StartTimerForCompletion(Map map)
        {
            GameCondition_WildHunt condition = (GameCondition_WildHunt)GameConditionMaker.MakeCondition(
                DefDatabase<GameConditionDef>.GetNamed("HSK_GameCondition_WildHunt"),
                AttackDuration * 60000);

            map.gameConditionManager.RegisterCondition(condition);
        }
    }

    public class GameCondition_WildHunt : GameCondition
    {
        private int lastBarghestCheckTick = 0;
        private const int CheckInterval = 60; // Проверка каждые 60 тиков

        public override void GameConditionTick()
        {
            base.GameConditionTick();

            Map map = SingleMap;
            if (map == null) return;

            // Проверяем каждые 60 тиков
            if (Find.TickManager.TicksGame - lastBarghestCheckTick > CheckInterval)
            {
                lastBarghestCheckTick = Find.TickManager.TicksGame;
                CheckForKillsAndSpawnBarghests(map);
            }
        }

        private void CheckForKillsAndSpawnBarghests(Map map)
        {
            // Проверяем текущее количество Barghest
            int currentBarghestCount = GetCurrentBarghestCount(map);
            if (currentBarghestCount >= 50)
                return;

            // Ищем всех Barghest на карте
            List<Pawn> barghests = map.mapPawns.AllPawnsSpawned
                .Where(p => p.kindDef?.defName == "Barghest")
                .ToList();

            foreach (Pawn barghest in barghests)
            {
                if (barghest.Destroyed || !barghest.Spawned)
                    continue;

                // Проверяем трупы вокруг каждого Barghest
                CheckForKillsAroundBarghest(barghest, map, ref currentBarghestCount);

                // Если достигли лимита, выходим
                if (currentBarghestCount >= 50)
                    break;
            }
        }

        private void CheckForKillsAroundBarghest(Pawn barghest, Map map, ref int currentBarghestCount)
        {
            // Получаем все трупы в радиусе 5 клеток от Barghest
            List<Thing> corpses = GenRadial.RadialDistinctThingsAround(barghest.Position, map, 5f, true)
                .Where(t => t is Corpse corpse && 
                           corpse.InnerPawn != null && 
                           IsValidKill(corpse.InnerPawn) &&
                           IsFreshCorpse(corpse))
                .ToList();

            foreach (Corpse corpse in corpses)
            {
                // Проверяем лимит перед созданием нового Barghest
                if (currentBarghestCount >= 50)
                    break;

                // Создаем нового Barghest на месте трупа
                SpawnNewBarghest(corpse.Position, map);
                currentBarghestCount++;
                
                // Уничтожаем труп
                corpse.Destroy();
                
                // Выходим после создания одного Barghest за проверку
                break;
            }
        }

        private bool IsValidKill(Pawn killedPawn)
        {
            // Проверяем, что убито ЛЮБОЕ живое существо, кроме Barghest и механикоидов
            return killedPawn.kindDef?.defName != "Barghest" &&
                   !killedPawn.RaceProps.IsMechanoid &&
                   killedPawn.RaceProps.IsFlesh; // Только плоть и кровь
        }

        private bool IsFreshCorpse(Corpse corpse)
        {
            // Проверяем, что труп свежий (не разлагающийся)
            // Используем CompRottable для проверки свежести
            CompRottable rotComp = corpse.TryGetComp<CompRottable>();
            if (rotComp != null)
            {
                // Считаем труп свежим, если он еще не начал разлагаться
                return rotComp.Stage == RotStage.Fresh;
            }
            
            // Если нет компонёнта гниения, считаем труп всегда свежим
            return true;
        }

        private void SpawnNewBarghest(IntVec3 position, Map map)
        {
            PawnKindDef barghestKindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail("Barghest");
            if (barghestKindDef == null)
                return;

            // Создаем нового Barghest
            Pawn newBarghest = PawnGenerator.GeneratePawn(barghestKindDef, null);
            newBarghest.Name = new NameSingle("Barghest");
            newBarghest.SetFactionDirect(null);
            newBarghest.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent);
            newBarghest.health.AddHediff(HediffDefOf.Scaria);

            // Спавним на месте трупа
            GenSpawn.Spawn(newBarghest, position, map);
            
            // Настраиваем атаку на ближайшую цель
            ForceAttackNearestTarget(newBarghest, map);
        }

        private void ForceAttackNearestTarget(Pawn barghest, Map map)
        {
            // Ищем ближайшее живое существо для атаки
            Pawn nearestTarget = (Pawn)GenClosest.ClosestThing_Global(barghest.Position, map.mapPawns.AllPawnsSpawned, 999f, 
                (Thing p) => 
                {
                    if (p == barghest || !(p is Pawn pawn))
                        return false;
                    
                    // Атакуем ВСЁ живое, кроме других Barghest и механикоидов
                    return pawn.kindDef?.defName != "Barghest" && 
                           !pawn.RaceProps.IsMechanoid &&
                           !pawn.Dead;
                });

            if (nearestTarget != null)
            {
                barghest.mindState.enemyTarget = nearestTarget;
            }
        }

        private int GetCurrentBarghestCount(Map map)
        {
            return map.mapPawns.AllPawnsSpawned
                .Count(p => p.kindDef?.defName == "Barghest");
        }

        public override void End()
        {
            Map map = SingleMap;
            base.End();

            if (map != null)
            {
                RemoveBarghestsFromMap(map);

                Find.LetterStack.ReceiveLetter("HSK_WildHunt_End_LetterLabel".Translate(),
                    "HSK_WildHunt_End_LetterText".Translate(), LetterDefOf.NeutralEvent);
            }
        }

        private void RemoveBarghestsFromMap(Map map)
        {
            List<Pawn> barghests = map.mapPawns.AllPawnsSpawned
                .Where(p => p.kindDef?.defName == "Barghest")
                .ToList();

            foreach (Pawn barghest in barghests)
            {
                if (!barghest.Destroyed)
                    barghest.Destroy();
            }
        }
    }
}*/
namespace HSK.Watcher.Content.Incident
{
    public class IncidentWorker_WildHunt : IncidentWorker
    {
        private const int MinDays = 50;
        private const int AttackDuration = 2; // дня
        private const int MaxBarghestCount = 50; // Максимальное количество Barghest на карте

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms.target as Map ?? Find.CurrentMap;

            // Проверяем, что прошло достаточно дней
            if (GenDate.DaysPassed < MinDays)
                return false;

            // Создаем письмо с событием
            Find.LetterStack.ReceiveLetter("HSK_WildHunt_LetterLabel".Translate(),
                "HSK_WildHunt_LetterText".Translate(), LetterDefOf.NegativeEvent);

            // Запускаем атаку Barghest
            StartBarghestAttack(map, parms.points);

            // Запускаем таймер для завершения события через 2 дня
            StartTimerForCompletion(map);

            return true;
        }

        private void StartBarghestAttack(Map map, float points = -1f)
        {
            if (points <= 0f)
                points = CalculateRaidForce(map);

            // Ищем PawnKindDef для Barghest
            PawnKindDef barghestKindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail("Barghest");
            if (barghestKindDef == null)
                return;

            // Проверяем текущее количество Barghest
            int currentBarghestCount = GetCurrentBarghestCount(map);
            if (currentBarghestCount >= MaxBarghestCount)
                return;

            // Создаем кастомную группу только из Barghest, но не более лимита
            List<Pawn> barghests = new List<Pawn>();
            float pointsLeft = points;

            while (pointsLeft > barghestKindDef.combatPower && currentBarghestCount < MaxBarghestCount)
            {
                Pawn barghest = CreateBarghest(barghestKindDef, map);
                barghests.Add(barghest);
                pointsLeft -= barghestKindDef.combatPower;
                currentBarghestCount++;
            }

            // Спавним на краю карты
            foreach (Pawn barghest in barghests)
            {
                IntVec3 spawnCell = FindEdgeCell(map);
                if (spawnCell.IsValid)
                {
                    GenSpawn.Spawn(barghest, spawnCell, map);
                }
            }

            // После спавна всех Barghest заставляем их всех атаковать
            ForceAllBarghestsToHunt(map);
        }

        private void ForceAllBarghestsToHunt(Map map)
        {
            List<Pawn> allBarghests = map.mapPawns.AllPawnsSpawned
                .Where(p => p.kindDef?.defName == "Barghest" && p.Spawned && !p.Downed && !p.Dead)
                .ToList();

            // Находим ВСЕ живые цели на карте
            List<Pawn> allLivingTargets = map.mapPawns.AllPawnsSpawned
                .Where(p => p.kindDef?.defName != "Barghest" &&
                           !p.RaceProps.IsMechanoid &&
                           !p.Dead &&
                           p.Spawned)
                .ToList();

            if (allLivingTargets.Count == 0 || allBarghests.Count == 0)
                return;

            // ПРИОРИТЕТ 1: Добиваем всех раненых и лежачих
            List<Pawn> downedTargets = allLivingTargets.Where(p => p.Downed).ToList();
            List<Pawn> woundedTargets = allLivingTargets.Where(p => !p.Downed && p.health.summaryHealth.SummaryHealthPercent < 0.4f).ToList();

            // Направляем Barghest добивать раненых
            foreach (Pawn downedTarget in downedTargets)
            {
                var availableBarghests = allBarghests
                    .Where(b => b.mindState?.enemyTarget != downedTarget && IsBarghestAvailable(b))
                    .OrderBy(b => (b.Position - downedTarget.Position).LengthHorizontalSquared)
                    .Take(2) // 2 Barghest на одного лежачего
                    .ToList();

                foreach (Pawn barghest in availableBarghests)
                {
                    SetBarghestToFinishTarget(barghest, downedTarget);
                }
            }

            // ПРИОРИТЕТ 2: Добиваем тяжелораненых
            foreach (Pawn woundedTarget in woundedTargets)
            {
                var availableBarghests = allBarghests
                    .Where(b => b.mindState?.enemyTarget != woundedTarget && IsBarghestAvailable(b))
                    .OrderBy(b => (b.Position - woundedTarget.Position).LengthHorizontalSquared)
                    .Take(1) // 1 Barghest на тяжелораненого
                    .ToList();

                foreach (Pawn barghest in availableBarghests)
                {
                    SetBarghestToFinishTarget(barghest, woundedTarget);
                }
            }

            // ПРИОРИТЕТ 3: Оставшиеся Barghest атакуют всех остальных живых
            var remainingBarghests = allBarghests.Where(b => IsBarghestAvailable(b)).ToList();
            var remainingTargets = allLivingTargets.Where(p => !p.Downed && p.health.summaryHealth.SummaryHealthPercent >= 0.4f).ToList();

            foreach (Pawn barghest in remainingBarghests)
            {
                if (remainingTargets.Count > 0)
                {
                    // Находим ближайшую цель
                    Pawn closestTarget = (Pawn)GenClosest.ClosestThing_Global(
                        barghest.Position, remainingTargets, 999f, null, null);

                    if (closestTarget != null)
                    {
                        SetBarghestToAttackTarget(barghest, closestTarget);
                    }
                }
            }
        }

        private bool IsBarghestAvailable(Pawn barghest)
        {
            if (barghest.mindState?.enemyTarget == null)
                return true;

            // Приводим enemyTarget к Pawn для проверки свойств
            Pawn targetPawn = barghest.mindState.enemyTarget as Pawn;
            return targetPawn == null || targetPawn.Destroyed || targetPawn.Dead;
        }

        private void SetBarghestToFinishTarget(Pawn barghest, Pawn target)
        {
            if (barghest.mindState != null)
            {
                barghest.mindState.enemyTarget = target;
                StartFinishingJob(barghest, target);
            }
        }

        private void SetBarghestToAttackTarget(Pawn barghest, Pawn target)
        {
            if (barghest.mindState != null)
            {
                barghest.mindState.enemyTarget = target;
                StartHuntingJob(barghest, target);
            }
        }

        private void StartFinishingJob(Pawn barghest, Pawn target)
        {
            // Создаем работу для добивания с высоким приоритетом
            Verse.AI.Job finishJob = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            finishJob.killIncappedTarget = true;
            finishJob.checkOverrideOnExpire = true;
            finishJob.expiryInterval = 120; // Очень короткий интервал для добивания

            barghest.jobs.StartJob(finishJob, JobCondition.InterruptForced, null, false, true, null, null, false);
        }

        private void StartHuntingJob(Pawn barghest, Pawn target)
        {
            // Создаем работу для охоты на здоровые цели
            Verse.AI.Job huntJob = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            huntJob.killIncappedTarget = true;
            huntJob.checkOverrideOnExpire = true;
            huntJob.expiryInterval = 200;

            barghest.jobs.StartJob(huntJob, JobCondition.InterruptForced, null, false, true, null, null, false);
        }

        private int GetCurrentBarghestCount(Map map)
        {
            return map.mapPawns.AllPawnsSpawned
                .Count(p => p.kindDef?.defName == "Barghest");
        }

        private Pawn CreateBarghest(PawnKindDef barghestKindDef, Map map)
        {
            Pawn barghest = PawnGenerator.GeneratePawn(barghestKindDef, null);
            barghest.Name = new NameSingle("Barghest");
            barghest.SetFactionDirect(null);

            // Делаем перманентным манхантером, чтобы атаковал всех
            barghest.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent);

            // Добавляем Scaria для агрессивного поведения
            barghest.health.AddHediff(HediffDefOf.Scaria);

            return barghest;
        }

        private IntVec3 FindEdgeCell(Map map)
        {
            List<IntVec3> edgeCells = new List<IntVec3>();

            foreach (IntVec3 cell in map.AllCells)
            {
                if (cell.OnEdge(map) && cell.Standable(map) && !cell.Fogged(map))
                {
                    edgeCells.Add(cell);
                }
            }

            if (edgeCells.Count > 0)
            {
                return edgeCells.RandomElement();
            }

            return CellFinder.RandomCell(map);
        }

        private float CalculateRaidForce(Map map)
        {
            return StorytellerUtility.DefaultThreatPointsNow(map) * 0.7f;
        }

        private void StartTimerForCompletion(Map map)
        {
            GameCondition_WildHunt condition = (GameCondition_WildHunt)GameConditionMaker.MakeCondition(
                DefDatabase<GameConditionDef>.GetNamed("HSK_GameCondition_WildHunt"),
                AttackDuration * 60000);

            map.gameConditionManager.RegisterCondition(condition);
        }
    }

    public class GameCondition_WildHunt : GameCondition
    {
        private int lastBarghestCheckTick = 0;
        private const int CheckInterval = 30; // Проверка каждые 30 тиков для быстрой реакции

        public override void GameConditionTick()
        {
            base.GameConditionTick();

            Map map = SingleMap;
            if (map == null) return;

            // Проверяем каждые 30 тиков для мгновенной реакции на раненых
            if (Find.TickManager.TicksGame - lastBarghestCheckTick > CheckInterval)
            {
                lastBarghestCheckTick = Find.TickManager.TicksGame;

                // Проверяем убийства и спавним новых Barghest
                CheckForKillsAndSpawnBarghests(map);

                // Постоянно перераспределяем Barghest для добивания раненых
                ForceBarghestsToFinishWounded(map);
            }
        }

        private void ForceBarghestsToFinishWounded(Map map)
        {
            List<Pawn> allBarghests = map.mapPawns.AllPawnsSpawned
                .Where(p => p.kindDef?.defName == "Barghest" && p.Spawned && !p.Downed && !p.Dead)
                .ToList();

            // Находим ВСЕХ раненых на карте (лежачих и тяжелораненых)
            List<Pawn> allWounded = map.mapPawns.AllPawnsSpawned
                .Where(p => p.kindDef?.defName != "Barghest" &&
                           !p.RaceProps.IsMechanoid &&
                           !p.Dead &&
                           p.Spawned &&
                           (p.Downed || p.health.summaryHealth.SummaryHealthPercent < 0.5f)) // Все с менее чем 50% здоровья
                .ToList();

            // Немедленно направляем Barghest добивать раненых
            foreach (Pawn wounded in allWounded)
            {
                // Находим ближайших доступных Barghest
                var closestBarghests = allBarghests
                    .Where(b => IsBarghestAvailableForFinish(b, wounded))
                    .OrderBy(b => (b.Position - wounded.Position).LengthHorizontalSquared)
                    .Take(wounded.Downed ? 3 : 2) // 3 на лежачего, 2 на тяжелораненого
                    .ToList();

                foreach (Pawn barghest in closestBarghests)
                {
                    SetBarghestToFinishTarget(barghest, wounded);
                }
            }

            // Оставшихся Barghest направляем на охоту за здоровыми целями
            var remainingBarghests = allBarghests.Where(b => IsBarghestAvailable(b)).ToList();
            var healthyTargets = map.mapPawns.AllPawnsSpawned
                .Where(p => p.kindDef?.defName != "Barghest" &&
                           !p.RaceProps.IsMechanoid &&
                           !p.Dead &&
                           p.Spawned &&
                           !p.Downed &&
                           p.health.summaryHealth.SummaryHealthPercent >= 0.5f)
                .ToList();

            foreach (Pawn barghest in remainingBarghests)
            {
                if (healthyTargets.Count > 0)
                {
                    // Находим ближайшую здоровую цель
                    Pawn closestTarget = (Pawn)GenClosest.ClosestThing_Global(
                        barghest.Position, healthyTargets, 999f, null, null);

                    if (closestTarget != null)
                    {
                        SetBarghestToAttackTarget(barghest, closestTarget);
                    }
                }
            }
        }

        private bool IsBarghestAvailableForFinish(Pawn barghest, Pawn woundedTarget)
        {
            if (barghest.mindState?.enemyTarget == null)
                return true;

            if (barghest.mindState.enemyTarget == woundedTarget)
                return false;

            // Если Barghest уже атакует кого-то, но этот кто-то не ранен - переключаем на раненого
            Pawn currentTarget = barghest.mindState.enemyTarget as Pawn;
            if (currentTarget != null && !currentTarget.Downed &&
                currentTarget.health.summaryHealth.SummaryHealthPercent > 0.5f)
                return true;

            return false;
        }

        private bool IsBarghestAvailable(Pawn barghest)
        {
            if (barghest.mindState?.enemyTarget == null)
                return true;

            // Приводим enemyTarget к Pawn для проверки свойств
            Pawn currentTarget = barghest.mindState.enemyTarget as Pawn;
            return currentTarget == null || currentTarget.Destroyed || currentTarget.Dead;
        }

        private void SetBarghestToFinishTarget(Pawn barghest, Pawn target)
        {
            if (barghest.mindState != null)
            {
                barghest.mindState.enemyTarget = target;
                StartFinishingJob(barghest, target);
            }
        }

        private void SetBarghestToAttackTarget(Pawn barghest, Pawn target)
        {
            if (barghest.mindState != null)
            {
                barghest.mindState.enemyTarget = target;
                StartHuntingJob(barghest, target);
            }
        }

        private void StartFinishingJob(Pawn barghest, Pawn target)
        {
            Verse.AI.Job finishJob = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            finishJob.killIncappedTarget = true;
            finishJob.checkOverrideOnExpire = true;
            finishJob.expiryInterval = 60; // Очень быстрая реакция

            barghest.jobs.StartJob(finishJob, JobCondition.InterruptForced, null, false, true, null, null, false);
        }

        private void StartHuntingJob(Pawn barghest, Pawn target)
        {
            Verse.AI.Job huntJob = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            huntJob.killIncappedTarget = true;
            huntJob.checkOverrideOnExpire = true;
            huntJob.expiryInterval = 150;

            barghest.jobs.StartJob(huntJob, JobCondition.InterruptForced, null, false, true, null, null, false);
        }

        private void CheckForKillsAndSpawnBarghests(Map map)
        {
            int currentBarghestCount = GetCurrentBarghestCount(map);
            if (currentBarghestCount >= 50)
                return;

            List<Pawn> barghests = map.mapPawns.AllPawnsSpawned
                .Where(p => p.kindDef?.defName == "Barghest")
                .ToList();

            foreach (Pawn barghest in barghests)
            {
                if (barghest.Destroyed || !barghest.Spawned)
                    continue;

                CheckForKillsAroundBarghest(barghest, map, ref currentBarghestCount);

                if (currentBarghestCount >= 50)
                    break;
            }
        }

        private void CheckForKillsAroundBarghest(Pawn barghest, Map map, ref int currentBarghestCount)
        {
            List<Thing> corpses = GenRadial.RadialDistinctThingsAround(barghest.Position, map, 5f, true)
                .Where(t => t is Corpse corpse &&
                           corpse.InnerPawn != null &&
                           IsValidKill(corpse.InnerPawn) &&
                           IsFreshCorpse(corpse))
                .ToList();

            foreach (Corpse corpse in corpses)
            {
                if (currentBarghestCount >= 50)
                    break;

                SpawnNewBarghest(corpse.Position, map);
                currentBarghestCount++;
                corpse.Destroy();
                break;
            }
        }

        private bool IsValidKill(Pawn killedPawn)
        {
            return killedPawn.kindDef?.defName != "Barghest" &&
                   !killedPawn.RaceProps.IsMechanoid &&
                   killedPawn.RaceProps.IsFlesh;
        }

        private bool IsFreshCorpse(Corpse corpse)
        {
            CompRottable rotComp = corpse.TryGetComp<CompRottable>();
            if (rotComp != null)
            {
                return rotComp.Stage == RotStage.Fresh;
            }
            return true;
        }

        private void SpawnNewBarghest(IntVec3 position, Map map)
        {
            PawnKindDef barghestKindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail("Barghest");
            if (barghestKindDef == null)
                return;

            Pawn newBarghest = PawnGenerator.GeneratePawn(barghestKindDef, null);
            newBarghest.Name = new NameSingle("Barghest");
            newBarghest.SetFactionDirect(null);
            newBarghest.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent);
            newBarghest.health.AddHediff(HediffDefOf.Scaria);

            GenSpawn.Spawn(newBarghest, position, map);
        }

        private int GetCurrentBarghestCount(Map map)
        {
            return map.mapPawns.AllPawnsSpawned
                .Count(p => p.kindDef?.defName == "Barghest");
        }

        public override void End()
        {
            Map map = SingleMap;
            base.End();

            if (map != null)
            {
                // Удаляем всех живых Barghest с карты
                RemoveBarghestsFromMap(map);

                // Удаляем все трупы Barghest с карты
                RemoveBarghestCorpsesFromMap(map);

                Find.LetterStack.ReceiveLetter("HSK_WildHunt_End_LetterLabel".Translate(),
                    "HSK_WildHunt_End_LetterText".Translate(), LetterDefOf.NeutralEvent);
            }
        }

        private void RemoveBarghestsFromMap(Map map)
        {
            List<Pawn> barghests = map.mapPawns.AllPawnsSpawned
                .Where(p => p.kindDef?.defName == "Barghest")
                .ToList();

            foreach (Pawn barghest in barghests)
            {
                if (!barghest.Destroyed)
                    barghest.Destroy();
            }
        }

        private void RemoveBarghestCorpsesFromMap(Map map)
        {
            // Находим все трупы Barghest на карте
            List<Corpse> barghestCorpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse)
                .Where(t => t is Corpse corpse &&
                           corpse.InnerPawn != null &&
                           corpse.InnerPawn.kindDef?.defName == "Barghest")
                .Cast<Corpse>()
                .ToList();

            // Удаляем все трупы Barghest
            foreach (Corpse corpse in barghestCorpses)
            {
                if (!corpse.Destroyed)
                    corpse.Destroy();
            }

            // Также удаляем все останки Barghest (скелеты и т.д.)
            List<Thing> barghestRemains = map.listerThings.AllThings
                .Where(t => t.def.defName.Contains("Barghest") ||
                           (t is Corpse c && c.InnerPawn?.kindDef?.defName == "Barghest"))
                .ToList();

            foreach (Thing remain in barghestRemains)
            {
                if (!remain.Destroyed)
                    remain.Destroy();
            }
        }
    }
}/*------------------------*/
namespace HSK.Watcher.Content.Incident
{
    public class IncidentWorker_CursedSoul : IncidentWorker
    {
        private const int MinDaysForCurse = 60;
        private const int CurseDurationDays = 5;
        private const int MaxCursedBarghestCount = 35;

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms.target as Map ?? Find.CurrentMap;

            if (GenDate.DaysPassed < MinDaysForCurse)
                return false;

            Pawn cursedPawn = FindRandomColonistForCurse(map);
            if (cursedPawn == null)
                return false;

            Find.LetterStack.ReceiveLetter("HSK_CursedSoul_LetterLabel".Translate(),
                "HSK_CursedSoul_LetterText".Translate(cursedPawn.Name.ToStringShort),
                LetterDefOf.NegativeEvent, cursedPawn);

            AddCurseEffectsToPawn(cursedPawn);
            StartGameCondition(map, cursedPawn);

            return true;
        }

        private Pawn FindRandomColonistForCurse(Map map)
        {
            var eligibleColonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed &&
                           !p.Dead &&
                           p.Spawned &&
                           !p.RaceProps.IsMechanoid)
                .ToList();

            if (eligibleColonists.Count == 0)
                return null;

            return eligibleColonists.RandomElement();
        }

        private void AddCurseEffectsToPawn(Pawn pawn)
        {
            // Добавляем негативные мысли
            var badMemoryDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("BadMemory");
            if (badMemoryDef != null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(badMemoryDef);
            }

            // Добавляем метку добычи
            var preyMarkDef = DefDatabase<HediffDef>.GetNamedSilentFail("PreyMark");
            if (preyMarkDef != null && pawn.health.hediffSet.GetFirstHediffOfDef(preyMarkDef) == null)
            {
                var hediff = HediffMaker.MakeHediff(preyMarkDef, pawn);
                pawn.health.AddHediff(hediff);
            }
        }

        private void AddCurseVisuals(Pawn pawn)
        {
            // Простой визуальный эффект - создаем искры
            if (pawn.Spawned && pawn.Position.IsValid)
            {
                try
                {
                    // Пытаемся использовать существующий метод для эффектов
                    var moteDef = DefDatabase<ThingDef>.GetNamedSilentFail("Mote_Spark");
                    if (moteDef != null)
                    {
                        MoteMaker.MakeStaticMote(pawn.Position, pawn.Map, moteDef, 1.0f);
                    }
                }
                catch
                {
                    // Игнорируем ошибки визуальных эффектов
                }
            }
        }

        private void RemoveCurseEffectsFromPawn(Pawn pawn)
        {
            // Убираем мысли
            var badMemoryDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("BadMemory");
            if (badMemoryDef != null)
            {
                var badMemory = pawn.needs.mood.thoughts.memories.Memories
                    .FirstOrDefault(m => m.def == badMemoryDef);
                if (badMemory != null)
                {
                    pawn.needs.mood.thoughts.memories.RemoveMemory(badMemory);
                }
            }

            // Убираем метку добычи
            var preyMarkDef = DefDatabase<HediffDef>.GetNamedSilentFail("PreyMark");
            if (preyMarkDef != null)
            {
                var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(preyMarkDef);
                if (hediff != null)
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }
        }

        private void StartGameCondition(Map map, Pawn cursedPawn)
        {
            GameCondition_CursedSoul condition = (GameCondition_CursedSoul)GameConditionMaker.MakeCondition(
                DefDatabase<GameConditionDef>.GetNamed("HSK_GameCondition_CursedSoul"),
                CurseDurationDays * 60000);

            condition.Initialize(cursedPawn);
            map.gameConditionManager.RegisterCondition(condition);
        }
    }

    public class GameCondition_CursedSoul : GameCondition
    {
        private Pawn cursedPawn;
        private int curseEndTick;
        private int lastBarghestSpawnCheck = 0;
        private int lastCurseStateCheck = 0;
        private int lastKillCheckTick = 0;
        private bool curseEventCompleted = false;
        private const int CurseSpawnInterval = 250;
        private const int CurseStateCheckInterval = 250;
        private const int KillCheckInterval = 30;
        private const int MaxBarghestForCurse = 35;

        public void Initialize(Pawn pawn)
        {
            cursedPawn = pawn;
            curseEndTick = Find.TickManager.TicksGame + (20 * 60000); // 20 дней
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref cursedPawn, "cursedPawn");
            Scribe_Values.Look(ref curseEndTick, "curseEndTick");
            Scribe_Values.Look(ref lastBarghestSpawnCheck, "lastBarghestSpawnCheck");
            Scribe_Values.Look(ref lastCurseStateCheck, "lastCurseStateCheck");
            Scribe_Values.Look(ref lastKillCheckTick, "lastKillCheckTick");
            Scribe_Values.Look(ref curseEventCompleted, "curseEventCompleted");
        }

        public override string Label
        {
            get
            {
                if (cursedPawn != null && !cursedPawn.Destroyed && !cursedPawn.Dead)
                {
                    return "HSK_CursedSoul_GameConditionLabel".Translate(cursedPawn.Name.ToStringShort);
                }
                return "HSK_CursedSoul_GameConditionLabelDefault".Translate();
            }
        }

        public override string TooltipString
        {
            get
            {
                if (cursedPawn != null && !cursedPawn.Destroyed && !cursedPawn.Dead)
                {
                    int daysLeft = (curseEndTick - Find.TickManager.TicksGame) / 60000;
                    return "HSK_CursedSoul_GameConditionTooltip".Translate(cursedPawn.Name.ToStringShort, daysLeft);
                }
                return base.TooltipString;
            }
        }

        public override void GameConditionTick()
        {
            base.GameConditionTick();

            if (curseEventCompleted) return;

            Map map = SingleMap;
            if (map == null) return;

            int currentTick = Find.TickManager.TicksGame;

            if (currentTick - lastCurseStateCheck > CurseStateCheckInterval)
            {
                lastCurseStateCheck = currentTick;

                if (CheckForCurseCompletion(map))
                    return;
            }

            // Проверяем убийства каждые 30 тиков для быстрой реакции
            if (currentTick - lastKillCheckTick > KillCheckInterval)
            {
                lastKillCheckTick = currentTick;
                CheckForKillsAndSpawnBarghests(map);
            }

            if (currentTick - lastBarghestSpawnCheck > CurseSpawnInterval)
            {
                lastBarghestSpawnCheck = currentTick;

                if (ShouldSpawnCurseBarghests(map))
                {
                    SpawnCurseBarghestGroup(map);
                }
            }

            if (currentTick >= curseEndTick)
            {
                CompleteCurseEvent(true);
                return;
            }

            ForceCurseBarghestsToHunt(map);
        }

        private bool CheckForCurseCompletion(Map map)
        {
            if (cursedPawn == null || cursedPawn.Destroyed || cursedPawn.Dead)
            {
                CompleteCurseEvent(false);
                return true;
            }

            if (!cursedPawn.Spawned || cursedPawn.Map != map)
            {
                CompleteCurseEvent(false);
                return true;
            }

            return false;
        }

        private bool ShouldSpawnCurseBarghests(Map map)
        {
            int currentBarghestCount = GetCurrentCurseBarghestCount(map);
            return currentBarghestCount < MaxBarghestForCurse &&
                   currentBarghestCount < GetDesiredCurseBarghestCount();
        }

        private int GetDesiredCurseBarghestCount()
        {
            int daysPassed = (Find.TickManager.TicksGame - (curseEndTick - (20 * 60000))) / 60000;
            return Math.Min(10 + (daysPassed * 2), MaxBarghestForCurse);
        }

        private void SpawnCurseBarghestGroup(Map map)
        {
            int currentCount = GetCurrentCurseBarghestCount(map);
            int desiredCount = GetDesiredCurseBarghestCount();
            int toSpawn = Math.Min(desiredCount - currentCount, 3);

            if (toSpawn <= 0) return;

            PawnKindDef barghestKindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail("Barghest");
            if (barghestKindDef == null) return;

            List<Pawn> barghests = new List<Pawn>();

            for (int i = 0; i < toSpawn; i++)
            {
                Pawn barghest = CreateCurseBarghest(barghestKindDef, map);
                barghests.Add(barghest);
            }

            foreach (Pawn barghest in barghests)
            {
                IntVec3 spawnCell = FindCurseEdgeCell(map);
                if (spawnCell.IsValid)
                {
                    GenSpawn.Spawn(barghest, spawnCell, map);
                }
            }
        }

        private void CheckForKillsAndSpawnBarghests(Map map)
        {
            int currentBarghestCount = GetCurrentCurseBarghestCount(map);
            if (currentBarghestCount >= MaxBarghestForCurse)
                return;

            List<Pawn> barghests = map.mapPawns.AllPawnsSpawned
                .Where(p => p.kindDef?.defName == "Barghest")
                .ToList();

            foreach (Pawn barghest in barghests)
            {
                if (barghest.Destroyed || !barghest.Spawned)
                    continue;

                CheckForKillsAroundBarghest(barghest, map, ref currentBarghestCount);

                if (currentBarghestCount >= MaxBarghestForCurse)
                    break;
            }
        }

        private void CheckForKillsAroundBarghest(Pawn barghest, Map map, ref int currentBarghestCount)
        {
            // Ищем свежие трупы вокруг баргеста
            List<Thing> corpses = GenRadial.RadialDistinctThingsAround(barghest.Position, map, 5f, true)
                .Where(t => t is Corpse corpse &&
                           corpse.InnerPawn != null &&
                           IsValidKill(corpse.InnerPawn) &&
                           IsFreshCorpse(corpse))
                .ToList();

            foreach (Corpse corpse in corpses)
            {
                if (currentBarghestCount >= MaxBarghestForCurse)
                    break;

                SpawnNewBarghestFromKill(corpse.Position, map);
                currentBarghestCount++;
                corpse.Destroy();
                break;
            }
        }

        private bool IsValidKill(Pawn killedPawn)
        {
            return killedPawn.kindDef?.defName != "Barghest" &&
                   !killedPawn.RaceProps.IsMechanoid &&
                   killedPawn.RaceProps.IsFlesh;
        }

        private bool IsFreshCorpse(Corpse corpse)
        {
            CompRottable rotComp = corpse.TryGetComp<CompRottable>();
            if (rotComp != null)
            {
                return rotComp.Stage == RotStage.Fresh;
            }
            return true;
        }

        private void SpawnNewBarghestFromKill(IntVec3 position, Map map)
        {
            PawnKindDef barghestKindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail("Barghest");
            if (barghestKindDef == null)
                return;

            Pawn newBarghest = PawnGenerator.GeneratePawn(barghestKindDef, null);
            newBarghest.SetFactionDirect(null);
            newBarghest.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent);
            newBarghest.health.AddHediff(HediffDefOf.Scaria);

            GenSpawn.Spawn(newBarghest, position, map);

            try
            {
                var moteDef = DefDatabase<ThingDef>.GetNamedSilentFail("Mote_Explosion");
                if (moteDef != null)
                {
                    MoteMaker.MakeStaticMote(position, map, moteDef, 1.0f);
                }
            }
            catch
            {
                // Игнорируем ошибки визуальных эффектов
            }
        }

        private void ForceCurseBarghestsToHunt(Map map)
        {
            List<Pawn> allBarghests = map.mapPawns.AllPawnsSpawned
                .Where(p => p.kindDef?.defName == "Barghest" && p.Spawned && !p.Downed && !p.Dead)
                .ToList();

            if (allBarghests.Count == 0) return;

            List<Pawn> allLivingTargets = map.mapPawns.AllPawnsSpawned
                .Where(p => p.kindDef?.defName != "Barghest" &&
                           !p.RaceProps.IsMechanoid &&
                           !p.Dead &&
                           p.Spawned)
                .ToList();

            if (allLivingTargets.Count == 0 || allBarghests.Count == 0)
                return;

            // ПРИОРИТЕТ 1: Добиваем всех раненых и лежачих
            List<Pawn> downedTargets = allLivingTargets.Where(p => p.Downed).ToList();
            List<Pawn> woundedTargets = allLivingTargets.Where(p => !p.Downed && p.health.summaryHealth.SummaryHealthPercent < 0.4f).ToList();

            foreach (Pawn downedTarget in downedTargets)
            {
                var availableBarghests = allBarghests
                    .Where(b => b.mindState?.enemyTarget != downedTarget && IsCurseBarghestAvailable(b))
                    .OrderBy(b => (b.Position - downedTarget.Position).LengthHorizontalSquared)
                    .Take(2)
                    .ToList();

                foreach (Pawn barghest in availableBarghests)
                {
                    SetCurseBarghestToFinishTarget(barghest, downedTarget);
                }
            }

            foreach (Pawn woundedTarget in woundedTargets)
            {
                var availableBarghests = allBarghests
                    .Where(b => b.mindState?.enemyTarget != woundedTarget && IsCurseBarghestAvailable(b))
                    .OrderBy(b => (b.Position - woundedTarget.Position).LengthHorizontalSquared)
                    .Take(1)
                    .ToList();

                foreach (Pawn barghest in availableBarghests)
                {
                    SetCurseBarghestToFinishTarget(barghest, woundedTarget);
                }
            }

            // ПРИОРИТЕТ 3: Проклятая пешка (особый приоритет)
            if (cursedPawn != null && cursedPawn.Spawned && !cursedPawn.Dead)
            {
                var availableForCurse = allBarghests
                    .Where(b => IsCurseBarghestAvailable(b) ||
                               (b.mindState?.enemyTarget != cursedPawn && cursedPawn.Spawned))
                    .OrderBy(b => (b.Position - cursedPawn.Position).LengthHorizontalSquared)
                    .Take(3)
                    .ToList();

                foreach (Pawn barghest in availableForCurse)
                {
                    SetCurseBarghestToAttackTarget(barghest, cursedPawn);
                }
            }

            var remainingBarghests = allBarghests.Where(b => IsCurseBarghestAvailable(b)).ToList();
            var remainingTargets = allLivingTargets.Where(p => !p.Downed && p.health.summaryHealth.SummaryHealthPercent >= 0.4f).ToList();

            foreach (Pawn barghest in remainingBarghests)
            {
                if (remainingTargets.Count > 0)
                {
                    Pawn closestTarget = (Pawn)GenClosest.ClosestThing_Global(
                        barghest.Position, remainingTargets, 999f, null, null);

                    if (closestTarget != null)
                    {
                        SetCurseBarghestToAttackTarget(barghest, closestTarget);
                    }
                }
            }
        }

        private bool IsCurseBarghestAvailable(Pawn barghest)
        {
            if (barghest.mindState?.enemyTarget == null)
                return true;

            Pawn targetPawn = barghest.mindState.enemyTarget as Pawn;
            return targetPawn == null || targetPawn.Destroyed || targetPawn.Dead;
        }

        private void SetCurseBarghestToFinishTarget(Pawn barghest, Pawn target)
        {
            if (barghest.mindState != null)
            {
                barghest.mindState.enemyTarget = target;
                StartCurseFinishingJob(barghest, target);
            }
        }

        private void SetCurseBarghestToAttackTarget(Pawn barghest, Pawn target)
        {
            if (barghest.mindState != null)
            {
                barghest.mindState.enemyTarget = target;
                StartCurseHuntingJob(barghest, target);
            }
        }

        private void StartCurseFinishingJob(Pawn barghest, Pawn target)
        {
            Job finishJob = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            finishJob.killIncappedTarget = true;
            finishJob.checkOverrideOnExpire = true;
            finishJob.expiryInterval = 120;

            barghest.jobs.StartJob(finishJob, JobCondition.InterruptForced, null, false, true, null, null, false);
        }

        private void StartCurseHuntingJob(Pawn barghest, Pawn target)
        {
            Job huntJob = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            huntJob.killIncappedTarget = true;
            huntJob.checkOverrideOnExpire = true;
            huntJob.expiryInterval = 200;

            barghest.jobs.StartJob(huntJob, JobCondition.InterruptForced, null, false, true, null, null, false);
        }

        private Pawn CreateCurseBarghest(PawnKindDef barghestKindDef, Map map)
        {
            Pawn barghest = PawnGenerator.GeneratePawn(barghestKindDef, null);
            barghest.SetFactionDirect(null);
            barghest.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent);
            barghest.health.AddHediff(HediffDefOf.Scaria);

            return barghest;
        }

        private IntVec3 FindCurseEdgeCell(Map map)
        {
            return CellFinder.RandomEdgeCell(map);
        }

        private int GetCurrentCurseBarghestCount(Map map)
        {
            return map.mapPawns.AllPawnsSpawned
                .Count(p => p.kindDef?.defName == "Barghest" && !p.Dead);
        }

        private void CompleteCurseEvent(bool survived)
        {
            if (curseEventCompleted) return;
            curseEventCompleted = true;

            Map map = SingleMap;

            if (cursedPawn != null && !cursedPawn.Destroyed && !cursedPawn.Dead)
            {
                RemoveCurseEffectsFromCursedPawn(cursedPawn);
            }

            if (survived)
            {
                Find.LetterStack.ReceiveLetter("HSK_CursedSoul_Survived_LetterLabel".Translate(),
                    "HSK_CursedSoul_Survived_LetterText".Translate(cursedPawn?.Name.ToStringShort ?? "unknown"),
                    LetterDefOf.PositiveEvent);
            }
            else
            {
                Find.LetterStack.ReceiveLetter("HSK_CursedSoul_Ended_LetterLabel".Translate(),
                    "HSK_CursedSoul_Ended_LetterText".Translate(),
                    LetterDefOf.NeutralEvent);
            }

            End();
        }

        private void RemoveCurseEffectsFromCursedPawn(Pawn pawn)
        {
            var badMemoryDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("BadMemory");
            if (badMemoryDef != null)
            {
                var badMemory = pawn.needs.mood.thoughts.memories.Memories
                    .FirstOrDefault(m => m.def == badMemoryDef);
                if (badMemory != null)
                {
                    pawn.needs.mood.thoughts.memories.RemoveMemory(badMemory);
                }
            }

            var preyMarkDef = DefDatabase<HediffDef>.GetNamedSilentFail("PreyMark");
            if (preyMarkDef != null)
            {
                var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(preyMarkDef);
                if (hediff != null)
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }
        }

        public override void End()
        {
            Map map = SingleMap;
            base.End();

            if (map != null)
            {
                RemoveAllCurseBarghests(map);
            }
        }

        private void RemoveAllCurseBarghests(Map map)
        {
            List<Pawn> barghests = map.mapPawns.AllPawnsSpawned
                .Where(p => p.kindDef?.defName == "Barghest")
                .ToList();

            foreach (Pawn barghest in barghests)
            {
                if (!barghest.Destroyed)
                    barghest.Destroy();
            }

            List<Corpse> barghestCorpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse)
                .Where(t => t is Corpse corpse &&
                           corpse.InnerPawn != null &&
                           corpse.InnerPawn.kindDef?.defName == "Barghest")
                .Cast<Corpse>()
                .ToList();

            foreach (Corpse corpse in barghestCorpses)
            {
                if (!corpse.Destroyed)
                    corpse.Destroy();
            }
        }
    }
}
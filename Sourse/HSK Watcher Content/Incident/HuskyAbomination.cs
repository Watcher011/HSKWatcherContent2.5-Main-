using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HSK.Watcher.Content.Incident
{
    // Класс для хранения настроек мода
    public class HSK_Settings : ModSettings
    {
        public static bool EnableHuskyAbominationEvent = true;
        public static bool EnableInfectedAnimalEvents = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref EnableHuskyAbominationEvent, "EnableHuskyAbominationEvent", true);
            Scribe_Values.Look(ref EnableInfectedAnimalEvents, "EnableInfectedAnimalEvents", true);
        }
    }

    // Меню настроек для мода
    public class HSK_Mod : Mod
    {
        private HSK_Settings settings;

        public HSK_Mod(ModContentPack content) : base(content)
        {
            this.settings = GetSettings<HSK_Settings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // Настройка события с хаски
            listing.CheckboxLabeled("HSK.EnableHuskyAbominationEvent".Translate(),
                ref HSK_Settings.EnableHuskyAbominationEvent,
                "HSK.EnableHuskyAbominationEvent.Tooltip".Translate());

            // Настройка зараженных животных
            listing.CheckboxLabeled("HSK.EnableInfectedAnimalEvents".Translate(),
                ref HSK_Settings.EnableInfectedAnimalEvents,
                "HSK.EnableInfectedAnimalEvents.Tooltip".Translate());

            listing.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "HSK.Watcher";
        }
    }

    public class IncidentWorker_HuskyAbominationEvent : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            // Проверяем, включено ли событие в настройках
            if (!HSK_Settings.EnableHuskyAbominationEvent)
                return false;

            return base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            // Дополнительная проверка на случай если событие было отключено после добавления в очередь
            if (!HSK_Settings.EnableHuskyAbominationEvent)
                return false;

            Map map = (Map)parms.target;

            // Шаг 1: Спавн собаки (выглядит как обычное событие)
            Pawn husky = PawnGenerator.GeneratePawn(PawnKindDef.Named("Husky"), Faction.OfPlayer);
            IntVec3 spawnCell = DropCellFinder.RandomDropSpot(map);
            GenSpawn.Spawn(husky, spawnCell, map);

            // Добавляем скрытый компонент к собаке для отслеживания
            husky.health.AddHediff(HediffDef.Named("Hediff_AbominationTracker"));

            // Шаг 2: Создаем GameCondition для отложенного рейда (6-12 часов)
            GameCondition_DelayedRaid delayedRaidCondition = (GameCondition_DelayedRaid)GameConditionMaker.MakeCondition(
                GameConditionDef.Named("DelayedRaidCondition"),
                Rand.Range(160800, 170800));
            delayedRaidCondition.husky = husky;
            map.gameConditionManager.RegisterCondition(delayedRaidCondition);

            // Шаг 3: Запуск таймера для спавна Abomination (2 дня)
            map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(
                GameConditionDef.Named("AbominationSpawnTimer"), 172800));

            // Отправка письма (выглядит как обычное позитивное событие)
            Find.LetterStack.ReceiveLetter("HSK.AnimalWanderIn_Husky.LetterLabel".Translate(),
                "HSK.AnimalWanderIn_Husky.LetterText".Translate(),
                LetterDefOf.PositiveEvent, husky);

            return true;
        }
    }

    // GameCondition для отложенного рейда
    public class GameCondition_DelayedRaid : GameCondition
    {
        public Pawn husky;

        public override void End()
        {
            base.End();

            // Проверяем, включено ли событие
            if (!HSK_Settings.EnableHuskyAbominationEvent)
                return;

            if (this.SingleMap != null && husky != null && husky.Map == this.SingleMap && !husky.Dead)
            {
                TriggerDelayedRaid(this.SingleMap);
            }
        }

        private void TriggerDelayedRaid(Map map)
        {
            // Проверяем, включено ли событие
            if (!HSK_Settings.EnableHuskyAbominationEvent)
                return;

            // Ищем фракцию пиратов индустриального уровня
            Faction pirateFaction = Find.FactionManager.AllFactions
                .FirstOrDefault(f => f.def.defName == "Pirate" && f.def.techLevel == TechLevel.Industrial);

            // Если не нашли пиратов, ищем любую враждебную индустриальную фракцию
            if (pirateFaction == null)
            {
                pirateFaction = Find.FactionManager.AllFactions
                    .FirstOrDefault(f => f.HostileTo(Faction.OfPlayer) &&
                                       f.def.techLevel == TechLevel.Industrial &&
                                       !f.def.hidden);
            }

            if (pirateFaction == null)
            {
                Log.Warning("No industrial pirate faction found for delayed raid");
                return;
            }

            // Создаем 3-4 рейдера
            int raiderCount = Rand.RangeInclusive(3, 4);
            float pointsPerRaider = 40f; // Базовые очки за рейдера
            float totalPoints = pointsPerRaider * raiderCount;

            IncidentParms raidParms = new IncidentParms
            {
                target = map,
                points = totalPoints,
                faction = pirateFaction,
                raidStrategy = DefDatabase<RaidStrategyDef>.GetNamed("ImmediateAttack"),
                raidArrivalMode = DefDatabase<PawnsArrivalModeDef>.GetNamed("EdgeWalkIn"),
                generateFightersOnly = true
            };

            IncidentDef raidDef = DefDatabase<IncidentDef>.GetNamed("RaidEnemy");
            if (raidDef.Worker.CanFireNow(raidParms))
            {
                raidDef.Worker.TryExecute(raidParms);

                // Письмо с неразборчивыми требованиями
                Find.LetterStack.ReceiveLetter("HSK.Raid.LetterLabel".Translate(),
                    "HSK.Raid.LetterText".Translate(),
                    LetterDefOf.ThreatBig);
            }
        }
    }

    // Hediff для отслеживания собаки
    public class Hediff_AbominationTracker : HediffWithComps
    {
        public override bool ShouldRemove => false;
    }

    // Класс для обработки смерти собаки через Hediff
    public class HediffComp_AbominationOnDeath : HediffComp
    {
        public override void Notify_PawnKilled()
        {
            base.Notify_PawnKilled();
            TriggerAbominationSpawn();
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            TriggerAbominationSpawn();
        }

        private void TriggerAbominationSpawn()
        {
            // Проверяем, включено ли событие
            if (!HSK_Settings.EnableHuskyAbominationEvent)
                return;

            Map map = this.Pawn.Map;
            if (map != null)
            {
                // Спавним Abomination на месте трупа собаки
                SpawnAbominationsFromCorpse(map, this.Pawn, 3);
                map.gameConditionManager.ActiveConditions.RemoveAll(c => c.def.defName == "AbominationSpawnTimer");
                map.gameConditionManager.ActiveConditions.RemoveAll(c => c.def.defName == "DelayedRaidCondition");
            }
        }

        private void SpawnAbominationsFromCorpse(Map map, Pawn deadHusky, int count)
        {
            PawnKindDef abominationKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Abomination");
            Faction abominationFaction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Abomination"));

            if (abominationFaction == null || abominationKind == null)
            {
                Log.Warning("Abomination faction or pawnkind not found");
                return;
            }

            List<Pawn> spawnedAbominations = new List<Pawn>();
            IntVec3 spawnPosition = deadHusky.Position;

            // Убираем труп собаки
            if (deadHusky.Corpse != null)
            {
                deadHusky.Corpse.Destroy();
            }

            for (int i = 0; i < count; i++)
            {
                Pawn abomination = PawnGenerator.GeneratePawn(abominationKind, abominationFaction);

                // Находим клетку рядом с местом смерти собаки
                IntVec3 spawnCell;
                if (CellFinder.TryFindRandomCellNear(spawnPosition, map, 3,
                    c => c.Standable(map) && !c.Fogged(map) && c.Walkable(map), out spawnCell))
                {
                    GenSpawn.Spawn(abomination, spawnCell, map);
                    spawnedAbominations.Add(abomination);
                }
            }

            if (spawnedAbominations.Count > 0)
            {
                Find.LetterStack.ReceiveLetter("HSK.AbominationSpawn.Death.LetterLabel".Translate(),
                    "HSK.AbominationSpawn.Death.LetterText".Translate(spawnedAbominations.Count),
                    LetterDefOf.ThreatBig, spawnedAbominations[0]);
            }
        }
    }

    public class GameCondition_AbominationTimer : GameCondition
    {
        public override void End()
        {
            base.End();

            // Проверяем, включено ли событие
            if (!HSK_Settings.EnableHuskyAbominationEvent)
                return;

            if (this.SingleMap != null)
            {
                // Ищем собаку на карте с помощью LINQ
                Pawn husky = this.SingleMap.mapPawns.AllPawnsSpawned.FirstOrDefault(p =>
                    p.def.defName == "Husky" &&
                    p.health?.hediffSet?.HasHediff(HediffDef.Named("Hediff_AbominationTracker")) == true);

                if (husky != null && !husky.Dead)
                {
                    SpawnAbominationsFromHusky(this.SingleMap, husky, 3);
                }
            }
        }

        private void SpawnAbominationsFromHusky(Map map, Pawn husky, int count)
        {
            PawnKindDef abominationKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Abomination");
            Faction abominationFaction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Abomination"));

            if (abominationFaction == null || abominationKind == null)
            {
                Log.Warning("Abomination faction or pawnkind not found");
                return;
            }

            List<Pawn> spawnedAbominations = new List<Pawn>();
            IntVec3 spawnPosition = husky.Position;

            // Убираем живую собаку
            husky.Destroy();

            for (int i = 0; i < count; i++)
            {
                Pawn abomination = PawnGenerator.GeneratePawn(abominationKind, abominationFaction);

                // Находим клетку рядом с местом где была собака
                IntVec3 spawnCell;
                if (CellFinder.TryFindRandomCellNear(spawnPosition, map, 3,
                    c => c.Standable(map) && !c.Fogged(map) && c.Walkable(map), out spawnCell))
                {
                    GenSpawn.Spawn(abomination, spawnCell, map);
                    spawnedAbominations.Add(abomination);
                }
            }

            if (spawnedAbominations.Count > 0)
            {
                Find.LetterStack.ReceiveLetter("HSK.AbominationSpawn.Timer.LetterLabel".Translate(),
                    "HSK.AbominationSpawn.Timer.LetterText".Translate(spawnedAbominations.Count),
                    LetterDefOf.ThreatBig, spawnedAbominations[0]);
            }
        }
    }

    // Def class для настроек животных
    public class AnimalSettings : Def
    {
        public List<string> includedRaces;
        public List<string> excludedRaces;
        public float maxBodySize = 3.0f;
        public float maxWildness = 0.95f;
    }

    public class IncidentWorker_AnimalWanderInEvent : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            // Проверяем, включены ли события с зараженными животными
            if (!HSK_Settings.EnableInfectedAnimalEvents)
                return false;

            return base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            // Дополнительная проверка
            if (!HSK_Settings.EnableInfectedAnimalEvents)
                return false;

            Map map = (Map)parms.target;

            // 30% шанс зараженного животного, 70% здорового
            bool isInfected = Rand.Value < 0.3f;

            // Получаем список доступных животных
            List<PawnKindDef> availableAnimals = GetAvailableAnimals();

            if (availableAnimals.Count == 0) return false;

            // Выбираем случайное животное
            PawnKindDef animalKindDef = availableAnimals.RandomElement();

            // Спавн животного
            Pawn animal = PawnGenerator.GeneratePawn(animalKindDef, Faction.OfPlayer);
            IntVec3 spawnCell = DropCellFinder.RandomDropSpot(map);
            GenSpawn.Spawn(animal, spawnCell, map);

            if (isInfected)
            {
                return SpawnInfectedAnimal(animal, map);
            }

            return true;
        }

        private List<PawnKindDef> GetAvailableAnimals()
        {
            AnimalSettings settings = DefDatabase<AnimalSettings>.GetNamedSilentFail("AnimalEventSettings");
            List<PawnKindDef> availableAnimals = new List<PawnKindDef>();

            // Сначала добавляем животных из включенного списка
            if (settings?.includedRaces != null)
            {
                foreach (string raceDefName in settings.includedRaces)
                {
                    PawnKindDef animalDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(raceDefName);
                    if (animalDef != null && IsValidAnimal(animalDef, settings))
                    {
                        availableAnimals.Add(animalDef);
                    }
                }
            }

            // Затем добавляем других подходящих животных из всех модов
            foreach (PawnKindDef pawnKind in DefDatabase<PawnKindDef>.AllDefs)
            {
                if (pawnKind.race != null &&
                    pawnKind.race.race != null &&
                    pawnKind.race.race.Animal &&
                    IsValidAnimal(pawnKind, settings) &&
                    !availableAnimals.Contains(pawnKind))
                {
                    // Проверяем что животное не в исключенном списке
                    if (settings?.excludedRaces == null || !settings.excludedRaces.Contains(pawnKind.defName))
                    {
                        availableAnimals.Add(pawnKind);
                    }
                }
            }

            return availableAnimals;
        }

        private bool IsValidAnimal(PawnKindDef pawnKind, AnimalSettings settings)
        {
            if (pawnKind.race == null) return false;
            if (!pawnKind.race.race.Animal) return false;
            if (pawnKind.race.race.Humanlike) return false;
            if (pawnKind.race.race.IsMechanoid) return false;

            // Максимальный размер тела
            float maxSize = settings?.maxBodySize ?? 3.0f;
            if (pawnKind.race.race.baseBodySize > maxSize) return false;

            // ✅ RimWorld 1.6: Wildness теперь StatDef
            float wildness = pawnKind.race.GetStatValueAbstract(StatDefOf.Wildness);
            float maxWildness = settings?.maxWildness ?? 0.95f;
            if (wildness > maxWildness) return false;

            // ✅ RimWorld 1.6: у животных нет defaultFaction – пропускаем проверку фракции
            // Если животное спавнится игроку – оно и так не из враждебной фракции

            return true;
        }
        private bool SpawnInfectedAnimal(Pawn animal, Map map)
        {
            animal.health.AddHediff(HediffDef.Named("Hediff_InfectedAnimalTracker"));

            GameCondition_InfectedAnimalRageTimer rageCondition = (GameCondition_InfectedAnimalRageTimer)GameConditionMaker.MakeCondition(
                GameConditionDef.Named("InfectedAnimalRageTimer"), 172800);
            rageCondition.infectedAnimal = animal;
            map.gameConditionManager.RegisterCondition(rageCondition);

            return true;
        }
    }

    // GameCondition для таймера ярости животного
    public class GameCondition_InfectedAnimalRageTimer : GameCondition
    {
        public Pawn infectedAnimal;

        public override void End()
        {
            base.End();

            // Проверяем, включены ли события
            if (!HSK_Settings.EnableInfectedAnimalEvents)
                return;

            if (this.SingleMap != null && infectedAnimal != null && !infectedAnimal.Dead)
            {
                TriggerAnimalRage(this.SingleMap, infectedAnimal);
            }
        }

        private void TriggerAnimalRage(Map map, Pawn animal)
        {
            if (animal.Map != map || animal.Dead) return;

            // Будим животное если оно спит
            if (animal.CurJob != null && animal.CurJob.def == JobDefOf.LayDown)
            {
                animal.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            // Останавливаем все текущие jobs
            animal.jobs.StopAll();

            // Принудительно делаем животное мэнхантером
            animal.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter);

            // Добавляем компонент для заражения через укусы
            animal.health.AddHediff(HediffDef.Named("Hediff_InfectionCarrier"));

            // Сообщение о ярости животного
            string letterText = "HSK.AnimalRage.LetterText".Translate(animal.LabelCap);
            Find.LetterStack.ReceiveLetter("HSK.AnimalRage.LetterLabel".Translate(),
                letterText,
                LetterDefOf.ThreatBig, animal);
        }
    }

    // Hediff для отслеживания зараженного животного
    public class Hediff_InfectedAnimalTracker : HediffWithComps
    {
        public override bool ShouldRemove => false;
    }

    // Hediff для носителя инфекции
    public class Hediff_InfectionCarrier : HediffWithComps
    {
        public override bool ShouldRemove => false;
    }

    // Компонент для носителя инфекции, который заражает при атаке
    public class HediffComp_InfectionCarrier : HediffComp
    {
        private int lastAttackTick = -1;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            // Проверяем, включены ли события
            if (!HSK_Settings.EnableInfectedAnimalEvents)
                return;

            // Простая проверка: если животное не мэнхантер, делаем его мэнхантером
            if (this.parent.pawn != null && !this.parent.pawn.Dead && this.parent.pawn.MentalState?.def != MentalStateDefOf.Manhunter)
            {
                this.parent.pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter);
            }
        }

        public override void Notify_PawnUsedVerb(Verb verb, LocalTargetInfo target)
        {
            base.Notify_PawnUsedVerb(verb, target);

            // Проверяем, включены ли события
            if (!HSK_Settings.EnableInfectedAnimalEvents)
                return;

            // Защита от спама - не более одной атаки в 60 тиков
            if (Find.TickManager.TicksGame - lastAttackTick < 60)
                return;

            lastAttackTick = Find.TickManager.TicksGame;

            if (verb.IsMeleeAttack && target.Pawn != null && target.Pawn.RaceProps.Humanlike)
            {
                Pawn colonist = target.Pawn;
                if (!colonist.Dead && !colonist.health.hediffSet.HasHediff(HediffDef.Named("Hediff_AbominationInfection")))
                {
                    // Скрыто заражаем колониста
                    Hediff infection = HediffMaker.MakeHediff(HediffDef.Named("Hediff_AbominationInfection"), colonist);
                    colonist.health.AddHediff(infection);

                    // Сообщение об укусе (без упоминания заражения)
                    string message = "HSK.InfectionBite.Message".Translate(colonist.Name.ToStringShort, this.parent.pawn.Label);
                    Messages.Message(message, colonist, MessageTypeDefOf.NeutralEvent);
                }
            }
        }
    }

    // Hediff для заражения (скрытый от игрока)
    public class Hediff_AbominationInfection : HediffWithComps
    {
        public override bool ShouldRemove => false;

        // Скрываем от интерфейса
        public override string TipStringExtra => "";
        public override string LabelInBrackets => "";
    }

    // Компонент для управления превращением с задержкой 2-7 дней
    public class HediffComp_AbominationTransformation : HediffComp
    {
        private int transformationTick = -1;
        private bool transformationScheduled = false;

        public override void CompPostMake()
        {
            base.CompPostMake();

            // Устанавливаем случайное время превращения (2-7 дней)
            int delayTicks = Rand.RangeInclusive(120000, 420000); // 2-7 дней в тиках
            transformationTick = Find.TickManager.TicksGame + delayTicks;
            transformationScheduled = true;
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            // Проверяем, включены ли события
            if (!HSK_Settings.EnableInfectedAnimalEvents)
            {
                // Если события отключены, удаляем инфекцию
                Hediff infection = this.parent.pawn.health.hediffSet.GetFirstHediffOfDef(this.parent.def);
                if (infection != null)
                {
                    this.parent.pawn.health.RemoveHediff(infection);
                }
                return;
            }

            // Проверяем каждые 60 тиков (1 секунда) для производительности
            if (Find.TickManager.TicksGame % 60 == 0 && transformationScheduled && transformationTick > 0)
            {
                if (Find.TickManager.TicksGame >= transformationTick)
                {
                    if (this.parent.pawn.Spawned && !this.parent.pawn.Dead)
                    {
                        TransformColonist();
                    }
                    transformationScheduled = false;
                }
            }
        }

        private void TransformColonist()
        {
            Pawn colonist = this.parent.pawn;
            Map map = colonist.Map;

            // Создаем Abomination
            PawnKindDef abominationKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Abomination");
            Faction abominationFaction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Abomination"));

            if (abominationFaction == null || abominationKind == null) return;

            // Находим позицию для спавна
            IntVec3 spawnPosition = colonist.Position;
            IntVec3 spawnCell;

            if (!CellFinder.TryFindRandomCellNear(spawnPosition, map, 2,
                c => c.Standable(map) && !c.Fogged(map) && c.Walkable(map), out spawnCell))
            {
                spawnCell = spawnPosition;
            }

            // Создаем и спавним Abomination
            Pawn abomination = PawnGenerator.GeneratePawn(abominationKind, abominationFaction);
            GenSpawn.Spawn(abomination, spawnCell, map);

            // Убиваем колониста с эффектом
            DamageInfo damageInfo = new DamageInfo(DamageDefOf.Bite, 1000f, 999f, -1f, null, colonist.health.hediffSet.GetBrain());
            colonist.Kill(damageInfo);

            // Создаем визуальный эффект
            FleckMaker.ThrowSmoke(colonist.DrawPos, map, 2f);
            FleckMaker.ThrowMicroSparks(colonist.DrawPos, map);

            // Сообщение о превращении
            string message = "HSK.InfectionTransformation.Message".Translate(colonist.Name.ToStringShort);
            Messages.Message(message, abomination, MessageTypeDefOf.NegativeEvent);

            // Убираем хедифф инфекции
            Hediff infection = colonist.health.hediffSet.GetFirstHediffOfDef(this.parent.def);
            if (infection != null)
            {
                colonist.health.RemoveHediff(infection);
            }
        }

        public override string CompTipStringExtra => "";
    }

    // Обработчик смерти зараженного животного
    public class HediffComp_InfectedAnimalOnDeath : HediffComp
    {
        public override void Notify_PawnKilled()
        {
            base.Notify_PawnKilled();
            TriggerAbominationSpawn();
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            TriggerAbominationSpawn();
        }

        private void TriggerAbominationSpawn()
        {
            // Проверяем, включены ли события
            if (!HSK_Settings.EnableInfectedAnimalEvents)
                return;

            Map map = this.parent.pawn.Map;
            if (map != null && this.parent.pawn.Spawned)
            {
                // Количество Abomination зависит от размера животного
                int abominationCount = GetAbominationCountByAnimalSize(this.parent.pawn);
                SpawnAbominationsFromCorpse(map, this.parent.pawn, abominationCount);
            }
        }

        private int GetAbominationCountByAnimalSize(Pawn animal)
        {
            float bodySize = animal.def.race.baseBodySize;
            if (bodySize >= 2.0f) return 3;
            if (bodySize >= 1.0f) return 2;
            return 1;
        }

        private void SpawnAbominationsFromCorpse(Map map, Pawn deadAnimal, int count)
        {
            PawnKindDef abominationKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Abomination");
            Faction abominationFaction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Abomination"));

            if (abominationFaction == null || abominationKind == null) return;

            List<Pawn> spawnedAbominations = new List<Pawn>();
            IntVec3 spawnPosition = deadAnimal.Position;

            // Убираем труп животного
            if (deadAnimal.Corpse != null)
            {
                deadAnimal.Corpse.Destroy();
            }

            for (int i = 0; i < count; i++)
            {
                Pawn abomination = PawnGenerator.GeneratePawn(abominationKind, abominationFaction);

                if (CellFinder.TryFindRandomCellNear(spawnPosition, map, 3,
                    c => c.Standable(map) && !c.Fogged(map) && c.Walkable(map), out IntVec3 spawnCell))
                {
                    GenSpawn.Spawn(abomination, spawnCell, map);
                    spawnedAbominations.Add(abomination);

                    // Эффект появления
                    FleckMaker.ThrowSmoke(spawnCell.ToVector3Shifted(), map, 1.5f);
                }
            }

            if (spawnedAbominations.Count > 0)
            {
                string letterText = "HSK.InfectedAnimalDeath.LetterText".Translate(deadAnimal.Label, spawnedAbominations.Count);
                Find.LetterStack.ReceiveLetter("HSK.InfectedAnimalDeath.LetterLabel".Translate(),
                    letterText,
                    LetterDefOf.ThreatBig, spawnedAbominations[0]);
            }
        }
    }
}
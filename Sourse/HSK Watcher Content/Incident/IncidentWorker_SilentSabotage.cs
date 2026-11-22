using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
/*
    ================================================================================
ИВЕНТ: "ТИХИЙ САБОТАЖ"

"Невидимый враг" — это скрытый диверсионный инцидент, в котором один из жителей вашей колонии оказывается предателем, тайно устанавливая взрывные устройства в самых уязвимых местах.
Игрок не получает предупреждений — он должен самостоятельно обнаружить опасность, прежде чем произойдет катастрофа.
    ================================================================================
    */
/* namespace HSK.Watcher.Content.Incident
{
    #region Конфигурация отладки

    public static class HSK_Debug
    {
        public static bool EnableDebugLogging = true;
        public static bool ShowVerboseLogs = false;
        public static bool LogTargetSelection = true;
        public static bool LogBombPlacement = true;
        public static bool LogRaidEvents = true;

        // Переименуем метод, чтобы избежать конфликта с Verse.Log
        public static void DebugLog(string message, bool isVerbose = false)
        {
            if (!EnableDebugLogging) return;
            if (isVerbose && !ShowVerboseLogs) return;

            Verse.Log.Message($"[HSK-DEBUG] {message}");
        }

        public static void DebugWarning(string message)
        {
            if (!EnableDebugLogging) return;
            Verse.Log.Warning($"[HSK-DEBUG] {message}");
        }

        public static void DebugError(string message)
        {
            // Ошибки всегда логируем
            Verse.Log.Error($"[HSK-DEBUG] {message}");
        }

        public static void ToggleDebug()
        {
            EnableDebugLogging = !EnableDebugLogging;
            string status = EnableDebugLogging ? "ВКЛЮЧЕНА" : "ОТКЛЮЧЕНА";
            Verse.Log.Message($"[HSK] Отладка {status}");

            if (EnableDebugLogging)
            {
                Verse.Log.Message($"[HSK] Подробные логи: {(ShowVerboseLogs ? "ВКЛ" : "ВЫКЛ")}");
                Verse.Log.Message($"[HSK] Логи целей: {(LogTargetSelection ? "ВКЛ" : "ВЫКЛ")}");
                Verse.Log.Message($"[HSK] Логи бомб: {(LogBombPlacement ? "ВКЛ" : "ВЫКЛ")}");
                Verse.Log.Message($"[HSK] Логи рейдов: {(LogRaidEvents ? "ВКЛ" : "ВЫКЛ")}");
            }
        }

        public static void ToggleVerbose()
        {
            ShowVerboseLogs = !ShowVerboseLogs;
            string status = ShowVerboseLogs ? "ВКЛЮЧЕНЫ" : "ОТКЛЮЧЕНЫ";
            Verse.Log.Message($"[HSK] Подробные логи {status}");
        }
    }

    #endregion

    #region Расширение настроек инцидента

    public class PredatorEventDefExtension : DefModExtension
    {
        public DamageDef explosionDamageType = null;
        public float explosionRadius = 3.9f;
        public int explosionDamage = 60;
        public float armorPenetration = 1f;
        public IntRange bombTimerRange = new IntRange(750, 900);
        public int minColonistsForEvent = 4;
        public int minDaysForEvent = 20;
        public float eventBaseChance = 0.8f;
        public int minBombs = 1;
        public int maxBombs = 3;

        // Новые настройки для усиленных рейдов
        public int colonyAgeThreshold = 1800; // 5 лет в днях (360 дней * 5)
        public int defenseScoreThreshold = 10; // Порог оценки обороны
        public float enhancedRaidMultiplier = 2.0f; // Множитель для усиленного рейда
        public float dropPodChance = 0.6f; // Шанс десанта вместо обычного рейда
        public int maxEnhancedRaiders = 15; // Максимальное количество рейдеров в усиленном рейде

        // Ключи перевода
        public string letterTitleKey = "PredatorEvent_LetterTitle";
        public string letterTextKey = "PredatorEvent_LetterText";
        public string bombDefusedKey = "PredatorEvent_BombDefused";
        public string bombExplodedKey = "PredatorEvent_BombExploded";
        public string defuseCommandLabelKey = "PredatorEvent_DefuseCommandLabel";
        public string defuseCommandDescKey = "PredatorEvent_DefuseCommandDesc";
        public string enhancedRaidMessageKey = "PredatorEvent_EnhancedRaidMessage";

        private static readonly HashSet<string> PriorityTargetDefNames = new HashSet<string>
        {
            "OrbitalTradeBeacon", "Turret_Oerlikonautocannon", "Turret_Minigun",
            "Turret_M2Browning", "Turret_SentryRocketLauncher", "Turret_MiniTurret",
            "NavalGunTurretGun", "AvengerTurretGun", "WoodFiredGenerator",
            "CoalPowerPlant", "ChemfuelPoweredGenerator", "TeslaLightningPower",
            "PlasmaGeothermalPlant", "PlasmaGen", "ReactorCoreA", "ReactorCoreB",
            "ReactorCoreC", "CoolingRadiator", "CoolingWater", "CoolingTower",
            "Transformer", "Battery", "OilWell", "DeepOilWell", "OilStorage",
            "FuelStorage", "FuelPowerplant", "LargeFuelPowerplant"
        };

        public DamageDef GetResolvedDamageDef()
        {
            if (explosionDamageType != null)
                return explosionDamageType;

            return DefDatabase<DamageDef>.GetNamedSilentFail("Bomb") ?? DamageDefOf.Bomb;
        }

        public static bool IsPriorityTarget(string defName)
        {
            return PriorityTargetDefNames.Contains(defName);
        }
    }

    #endregion

    #region Трекер событий

    public class GameComponent_PredatorEvent : GameComponent
    {
        private Dictionary<IntVec3, string> bombPredators;
        private Dictionary<string, bool> predatorRevealed;
        private Dictionary<string, Faction> predatorOriginalFactions;
        private int raidTriggerCounter = 0;
        private const int MAX_RAIDS_PER_EVENT = 2; // Увеличили до 2 рейдов за событие
        private bool enhancedRaidTriggered = false; // Флаг усиленного рейда

        public GameComponent_PredatorEvent(Game game)
        {
            InitializeDictionaries();
        }

        private void InitializeDictionaries()
        {
            bombPredators = new Dictionary<IntVec3, string>();
            predatorRevealed = new Dictionary<string, bool>();
            predatorOriginalFactions = new Dictionary<string, Faction>();
        }

        public void RegisterBomb(IntVec3 position, string predatorName)
        {
            if (bombPredators == null) InitializeDictionaries();

            bombPredators[position] = predatorName;
            if (!predatorRevealed.ContainsKey(predatorName))
            {
                predatorRevealed[predatorName] = false;
            }

            HSK_Debug.DebugLog($"Зарегистрирована бомба в {position} для предателя {predatorName}", false);
        }

        public void RegisterPredatorFaction(string predatorName, Faction faction)
        {
            if (predatorOriginalFactions == null)
            {
                predatorOriginalFactions = new Dictionary<string, Faction>();
            }

            if (predatorName == null)
            {
                HSK_Debug.DebugWarning("Попытка зарегистрировать фракцию для предателя с null именем");
                return;
            }

            if (faction == null)
            {
                HSK_Debug.DebugWarning($"Попытка зарегистрировать null фракцию для предателя: {predatorName}");
                predatorOriginalFactions[predatorName] = null;
            }
            else
            {
                predatorOriginalFactions[predatorName] = faction;
                HSK_Debug.DebugLog($"Зарегистрирована фракция {faction.Name} для предателя {predatorName}", true);
            }
        }

        public Faction GetPredatorOriginalFaction(string predatorName)
        {
            if (predatorOriginalFactions == null) return null;
            return predatorOriginalFactions.TryGetValue(predatorName, out Faction faction) ? faction : null;
        }

        public bool HasBombAt(IntVec3 position)
        {
            if (bombPredators == null) return false;
            return bombPredators.ContainsKey(position);
        }

        public string GetPredatorForBomb(IntVec3 position)
        {
            if (bombPredators == null) return null;
            return bombPredators.TryGetValue(position, out string name) ? name : null;
        }

        public void RemoveBomb(IntVec3 position)
        {
            if (bombPredators == null) return;

            if (bombPredators.ContainsKey(position))
            {
                string predatorName = bombPredators[position];
                bombPredators.Remove(position);
                HSK_Debug.DebugLog($"Удалена бомба в {position} (предатель: {predatorName})", true);
            }
        }

        public void RevealPredator(string predatorName, Map map)
        {
            if (predatorRevealed == null) return;

            if (predatorRevealed.ContainsKey(predatorName) && !predatorRevealed[predatorName])
            {
                predatorRevealed[predatorName] = true;
                HSK_Debug.DebugLog($"Раскрытие предателя: {predatorName}");

                Pawn predator = FindPredatorPawn(predatorName, map);
                if (predator != null && !predator.Dead && !predator.Downed)
                {
                    MakePredatorHostile(predator);
                }
                else
                {
                    HSK_Debug.DebugWarning($"Не найден живой предатель {predatorName} для раскрытия");
                }
            }
        }

        private Pawn FindPredatorPawn(string predatorName, Map map)
        {
            if (map == null) return null;

            return map.mapPawns.AllPawnsSpawned
                .Where(p => p.Name != null && p.Name.ToStringShort == predatorName)
                .FirstOrDefault();
        }

        private void MakePredatorHostile(Pawn predator)
        {
            try
            {
                if (predator == null) return;

                Faction originalFaction = predator.Faction;
                RegisterPredatorFaction(predator.Name?.ToStringShort ?? "Unknown", originalFaction);

                Faction hostileFaction = Find.FactionManager.AllFactions
                    .Where(f => f != null && f.HostileTo(Faction.OfPlayer) && !f.def.hidden)
                    .FirstOrDefault();

                if (hostileFaction == null)
                {
                    hostileFaction = Faction.OfAncientsHostile;
                }

                predator.SetFaction(hostileFaction);

                Pawn target = FindTargetForPredator(predator);
                if (target != null)
                {
                    if (predator.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                    {
                        Job attackJob = null;
                        if (predator.equipment?.Primary != null)
                        {
                            var compEquip = predator.equipment.Primary.TryGetComp<CompEquippable>();
                            if (compEquip != null && compEquip.PrimaryVerb is Verb_Shoot)
                            {
                                attackJob = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
                            }
                            else
                            {
                                attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
                            }
                        }
                        else
                        {
                            attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
                        }

                        if (attackJob != null)
                        {
                            predator.jobs.StartJob(attackJob, JobCondition.InterruptForced);
                        }
                    }

                    if (predator.mindState != null)
                    {
                        predator.mindState.mentalStateHandler.TryStartMentalState(
                            MentalStateDefOf.Manhunter,
                            "HSK_TraitorRevealed".Translate()
                        );
                    }
                }

                HSK_Debug.DebugLog($"Предатель {predator.Name} раскрыт! Фракция: {hostileFaction.Name}");
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при раскрытии предателя: {ex}");
            }
        }

        private Pawn FindTargetForPredator(Pawn predator)
        {
            if (predator?.Map == null) return null;

            return predator.Map.mapPawns.FreeColonists
                .Where(p => !p.Dead && !p.Downed)
                .OrderBy(p => p.Position.DistanceTo(predator.Position))
                .FirstOrDefault();
        }

        public bool CanTriggerRaid(bool isEnhanced = false)
        {
            if (raidTriggerCounter >= MAX_RAIDS_PER_EVENT)
            {
                HSK_Debug.DebugLog($"Достигнут лимит рейдов: {raidTriggerCounter}/{MAX_RAIDS_PER_EVENT}", true);
                return false;
            }

            // Если это усиленный рейд, проверяем не был ли уже запущен
            if (isEnhanced && enhancedRaidTriggered)
            {
                HSK_Debug.DebugLog("Усиленный рейд уже был запущен", true);
                return false;
            }

            raidTriggerCounter++;
            if (isEnhanced)
                enhancedRaidTriggered = true;

            HSK_Debug.DebugLog($"Рейд разрешен. Счетчик: {raidTriggerCounter}, Усиленный: {isEnhanced}", true);
            return true;
        }

        public void ResetRaidCounter()
        {
            HSK_Debug.DebugLog($"Сброс счетчика рейдов: {raidTriggerCounter} → 0", true);
            raidTriggerCounter = 0;
            enhancedRaidTriggered = false;
        }

        public bool IsPredatorRevealed(string predatorName)
        {
            if (predatorRevealed == null) return false;
            return predatorRevealed.TryGetValue(predatorName, out bool revealed) && revealed;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (bombPredators == null) InitializeDictionaries();
            }

            Scribe_Collections.Look(ref bombPredators, "bombPredators", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref predatorRevealed, "predatorRevealed", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref predatorOriginalFactions, "predatorOriginalFactions", LookMode.Value, LookMode.Reference);
            Scribe_Values.Look(ref raidTriggerCounter, "raidTriggerCounter", 0);
            Scribe_Values.Look(ref enhancedRaidTriggered, "enhancedRaidTriggered", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (bombPredators == null) InitializeDictionaries();
            }
        }
    }

    #endregion

    #region Класс бомбы

    [StaticConstructorOnStartup]
    public class Building_PredatorBomb : Building_Trap
    {
        private string predatorName = "Unknown";
        private GameComponent_PredatorEvent eventComp;
        private bool detonating = false;
        private int detonationTicks = 0;
        private int ticksUntilExplosion = -1;
        private bool timerActive = false;

        private static readonly Material WarningOverlay = SolidColorMaterials.SimpleSolidColorMaterial(new Color(1f, 0.2f, 0.1f, 0.3f));
        private int lastPulseTick = 0;
        private const int PULSE_INTERVAL = 15;
        private float currentPulse = 0f;
        private bool drawWarning = false;

        static Building_PredatorBomb()
        {
            // Инициализация переводов при старте
            InitTranslations();
        }

        private static void InitTranslations()
        {
            // Эти строки будут использоваться если нет перевода
            if ("HSK_BombDetonating".CanTranslate())
            {
                // Переводы уже загружены
                return;
            }
        }

        protected override void SpringSub(Pawn p)
        {
            if (!detonating)
            {
                StartDetonation();
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (Current.Game != null)
            {
                eventComp = Current.Game.GetComponent<GameComponent_PredatorEvent>();

                if (eventComp == null)
                {
                    eventComp = new GameComponent_PredatorEvent(Current.Game);
                    Current.Game.components.Add(eventComp);
                }
            }

            if (!respawningAfterLoad)
            {
                var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail("HSK_PredatorGuest");
                var extension = incidentDef?.GetModExtension<PredatorEventDefExtension>();

                if (extension != null)
                {
                    ticksUntilExplosion = extension.bombTimerRange.RandomInRange;
                    timerActive = true;
                    HSK_Debug.DebugLog($"Бомба создана. Таймер: {ticksUntilExplosion} тиков ({ticksUntilExplosion / 60f:F1} минут)", true);
                }
                else
                {
                    ticksUntilExplosion = 750;
                    timerActive = true;
                    HSK_Debug.DebugLog($"Бомба создана. Таймер по умолчанию: {ticksUntilExplosion} тиков", true);
                }

                if (eventComp != null)
                {
                    predatorName = eventComp.GetPredatorForBomb(Position) ?? "Unknown";
                }
            }
            else
            {
                HSK_Debug.DebugLog($"Бомба загружена в {Position}. Предатель: {predatorName}", true);
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (timerActive && ticksUntilExplosion > 0 && !detonating)
            {
                ticksUntilExplosion--;

                if (ticksUntilExplosion < 300)
                {
                    drawWarning = true;
                    UpdatePulseEffect();
                }
                else
                {
                    drawWarning = false;
                }

                if (ticksUntilExplosion <= 0)
                {
                    StartDetonation();
                }
            }

            if (detonating)
            {
                detonationTicks++;

                if (detonationTicks % 3 == 0)
                {
                    float progress = detonationTicks / 15f;
                    FleckMaker.ThrowSmoke(Position.ToVector3Shifted(), Map, 0.5f * progress);
                }

                if (detonationTicks >= 15)
                {
                    Detonate();
                }
            }
        }

        private void UpdatePulseEffect()
        {
            if (Find.TickManager.TicksGame - lastPulseTick >= PULSE_INTERVAL)
            {
                lastPulseTick = Find.TickManager.TicksGame;
                currentPulse = Mathf.Sin(Find.TickManager.TicksGame * 0.1f) * 0.5f + 0.5f;

                if (ticksUntilExplosion < 150 && ticksUntilExplosion % 30 == 0)
                {
                    float minutes = ticksUntilExplosion / 60f;
                    MoteMaker.ThrowText(Position.ToVector3Shifted(), Map,
                        minutes.ToString("F0") + " " + "HSK_MinutesAbbr".Translate(), Color.red);
                }
            }
        }

        public void StartDetonation()
        {
            if (!detonating)
            {
                detonating = true;
                detonationTicks = 0;
                drawWarning = false;

                HSK_Debug.DebugLog($"Бомба в {Position} начинает детонацию");

                Messages.Message("HSK_BombFuseLit".Translate(),
                    new TargetInfo(Position, Map, false),
                    MessageTypeDefOf.ThreatBig);
            }
        }

        private void Detonate()
        {
            try
            {
                HSK_Debug.DebugLog($"Бомба в {Position} взрывается. Предатель: {predatorName}");

                var explosiveComp = GetComp<CompExplosive>();
                if (explosiveComp != null)
                {
                    explosiveComp.StartWick();
                }
                else
                {
                    GenExplosion.DoExplosion(
                        center: Position,
                        map: Map,
                        radius: 3.9f,
                        damType: DamageDefOf.Bomb,
                        instigator: null,
                        damAmount: 60,
                        armorPenetration: 1f,
                        explosionSound: null
                    );
                }

                if (!string.IsNullOrEmpty(predatorName) && eventComp != null)
                {
                    eventComp.RevealPredator(predatorName, Map);
                }

                // НОВАЯ ЛОГИКА: Проверяем условия для усиленного рейда
                var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail("HSK_PredatorGuest");
                var settings = incidentDef?.GetModExtension<PredatorEventDefExtension>();

                if (ShouldStartEnhancedRaid(Map, settings))
                {
                    StartEnhancedRaid(Map, settings);
                }
                else
                {
                    StartSingleRaidAfterExplosion();
                }

                if (eventComp != null)
                {
                    eventComp.RemoveBomb(Position);
                }
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при детонации: {ex}");
            }
        }

        /// <summary>
        /// Проверяет условия для усиленного рейда
        /// </summary>
        private bool ShouldStartEnhancedRaid(Map map, PredatorEventDefExtension settings)
        {
            if (map == null || settings == null) return false;

            // Проверяем возраст колонии
            bool colonyIsOld = GenDate.DaysPassed >= settings.colonyAgeThreshold;

            // Вычисляем счет обороны
            int defenseScore = CalculateDefenseScore(map);
            bool hasStrongDefenses = defenseScore >= settings.defenseScoreThreshold;

            if (HSK_Debug.LogRaidEvents)
            {
                HSK_Debug.DebugLog($"Проверка усиленного рейда: возраст {GenDate.DaysPassed}/{settings.colonyAgeThreshold}, защита {defenseScore}/{settings.defenseScoreThreshold}");
            }

            return colonyIsOld || hasStrongDefenses;
        }

        /// <summary>
        /// Вычисляет счет обороны колонии
        /// </summary>
        private int CalculateDefenseScore(Map map)
        {
            int score = 0;

            try
            {
                // Турели и оборонительные сооружения
                var turrets = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.building?.IsTurret == true ||
                               b.def.defName.Contains("Turret") ||
                               b.def.defName.Contains("Gun") ||
                               b.def.defName.Contains("Cannon"))
                    .ToList();

                score += turrets.Count;

                // Укрепленные стены и блоки
                var defensiveWalls = map.listerBuildings.allBuildingsColonist
                    .Where(b => (b.def.graphicData != null && b.def.graphicData.Linked) ||
                               b.def.defName.Contains("Sandbag") ||
                               b.def.defName.Contains("Barricade") ||
                               b.def.defName.Contains("Embrasure") ||
                               b.def.defName.Contains("Wall"))
                    .ToList();

                score += defensiveWalls.Count / 5; // Каждые 5 блоков дают 1 очко

                // Защитные ловушки
                var traps = map.listerBuildings.allBuildingsColonist
                    .Where(b => b is Building_Trap)
                    .ToList();

                score += traps.Count / 3; // Каждые 3 ловушки дают 1 очко

                // Оборонные орудия (ракетные установки и т.д.)
                var heavyDefenses = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.defName.Contains("Turret") ||
                               b.def.defName.Contains("Cannon") ||
                               b.def.defName.Contains("Mortar") ||
                               b.def.defName.Contains("Launcher"))
                    .Count();

                score += heavyDefenses * 2; // Тяжелая защита дает 2 очка за единицу

                // Колонисты с боевыми навыками
                var skilledColonists = map.mapPawns.FreeColonists
                    .Where(p => p.skills.GetSkill(SkillDefOf.Shooting).Level >= 10 ||
                               p.skills.GetSkill(SkillDefOf.Melee).Level >= 10)
                    .Count();

                score += skilledColonists;

                if (HSK_Debug.LogRaidEvents)
                {
                    HSK_Debug.DebugLog($"Счет обороны: {score} (турели: {turrets.Count}, стены: {defensiveWalls.Count}, ловушки: {traps.Count}, тяжелая защита: {heavyDefenses}, опытные колонисты: {skilledColonists})");
                }
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при расчете счета обороны: {ex}");
            }

            return Mathf.Max(0, score);
        }

        /// <summary>
        /// Запускает усиленный рейд с возможностью десанта
        /// </summary>
        private void StartEnhancedRaid(Map map, PredatorEventDefExtension settings)
        {
            if (map == null) return;

            if (eventComp != null && !eventComp.CanTriggerRaid(true))
            {
                HSK_Debug.DebugLog("Усиленный рейд уже был запущен", true);
                return;
            }

            Faction hostileFaction = Find.FactionManager.AllFactions
                .Where(f => f.HostileTo(Faction.OfPlayer) && !f.def.hidden && f.def.humanlikeFaction)
                .FirstOrDefault();

            if (hostileFaction == null)
            {
                hostileFaction = Faction.OfAncientsHostile;
            }

            // Увеличенные очки угрозы для усиленного рейда
            float basePoints = StorytellerUtility.DefaultThreatPointsNow(map);
            float enhancedPoints = basePoints * settings.enhancedRaidMultiplier;
            enhancedPoints = Mathf.Clamp(enhancedPoints, 500f, 5000f);

            // Определяем тип рейда: десант или обычный
            bool useDropPods = Rand.Value < settings.dropPodChance;
            PawnsArrivalModeDef arrivalMode = useDropPods ?
                PawnsArrivalModeDefOf.CenterDrop :
                PawnsArrivalModeDefOf.EdgeWalkIn;

            IncidentParms parms = new IncidentParms
            {
                target = map,
                points = enhancedPoints,
                faction = hostileFaction,
                raidStrategy = RaidStrategyDefOf.ImmediateAttack,
                raidArrivalMode = arrivalMode,
                forced = true,
                raidForceOneDowned = false
            };

            try
            {
                IncidentDef raidDef = IncidentDefOf.RaidEnemy;
                if (raidDef.Worker.CanFireNow(parms))
                {
                    bool raidSuccess = raidDef.Worker.TryExecute(parms);
                    if (raidSuccess)
                    {
                        string raidType = useDropPods ? "десант" : "усиленный рейд";
                        Messages.Message(
                            settings.enhancedRaidMessageKey.Translate(raidType, hostileFaction.Name),
                            MessageTypeDefOf.ThreatBig
                        );

                        // Добавляем дополнительных рейдеров если их мало
                        PostProcessEnhancedRaid(map, hostileFaction, enhancedPoints, useDropPods);

                        if (HSK_Debug.LogRaidEvents)
                        {
                            HSK_Debug.DebugLog($"Усиленный рейд ({raidType}) запущен: {enhancedPoints:F0} очков, фракция: {hostileFaction.Name}");
                        }
                    }
                    else
                    {
                        if (eventComp != null)
                        {
                            eventComp.ResetRaidCounter();
                        }
                        HSK_Debug.DebugLog("Не удалось запустить усиленный рейд", true);
                    }
                }
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при запуске усиленного рейда: {ex}");
                if (eventComp != null)
                {
                    eventComp.ResetRaidCounter();
                }
            }
        }

        /// <summary>
        /// Постобработка усиленного рейда - добавляем рейдеров если их мало
        /// </summary>
        private void PostProcessEnhancedRaid(Map map, Faction faction, float points, bool dropPods)
        {
            try
            {
                // Получаем текущих рейдеров на карте
                var currentRaiders = map.mapPawns.AllPawnsSpawned
                    .Where(p => p.Faction == faction && p.HostileTo(Faction.OfPlayer))
                    .ToList();

                if (HSK_Debug.LogRaidEvents)
                {
                    HSK_Debug.DebugLog($"Текущее количество рейдеров: {currentRaiders.Count}", true);
                }

                var settings = DefDatabase<IncidentDef>.GetNamedSilentFail("HSK_PredatorGuest")
                    ?.GetModExtension<PredatorEventDefExtension>();

                if (settings == null) return;

                // Если рейдеров слишком мало, добавляем еще
                if (currentRaiders.Count < settings.maxEnhancedRaiders / 2)
                {
                    int raidersToAdd = Mathf.Min(
                        settings.maxEnhancedRaiders - currentRaiders.Count,
                        Mathf.CeilToInt(points / 100f)
                    );

                    if (raidersToAdd > 0)
                    {
                        AddExtraRaiders(map, faction, raidersToAdd, dropPods);
                        if (HSK_Debug.LogRaidEvents)
                        {
                            HSK_Debug.DebugLog($"Добавлено дополнительных рейдеров: {raidersToAdd}", true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка в постобработке рейда: {ex}");
            }
        }

        /// <summary>
        /// Добавляет дополнительных рейдеров
        /// </summary>
        private void AddExtraRaiders(Map map, Faction faction, int count, bool dropPods)
        {
            try
            {
                PawnGroupMakerParms parms = new PawnGroupMakerParms
                {
                    faction = faction,
                    points = count * 100f, // Примерно по 100 очков на рейдера
                    groupKind = PawnGroupKindDefOf.Combat,
                    tile = map.Tile,
                    generateFightersOnly = true,
                    raidStrategy = RaidStrategyDefOf.ImmediateAttack
                };

                List<Pawn> extraRaiders = PawnGroupMakerUtility.GeneratePawns(parms).Take(count).ToList();

                if (extraRaiders.Any())
                {
                    IntVec3 spawnSpot;
                    if (dropPods)
                    {
                        // Для десанта - падение в центр колонии
                        spawnSpot = DropCellFinder.FindRaidDropCenterDistant(map);
                    }
                    else
                    {
                        // Для обычного рейда - появление с края карты
                        spawnSpot = DropCellFinder.FindRaidDropCenterDistant(map, false);
                    }

                    if (dropPods && spawnSpot.IsValid)
                    {
                        DropPodUtility.DropThingsNear(spawnSpot, map, extraRaiders.Cast<Thing>(), 110, false, true, true);
                    }
                    else if (spawnSpot.IsValid)
                    {
                        foreach (var raider in extraRaiders)
                        {
                            GenSpawn.Spawn(raider, spawnSpot, map, Rot4.Random);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при добавлении дополнительных рейдеров: {ex}");
            }
        }

        private void StartSingleRaidAfterExplosion()
        {
            if (Map == null) return;

            if (eventComp != null && !eventComp.CanTriggerRaid())
            {
                HSK_Debug.DebugLog("Рейд уже был запущен", true);
                return;
            }

            Faction hostileFaction = Find.FactionManager.AllFactions
                .Where(f => f.HostileTo(Faction.OfPlayer) && !f.def.hidden)
                .FirstOrDefault();

            if (hostileFaction == null)
            {
                hostileFaction = Faction.OfAncientsHostile;
            }

            float raidPoints = StorytellerUtility.DefaultThreatPointsNow(Map) * 1.5f;
            raidPoints = Mathf.Clamp(raidPoints, 200f, 3000f);

            IncidentParms parms = new IncidentParms
            {
                target = Map,
                points = raidPoints,
                faction = hostileFaction,
                raidStrategy = RaidStrategyDefOf.ImmediateAttack,
                raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn,
                forced = true
            };

            try
            {
                IncidentDef raidDef = IncidentDefOf.RaidEnemy;
                if (raidDef.Worker.CanFireNow(parms))
                {
                    bool raidSuccess = raidDef.Worker.TryExecute(parms);
                    if (raidSuccess)
                    {
                        Messages.Message("HSK_RaidAfterExplosion".Translate(),
                            MessageTypeDefOf.ThreatBig);

                        if (HSK_Debug.LogRaidEvents)
                        {
                            HSK_Debug.DebugLog($"Рейд запущен: {raidPoints} очков, фракция: {hostileFaction.Name}");
                        }
                    }
                    else
                    {
                        if (eventComp != null)
                        {
                            eventComp.ResetRaidCounter();
                        }
                        HSK_Debug.DebugLog("Не удалось запустить рейд", true);
                    }
                }
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при запуске рейда: {ex}");
                if (eventComp != null)
                {
                    eventComp.ResetRaidCounter();
                }
            }
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            if (drawWarning && Find.Selector.IsSelected(this))
            {
                Vector3 drawPos = DrawPos;
                drawPos.y += 0.05f;

                float pulseScale = 0.5f + currentPulse * 0.3f;
                Matrix4x4 matrix = Matrix4x4.TRS(
                    drawPos,
                    Quaternion.identity,
                    new Vector3(pulseScale, 1f, pulseScale)
                );

                Graphics.DrawMesh(
                    MeshPool.plane10,
                    matrix,
                    WarningOverlay,
                    0
                );
            }
        }

        public override string GetInspectString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            if (detonating)
            {
                sb.AppendLine("HSK_BombDetonating".Translate().Colorize(Color.red));
            }
            else if (timerActive && ticksUntilExplosion > 0)
            {
                float minutes = ticksUntilExplosion / 60f;
                sb.AppendLine("HSK_TimeUntilExplosion".Translate() + ": " +
                    minutes.ToString("F0") + " " + "HSK_Minutes".Translate());

                if (ticksUntilExplosion < 300)
                {
                    sb.AppendLine("HSK_DangerImminent".Translate().Colorize(Color.red));
                }
            }

            string baseString = base.GetInspectString();
            if (!string.IsNullOrEmpty(baseString))
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(baseString);
            }

            return sb.ToString().TrimEndNewlines();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
                yield return gizmo;

            if (!detonating)
            {
                Command_Action defuseCommand = new Command_Action
                {
                    defaultLabel = "HSK_DefuseCommandLabel".Translate(),
                    defaultDesc = "HSK_DefuseCommandDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", true),
                    action = () => StartDefuseJob()
                };

                if (detonating)
                {
                    defuseCommand.Disable("HSK_BombDetonating".Translate());
                }

                yield return defuseCommand;
            }
        }

        private void StartDefuseJob()
        {
            Pawn bestDefuser = Map?.mapPawns.FreeColonists
                .Where(p => !p.Dead && !p.Downed &&
                           p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Crafting).Level)
                .FirstOrDefault();

            if (bestDefuser != null)
            {
                Job defuseJob = JobMaker.MakeJob(JobDefOf.Deconstruct, this);
                defuseJob.expiryInterval = 3000;

                bestDefuser.jobs.TryTakeOrderedJob(defuseJob);

                Messages.Message("HSK_DefusingBomb".Translate(bestDefuser.Name.ToStringShort),
                    bestDefuser, MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Messages.Message("HSK_NoDefuser".Translate(),
                    MessageTypeDefOf.RejectInput);
            }
        }

        public void DefuseBomb(Pawn defuser)
        {
            int skillLevel = defuser.skills.GetSkill(SkillDefOf.Crafting).Level;
            float successChance = Mathf.Clamp01(0.3f + (skillLevel * 0.07f));

            HSK_Debug.DebugLog($"Попытка обезвреживания бомбы. Навык: {skillLevel}, шанс: {successChance:P0}");

            if (Rand.Value < successChance)
            {
                var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail("HSK_PredatorGuest");
                var extension = incidentDef?.GetModExtension<PredatorEventDefExtension>();

                Messages.Message(extension?.bombDefusedKey.Translate() ?? "HSK_BombDefused".Translate(),
                    new TargetInfo(Position, Map, false),
                    MessageTypeDefOf.PositiveEvent);

                MoteMaker.ThrowText(Position.ToVector3Shifted(), Map, "HSK_Defused".Translate(), Color.green);
                defuser.skills.Learn(SkillDefOf.Crafting, 800f);
                this.Destroy(DestroyMode.KillFinalize);

                if (eventComp != null)
                {
                    eventComp.RemoveBomb(Position);
                }

                HSK_Debug.DebugLog($"Бомба успешно обезврежена {defuser.Name} в {Position}");
            }
            else
            {
                Messages.Message("HSK_DefuseFailed".Translate(defuser.Name.ToStringShort),
                    defuser, MessageTypeDefOf.NegativeEvent);

                if (Rand.Value < 0.4f)
                {
                    HSK_Debug.DebugLog($"Обезвреживание провалено, бомба активирована (шанс: 40%)");
                    StartDetonation();
                    Messages.Message("HSK_BombTriggered".Translate(),
                        new TargetInfo(Position, Map, false),
                        MessageTypeDefOf.ThreatBig);
                }
                else
                {
                    HSK_Debug.DebugLog($"Обезвреживание провалено, бомба не активирована", true);
                }
            }
        }

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);

            if (dinfo.Def.harmsHealth && totalDamageDealt > this.MaxHitPoints * 0.3f)
            {
                if (!detonating && timerActive)
                {
                    HSK_Debug.DebugLog($"Бомба активирована уроном: {totalDamageDealt}/{MaxHitPoints}");
                    StartDetonation();
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref predatorName, "predatorName", "Unknown");
            Scribe_Values.Look(ref ticksUntilExplosion, "ticksUntilExplosion", -1);
            Scribe_Values.Look(ref timerActive, "timerActive", false);
            Scribe_Values.Look(ref detonating, "detonating", false);
            Scribe_Values.Look(ref detonationTicks, "detonationTicks", 0);
            Scribe_Values.Look(ref drawWarning, "drawWarning", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (Current.Game != null)
                {
                    eventComp = Current.Game.GetComponent<GameComponent_PredatorEvent>();
                }
            }
        }
    }

    #endregion

    #region Класс инцидента

    public class Incident_PredatorGuest : IncidentWorker
    {
        // Кэш для категории BuildingsSecurity
        private static ThingCategoryDef buildingsSecurityCategory = null;

        private ThingCategoryDef BuildingsSecurityCategory
        {
            get
            {
                if (buildingsSecurityCategory == null)
                {
                    buildingsSecurityCategory = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("BuildingsSecurity");
                    if (buildingsSecurityCategory == null)
                    {
                        HSK_Debug.DebugLog("Категория BuildingsSecurity не найдена!");
                    }
                }
                return buildingsSecurityCategory;
            }
        }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            try
            {
                Map map = (Map)parms.target;
                var settings = def?.GetModExtension<PredatorEventDefExtension>();

                if (map == null || settings == null)
                {
                    HSK_Debug.DebugWarning("Map или settings равны null");
                    return false;
                }

                if (map.mapPawns.FreeColonistsCount < settings.minColonistsForEvent)
                {
                    HSK_Debug.DebugLog($"Недостаточно колонистов: {map.mapPawns.FreeColonistsCount} < {settings.minColonistsForEvent}");
                    return false;
                }

                if (GenDate.DaysPassed < settings.minDaysForEvent)
                {
                    HSK_Debug.DebugLog($"Слишком рано: {GenDate.DaysPassed} < {settings.minDaysForEvent}");
                    return false;
                }

                // Базовая проверка шанса
                float finalChance = settings.eventBaseChance;
                if (Rand.Value > finalChance)
                {
                    HSK_Debug.DebugLog($"Событие не сработало по шансу: {finalChance:P0}");
                    return false;
                }

                Pawn predator = FindPredatorCandidate(map);
                if (predator == null)
                {
                    HSK_Debug.DebugWarning("Не найден кандидат в предатели");
                    return false;
                }

                string predatorName = predator.Name?.ToStringShort;
                if (string.IsNullOrEmpty(predatorName))
                {
                    HSK_Debug.DebugWarning("Найден предатель без имени");
                }

                var eventComp = Current.Game?.GetComponent<GameComponent_PredatorEvent>();
                if (eventComp == null)
                {
                    if (Current.Game == null)
                    {
                        HSK_Debug.DebugError("Current.Game равен null!");
                        return false;
                    }

                    eventComp = new GameComponent_PredatorEvent(Current.Game);
                    Current.Game.components.Add(eventComp);
                }

                eventComp.RegisterPredatorFaction(predatorName, predator.Faction);

                List<IntVec3> positions = FindBombPositions(map, settings);
                if (positions.Count == 0)
                {
                    HSK_Debug.DebugWarning("Не найдено подходящих позиций для бомб");
                    return false;
                }

                int bombsPlaced = PlaceBombs(map, positions, predatorName, eventComp);

                if (bombsPlaced > 0)
                {
                    // Отправляем письмо игроку
                    SendNotificationLetter(map, settings, predatorName, bombsPlaced);

                    HSK_Debug.DebugLog($"Инцидент успешно выполнен. Установлено {bombsPlaced} бомб");
                    return true;
                }

                HSK_Debug.DebugWarning($"Не удалось установить ни одной бомбы");
                return false;
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Критическая ошибка в TryExecuteWorker: {ex}");
                return false;
            }
        }

        private void SendNotificationLetter(Map map, PredatorEventDefExtension settings, string predatorName, int bombsPlaced)
        {
            try
            {
                string title = settings.letterTitleKey.Translate();
                string text = settings.letterTextKey.Translate(predatorName, bombsPlaced);

                Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.ThreatBig,
                    new TargetInfo(map.Center, map, false));

                HSK_Debug.DebugLog($"Отправлено письмо игроку: {predatorName}, бомб: {bombsPlaced}");
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при отправке письма: {ex}");
            }
        }

        private Pawn FindPredatorCandidate(Map map)
        {
            try
            {
                if (map == null) return null;

                // Поиск предателей-гостей
                var guests = map.mapPawns.AllPawnsSpawned
                    .Where(p => p != null &&
                           p.RaceProps?.Humanlike == true &&
                           !p.Dead && !p.Downed &&
                           p.guest != null &&
                           p.guest.HostFaction == Faction.OfPlayer &&
                           !p.IsPrisoner)
                    .ToList();

                if (guests.Any())
                {
                    var predator = guests.RandomElement();
                    HSK_Debug.DebugLog($"Найден предатель-гость: {predator.Name}");
                    return predator;
                }

                // Поиск предателей-колонистов (с низким настроением)
                var colonistPredator = map.mapPawns.FreeColonists
                    .Where(p => p != null &&
                           !p.Dead && !p.Downed &&
                           p.needs?.mood?.CurLevel < 0.3f)
                    .RandomElementWithFallback();

                if (colonistPredator != null)
                {
                    HSK_Debug.DebugLog($"Найден предатель-колонист: {colonistPredator.Name}");
                    return colonistPredator;
                }

                HSK_Debug.DebugLog("Не найден подходящий кандидат в предатели");
                return null;
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при поиске предателя: {ex}");
                return null;
            }
        }

        /// <summary>
        /// ИСПРАВЛЕННЫЙ МЕТОД: Ставит по 1 бомбе у каждой доступной цели и выводит отладочную информацию
        /// </summary>
        private List<IntVec3> FindBombPositions(Map map, PredatorEventDefExtension settings)
        {
            List<IntVec3> positions = new List<IntVec3>();

            try
            {
                if (!HSK_Debug.LogTargetSelection)
                {
                    // Быстрая версия без отладки
                    var priorityTargets = map.listerBuildings.allBuildingsColonist
                        .Where(b => PredatorEventDefExtension.IsPriorityTarget(b.def.defName))
                        .ToList();

                    foreach (var building in priorityTargets)
                    {
                        if (positions.Count >= settings.maxBombs) break;

                        IntVec3 bombCell = FindAdjacentCell(building.Position, map, 2, building.Rotation);
                        if (bombCell.IsValid && !positions.Contains(bombCell))
                        {
                            positions.Add(bombCell);
                        }
                    }

                    return positions.Take(Math.Min(positions.Count, settings.maxBombs)).ToList();
                }

                // Полная версия с отладкой
                HSK_Debug.DebugLog($"=== ОТЛАДКА: ПОИСК ЦЕЛЕЙ ДЛЯ БОМБ ===");
                HSK_Debug.DebugLog($"Всего строений в колонии: {map.listerBuildings.allBuildingsColonist.Count}");

                // Получаем ВСЕ доступные цели из приоритетного списка
                var priorityTargetList = map.listerBuildings.allBuildingsColonist
                    .Where(b => PredatorEventDefExtension.IsPriorityTarget(b.def.defName))
                    .OrderBy(b => b.def.defName)
                    .ToList();

                if (!priorityTargetList.Any())
                {
                    HSK_Debug.DebugWarning($"НИ ОДНОЙ ЦЕЛИ ИЗ ПРИОРИТЕТНОГО СПИСКА НЕ НАЙДЕНО!");

                    if (HSK_Debug.ShowVerboseLogs)
                    {
                        HSK_Debug.DebugLog($"Список всех строений в колонии:");
                        int counter = 0;
                        foreach (var building in map.listerBuildings.allBuildingsColonist.Take(20))
                        {
                            HSK_Debug.DebugLog($"  {counter++}: {building.def.defName} в {building.Position}", true);
                        }
                    }

                    return positions;
                }

                HSK_Debug.DebugLog($"Найдено целей из приоритетного списка: {priorityTargetList.Count}");

                // Выводим информацию по каждой цели
                int targetNumber = 0;
                foreach (var building in priorityTargetList)
                {
                    targetNumber++;
                    HSK_Debug.DebugLog($"Цель #{targetNumber}: {building.def.defName} в {building.Position}");

                    // Пробуем найти позицию для бомбы (радиус увеличен до 2)
                    IntVec3 bombCell = FindAdjacentCell(building.Position, map, 2, building.Rotation);

                    if (bombCell.IsValid)
                    {
                        if (!positions.Contains(bombCell))
                        {
                            positions.Add(bombCell);
                            float distance = building.Position.DistanceTo(bombCell);
                            HSK_Debug.DebugLog($"  ✓ Бомба назначена в {bombCell} (расстояние от цели: {distance:F1} клеток)");
                        }
                        else
                        {
                            HSK_Debug.DebugLog($"  ✗ Позиция {bombCell} уже занята другой бомбой");
                        }
                    }
                    else
                    {
                        HSK_Debug.DebugLog($"  ✗ Не удалось найти валидную позицию для бомбы в радиусе 2 клеток от цели");
                    }
                }

                HSK_Debug.DebugLog($"Итоговое количество позиций для бомб: {positions.Count}");

                // ДОБАВЛЯЕМ БОМБЫ У ВСЕХ ОСТАЛЬНЫХ ЦЕЛЕЙ (BuildingsSecurity) если нужно больше бомб
                if (positions.Count < settings.minBombs)
                {
                    HSK_Debug.DebugLog($"Мало бомб ({positions.Count}), ищем дополнительные цели в категории BuildingsSecurity");

                    var securityBuildings = GetSecurityBuildings(map);
                    HSK_Debug.DebugLog($"Найдено строений безопасности: {securityBuildings.Count}");

                    foreach (var building in securityBuildings)
                    {
                        if (positions.Count >= settings.maxBombs) break;

                        IntVec3 bombCell = FindAdjacentCell(building.Position, map, 2, building.Rotation);
                        if (bombCell.IsValid && !positions.Contains(bombCell))
                        {
                            positions.Add(bombCell);
                            HSK_Debug.DebugLog($"  + Доп. бомба у BuildingsSecurity: {building.def.defName} в {bombCell}");
                        }
                    }
                }

                HSK_Debug.DebugLog($"=== ОТЛАДКА ЗАВЕРШЕНА ===");
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при поиске позиций для бомб: {ex}");
            }

            // Ограничиваем максимальным количеством бомб, но стараемся поставить как можно больше
            return positions.Take(Math.Min(positions.Count, settings.maxBombs)).ToList();
        }

        private List<Building> GetSecurityBuildings(Map map)
        {
            var securityBuildings = new List<Building>();

            if (BuildingsSecurityCategory == null)
                return securityBuildings;

            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                if (IsInSecurityCategory(building.def))
                {
                    securityBuildings.Add(building);
                }
            }

            return securityBuildings;
        }

        private bool IsInSecurityCategory(ThingDef def)
        {
            if (BuildingsSecurityCategory == null)
                return false;

            return def.thingCategories != null &&
                   def.thingCategories.Contains(BuildingsSecurityCategory);
        }

        /// <summary>
        /// ИСПРАВЛЕННЫЙ МЕТОД: Поиск позиции для бомбы в радиусе до 2 клеток от цели
        /// </summary>
        private IntVec3 FindAdjacentCell(IntVec3 center, Map map, int maxRadius, Rot4 buildingRotation)
        {
            try
            {
                // Проверяем все клетки в радиусе от 1 до maxRadius (теперь до 2 клеток)
                for (int radius = 1; radius <= maxRadius; radius++)
                {
                    // Получаем все клетки в текущем радиусе
                    var cellsInRadius = GenRadial.RadialCellsAround(center, radius, false)
                        .Where(cell => IsValidBombCell(cell, map))
                        .ToList();

                    if (cellsInRadius.Any())
                    {
                        // Сначала проверяем более близкие позиции (приоритет клеткам на том же расстоянии от центра)
                        // Сортируем по углу для разнообразия
                        return cellsInRadius.OrderBy(c =>
                            Mathf.Abs((c.x - center.x) + (c.z - center.z)) // Предпочтение диагональным клеткам
                        ).FirstOrDefault();
                    }
                }

                // Если в радиусе 2 клеток не нашлось места, пробуем радиальные клетки вокруг
                var radialCells = GenRadial.RadialCellsAround(center, maxRadius, true)
                    .Where(cell => IsValidBombCell(cell, map))
                    .ToList();

                if (radialCells.Any())
                {
                    return radialCells.RandomElement();
                }

                return IntVec3.Invalid;
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка в FindAdjacentCell: {ex}");
                return IntVec3.Invalid;
            }
        }

        /// <summary>
        /// Проверка валидности клетки для бомбы (ИСПРАВЛЕННАЯ)
        /// </summary>
        private bool IsValidBombCell(IntVec3 cell, Map map)
        {
            try
            {
                // Базовые проверки
                if (!cell.InBounds(map))
                {
                    return false;
                }

                // Бомба должна быть на проходимой поверхности
                if (!cell.Standable(map))
                {
                    return false;
                }

                if (cell.Fogged(map))
                {
                    return false;
                }

                // Проверяем, нет ли здесь другого строения
                Building buildingAtCell = cell.GetEdifice(map);
                if (buildingAtCell != null && buildingAtCell.def.category == ThingCategory.Building)
                {
                    // Разрешаем только декоративные или мелкие строения
                    if (buildingAtCell.def.passability != Traversability.Standable ||
                        buildingAtCell.def.defName.Contains("Plant") || // Разрешаем растения
                        buildingAtCell.def.defName.Contains("Flower") ||
                        buildingAtCell.def.defName.Contains("Grass"))
                    {
                        // Это растение или декорация - разрешаем
                    }
                    else
                    {
                        // Это серьёзное строение - запрещаем
                        return false;
                    }
                }

                // Проверяем, нет ли здесь двери
                if (cell.GetDoor(map) != null)
                {
                    return false;
                }

                // Проверяем, нет ли здесь большого предмета
                var firstItem = cell.GetFirstItem(map);
                if (firstItem != null && !firstItem.def.smallVolume)
                {
                    return false;
                }

                // Проверяем, не находится ли клетка под толстой крышей
                if (cell.Roofed(map))
                {
                    var roof = cell.GetRoof(map);
                    if (roof != null && roof.isThickRoof)
                    {
                        return false;
                    }
                }

                // Проверяем, что клетка не в глубокой воде/болоте
                TerrainDef terrain = cell.GetTerrain(map);
                if (terrain != null)
                {
                    // Запрещаем непроходимые поверхности
                    if (terrain.passability == Traversability.Impassable)
                    {
                        return false;
                    }

                    // Запрещаем глубокую воду
                    if (terrain.defName.Contains("Water") ||
                        terrain.defName.Contains("Marsh") ||
                        terrain.defName.Contains("Mud"))
                    {
                        // Проверяем глубину через проходимость
                        if (terrain.passability != Traversability.Standable)
                        {
                            return false;
                        }
                    }
                }

                // Проверяем, что клетка не заблокирована пешкой
                if (cell.GetFirstPawn(map) != null)
                {
                    return false;
                }

                // Проверяем, что клетка не слишком близко к другим бомбам (минимум 3 клетки между бомбами)
                var nearbyBombs = map.listerBuildings.allBuildingsColonist
                    .OfType<Building_PredatorBomb>()
                    .Where(b => b.Position.DistanceTo(cell) < 3f)
                    .Any();

                if (nearbyBombs)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Установка бомб
        /// </summary>
        private int PlaceBombs(Map map, List<IntVec3> positions, string predatorName, GameComponent_PredatorEvent eventComp)
        {
            int placed = 0;
            ThingDef bombDef = DefDatabase<ThingDef>.GetNamedSilentFail("HSK_PredatorBomb");

            if (bombDef == null)
            {
                HSK_Debug.DebugError("Def HSK_PredatorBomb не найден!");
                return 0;
            }

            if (HSK_Debug.LogBombPlacement)
            {
                HSK_Debug.DebugLog($"Начинаем установку бомб на {positions.Count} позиций");
            }

            foreach (var position in positions)
            {
                try
                {
                    if (!IsValidBombCell(position, map))
                    {
                        HSK_Debug.DebugLog($"Позиция {position} не подходит для бомбы (повторная проверка)", true);
                        continue;
                    }

                    // Удаляем любые мелкие предметы с клетки
                    List<Thing> thingsAtPos = position.GetThingList(map).ToList();
                    foreach (var thing in thingsAtPos)
                    {
                        if (thing.def.category == ThingCategory.Item && thing.def.smallVolume)
                        {
                            thing.Destroy(DestroyMode.Vanish);
                            HSK_Debug.DebugLog($"Удален предмет {thing.def.defName} с позиции {position}", true);
                        }
                    }

                    // Удаляем растения если они есть
                    var plant = position.GetPlant(map);
                    if (plant != null)
                    {
                        plant.Destroy(DestroyMode.Vanish);
                        HSK_Debug.DebugLog($"Удалено растение {plant.def.defName} с позиции {position}", true);
                    }

                    // Удаляем мелкие декоративные строения
                    var building = position.GetEdifice(map);
                    if (building != null &&
                        building.def.category == ThingCategory.Building &&
                        (building.def.defName.Contains("Plant") ||
                         building.def.defName.Contains("Flower") ||
                         building.def.defName.Contains("Grass")))
                    {
                        building.Destroy(DestroyMode.Vanish);
                        HSK_Debug.DebugLog($"Удалено декоративное строение {building.def.defName} с позиции {position}", true);
                    }

                    Building_PredatorBomb bomb = (Building_PredatorBomb)ThingMaker.MakeThing(bombDef);

                    // Устанавливаем бомбу
                    GenSpawn.Spawn(bomb, position, map, Rot4.North, WipeMode.Vanish);

                    // Регистрируем бомбу
                    eventComp.RegisterBomb(position, predatorName);
                    placed++;

                    // Визуальный эффект
                    FleckMaker.ThrowMetaPuffs(new TargetInfo(position, map, false));

                    if (HSK_Debug.LogBombPlacement)
                    {
                        HSK_Debug.DebugLog($"✓ Бомба #{placed} установлена в {position}");
                    }
                }
                catch (Exception ex)
                {
                    HSK_Debug.DebugError($"Ошибка установки бомбы в {position}: {ex.Message}");
                    if (ex.InnerException != null)
                        HSK_Debug.DebugError($"Внутренняя ошибка: {ex.InnerException.Message}");
                }
            }

            HSK_Debug.DebugLog($"Установлено {placed} бомб из {positions.Count} запланированных для предателя {predatorName}");

            // Визуальная обратная связь
            if (placed > 0)
            {
                Messages.Message("HSK_BombsPlaced".Translate(predatorName, placed),
                    MessageTypeDefOf.ThreatSmall);
            }

            return placed;
        }
    }

    #endregion
} */

namespace HSK.Watcher.Content.Incident
{
    #region Конфигурация отладки

    public static class HSK_Debug
    {
        public static bool EnableDebugLogging = false;
        public static bool ShowVerboseLogs = false;
        public static bool LogTargetSelection = true;
        public static bool LogBombPlacement = true;
        public static bool LogRaidEvents = true;

        public static void DebugLog(string message, bool isVerbose = false)
        {
            if (!EnableDebugLogging) return;
            if (isVerbose && !ShowVerboseLogs) return;

            Verse.Log.Message($"[HSK-DEBUG] {message}");
        }

        public static void DebugWarning(string message)
        {
            if (!EnableDebugLogging) return;
            Verse.Log.Warning($"[HSK-DEBUG] {message}");
        }

        public static void DebugError(string message)
        {
            Verse.Log.Error($"[HSK-DEBUG] {message}");
        }

        public static void ToggleDebug()
        {
            EnableDebugLogging = !EnableDebugLogging;
            string status = EnableDebugLogging ? "ВКЛЮЧЕНА" : "ОТКЛЮЧЕНА";
            Verse.Log.Message($"[HSK] Отладка {status}");

            if (EnableDebugLogging)
            {
                Verse.Log.Message($"[HSK] Подробные логи: {(ShowVerboseLogs ? "ВКЛ" : "ВЫКЛ")}");
                Verse.Log.Message($"[HSK] Логи целей: {(LogTargetSelection ? "ВКЛ" : "ВЫКЛ")}");
                Verse.Log.Message($"[HSK] Логи бомб: {(LogBombPlacement ? "ВКЛ" : "ВЫКЛ")}");
                Verse.Log.Message($"[HSK] Логи рейдов: {(LogRaidEvents ? "ВКЛ" : "ВЫКЛ")}");
            }
        }

        public static void ToggleVerbose()
        {
            ShowVerboseLogs = !ShowVerboseLogs;
            string status = ShowVerboseLogs ? "ВКЛЮЧЕНЫ" : "ОТКЛЮЧЕНЫ";
            Verse.Log.Message($"[HSK] Подробные логи {status}");
        }
    }

    #endregion

    #region Расширение настроек инцидента

    public class PredatorEventDefExtension : DefModExtension
    {
        public DamageDef explosionDamageType = null;
        public float explosionRadius = 3.9f;
        public int explosionDamage = 2500;
        public float armorPenetration = 1f;
        public IntRange bombTimerRange = new IntRange(750, 900);
        public int minColonistsForEvent = 4;
        public int minDaysForEvent = 20;
        public float eventBaseChance = 0.8f;
        public int minBombs = 1;
        public int maxBombs = 6;

        public int colonyAgeThreshold = 1800;
        public float enhancedRaidMultiplier = 2.0f;
        public float dropPodChance = 0.6f;
        public int maxEnhancedRaiders = 15;

        public bool allowTraderPredators = true;
        public float traderPredatorChance = 0.15f;
        public bool traderLeavesBeforeExplosion = true;
        public float traderEscapeDelayHours = 4f;

        public string letterTitleKey = "PredatorEvent_LetterTitle";
        public string letterTextKey = "PredatorEvent_LetterText";
        public string bombDefusedKey = "PredatorEvent_BombDefused";
        public string bombExplodedKey = "PredatorEvent_BombExploded";
        public string defuseCommandLabelKey = "PredatorEvent_DefuseCommandLabel";
        public string defuseCommandDescKey = "PredatorEvent_DefuseCommandDesc";
        public string enhancedRaidMessageKey = "PredatorEvent_EnhancedRaidMessage";
        public string traderPredatorLetterKey = "PredatorEvent_TraderLetter";

        private static readonly HashSet<string> PriorityTargetDefNames = new HashSet<string>
        {
            "OrbitalTradeBeacon", "Turret_Oerlikonautocannon", "Turret_Minigun",
            "Turret_M2Browning", "Turret_SentryRocketLauncher", "Turret_MiniTurret",
            "NavalGunTurretGun", "AvengerTurretGun", "WoodFiredGenerator",
            "CoalPowerPlant", "ChemfuelPoweredGenerator", "TeslaLightningPower",
            "PlasmaGeothermalPlant", "PlasmaGen", "ReactorCoreA", "ReactorCoreB",
            "ReactorCoreC", "CoolingRadiator", "CoolingWater", "CoolingTower",
            "Transformer", "Battery", "OilWell", "DeepOilWell", "OilStorage",
            "FuelStorage", "FuelPowerplant", "LargeFuelPowerplant"
        };

        public DamageDef GetResolvedDamageDef()
        {
            if (explosionDamageType != null)
                return explosionDamageType;

            return DefDatabase<DamageDef>.GetNamedSilentFail("Bomb") ?? DamageDefOf.Bomb;
        }

        public static bool IsPriorityTarget(string defName)
        {
            return PriorityTargetDefNames.Contains(defName);
        }
    }

    #endregion

    #region Трекер событий

    public class GameComponent_PredatorEvent : GameComponent
    {
        private Dictionary<IntVec3, string> bombPredators;
        private Dictionary<string, bool> predatorRevealed;
        private Dictionary<string, Faction> predatorOriginalFactions;
        private int raidTriggerCounter = 0;
        private const int MAX_RAIDS_PER_EVENT = 2;
        private bool enhancedRaidTriggered = false;

        private Dictionary<string, bool> traderPredators;
        private Dictionary<string, int> traderEscapeTimers;

        public GameComponent_PredatorEvent(Game game)
        {
            InitializeDictionaries();
        }

        private void InitializeDictionaries()
        {
            bombPredators = new Dictionary<IntVec3, string>();
            predatorRevealed = new Dictionary<string, bool>();
            predatorOriginalFactions = new Dictionary<string, Faction>();
            traderPredators = new Dictionary<string, bool>();
            traderEscapeTimers = new Dictionary<string, int>();
        }

        public void RegisterTraderPredator(string traderName)
        {
            if (traderPredators == null)
                traderPredators = new Dictionary<string, bool>();

            if (!string.IsNullOrEmpty(traderName) && !traderPredators.ContainsKey(traderName))
            {
                traderPredators[traderName] = true;

                var settings = GetSettings();
                if (settings != null && settings.traderLeavesBeforeExplosion)
                {
                    int escapeTicks = Mathf.RoundToInt(settings.traderEscapeDelayHours * 2500);
                    traderEscapeTimers[traderName] = Find.TickManager.TicksGame + escapeTicks;
                }

                HSK_Debug.DebugLog($"Зарегистрирован предатель-торговец: {traderName}", false);
            }
        }

        public bool IsTraderPredator(string traderName)
        {
            if (traderPredators == null) return false;
            return traderPredators.TryGetValue(traderName, out bool isPredator) && isPredator;
        }

        public bool ShouldTraderLeave(string traderName)
        {
            var settings = GetSettings();
            if (settings == null || !settings.traderLeavesBeforeExplosion) return false;

            if (traderEscapeTimers.TryGetValue(traderName, out int leaveTime))
            {
                return Find.TickManager.TicksGame >= leaveTime;
            }
            return false;
        }

        public void RemoveTraderPredator(string traderName)
        {
            if (traderPredators != null && traderPredators.ContainsKey(traderName))
            {
                traderPredators.Remove(traderName);
                traderEscapeTimers?.Remove(traderName);
                HSK_Debug.DebugLog($"Удалена информация о торговце-предателе: {traderName}", true);
            }
        }

        private PredatorEventDefExtension GetSettings()
        {
            var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail("HSK_PredatorGuest");
            return incidentDef?.GetModExtension<PredatorEventDefExtension>();
        }

        public void RegisterBomb(IntVec3 position, string predatorName)
        {
            if (bombPredators == null) InitializeDictionaries();

            bombPredators[position] = predatorName;
            if (!predatorRevealed.ContainsKey(predatorName))
            {
                predatorRevealed[predatorName] = false;
            }

            HSK_Debug.DebugLog($"Зарегистрирована бомба в {position} для предателя {predatorName}", false);
        }

        public void RegisterPredatorFaction(string predatorName, Faction faction)
        {
            if (predatorOriginalFactions == null)
            {
                predatorOriginalFactions = new Dictionary<string, Faction>();
            }

            if (predatorName == null)
            {
                HSK_Debug.DebugWarning("Попытка зарегистрировать фракцию для предателя с null именем");
                return;
            }

            if (faction == null)
            {
                HSK_Debug.DebugWarning($"Попытка зарегистрировать null фракцию для предателя: {predatorName}");
                predatorOriginalFactions[predatorName] = null;
            }
            else
            {
                predatorOriginalFactions[predatorName] = faction;
                HSK_Debug.DebugLog($"Зарегистрирована фракция {faction.Name} для предателя {predatorName}", true);
            }
        }

        public Faction GetPredatorOriginalFaction(string predatorName)
        {
            if (predatorOriginalFactions == null) return null;
            return predatorOriginalFactions.TryGetValue(predatorName, out Faction faction) ? faction : null;
        }

        public bool HasBombAt(IntVec3 position)
        {
            if (bombPredators == null) return false;
            return bombPredators.ContainsKey(position);
        }

        public string GetPredatorForBomb(IntVec3 position)
        {
            if (bombPredators == null) return null;
            return bombPredators.TryGetValue(position, out string name) ? name : null;
        }

        public void RemoveBomb(IntVec3 position)
        {
            if (bombPredators == null) return;

            if (bombPredators.ContainsKey(position))
            {
                string predatorName = bombPredators[position];
                bombPredators.Remove(position);
                HSK_Debug.DebugLog($"Удалена бомба в {position} (предатель: {predatorName})", true);
            }
        }

        public void RevealPredator(string predatorName, Map map)
        {
            if (predatorRevealed == null) return;

            if (predatorRevealed.ContainsKey(predatorName) && !predatorRevealed[predatorName])
            {
                predatorRevealed[predatorName] = true;
                HSK_Debug.DebugLog($"Раскрытие предателя: {predatorName}");

                Pawn predator = FindPredatorPawn(predatorName, map);
                if (predator != null && !predator.Dead && !predator.Downed)
                {
                    MakePredatorHostile(predator);
                }
                else
                {
                    HSK_Debug.DebugWarning($"Не найден живой предатель {predatorName} для раскрытия");
                }
            }
        }

        private Pawn FindPredatorPawn(string predatorName, Map map)
        {
            if (map == null) return null;

            return map.mapPawns.AllPawnsSpawned
                .Where(p => p.Name != null && p.Name.ToStringShort == predatorName)
                .FirstOrDefault();
        }

        private void MakePredatorHostile(Pawn predator)
        {
            try
            {
                if (predator == null) return;

                Faction originalFaction = predator.Faction;
                RegisterPredatorFaction(predator.Name?.ToStringShort ?? "Unknown", originalFaction);

                Faction hostileFaction = Find.FactionManager.AllFactions
                    .Where(f => f != null && f.HostileTo(Faction.OfPlayer) && !f.def.hidden)
                    .FirstOrDefault();

                if (hostileFaction == null)
                {
                    hostileFaction = Faction.OfAncientsHostile;
                }

                predator.SetFaction(hostileFaction);

                Pawn target = FindTargetForPredator(predator);
                if (target != null)
                {
                    if (predator.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                    {
                        Job attackJob = null;
                        if (predator.equipment?.Primary != null)
                        {
                            var compEquip = predator.equipment.Primary.TryGetComp<CompEquippable>();
                            if (compEquip != null && compEquip.PrimaryVerb is Verb_Shoot)
                            {
                                attackJob = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
                            }
                            else
                            {
                                attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
                            }
                        }
                        else
                        {
                            attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
                        }

                        if (attackJob != null)
                        {
                            predator.jobs.StartJob(attackJob, JobCondition.InterruptForced);
                        }
                    }

                    if (predator.mindState != null)
                    {
                        predator.mindState.mentalStateHandler.TryStartMentalState(
                            MentalStateDefOf.Manhunter,
                            "HSK_TraitorRevealed".Translate()
                        );
                    }
                }

                HSK_Debug.DebugLog($"Предатель {predator.Name} раскрыт! Фракция: {hostileFaction.Name}");
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при раскрытии предателя: {ex}");
            }
        }

        private Pawn FindTargetForPredator(Pawn predator)
        {
            if (predator?.Map == null) return null;

            return predator.Map.mapPawns.FreeColonists
                .Where(p => !p.Dead && !p.Downed)
                .OrderBy(p => p.Position.DistanceTo(predator.Position))
                .FirstOrDefault();
        }

        public bool CanTriggerRaid(bool isEnhanced = false)
        {
            if (raidTriggerCounter >= MAX_RAIDS_PER_EVENT)
            {
                HSK_Debug.DebugLog($"Достигнут лимит рейдов: {raidTriggerCounter}/{MAX_RAIDS_PER_EVENT}", true);
                return false;
            }

            if (isEnhanced && enhancedRaidTriggered)
            {
                HSK_Debug.DebugLog("Усиленный рейд уже был запущен", true);
                return false;
            }

            raidTriggerCounter++;
            if (isEnhanced)
                enhancedRaidTriggered = true;

            HSK_Debug.DebugLog($"Рейд разрешен. Счетчик: {raidTriggerCounter}, Усиленный: {isEnhanced}", true);
            return true;
        }

        public void ResetRaidCounter()
        {
            HSK_Debug.DebugLog($"Сброс счетчика рейдов: {raidTriggerCounter} → 0", true);
            raidTriggerCounter = 0;
            enhancedRaidTriggered = false;
        }

        public bool IsPredatorRevealed(string predatorName)
        {
            if (predatorRevealed == null) return false;
            return predatorRevealed.TryGetValue(predatorName, out bool revealed) && revealed;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref bombPredators, "bombPredators", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref predatorRevealed, "predatorRevealed", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref predatorOriginalFactions, "predatorOriginalFactions", LookMode.Value, LookMode.Reference);
            Scribe_Collections.Look(ref traderPredators, "traderPredators", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref traderEscapeTimers, "traderEscapeTimers", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref raidTriggerCounter, "raidTriggerCounter", 0);
            Scribe_Values.Look(ref enhancedRaidTriggered, "enhancedRaidTriggered", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (bombPredators == null) InitializeDictionaries();
            }
        }
    }

    #endregion

    #region Класс бомбы (упрощенный)

    [StaticConstructorOnStartup]
    public class Building_PredatorBomb : Building
    {
        private string predatorName = "Unknown";
        private GameComponent_PredatorEvent eventComp;
        private bool detonating = false;
        private int detonationTicks = 0;
        private int ticksUntilExplosion = -1;
        private bool timerActive = false;

        private static readonly Material WarningOverlay = SolidColorMaterials.SimpleSolidColorMaterial(new Color(1f, 0.2f, 0.1f, 0.3f));
        private int lastPulseTick = 0;
        private const int PULSE_INTERVAL = 15;
        private float currentPulse = 0f;
        private bool drawWarning = false;

        // Public свойство для доступа к состоянию детонации
        public bool IsDetonating => detonating;

        static Building_PredatorBomb()
        {
            InitTranslations();
        }

        private static void InitTranslations()
        {
            if ("HSK_BombDetonating".CanTranslate())
            {
                return;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (Current.Game != null)
            {
                eventComp = Current.Game.GetComponent<GameComponent_PredatorEvent>();

                if (eventComp == null)
                {
                    eventComp = new GameComponent_PredatorEvent(Current.Game);
                    Current.Game.components.Add(eventComp);
                }
            }

            if (!respawningAfterLoad)
            {
                var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail("HSK_PredatorGuest");
                var extension = incidentDef?.GetModExtension<PredatorEventDefExtension>();

                if (extension != null)
                {
                    ticksUntilExplosion = extension.bombTimerRange.RandomInRange;
                    timerActive = true;
                    HSK_Debug.DebugLog($"Бомба создана. Таймер: {ticksUntilExplosion} тиков ({ticksUntilExplosion / 60f:F1} минут)", true);
                }
                else
                {
                    ticksUntilExplosion = 750;
                    timerActive = true;
                    HSK_Debug.DebugLog($"Бомба создана. Таймер по умолчанию: {ticksUntilExplosion} тиков", true);
                }

                if (eventComp != null)
                {
                    predatorName = eventComp.GetPredatorForBomb(Position) ?? "Unknown";
                }
            }
            else
            {
                HSK_Debug.DebugLog($"Бомба загружена в {Position}. Предатель: {predatorName}", true);
            }
        }

        protected override void Tick()  // <-- изменено с public на protected
        {
            base.Tick();

            if (timerActive && ticksUntilExplosion > 0 && !detonating)
            {
                ticksUntilExplosion--;

                if (ticksUntilExplosion < 300)
                {
                    drawWarning = true;
                    UpdatePulseEffect();
                }
                else
                {
                    drawWarning = false;
                }

                if (ticksUntilExplosion <= 0)
                {
                    StartDetonation();
                }
            }

            if (detonating)
            {
                detonationTicks++;

                if (detonationTicks % 3 == 0)
                {
                    float progress = detonationTicks / 15f;
                    FleckMaker.ThrowSmoke(Position.ToVector3Shifted(), Map, 0.5f * progress);
                }

                if (detonationTicks >= 15)
                {
                    Detonate();
                }
            }
        }

        private void UpdatePulseEffect()
        {
            if (Find.TickManager.TicksGame - lastPulseTick >= PULSE_INTERVAL)
            {
                lastPulseTick = Find.TickManager.TicksGame;
                currentPulse = Mathf.Sin(Find.TickManager.TicksGame * 0.1f) * 0.5f + 0.5f;

                if (ticksUntilExplosion < 150 && ticksUntilExplosion % 30 == 0)
                {
                    float minutes = ticksUntilExplosion / 60f;
                    MoteMaker.ThrowText(Position.ToVector3Shifted(), Map,
                        minutes.ToString("F0") + " " + "HSK_MinutesAbbr".Translate(), Color.red);
                }
            }
        }

        public void StartDetonation()
        {
            if (!detonating)
            {
                detonating = true;
                detonationTicks = 0;
                drawWarning = false;

                HSK_Debug.DebugLog($"Бомба в {Position} начинает детонацию");

                Messages.Message("HSK_BombFuseLit".Translate(),
                    new TargetInfo(Position, Map, false),
                    MessageTypeDefOf.ThreatBig);
            }
        }

        private void Detonate()
        {
            try
            {
                HSK_Debug.DebugLog($"Бомба в {Position} взрывается. Предатель: {predatorName}");

                var explosiveComp = GetComp<CompExplosive>();
                if (explosiveComp != null)
                {
                    explosiveComp.StartWick();
                }
                else
                {
                    GenExplosion.DoExplosion(
                        center: Position,
                        map: Map,
                        radius: 3.9f,
                        damType: DamageDefOf.Bomb,
                        instigator: null,
                        damAmount: 2600,
                        armorPenetration: 1f,
                        explosionSound: null
                    );
                }

                var eventComp = Current.Game?.GetComponent<GameComponent_PredatorEvent>();
                if (!string.IsNullOrEmpty(predatorName) && eventComp != null)
                {
                    bool isTraderPredator = eventComp.IsTraderPredator(predatorName);

                    if (isTraderPredator)
                    {
                        HSK_Debug.DebugLog($"Взорвана бомба торговца-предателя: {predatorName}", true);

                        eventComp.RemoveTraderPredator(predatorName);

                        Messages.Message("HSK_TraderBombExploded".Translate(predatorName),
                            new TargetInfo(Position, Map, false),
                            MessageTypeDefOf.ThreatBig);
                    }
                    else
                    {
                        eventComp.RevealPredator(predatorName, Map);
                    }
                }

                var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail("HSK_PredatorGuest");
                var settings = incidentDef?.GetModExtension<PredatorEventDefExtension>();

                if (ShouldStartEnhancedRaid(Map, settings))
                {
                    StartEnhancedRaid(Map, settings);
                }
                else
                {
                    StartSingleRaidAfterExplosion();
                }

                if (eventComp != null)
                {
                    eventComp.RemoveBomb(Position);
                }
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при детонации: {ex}");
            }
        }

        private bool ShouldStartEnhancedRaid(Map map, PredatorEventDefExtension settings)
        {
            if (map == null || settings == null) return false;

            bool colonyIsOld = GenDate.DaysPassed >= settings.colonyAgeThreshold;

            if (HSK_Debug.LogRaidEvents)
            {
                HSK_Debug.DebugLog($"Проверка усиленного рейда: возраст {GenDate.DaysPassed}/{settings.colonyAgeThreshold}");
            }

            return colonyIsOld;
        }

        private void StartEnhancedRaid(Map map, PredatorEventDefExtension settings)
        {
            if (map == null) return;

            if (eventComp != null && !eventComp.CanTriggerRaid(true))
            {
                HSK_Debug.DebugLog("Усиленный рейд уже был запущен", true);
                return;
            }

            Faction hostileFaction = Find.FactionManager.AllFactions
                .Where(f => f.HostileTo(Faction.OfPlayer) && !f.def.hidden && f.def.humanlikeFaction)
                .FirstOrDefault();

            if (hostileFaction == null)
            {
                hostileFaction = Faction.OfAncientsHostile;
            }

            float basePoints = StorytellerUtility.DefaultThreatPointsNow(map);
            float enhancedPoints = basePoints * settings.enhancedRaidMultiplier;
            enhancedPoints = Mathf.Clamp(enhancedPoints, 500f, 5000f);

            bool useDropPods = Rand.Value < settings.dropPodChance;
            PawnsArrivalModeDef arrivalMode = useDropPods ?
                PawnsArrivalModeDefOf.CenterDrop :
                PawnsArrivalModeDefOf.EdgeWalkIn;

            IncidentParms parms = new IncidentParms
            {
                target = map,
                points = enhancedPoints,
                faction = hostileFaction,
                raidStrategy = RaidStrategyDefOf.ImmediateAttack,
                raidArrivalMode = arrivalMode,
                forced = true,
                raidForceOneDowned = false
            };

            try
            {
                IncidentDef raidDef = IncidentDefOf.RaidEnemy;
                if (raidDef.Worker.CanFireNow(parms))
                {
                    bool raidSuccess = raidDef.Worker.TryExecute(parms);
                    if (raidSuccess)
                    {
                        string raidType = useDropPods ? "десант" : "усиленный рейд";
                        Messages.Message(
                            settings.enhancedRaidMessageKey.Translate(raidType, hostileFaction.Name),
                            MessageTypeDefOf.ThreatBig
                        );

                        PostProcessEnhancedRaid(map, hostileFaction, enhancedPoints, useDropPods);

                        if (HSK_Debug.LogRaidEvents)
                        {
                            HSK_Debug.DebugLog($"Усиленный рейд ({raidType}) запущен: {enhancedPoints:F0} очков, фракция: {hostileFaction.Name}");
                        }
                    }
                    else
                    {
                        if (eventComp != null)
                        {
                            eventComp.ResetRaidCounter();
                        }
                        HSK_Debug.DebugLog("Не удалось запустить усиленный рейд", true);
                    }
                }
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при запуске усиленного рейда: {ex}");
                if (eventComp != null)
                {
                    eventComp.ResetRaidCounter();
                }
            }
        }

        private void PostProcessEnhancedRaid(Map map, Faction faction, float points, bool dropPods)
        {
            try
            {
                var currentRaiders = map.mapPawns.AllPawnsSpawned
                    .Where(p => p.Faction == faction && p.HostileTo(Faction.OfPlayer))
                    .ToList();

                if (HSK_Debug.LogRaidEvents)
                {
                    HSK_Debug.DebugLog($"Текущее количество рейдеров: {currentRaiders.Count}", true);
                }

                var settings = DefDatabase<IncidentDef>.GetNamedSilentFail("HSK_PredatorGuest")
                    ?.GetModExtension<PredatorEventDefExtension>();

                if (settings == null) return;

                if (currentRaiders.Count < settings.maxEnhancedRaiders / 2)
                {
                    int raidersToAdd = Mathf.Min(
                        settings.maxEnhancedRaiders - currentRaiders.Count,
                        Mathf.CeilToInt(points / 100f)
                    );

                    if (raidersToAdd > 0)
                    {
                        AddExtraRaiders(map, faction, raidersToAdd, dropPods);
                        if (HSK_Debug.LogRaidEvents)
                        {
                            HSK_Debug.DebugLog($"Добавлено дополнительных рейдеров: {raidersToAdd}", true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка в постобработке рейда: {ex}");
            }
        }

        private void AddExtraRaiders(Map map, Faction faction, int count, bool dropPods)
        {
            try
            {
                PawnGroupMakerParms parms = new PawnGroupMakerParms
                {
                    faction = faction,
                    points = count * 100f,
                    groupKind = PawnGroupKindDefOf.Combat,
                    tile = map.Tile,
                    generateFightersOnly = true,
                    raidStrategy = RaidStrategyDefOf.ImmediateAttack
                };

                List<Pawn> extraRaiders = PawnGroupMakerUtility.GeneratePawns(parms).Take(count).ToList();

                if (extraRaiders.Any())
                {
                    IntVec3 spawnSpot;
                    if (dropPods)
                    {
                        spawnSpot = DropCellFinder.FindRaidDropCenterDistant(map);
                    }
                    else
                    {
                        spawnSpot = DropCellFinder.FindRaidDropCenterDistant(map, false);
                    }

                    if (dropPods && spawnSpot.IsValid)
                    {
                        DropPodUtility.DropThingsNear(spawnSpot, map, extraRaiders.Cast<Thing>(), 110, false, true, true);
                    }
                    else if (spawnSpot.IsValid)
                    {
                        foreach (var raider in extraRaiders)
                        {
                            GenSpawn.Spawn(raider, spawnSpot, map, Rot4.Random);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при добавлении дополнительных рейдеров: {ex}");
            }
        }

        private void StartSingleRaidAfterExplosion()
        {
            if (Map == null) return;

            if (eventComp != null && !eventComp.CanTriggerRaid())
            {
                HSK_Debug.DebugLog("Рейд уже был запущен", true);
                return;
            }

            Faction hostileFaction = Find.FactionManager.AllFactions
                .Where(f => f.HostileTo(Faction.OfPlayer) && !f.def.hidden)
                .FirstOrDefault();

            if (hostileFaction == null)
            {
                hostileFaction = Faction.OfAncientsHostile;
            }

            float raidPoints = StorytellerUtility.DefaultThreatPointsNow(Map) * 1.5f;
            raidPoints = Mathf.Clamp(raidPoints, 200f, 3000f);

            IncidentParms parms = new IncidentParms
            {
                target = Map,
                points = raidPoints,
                faction = hostileFaction,
                raidStrategy = RaidStrategyDefOf.ImmediateAttack,
                raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn,
                forced = true
            };

            try
            {
                IncidentDef raidDef = IncidentDefOf.RaidEnemy;
                if (raidDef.Worker.CanFireNow(parms))
                {
                    bool raidSuccess = raidDef.Worker.TryExecute(parms);
                    if (raidSuccess)
                    {
                        Messages.Message("HSK_RaidAfterExplosion".Translate(),
                            MessageTypeDefOf.ThreatBig);

                        if (HSK_Debug.LogRaidEvents)
                        {
                            HSK_Debug.DebugLog($"Рейд запущен: {raidPoints} очков, фракция: {hostileFaction.Name}");
                        }
                    }
                    else
                    {
                        if (eventComp != null)
                        {
                            eventComp.ResetRaidCounter();
                        }
                        HSK_Debug.DebugLog("Не удалось запустить рейд", true);
                    }
                }
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при запуске рейда: {ex}");
                if (eventComp != null)
                {
                    eventComp.ResetRaidCounter();
                }
            }
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            if (drawWarning && Find.Selector.IsSelected(this))
            {
                Vector3 drawPos = DrawPos;
                drawPos.y += 0.05f;

                float pulseScale = 0.5f + currentPulse * 0.3f;
                Matrix4x4 matrix = Matrix4x4.TRS(
                    drawPos,
                    Quaternion.identity,
                    new Vector3(pulseScale, 1f, pulseScale)
                );

                Graphics.DrawMesh(
                    MeshPool.plane10,
                    matrix,
                    WarningOverlay,
                    0
                );
            }
        }

        public override string GetInspectString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            if (detonating)
            {
                sb.AppendLine("HSK_BombDetonating".Translate().Colorize(Color.red));
            }
            else if (timerActive && ticksUntilExplosion > 0)
            {
                float minutes = ticksUntilExplosion / 60f;
                sb.AppendLine("HSK_TimeUntilExplosion".Translate() + ": " +
                    minutes.ToString("F0") + " " + "HSK_Minutes".Translate());

                if (ticksUntilExplosion < 300)
                {
                    sb.AppendLine("HSK_DangerImminent".Translate().Colorize(Color.red));
                }
            }

            string baseString = base.GetInspectString();
            if (!string.IsNullOrEmpty(baseString))
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(baseString);
            }

            return sb.ToString().TrimEndNewlines();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
                yield return gizmo;

            // Убраны кнопки обезвреживания - теперь используется стандартное меню разборки
        }

        // Убраны методы StartDefuseJob и DefuseBomb

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);

            if (dinfo.Def.harmsHealth && totalDamageDealt > this.MaxHitPoints * 0.3f)
            {
                if (!detonating && timerActive)
                {
                    HSK_Debug.DebugLog($"Бомба активирована уроном: {totalDamageDealt}/{MaxHitPoints}");
                    StartDetonation();
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref predatorName, "predatorName", "Unknown");
            Scribe_Values.Look(ref ticksUntilExplosion, "ticksUntilExplosion", -1);
            Scribe_Values.Look(ref timerActive, "timerActive", false);
            Scribe_Values.Look(ref detonating, "detonating", false);
            Scribe_Values.Look(ref detonationTicks, "detonationTicks", 0);
            Scribe_Values.Look(ref drawWarning, "drawWarning", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (Current.Game != null)
                {
                    eventComp = Current.Game.GetComponent<GameComponent_PredatorEvent>();
                }
            }
        }
    }

    #endregion

    #region Класс инцидента

    public class Incident_PredatorGuest : IncidentWorker
    {
        private static ThingCategoryDef buildingsSecurityCategory = null;

        private ThingCategoryDef BuildingsSecurityCategory
        {
            get
            {
                if (buildingsSecurityCategory == null)
                {
                    buildingsSecurityCategory = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("BuildingsSecurity");
                    if (buildingsSecurityCategory == null)
                    {
                        HSK_Debug.DebugLog("Категория BuildingsSecurity не найдена!");
                    }
                }
                return buildingsSecurityCategory;
            }
        }

        public bool TryInitializeForTrader(Pawn trader, Map map)
        {
            try
            {
                var settings = def?.GetModExtension<PredatorEventDefExtension>();

                if (settings == null || !settings.allowTraderPredators)
                {
                    return false;
                }

                if (map.mapPawns.FreeColonistsCount < settings.minColonistsForEvent)
                {
                    HSK_Debug.DebugLog($"Для торговца-предателя недостаточно колонистов: {map.mapPawns.FreeColonistsCount} < {settings.minColonistsForEvent}");
                    return false;
                }

                if (GenDate.DaysPassed < settings.minDaysForEvent)
                {
                    HSK_Debug.DebugLog($"Для торговца-предателя слишком рано: {GenDate.DaysPassed} < {settings.minDaysForEvent}");
                    return false;
                }

                if (Rand.Value > settings.traderPredatorChance)
                {
                    HSK_Debug.DebugLog($"Торговец {trader.Name} не стал предателем (шанс: {settings.traderPredatorChance:P0})");
                    return false;
                }

                string traderName = trader.Name?.ToStringShort;
                if (string.IsNullOrEmpty(traderName))
                {
                    HSK_Debug.DebugWarning("Торговец без имени");
                    return false;
                }

                var eventComp = Current.Game?.GetComponent<GameComponent_PredatorEvent>();
                if (eventComp == null)
                {
                    if (Current.Game == null) return false;
                    eventComp = new GameComponent_PredatorEvent(Current.Game);
                    Current.Game.components.Add(eventComp);
                }

                eventComp.RegisterTraderPredator(traderName);
                eventComp.RegisterPredatorFaction(traderName, trader.Faction);

                List<IntVec3> positions = FindBombPositions(map, settings);
                int bombsPlaced = PlaceBombs(map, positions, traderName, eventComp);

                if (bombsPlaced > 0)
                {
                    SendTraderNotification(map, settings, traderName, bombsPlaced);

                    HSK_Debug.DebugLog($"Торговец-предатель {traderName} установил {bombsPlaced} бомб");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при инициализации торговца-предателя: {ex}");
                return false;
            }
        }

        private void SendTraderNotification(Map map, PredatorEventDefExtension settings, string traderName, int bombsPlaced)
        {
            try
            {
                string title = "HSK_TraderPredatorLetterTitle".Translate();
                string text = "HSK_TraderPredatorLetterText".Translate(traderName, bombsPlaced,
                    settings.traderEscapeDelayHours.ToString("F1"));

                Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.ThreatBig,
                    new TargetInfo(map.Center, map, false));

                HSK_Debug.DebugLog($"Отправлено письмо о торговце-предателе: {traderName}, бомб: {bombsPlaced}");
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при отправке письма о торговце: {ex}");
            }
        }

        public void CheckTraderDeparture(Pawn trader, Map map)
        {
            try
            {
                var eventComp = Current.Game?.GetComponent<GameComponent_PredatorEvent>();
                if (eventComp == null) return;

                string traderName = trader.Name?.ToStringShort;
                if (string.IsNullOrEmpty(traderName)) return;

                if (eventComp.IsTraderPredator(traderName))
                {
                    var settings = def?.GetModExtension<PredatorEventDefExtension>();
                    if (settings != null && settings.traderLeavesBeforeExplosion)
                    {
                        if (eventComp.ShouldTraderLeave(traderName))
                        {
                            Messages.Message("HSK_SuspiciousTraderLeaves".Translate(traderName),
                                trader, MessageTypeDefOf.NeutralEvent);

                            HSK_Debug.DebugLog($"Торговец-предатель {traderName} уезжает с карты");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при проверке ухода торговца: {ex}");
            }
        }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            try
            {
                Map map = (Map)parms.target;
                var settings = def?.GetModExtension<PredatorEventDefExtension>();

                if (map == null || settings == null)
                {
                    HSK_Debug.DebugWarning("Map или settings равны null");
                    return false;
                }

                if (map.mapPawns.FreeColonistsCount < settings.minColonistsForEvent)
                {
                    HSK_Debug.DebugLog($"Недостаточно колонистов: {map.mapPawns.FreeColonistsCount} < {settings.minColonistsForEvent}");
                    return false;
                }

                if (GenDate.DaysPassed < settings.minDaysForEvent)
                {
                    HSK_Debug.DebugLog($"Слишком рано: {GenDate.DaysPassed} < {settings.minDaysForEvent}");
                    return false;
                }

                float finalChance = settings.eventBaseChance;
                if (Rand.Value > finalChance)
                {
                    HSK_Debug.DebugLog($"Событие не сработало по шансу: {finalChance:P0}");
                    return false;
                }

                Pawn predator = FindPredatorCandidate(map);
                if (predator == null)
                {
                    HSK_Debug.DebugWarning("Не найден кандидат в предатели");
                    return false;
                }

                string predatorName = predator.Name?.ToStringShort;
                if (string.IsNullOrEmpty(predatorName))
                {
                    HSK_Debug.DebugWarning("Найден предатель без имени");
                }

                var eventComp = Current.Game?.GetComponent<GameComponent_PredatorEvent>();
                if (eventComp == null)
                {
                    if (Current.Game == null)
                    {
                        HSK_Debug.DebugError("Current.Game равен null!");
                        return false;
                    }

                    eventComp = new GameComponent_PredatorEvent(Current.Game);
                    Current.Game.components.Add(eventComp);
                }

                bool isTrader = IsTraderPawn(predator);
                if (isTrader)
                {
                    eventComp.RegisterTraderPredator(predatorName);
                }

                eventComp.RegisterPredatorFaction(predatorName, predator.Faction);

                List<IntVec3> positions = FindBombPositions(map, settings);
                if (positions.Count == 0)
                {
                    HSK_Debug.DebugWarning("Не найдено подходящих позиций для бомб");
                    return false;
                }

                int bombsPlaced = PlaceBombs(map, positions, predatorName, eventComp);

                if (bombsPlaced > 0)
                {
                    if (isTrader)
                    {
                        SendTraderNotification(map, settings, predatorName, bombsPlaced);
                    }
                    else
                    {
                        SendNotificationLetter(map, settings, predatorName, bombsPlaced);
                    }

                    HSK_Debug.DebugLog($"Инцидент успешно выполнен. Установлено {bombsPlaced} бомб. Торговец: {isTrader}");
                    return true;
                }

                HSK_Debug.DebugWarning($"Не удалось установить ни одной бомбы");
                return false;
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Критическая ошибка в TryExecuteWorker: {ex}");
                return false;
            }
        }

        private bool IsTraderPawn(Pawn pawn)
        {
            if (pawn == null) return false;

            // Упрощенная проверка: если пешка имеет компонент trader или является гостем
            return pawn.trader != null ||
                   (pawn.guest != null && pawn.guest.HostFaction != null && !pawn.IsPrisoner);
        }

        private void SendNotificationLetter(Map map, PredatorEventDefExtension settings, string predatorName, int bombsPlaced)
        {
            try
            {
                string title = settings.letterTitleKey.Translate();
                string text = settings.letterTextKey.Translate(predatorName, bombsPlaced);

                Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.ThreatBig,
                    new TargetInfo(map.Center, map, false));

                HSK_Debug.DebugLog($"Отправлено письмо игроку: {predatorName}, бомб: {bombsPlaced}");
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при отправке письма: {ex}");
            }
        }

        private Pawn FindPredatorCandidate(Map map)
        {
            try
            {
                if (map == null) return null;

                var settings = def?.GetModExtension<PredatorEventDefExtension>();

                // 1. Проверяем торговцев (если разрешено)
                if (settings != null && settings.allowTraderPredators)
                {
                    var traders = map.mapPawns.AllPawnsSpawned
                        .Where(p => p != null &&
                               p.RaceProps?.Humanlike == true &&
                               !p.Dead && !p.Downed &&
                               IsTraderPawn(p))
                        .ToList();

                    if (traders.Any())
                    {
                        if (Rand.Value < settings.traderPredatorChance)
                        {
                            var trader = traders.RandomElement();
                            HSK_Debug.DebugLog($"Найден предатель-торговец: {trader.Name} (шанс: {settings.traderPredatorChance:P0})");
                            return trader;
                        }
                    }
                }

                // 2. Поиск гостей
                var guests = map.mapPawns.AllPawnsSpawned
                    .Where(p => p != null &&
                           p.RaceProps?.Humanlike == true &&
                           !p.Dead && !p.Downed &&
                           p.guest != null &&
                           p.guest.HostFaction == Faction.OfPlayer &&
                           !p.IsPrisoner)
                    .ToList();

                if (guests.Any())
                {
                    var predator = guests.RandomElement();
                    HSK_Debug.DebugLog($"Найден предатель-гость: {predator.Name}");
                    return predator;
                }

                // 3. Поиск колонистов с низким настроением
                var colonistPredator = map.mapPawns.FreeColonists
                    .Where(p => p != null &&
                           !p.Dead && !p.Downed &&
                           p.needs?.mood?.CurLevel < 0.3f)
                    .RandomElementWithFallback();

                if (colonistPredator != null)
                {
                    HSK_Debug.DebugLog($"Найден предатель-колонист: {colonistPredator.Name}");
                    return colonistPredator;
                }

                HSK_Debug.DebugLog("Не найден подходящий кандидат в предатели");
                return null;
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при поиске предателя: {ex}");
                return null;
            }
        }

        private List<IntVec3> FindBombPositions(Map map, PredatorEventDefExtension settings)
        {
            List<IntVec3> positions = new List<IntVec3>();

            try
            {
                if (!HSK_Debug.LogTargetSelection)
                {
                    var priorityTargets = map.listerBuildings.allBuildingsColonist
                        .Where(b => PredatorEventDefExtension.IsPriorityTarget(b.def.defName))
                        .ToList();

                    foreach (var building in priorityTargets)
                    {
                        if (positions.Count >= settings.maxBombs) break;

                        IntVec3 bombCell = FindAdjacentCell(building.Position, map, 2, building.Rotation);
                        if (bombCell.IsValid && !positions.Contains(bombCell))
                        {
                            positions.Add(bombCell);
                        }
                    }

                    return positions.Take(Math.Min(positions.Count, settings.maxBombs)).ToList();
                }

                HSK_Debug.DebugLog($"=== ОТЛАДКА: ПОИСК ЦЕЛЕЙ ДЛЯ БОМБ ===");
                HSK_Debug.DebugLog($"Всего строений в колонии: {map.listerBuildings.allBuildingsColonist.Count}");

                var priorityTargetList = map.listerBuildings.allBuildingsColonist
                    .Where(b => PredatorEventDefExtension.IsPriorityTarget(b.def.defName))
                    .OrderBy(b => b.def.defName)
                    .ToList();

                if (!priorityTargetList.Any())
                {
                    HSK_Debug.DebugWarning($"НИ ОДНОЙ ЦЕЛИ ИЗ ПРИОРИТЕТНОГО СПИСКА НЕ НАЙДЕНО!");

                    if (HSK_Debug.ShowVerboseLogs)
                    {
                        HSK_Debug.DebugLog($"Список всех строений в колонии:");
                        int counter = 0;
                        foreach (var building in map.listerBuildings.allBuildingsColonist.Take(20))
                        {
                            HSK_Debug.DebugLog($"  {counter++}: {building.def.defName} в {building.Position}", true);
                        }
                    }

                    return positions;
                }

                HSK_Debug.DebugLog($"Найдено целей из приоритетного списка: {priorityTargetList.Count}");

                int targetNumber = 0;
                foreach (var building in priorityTargetList)
                {
                    targetNumber++;
                    HSK_Debug.DebugLog($"Цель #{targetNumber}: {building.def.defName} в {building.Position}");

                    IntVec3 bombCell = FindAdjacentCell(building.Position, map, 2, building.Rotation);

                    if (bombCell.IsValid)
                    {
                        if (!positions.Contains(bombCell))
                        {
                            positions.Add(bombCell);
                            float distance = building.Position.DistanceTo(bombCell);
                            HSK_Debug.DebugLog($"  ✓ Бомба назначена в {bombCell} (расстояние от цели: {distance:F1} клеток)");
                        }
                        else
                        {
                            HSK_Debug.DebugLog($"  ✗ Позиция {bombCell} уже занята другой бомбой");
                        }
                    }
                    else
                    {
                        HSK_Debug.DebugLog($"  ✗ Не удалось найти валидную позицию для бомбы в радиусе 2 клеток от цели");
                    }
                }

                HSK_Debug.DebugLog($"Итоговое количество позиций для бомб: {positions.Count}");

                if (positions.Count < settings.minBombs)
                {
                    HSK_Debug.DebugLog($"Мало бомб ({positions.Count}), ищем дополнительные цели в категории BuildingsSecurity");

                    var securityBuildings = GetSecurityBuildings(map);
                    HSK_Debug.DebugLog($"Найдено строений безопасности: {securityBuildings.Count}");

                    foreach (var building in securityBuildings)
                    {
                        if (positions.Count >= settings.maxBombs) break;

                        IntVec3 bombCell = FindAdjacentCell(building.Position, map, 2, building.Rotation);
                        if (bombCell.IsValid && !positions.Contains(bombCell))
                        {
                            positions.Add(bombCell);
                            HSK_Debug.DebugLog($"  + Доп. бомба у BuildingsSecurity: {building.def.defName} в {bombCell}");
                        }
                    }
                }

                HSK_Debug.DebugLog($"=== ОТЛАДКА ЗАВЕРШЕНА ===");
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка при поиске позиций для бомб: {ex}");
            }

            return positions.Take(Math.Min(positions.Count, settings.maxBombs)).ToList();
        }

        private List<Building> GetSecurityBuildings(Map map)
        {
            var securityBuildings = new List<Building>();

            if (BuildingsSecurityCategory == null)
                return securityBuildings;

            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                if (IsInSecurityCategory(building.def))
                {
                    securityBuildings.Add(building);
                }
            }

            return securityBuildings;
        }

        private bool IsInSecurityCategory(ThingDef def)
        {
            if (BuildingsSecurityCategory == null)
                return false;

            return def.thingCategories != null &&
                   def.thingCategories.Contains(BuildingsSecurityCategory);
        }

        private IntVec3 FindAdjacentCell(IntVec3 center, Map map, int maxRadius, Rot4 buildingRotation)
        {
            try
            {
                for (int radius = 1; radius <= maxRadius; radius++)
                {
                    var cellsInRadius = GenRadial.RadialCellsAround(center, radius, false)
                        .Where(cell => IsValidBombCell(cell, map))
                        .ToList();

                    if (cellsInRadius.Any())
                    {
                        return cellsInRadius.OrderBy(c =>
                            Mathf.Abs((c.x - center.x) + (c.z - center.z))
                        ).FirstOrDefault();
                    }
                }

                var radialCells = GenRadial.RadialCellsAround(center, maxRadius, true)
                    .Where(cell => IsValidBombCell(cell, map))
                    .ToList();

                if (radialCells.Any())
                {
                    return radialCells.RandomElement();
                }

                return IntVec3.Invalid;
            }
            catch (Exception ex)
            {
                HSK_Debug.DebugError($"Ошибка в FindAdjacentCell: {ex}");
                return IntVec3.Invalid;
            }
        }

        private bool IsValidBombCell(IntVec3 cell, Map map)
        {
            try
            {
                if (!cell.InBounds(map))
                {
                    return false;
                }

                if (!cell.Standable(map))
                {
                    return false;
                }

                if (cell.Fogged(map))
                {
                    return false;
                }

                Building buildingAtCell = cell.GetEdifice(map);
                if (buildingAtCell != null && buildingAtCell.def.category == ThingCategory.Building)
                {
                    if (buildingAtCell.def.passability != Traversability.Standable ||
                        buildingAtCell.def.defName.Contains("Plant") ||
                        buildingAtCell.def.defName.Contains("Flower") ||
                        buildingAtCell.def.defName.Contains("Grass"))
                    {
                    }
                    else
                    {
                        return false;
                    }
                }

                if (cell.GetDoor(map) != null)
                {
                    return false;
                }

                var firstItem = cell.GetFirstItem(map);
                if (firstItem != null && !firstItem.def.smallVolume)
                {
                    return false;
                }

                if (cell.Roofed(map))
                {
                    var roof = cell.GetRoof(map);
                    if (roof != null && roof.isThickRoof)
                    {
                        return false;
                    }
                }

                TerrainDef terrain = cell.GetTerrain(map);
                if (terrain != null)
                {
                    if (terrain.passability == Traversability.Impassable)
                    {
                        return false;
                    }

                    if (terrain.defName.Contains("Water") ||
                        terrain.defName.Contains("Marsh") ||
                        terrain.defName.Contains("Mud"))
                    {
                        if (terrain.passability != Traversability.Standable)
                        {
                            return false;
                        }
                    }
                }

                if (cell.GetFirstPawn(map) != null)
                {
                    return false;
                }

                var nearbyBombs = map.listerBuildings.allBuildingsColonist
                    .OfType<Building_PredatorBomb>()
                    .Where(b => b.Position.DistanceTo(cell) < 3f)
                    .Any();

                if (nearbyBombs)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private int PlaceBombs(Map map, List<IntVec3> positions, string predatorName, GameComponent_PredatorEvent eventComp)
        {
            int placed = 0;
            ThingDef bombDef = DefDatabase<ThingDef>.GetNamedSilentFail("HSK_PredatorBomb");

            if (bombDef == null)
            {
                HSK_Debug.DebugError("Def HSK_PredatorBomb не найден!");
                return 0;
            }

            if (HSK_Debug.LogBombPlacement)
            {
                HSK_Debug.DebugLog($"Начинаем установку бомб на {positions.Count} позиций");
            }

            foreach (var position in positions)
            {
                try
                {
                    if (!IsValidBombCell(position, map))
                    {
                        HSK_Debug.DebugLog($"Позиция {position} не подходит для бомбы (повторная проверка)", true);
                        continue;
                    }

                    List<Thing> thingsAtPos = position.GetThingList(map).ToList();
                    foreach (var thing in thingsAtPos)
                    {
                        if (thing.def.category == ThingCategory.Item && thing.def.smallVolume)
                        {
                            thing.Destroy(DestroyMode.Vanish);
                            HSK_Debug.DebugLog($"Удален предмет {thing.def.defName} с позиции {position}", true);
                        }
                    }

                    var plant = position.GetPlant(map);
                    if (plant != null)
                    {
                        plant.Destroy(DestroyMode.Vanish);
                        HSK_Debug.DebugLog($"Удалено растение {plant.def.defName} с позиции {position}", true);
                    }

                    var building = position.GetEdifice(map);
                    if (building != null &&
                        building.def.category == ThingCategory.Building &&
                        (building.def.defName.Contains("Plant") ||
                         building.def.defName.Contains("Flower") ||
                         building.def.defName.Contains("Grass")))
                    {
                        building.Destroy(DestroyMode.Vanish);
                        HSK_Debug.DebugLog($"Удалено декоративное строение {building.def.defName} с позиции {position}", true);
                    }

                    Building_PredatorBomb bomb = (Building_PredatorBomb)ThingMaker.MakeThing(bombDef);

                    GenSpawn.Spawn(bomb, position, map, Rot4.North, WipeMode.Vanish);

                    eventComp.RegisterBomb(position, predatorName);
                    placed++;

                    FleckMaker.ThrowMetaPuffs(new TargetInfo(position, map, false));

                    if (HSK_Debug.LogBombPlacement)
                    {
                        HSK_Debug.DebugLog($"✓ Бомба #{placed} установлена в {position}");
                    }
                }
                catch (Exception ex)
                {
                    HSK_Debug.DebugError($"Ошибка установки бомбы в {position}: {ex.Message}");
                    if (ex.InnerException != null)
                        HSK_Debug.DebugError($"Внутренняя ошибка: {ex.InnerException.Message}");
                }
            }

            HSK_Debug.DebugLog($"Установлено {placed} бомб из {positions.Count} запланированных для предателя {predatorName}");

            if (placed > 0)
            {
                Messages.Message("HSK_BombsPlaced".Translate(predatorName, placed),
                    MessageTypeDefOf.ThreatSmall);
            }

            return placed;
        }
    }

    #endregion
}
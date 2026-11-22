using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace HSK.Watcher.Content.Incident
{
    // Def для хранения настроек
    public class RaiderTributeSettings : Def
    {
        public float baseFee;
        public float wealthPercentage;
        public float buildingsPercentage;
        public float colonistValue;
        public int minDemand;
        public int maxDemand;
        public float randomFactorMin;
        public float randomFactorMax;
        public int roundToNearest;

        public float baseRaidPoints;
        public float raidWealthPercentage;
        public float raidBuildingsPercentage;
        public float raidColonistValue;
        public float minRaidPoints;
        public float maxRaidPoints;
        public float raidRandomFactorMin;
        public float raidRandomFactorMax;

        public int minColonists;
        public int minWealth;

        // Новые настройки для случайных событий
        public float doubleCrossChance; // Шанс что пираты атакуют даже после выплаты
        public float bluffChance; // Шанс что пираты блефовали и отступают

        // Настройки для орбитальной бомбардировки
        public int minColonyAgeForOrbitalStrike; // Минимальный возраст колонии для орбитальной бомбардировки
        public float orbitalStrikeChance; // Шанс орбитальной бомбардировки
        public int orbitalStrikeWarmupTicks; // Время подготовки удара
        public float orbitalStrikeRadius; // Радиус удара

        private static RaiderTributeSettings _cachedSettings;

        public static RaiderTributeSettings Settings
        {
            get
            {
                if (_cachedSettings == null)
                {
                    _cachedSettings = DefDatabase<RaiderTributeSettings>.GetNamedSilentFail("RaiderTributeSettings");
                    if (_cachedSettings == null)
                    {
                        //Log.Warning("PirateTribute: Settings not found, using defaults");
                        _cachedSettings = CreateDefaultSettings();
                    }
                }
                return _cachedSettings;
            }
        }

        private static RaiderTributeSettings CreateDefaultSettings()
        {
            return new RaiderTributeSettings
            {
                baseFee = 300f,
                wealthPercentage = 0.015f,
                buildingsPercentage = 0.005f,
                colonistValue = 1000f,
                minDemand = 100,
                maxDemand = 10000,
                randomFactorMin = 0.8f,
                randomFactorMax = 1.2f,
                roundToNearest = 10,
                baseRaidPoints = 350f,
                raidWealthPercentage = 0.002f,
                raidBuildingsPercentage = 0.001f,
                raidColonistValue = 50f,
                minRaidPoints = 350f,
                maxRaidPoints = 10000f,
                raidRandomFactorMin = 0.9f,
                raidRandomFactorMax = 1.1f,
                minColonists = 2,
                minWealth = 25000,
                doubleCrossChance = 0.05f, // 5% шанс двойной игры
                bluffChance = 0.08f, // 8% шанс блефа
                minColonyAgeForOrbitalStrike = 5, // 5 лет
                orbitalStrikeChance = 0.15f, // 15% шанс
                orbitalStrikeWarmupTicks = 300, // 5 секунд подготовки
                orbitalStrikeRadius = 8f // Радиус удара
            };
        }
    }

    // MapComponent для отслеживания орбитальных ударов
    public class MapComponent_OrbitalStrikeTracker : MapComponent
    {
        private List<ScheduledStrike> scheduledStrikes = new List<ScheduledStrike>();

        public MapComponent_OrbitalStrikeTracker(Map map) : base(map) { }

        public void ScheduleStrike(IntVec3 position, int warmupTicks, float radius)
        {
            scheduledStrikes.Add(new ScheduledStrike
            {
                position = position,
                startTick = Find.TickManager.TicksGame,
                warmupTicks = warmupTicks,
                radius = radius
            });

            //Log.Message($"PirateTribute: Scheduled orbital strike at {position} in {warmupTicks} ticks");
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            for (int i = scheduledStrikes.Count - 1; i >= 0; i--)
            {
                var strike = scheduledStrikes[i];
                int elapsedTicks = Find.TickManager.TicksGame - strike.startTick;

                if (elapsedTicks >= strike.warmupTicks)
                {
                    ExecuteStrike(strike);
                    scheduledStrikes.RemoveAt(i);
                }
            }
        }

        private void ExecuteStrike(ScheduledStrike strike)
        {
            try
            {
                //Log.Message($"PirateTribute: Executing orbital strike at {strike.position}");

                // Создаем основной взрыв
                GenExplosion.DoExplosion(
                    strike.position,
                    map,
                    strike.radius,
                    DamageDefOf.Bomb,
                    null,
                    damAmount: 100,
                    armorPenetration: 0
                );

                // Создаем огненные взрывы вместо отдельных пожаров
                for (int i = 0; i < 4; i++)
                {
                    IntVec3 firePos = strike.position + new IntVec3(Rand.Range(-4, 5), 0, Rand.Range(-4, 5));
                    if (firePos.InBounds(map))
                    {
                        GenExplosion.DoExplosion(
                            firePos,
                            map,
                            strike.radius / 2,
                            DamageDefOf.Flame,
                            null,
                            damAmount: 40,
                            armorPenetration: 0
                        );
                    }
                }

            }
            catch (System.Exception ex)
            {
                //Log.Error($"PirateTribute strike execution error: {ex}");
            }
        }

        private struct ScheduledStrike
        {
            public IntVec3 position;
            public int startTick;
            public int warmupTicks;
            public float radius;
        }
    }

    public class IncidentWorker_RaiderTribute : IncidentWorker
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms?.target as Map;

            if (map == null)
            {
                //Log.Error("PirateTribute: Map is null");
                return false;
            }

            //Log.Message("PirateTribute: Starting tribute event");

            // Создаем диалог требования дани
            Find.WindowStack.Add(new Dialog_RaiderTribute(map));
            return true;
        }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (parms?.target == null)
            {
                //Log.Message("PirateTribute: CanFireNowSub - parms or target is null");
                return false;
            }

            Map map = parms.target as Map;
            if (map == null || map.mapPawns == null)
            {
                //Log.Message("PirateTribute: CanFireNowSub - map or mapPawns is null");
                return false;
            }

            var settings = RaiderTributeSettings.Settings;

            bool canFire = map.mapPawns.FreeColonistsCount >= settings.minColonists &&
                          map.wealthWatcher.WealthTotal >= settings.minWealth;

            //Log.Message($"PirateTribute: CanFireNowSub - colonists: {map.mapPawns.FreeColonistsCount}, wealth: {map.wealthWatcher.WealthTotal}, canFire: {canFire}");

            return canFire;
        }
    }

    public class Dialog_RaiderTribute : Dialog_NodeTree
    {
        private Map map;

        public Dialog_RaiderTribute(Map map) : base(CreateRootNode(map), false, false, null)
        {
            this.map = map ?? throw new System.ArgumentNullException(nameof(map));

            // Убеждаемся, что MapComponent существует
            if (map.GetComponent<MapComponent_OrbitalStrikeTracker>() == null)
            {
                map.components.Add(new MapComponent_OrbitalStrikeTracker(map));
            }
        }

        private static DiaNode CreateRootNode(Map map)
        {
            try
            {
                var settings = RaiderTributeSettings.Settings;

                // --- ФОРМУЛА РАСЧЕТА ВЫКУПА ---
                float mapWealth = map.wealthWatcher.WealthTotal;
                float buildingsValue = CalculateBuildingsValue(map);
                int colonistsCount = map.mapPawns.FreeColonistsCount;
                float threatScale = Find.Storyteller.difficulty.threatScale;

                // Основная формула
                float baseAmount = settings.baseFee +
                                 (mapWealth * settings.wealthPercentage) +
                                 (buildingsValue * settings.buildingsPercentage) +
                                 (colonistsCount * settings.colonistValue);

                // Применяем сложность и случайность
                float calculatedAmount = baseAmount * threatScale;
                float randomFactor = Rand.Range(settings.randomFactorMin, settings.randomFactorMax);
                int demandAmount = (int)(calculatedAmount * randomFactor);

                // Округляем и ограничиваем
                demandAmount = (int)(System.Math.Round(demandAmount / (float)settings.roundToNearest) * settings.roundToNearest);
                if (demandAmount < settings.minDemand) demandAmount = settings.minDemand;
                if (demandAmount > settings.maxDemand) demandAmount = settings.maxDemand;

                float demandedSilver = demandAmount;
                Thing demandedItem = FindAlternativeItem(map, demandedSilver);

                //Log.Message($"PirateTribute: Map wealth: {mapWealth}, Buildings: {buildingsValue}, Colonists: {colonistsCount}, " +
                    // $"Threat scale: {threatScale}, Base amount: {baseAmount}, Calculated: {calculatedAmount}, Final demand: {demandedSilver}");

                // Создаем текст диалога
                string text = "RaiderTribute.DemandText".Translate(demandedSilver.ToStringMoney());

                if (demandedItem != null)
                {
                    text += "\n\n" + "RaiderTribute.AlternativeItem".Translate(demandedItem.LabelCap);
                }

                DiaNode node = new DiaNode(text);

                // Получаем данные о серебре
                float silverAmount = GetSilverAmount(map);
                bool hasEnoughSilver = silverAmount >= demandedSilver;

                //Log.Message($"PirateTribute: Silver available - {silverAmount}, needed: {demandedSilver}, hasEnough: {hasEnoughSilver}");

                // Кнопка оплаты серебром
                if (hasEnoughSilver)
                {
                    node.options.Add(new DiaOption("RaiderTribute.PaySilver".Translate())
                    {
                        action = () => PayWithSilver(map, demandedSilver, mapWealth, buildingsValue, colonistsCount),
                        resolveTree = true
                    });
                }
                else
                {
                    node.options.Add(new DiaOption("RaiderTribute.NoSilverOption".Translate())
                    {
                        action = () => { },
                        resolveTree = false,
                        disabled = true
                    });
                }

                // Кнопка отдачи предмета
                if (demandedItem != null && demandedItem.Spawned && !demandedItem.Destroyed)
                {
                    Thing itemToGive = demandedItem;
                    node.options.Add(new DiaOption("RaiderTribute.GiveItem".Translate(itemToGive.LabelCap))
                    {
                        action = () => PayWithItem(map, itemToGive, mapWealth, buildingsValue, colonistsCount),
                        resolveTree = true
                    });
                }
                else
                {
                    node.options.Add(new DiaOption("RaiderTribute.NoItemOption".Translate())
                    {
                        action = () => { },
                        resolveTree = false,
                        disabled = true
                    });
                }

                // Кнопка отказа
                node.options.Add(new DiaOption("RaiderTribute.Refuse".Translate())
                {
                    action = () => RefusePayment(map, mapWealth, buildingsValue, colonistsCount),
                    resolveTree = true
                });

                return node;
            }
            catch (System.Exception ex)
            {
                //Log.Error($"PirateTribute dialog creation error: {ex}");
                return CreateErrorNode("RaiderTribute.ErrorCreatingDialog".Translate());
            }
        }

        private static float CalculateBuildingsValue(Map map)
        {
            if (map?.listerBuildings?.allBuildingsColonist == null) return 0f;

            float value = 0f;
            foreach (Building building in map.listerBuildings.allBuildingsColonist)
            {
                if (building != null && building.def != null && building.Spawned)
                {
                    value += building.MarketValue;
                }
            }
            return value;
        }

        private static DiaNode CreateErrorNode(string errorText)
        {
            DiaNode errorNode = new DiaNode(errorText);
            errorNode.options.Add(new DiaOption("OK".Translate())
            {
                action = () => { },
                resolveTree = true
            });
            return errorNode;
        }

        private static Thing FindAlternativeItem(Map map, float minValue)
        {
            if (map?.listerThings == null) return null;

            try
            {
                var items = map.listerThings.AllThings
                    .Where(thing => thing != null && thing.Spawned && !thing.Destroyed &&
                           (thing.Faction == Faction.OfPlayer || thing.IsInAnyStorage()) &&
                           thing.def != ThingDefOf.Silver &&
                           thing.def.EverHaulable &&
                           thing.MarketValue * thing.stackCount >= minValue)
                    .OrderByDescending(thing => thing.MarketValue * thing.stackCount)
                    .ToList();

                //Log.Message($"PirateTribute: Found {items.Count} alternative items");
                return items.FirstOrDefault();
            }
            catch (System.Exception ex)
            {
                //Log.Error($"PirateTribute find alternative item error: {ex}");
                return null;
            }
        }

        private static float GetSilverAmount(Map map)
        {
            if (map?.listerThings == null) return 0f;

            try
            {
                var silver = map.listerThings.AllThings
                    .Where(thing => thing != null && thing.Spawned && !thing.Destroyed &&
                           (thing.Faction == Faction.OfPlayer || thing.IsInAnyStorage()) &&
                           thing.def == ThingDefOf.Silver)
                    .Sum(thing => thing.MarketValue * thing.stackCount);

                //Log.Message($"PirateTribute: Total silver - {silver}");
                return silver;
            }
            catch (System.Exception ex)
            {
                //Log.Error($"PirateTribute get silver amount error: {ex}");
                return 0f;
            }
        }

        private static void PayWithSilver(Map map, float demandedSilver, float mapWealth, float buildingsValue, int colonistsCount)
        {
            var settings = RaiderTributeSettings.Settings;

            // Проверяем шанс двойной игры
            if (Rand.Chance(settings.doubleCrossChance))
            {
                //Log.Message("PirateTribute: Double cross! Pirates attack despite payment");

                // Забираем серебро
                ProcessSilverPayment(map, demandedSilver);

                // Но все равно атакуем - выводим письмо
                Find.LetterStack.ReceiveLetter(
                    "RaiderTribute.DoubleCrossLetterLabel".Translate(),
                    "RaiderTribute.DoubleCrossLetterText".Translate(),
                    LetterDefOf.ThreatBig
                );

                StartPirateAttack(map, mapWealth, buildingsValue, colonistsCount, true);
            }
            else
            {
                // Стандартная выплата - мирный исход
                ProcessSilverPayment(map, demandedSilver);
                Find.LetterStack.ReceiveLetter(
                    "RaiderTribute.PaidSilverLetterLabel".Translate(),
                    "RaiderTribute.PaidSilverLetterText".Translate(),
                    LetterDefOf.PositiveEvent
                );
            }
        }

        private static void ProcessSilverPayment(Map map, float demandedSilver)
        {
            float silverNeeded = demandedSilver;
            List<Thing> silverPiles = map.listerThings.AllThings
                .Where(thing => thing != null && thing.Spawned && !thing.Destroyed &&
                       (thing.Faction == Faction.OfPlayer || thing.IsInAnyStorage()) &&
                       thing.def == ThingDefOf.Silver)
                .OrderBy(thing => thing.stackCount)
                .ToList();

            //Log.Message($"PirateTribute: Found {silverPiles.Count} silver piles");

            foreach (Thing silver in silverPiles)
            {
                if (silverNeeded <= 0) break;

                float silverValue = silver.MarketValue * silver.stackCount;
                //Log.Message($"PirateTribute: Processing silver pile - value: {silverValue}, needed: {silverNeeded}");

                if (silverValue <= silverNeeded)
                {
                    silverNeeded -= silverValue;
                    silver.Destroy();
                    //Log.Message($"PirateTribute: Destroyed silver pile, remaining needed: {silverNeeded}");
                }
                else
                {
                    int toRemove = Mathf.FloorToInt(silverNeeded / silver.MarketValue);
                    if (toRemove > 0)
                    {
                        silver.stackCount -= toRemove;
                        silverNeeded = 0;
                        //Log.Message($"PirateTribute: Removed {toRemove} silver, stack now: {silver.stackCount}");
                        if (silver.stackCount <= 0)
                        {
                            silver.Destroy();
                            //Log.Message("PirateTribute: Destroyed empty silver pile");
                        }
                        break;
                    }
                }
            }
        }

        private static void PayWithItem(Map map, Thing item, float mapWealth, float buildingsValue, int colonistsCount)
        {
            if (item != null && item.Spawned && !item.Destroyed)
            {
                var settings = RaiderTributeSettings.Settings;

                // Проверяем шанс двойной игры и для предметов
                if (Rand.Chance(settings.doubleCrossChance))
                {
                    //Log.Message("PirateTribute: Double cross! Pirates attack despite item payment");

                    string itemName = item.LabelCap;
                    item.Destroy();

                    Find.LetterStack.ReceiveLetter(
                        "RaiderTribute.DoubleCrossWithItemLetterLabel".Translate(),
                        "RaiderTribute.DoubleCrossWithItemLetterText".Translate(itemName),
                        LetterDefOf.ThreatBig
                    );

                    StartPirateAttack(map, mapWealth, buildingsValue, colonistsCount, true);
                }
                else
                {
                    Log.Message($"PirateTribute: Player paid with item - {item.LabelCap}");
                    string itemName = item.LabelCap;
                    item.Destroy();
                    Find.LetterStack.ReceiveLetter(
                        "RaiderTribute.PaidItemLetterLabel".Translate(),
                        "RaiderTribute.PaidItemLetterText".Translate(itemName),
                        LetterDefOf.PositiveEvent
                    );
                }
            }
            else
            {
                //Log.Error("PirateTribute: Item is null, not spawned, or destroyed");
            }
        }

        private static void RefusePayment(Map map, float mapWealth, float buildingsValue, int colonistsCount)
        {
            var settings = RaiderTributeSettings.Settings;

            // Проверяем шанс блефа
            if (Rand.Chance(settings.bluffChance))
            {
                //Log.Message("PirateTribute: Pirates were bluffing! They retreat without attack");
                Find.LetterStack.ReceiveLetter(
                    "RaiderTribute.BluffRetreatLetterLabel".Translate(),
                    "RaiderTribute.BluffRetreatLetterText".Translate(),
                    LetterDefOf.PositiveEvent
                );
            }
            else
            {
                //Log.Message("PirateTribute: Player refused payment - starting pirate attack");
                Find.LetterStack.ReceiveLetter(
                    "RaiderTribute.RefusedLetterLabel".Translate(),
                    "RaiderTribute.RefusedLetterText".Translate(),
                    LetterDefOf.ThreatBig
                );
                StartPirateAttack(map, mapWealth, buildingsValue, colonistsCount, false);
            }
        }

        private static void StartPirateAttack(Map map, float mapWealth, float buildingsValue, int colonistsCount, bool isDoubleCross = false)
        {
            var settings = RaiderTributeSettings.Settings;

            // Формула мощи атаки пиратов
            float threatScale = Find.Storyteller.difficulty.threatScale;
            float baseRaid = settings.baseRaidPoints +
                           (mapWealth * settings.raidWealthPercentage) +
                           (buildingsValue * settings.raidBuildingsPercentage) +
                           (colonistsCount * settings.raidColonistValue);

            float raidPoints = baseRaid * threatScale;

            // Если это двойная игра, немного увеличиваем мощь атаки
            if (isDoubleCross)
            {
                raidPoints *= 1.2f; // +20% к мощи при двойной игре
                //Log.Message("PirateTribute: Double cross raid - increased points by 20%");
            }

            // Ограничиваем мощь
            raidPoints = Mathf.Clamp(raidPoints, settings.minRaidPoints, settings.maxRaidPoints);

            // Добавляем случайный фактор
            raidPoints *= Rand.Range(settings.raidRandomFactorMin, settings.raidRandomFactorMax);

            //Log.Message($"PirateTribute: Starting pirate attack with {raidPoints} points " +
            //           $"(Wealth: {mapWealth}, Buildings: {buildingsValue}, Colonists: {colonistsCount}, Threat scale: {threatScale}, DoubleCross: {isDoubleCross})");

            // Проверяем возможность орбитальной бомбардировки для старых колоний
            bool useOrbitalStrike = ShouldUseOrbitalStrike(map, settings);

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.points = raidPoints;

            if (useOrbitalStrike)
            {
                // Специальная стратегия для атаки с орбитальной бомбардировкой
                parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
                parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            }
            else
            {
                // Стандартная атака
                parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
                parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            }

            // Используем стандартных пиратов из RimWorld
            parms.faction = Find.FactionManager.AllFactions
                .Where(f => f != null &&
                       f.HostileTo(Faction.OfPlayer) &&
                       !f.def.hidden &&
                       f.def.humanlikeFaction &&
                       IsPirateFaction(f.def))
                .FirstOrDefault();

            // Если пиратов нет, используем стандартный метод поиска вражеской фракции
            if (parms.faction == null)
            {
                //Log.Message("PirateTribute: No pirate faction found, using default enemy faction");
                parms.faction = Find.FactionManager.RandomEnemyFaction(false, false, true, TechLevel.Undefined);
            }

            // Если и это не помогло, используем механидов
            if (parms.faction == null)
            {
                parms.faction = Faction.OfMechanoids;
                //Log.Message("PirateTribute: No enemy faction found, using mechanoids");
            }
            else
            {
                //Log.Message($"PirateTribute: Using pirate faction: {parms.faction.Name} (def: {parms.faction.def.defName})");
            }

            // Запускаем рейд
            bool raidStarted = IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);

            // Если рейд начался и колония старая, добавляем орбитальную бомбардировку
            if (raidStarted && useOrbitalStrike)
            {
                StartOrbitalStrike(map, settings);
            }
        }

        // Проверка условий для орбитальной бомбардировки
        private static bool ShouldUseOrbitalStrike(Map map, RaiderTributeSettings settings)
        {
            // Проверяем возраст колонии
            int colonyAgeYears = GetColonyAgeYears(map);
            bool isOldColony = colonyAgeYears >= settings.minColonyAgeForOrbitalStrike;

            if (!isOldColony)
            {
                //Log.Message($"PirateTribute: Colony is {colonyAgeYears} years old - too young for orbital strike (min: {settings.minColonyAgeForOrbitalStrike})");
                return false;
            }

            // Проверяем шанс
            bool willStrike = Rand.Chance(settings.orbitalStrikeChance);
            //Log.Message($"PirateTribute: Colony age {colonyAgeYears} years, orbital strike chance: {settings.orbitalStrikeChance}, will strike: {willStrike}");

            return willStrike;
        }

        // Получение возраста колонии в годах
        private static int GetColonyAgeYears(Map map)
        {
            if (map == null) return 0;

            try
            {
                // Получаем возраст колонии в тиках и конвертируем в годы
                long colonyAgeTicks = Find.TickManager.TicksGame - map.generationTick;
                float colonyAgeDays = colonyAgeTicks / 60000f; // 60,000 тиков в дне
                int colonyAgeYears = Mathf.FloorToInt(colonyAgeDays / 60f); // 60 дней в году RimWorld

                //Log.Message($"PirateTribute: Colony age - {colonyAgeTicks} ticks, {colonyAgeDays:F1} days, {colonyAgeYears} years");

                return colonyAgeYears;
            }
            catch (System.Exception ex)
            {
                //Log.Error($"PirateTribute error calculating colony age: {ex}");
                return 0;
            }
        }

        // Запуск орбитальной бомбардировки
        private static void StartOrbitalStrike(Map map, RaiderTributeSettings settings)
        {
            try
            {
                //Log.Message("PirateTribute: Starting orbital strike");

                // Находим случайную позицию для удара (предпочтительно рядом с постройками)
                IntVec3 strikePos = FindGoodStrikePosition(map);

                if (!strikePos.IsValid)
                {
                    //Log.Warning("PirateTribute: Could not find valid strike position");
                    return;
                }

                // Убеждаемся, что MapComponent существует
                var strikeTracker = map.GetComponent<MapComponent_OrbitalStrikeTracker>();
                if (strikeTracker == null)
                {
                    strikeTracker = new MapComponent_OrbitalStrikeTracker(map);
                    map.components.Add(strikeTracker);
                }

                // Запускаем бомбардировку через заданное время
                strikeTracker.ScheduleStrike(
                    strikePos,
                    settings.orbitalStrikeWarmupTicks,
                    settings.orbitalStrikeRadius
                );

                // Сообщение игроку
                Find.LetterStack.ReceiveLetter(
                    "RaiderTribute.OrbitalStrikeLetterLabel".Translate(),
                    "RaiderTribute.OrbitalStrikeLetterText".Translate(),
                    LetterDefOf.ThreatBig
                );

            }
            catch (System.Exception ex)
            {
                //Log.Error($"PirateTribute orbital strike error: {ex}");
            }
        }

        // Поиск хорошей позиции для удара
        private static IntVec3 FindGoodStrikePosition(Map map)
        {
            // Сначала ищем рядом с постройками
            var buildings = map.listerBuildings.allBuildingsColonist;
            if (buildings != null && buildings.Count > 0)
            {
                for (int i = 0; i < 20; i++) // 20 попыток
                {
                    Building randomBuilding = buildings.RandomElement();
                    if (randomBuilding != null && randomBuilding.Spawned)
                    {
                        IntVec3 candidate = CellFinder.RandomClosewalkCellNear(randomBuilding.Position, map, 10);
                        if (candidate.IsValid && candidate.Standable(map) && !candidate.Fogged(map))
                        {
                            return candidate;
                        }
                    }
                }
            }

            // Если не нашли рядом с постройками, ищем любую подходящую клетку
            return CellFinderLoose.RandomCellWith((IntVec3 c) =>
                c.Standable(map) && !c.Fogged(map), map, 1000);
        }

        private static bool IsPirateFaction(FactionDef factionDef)
        {
            // Стандартные пиратские фракции RimWorld
            string[] pirateDefNames = {
                "Pirates",
                "Pirate",
                "OutlanderPirates",
                "RoughPirates",
                "Pirate_Rough",
                "Pirate_Savage",
                "SavagePirates"
            };

            return pirateDefNames.Contains(factionDef.defName) ||
                   factionDef.defName.ToLower().Contains("pirate") ||
                   (factionDef.label?.ToLower().Contains("pirate") == true);
        }
    }
}
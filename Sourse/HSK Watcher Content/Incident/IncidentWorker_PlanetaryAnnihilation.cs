using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SK;
using SK.Events;
using UnityEngine;
using Verse;

namespace HSK.Watcher.Content.Incident
{
    public class IncidentWorker_PlanetaryAnnihilation : IncidentWorker
    {
        private const int BombardmentDurationTicks = 24000;
        private const int WarningDelayTicks = 2500;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            return map != null && map.mapPawns.FreeColonistsCount > 0;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (map == null) return false;

            SendWarningLetter(map);

            MapComponent_DelayedBombardment delayedStart = map.GetComponent<MapComponent_DelayedBombardment>();
            if (delayedStart == null)
            {
                delayedStart = new MapComponent_DelayedBombardment(map);
                map.components.Add(delayedStart);
            }
            delayedStart.ScheduleBombardment(Find.TickManager.TicksGame + WarningDelayTicks);

            return true;
        }

        private void SendWarningLetter(Map map)
        {
            string label = "HSK_PlanetaryAnnihilation_WarningLabel".Translate();
            string text = "HSK_PlanetaryAnnihilation_WarningText".Translate(
                map.Parent.LabelShort,
                map.mapPawns.FreeColonistsCount.ToString()
            );

            Find.LetterStack.ReceiveLetter(
                label,
                text,
                LetterDefOf.ThreatBig,
                new LookTargets(map.mapPawns.FreeColonists),
                null,
                null,
                null,
                null,
                0,
                false
            );
        }
    }

    public class MapComponent_DelayedBombardment : MapComponent
    {
        private int bombardmentStartTick = -1;
        private bool scheduled = false;

        public MapComponent_DelayedBombardment(Map map) : base(map) { }

        public void ScheduleBombardment(int startTick)
        {
            bombardmentStartTick = startTick;
            scheduled = true;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (!scheduled) return;
            if (Find.TickManager.TicksGame < bombardmentStartTick) return;

            scheduled = false;
            StartActualBombardment();
        }

        private void StartActualBombardment()
        {
            if (map == null) return;

            GameCondition condition = GameConditionMaker.MakeCondition(
                DefDatabase<GameConditionDef>.GetNamed("HSK_PlanetaryBombardment"),
                24000
            );
            map.GameConditionManager.RegisterCondition(condition);

            MapComponent_BombardmentController controller = map.GetComponent<MapComponent_BombardmentController>();
            if (controller == null)
            {
                controller = new MapComponent_BombardmentController(map);
                map.components.Add(controller);
            }
            controller.StartBombardment();

            SendStartLetter();
        }

        private void SendStartLetter()
        {
            string label = "HSK_PlanetaryAnnihilation_StartLabel".Translate();
            string text = "HSK_PlanetaryAnnihilation_StartText".Translate(
                map.Parent.LabelShort,
                map.mapPawns.FreeColonistsCount.ToString()
            );

            Find.LetterStack.ReceiveLetter(
                label,
                text,
                LetterDefOf.ThreatBig,
                new LookTargets(map.mapPawns.FreeColonists),
                null,
                null,
                null,
                null,
                0,
                false
            );
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref bombardmentStartTick, "bombardmentStartTick", -1);
            Scribe_Values.Look(ref scheduled, "scheduled", false);
        }
    }

    public class MapComponent_BombardmentController : MapComponent
    {
        // Настройки логов
        private static bool EnableDebugLogs = true;
        private static bool EnableVerboseLogs = false;
        private static bool EnableErrorLogs = true;

        private bool bombardmentActive = false;
        private int nextStrikeTick = 0;
        private int strikeCounter = 0;

        private const int SimultaneousBeams = 5;
        private static readonly IntRange StrikeInterval = new IntRange(300, 500);
        private const int MaxIonStrikes = 30;

        // 3 ядерных удара на разных этапах
        private const int FirstNuclearStrike = 10;
        private const int SecondNuclearStrike = 20;
        private const int ThirdNuclearStrike = 30;

        private int nuclearStrikesDone = 0;

        private const int HeatPhaseStartTicks = 6000;
        private bool heatPhaseActive = false;
        private int heatPhaseStartTick = 0;

        private int nextColonistCheckTick = 0;
        private const int ColonistCheckInterval = 60;

        private bool gameEndTriggered = false;
        private bool finalLetterSent = false;

        public MapComponent_BombardmentController(Map map) : base(map) { }

        private void LogDebug(string message)
        {
            if (EnableDebugLogs)
            {
                Log.Message("[PlanetaryAnnihilation] " + message);
            }
        }

        private void LogVerbose(string message)
        {
            if (EnableVerboseLogs)
            {
                Log.Message("[PlanetaryAnnihilation-VERBOSE] " + message);
            }
        }

        private void LogError(string message)
        {
            if (EnableErrorLogs)
            {
                Log.Error("[PlanetaryAnnihilation-ERROR] " + message);
            }
        }

        public void StartBombardment()
        {
            LogDebug("=== INITIALIZING PLANETARY BOMBARDMENT ===");
            LogDebug($"Strike interval: {StrikeInterval.min}-{StrikeInterval.max} ticks");
            LogDebug($"Nuclear strikes at: {FirstNuclearStrike}, {SecondNuclearStrike}, {ThirdNuclearStrike}");

            bombardmentActive = true;
            nextStrikeTick = Find.TickManager.TicksGame + 60;
            strikeCounter = 0;
            nuclearStrikesDone = 0;
            heatPhaseActive = false;
            heatPhaseStartTick = Find.TickManager.TicksGame + 18000;
            nextColonistCheckTick = Find.TickManager.TicksGame + 60;
            gameEndTriggered = false;
            finalLetterSent = false;

            LogDebug($"Bombardment activated. First strike at tick {nextStrikeTick}");

            Messages.Message("HSK_PlanetaryAnnihilation_BombardmentStart".Translate(), MessageTypeDefOf.ThreatBig);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!bombardmentActive) return;

            int currentTick = Find.TickManager.TicksGame;

            // Проверка на завершение игры после окончания бомбардировки
            if (!bombardmentActive && !gameEndTriggered)
            {
                gameEndTriggered = true;
                EndGame();
                return;
            }

            if (currentTick >= nextColonistCheckTick)
            {
                nextColonistCheckTick = currentTick + 60;

                if (AreAllColonistsDead())
                {
                    LogDebug("All colonists dead. Stopping bombardment.");
                    Messages.Message("HSK_PlanetaryAnnihilation_AllDead".Translate(), MessageTypeDefOf.NegativeEvent);
                    bombardmentActive = false;
                    return;
                }
            }

            if (!heatPhaseActive && currentTick >= heatPhaseStartTick)
            {
                heatPhaseActive = true;
                StartHeatPhase();
            }

            if (currentTick >= nextStrikeTick)
            {
                strikeCounter++;
                LogDebug($"Strike #{strikeCounter}/{MaxIonStrikes} at tick {currentTick}");

                // Проверяем, нужно ли нанести ядерный удар
                if (strikeCounter == FirstNuclearStrike && nuclearStrikesDone < 1)
                {
                    LogDebug("=== FIRST NUCLEAR STRIKE ===");
                    LaunchNuclearStrike(1);
                    nuclearStrikesDone++;
                }
                else if (strikeCounter == SecondNuclearStrike && nuclearStrikesDone < 2)
                {
                    LogDebug("=== SECOND NUCLEAR STRIKE ===");
                    LaunchNuclearStrike(2);
                    nuclearStrikesDone++;
                }
                else if (strikeCounter == ThirdNuclearStrike && nuclearStrikesDone < 3)
                {
                    LogDebug("=== THIRD NUCLEAR STRIKE ===");
                    LaunchNuclearStrike(3);
                    nuclearStrikesDone++;
                }
                else
                {
                    LaunchSimultaneousIonBeams();
                }

                if (strikeCounter >= MaxIonStrikes)
                {
                    LogDebug($"=== ALL {MaxIonStrikes} STRIKES COMPLETE ===");
                    bombardmentActive = false;
                    // Отправляем финальное письмо
                    if (!finalLetterSent)
                    {
                        finalLetterSent = true;
                        SendFinalLetter();
                    }
                    return;
                }

                nextStrikeTick = currentTick + StrikeInterval.RandomInRange;
            }
        }

        private bool AreAllColonistsDead()
        {
            if (map == null) return true;

            if (map.mapPawns.FreeColonistsCount > 0)
                return false;

            foreach (Pawn pawn in map.mapPawns.AllPawns)
            {
                if (pawn.IsColonist && !pawn.Dead)
                    return false;
            }

            return true;
        }

        private void StartHeatPhase()
        {
            if (map == null) return;

            LogDebug("=== HEAT PHASE ACTIVATED ===");
            Messages.Message("HSK_PlanetaryAnnihilation_HeatPhase".Translate(), MessageTypeDefOf.ThreatBig);

            int firesStarted = 0;
            foreach (IntVec3 cell in map.AllCells)
            {
                if (Rand.Chance(0.3f))
                {
                    FireUtility.TryStartFireIn(cell, map, Rand.Range(0.5f, 1.0f), null);
                    firesStarted++;
                }
            }
            LogDebug($"Heat phase: {firesStarted} fires started");

            GameCondition heatCondition = GameConditionMaker.MakeCondition(
                GameConditionDefOf.HeatWave,
                6000
            );
            map.GameConditionManager.RegisterCondition(heatCondition);
        }

        private void LaunchSimultaneousIonBeams()
        {
            if (map == null) return;

            for (int i = 0; i < SimultaneousBeams; i++)
            {
                IntVec3 targetCell = CellFinder.RandomCell(map);
                SK_Utility.PowerBeamStrike(null, map, targetCell, Rand.Range(300, 500));
                CreateLavaCellsAround(targetCell);
            }

            Find.CameraDriver.shaker.DoShake(0.5f);
        }

        private IntVec3 FindBaseCenter()
        {
            if (map == null) return IntVec3.Invalid;

            List<Building> playerBuildings = map.listerBuildings.allBuildingsColonist;

            if (playerBuildings.Count > 0)
            {
                Vector3 center = Vector3.zero;
                int count = 0;

                foreach (Building building in playerBuildings)
                {
                    if (building.def.category == ThingCategory.Building)
                    {
                        center += building.Position.ToVector3();
                        count++;
                    }
                }

                if (count > 0)
                {
                    center /= count;
                    IntVec3 baseCenter = new IntVec3((int)center.x, 0, (int)center.z);

                    if (baseCenter.InBounds(map))
                    {
                        return baseCenter;
                    }
                }
                return playerBuildings[0].Position;
            }

            if (map.mapPawns.FreeColonistsCount > 0)
                return map.mapPawns.FreeColonists[0].Position;

            return map.Center;
        }

        private void SendNuclearStrikeLetter(int strikeNumber)
        {
            LogDebug($"Sending nuclear strike #{strikeNumber} letter");

            string label = "HSK_PlanetaryAnnihilation_NuclearLabel".Translate();
            string text = "HSK_PlanetaryAnnihilation_NuclearText".Translate(strikeNumber.ToString());

            Find.LetterStack.ReceiveLetter(
                label,
                text,
                LetterDefOf.ThreatBig,
                new LookTargets(map.mapPawns.FreeColonists),
                null,
                null,
                null,
                null,
                0,
                false
            );
        }

        private void SendFinalLetter()
        {
            LogDebug("Sending final bombardment complete letter");

            string label = "HSK_PlanetaryAnnihilation_CompleteLabel".Translate();
            string text = "HSK_PlanetaryAnnihilation_CompleteText".Translate();

            Find.LetterStack.ReceiveLetter(
                label,
                text,
                LetterDefOf.NegativeEvent,
                new LookTargets(map.mapPawns.FreeColonists),
                null,
                null,
                null,
                null,
                0,
                false
            );
        }

        private void LaunchNuclearStrike(int strikeNumber)
        {
            if (map == null) return;

            LogDebug($"=== NUCLEAR STRIKE #{strikeNumber} INITIATED ===");

            IntVec3 targetCell = FindBaseCenter();

            if (!targetCell.IsValid || !targetCell.InBounds(map))
            {
                targetCell = map.Center;
            }

            LogDebug($"Nuclear strike #{strikeNumber} target: {targetCell}");

            SendNuclearStrikeLetter(strikeNumber);
            Messages.Message($"HSK_PlanetaryAnnihilation_NuclearStrike_{strikeNumber}".Translate(), MessageTypeDefOf.ThreatBig);

            // Предупреждающие удары
            for (int i = 0; i < 5; i++)
            {
                IntVec3 warningCell = targetCell + new IntVec3(Rand.Range(-15, 16), 0, Rand.Range(-15, 16));
                if (warningCell.InBounds(map))
                {
                    SK_Utility.PowerBeamStrike(null, map, warningCell, Rand.Range(100, 200));
                }
            }

            // Основной ядерный удар
            for (int i = 0; i < 5; i++)
            {
                IntVec3 blastCell = targetCell + new IntVec3(Rand.Range(-8, 9), 0, Rand.Range(-8, 9));
                if (blastCell.InBounds(map))
                {
                    SK_Utility.PowerBeamStrike(null, map, blastCell, Rand.Range(1200, 1800));
                }
            }

            // Лава вокруг эпицентра
            int lavaRadius = 20;
            int lavaCreated = 0;
            TerrainDef lavaTerrain = DefDatabase<TerrainDef>.GetNamedSilentFail("LavaShallow");

            if (lavaTerrain != null)
            {
                for (int dx = -lavaRadius; dx <= lavaRadius; dx++)
                {
                    for (int dz = -lavaRadius; dz <= lavaRadius; dz++)
                    {
                        float distance = Mathf.Sqrt(dx * dx + dz * dz);
                        if (distance <= lavaRadius && Rand.Chance(1f - distance / lavaRadius))
                        {
                            IntVec3 lavaCell = targetCell + new IntVec3(dx, 0, dz);
                            if (lavaCell.InBounds(map))
                            {
                                map.terrainGrid.SetTerrain(lavaCell, lavaTerrain);
                                FireUtility.TryStartFireIn(lavaCell, map, 3.0f, null);
                                lavaCreated++;
                            }
                        }
                    }
                }
                LogDebug($"Nuclear strike #{strikeNumber} lava created: {lavaCreated} cells");
            }

            // Уничтожение врагов поблизости
            int enemiesKilled = 0;
            List<Thing> allThings = map.listerThings.AllThings;
            foreach (Thing thing in allThings.ToList())
            {
                if (thing is Pawn pawn && pawn.Faction != Faction.OfPlayer && !pawn.Dead)
                {
                    float dist = Vector3.Distance(pawn.Position.ToVector3(), targetCell.ToVector3());
                    if (dist <= lavaRadius)
                    {
                        pawn.Kill(null);
                        enemiesKilled++;
                    }
                }
            }
            LogDebug($"Enemies destroyed by nuclear strike #{strikeNumber}: {enemiesKilled}");

            Find.CameraDriver.shaker.DoShake(2.0f);

            LogDebug($"=== NUCLEAR STRIKE #{strikeNumber} COMPLETE ===");
        }

        private void CreateLavaCellsAround(IntVec3 center)
        {
            if (map == null) return;

            TerrainDef lavaTerrain = DefDatabase<TerrainDef>.GetNamedSilentFail("LavaShallow");

            if (lavaTerrain == null) return;

            List<IntVec3> possiblePositions = new List<IntVec3>();

            for (int dx = -6; dx <= 6; dx++)
            {
                for (int dz = -6; dz <= 6; dz++)
                {
                    if (Math.Abs(dx) + Math.Abs(dz) > 8) continue;

                    float distanceFactor = 1f - (Math.Abs(dx) + Math.Abs(dz)) / 9f;
                    if (Rand.Value > distanceFactor + 0.3f) continue;

                    IntVec3 candidate = center + new IntVec3(dx, 0, dz);

                    if (candidate.InBounds(map) && !IsCellProtected(candidate))
                    {
                        possiblePositions.Add(candidate);
                    }
                }
            }

            int targetLavaCount = Rand.Range(8, 12);
            int createdCount = 0;

            for (int i = 0; i < possiblePositions.Count; i++)
            {
                int randomIndex = Rand.Range(i, possiblePositions.Count);
                IntVec3 temp = possiblePositions[i];
                possiblePositions[i] = possiblePositions[randomIndex];
                possiblePositions[randomIndex] = temp;
            }

            foreach (IntVec3 lavaCell in possiblePositions)
            {
                if (createdCount >= targetLavaCount) break;

                TerrainDef currentTerrain = map.terrainGrid.TerrainAt(lavaCell);
                if (currentTerrain?.defName == "LavaShallow") continue;

                foreach (Thing thing in map.thingGrid.ThingsListAt(lavaCell).ToList())
                {
                    if (thing.def.destroyable)
                    {
                        Building building = thing as Building;
                        if (building == null || building.Faction != Faction.OfPlayer)
                        {
                            thing.Destroy();
                        }
                    }
                }

                map.terrainGrid.SetTerrain(lavaCell, lavaTerrain);
                FireUtility.TryStartFireIn(lavaCell, map, 1.5f, null);
                createdCount++;
            }
        }

        private bool IsCellProtected(IntVec3 cell)
        {
            if (map == null) return false;

            foreach (Thing thing in map.thingGrid.ThingsListAt(cell))
            {
                if (thing is Building building && building.Faction == Faction.OfPlayer)
                    return true;
                if (thing is Pawn pawn && pawn.IsColonist && pawn.Faction == Faction.OfPlayer)
                    return true;
            }
            return false;
        }

        private void EndGame()
        {
            LogDebug("=== ENDING GAME ===");

            DiaNode dialogNode = new DiaNode("HSK_PlanetaryAnnihilation_EndText".Translate());

            DiaOption endGameOption = new DiaOption("HSK_PlanetaryAnnihilation_EndButton".Translate());
            endGameOption.resolveTree = true;
            endGameOption.action = GameOver;

            dialogNode.options.Add(endGameOption);
            Find.WindowStack.Add(new Dialog_NodeTree(dialogNode, true, false, null));

            Find.LetterStack.ReceiveLetter(
                "HSK_PlanetaryAnnihilation_EndLabel".Translate(),
                "HSK_PlanetaryAnnihilation_EndText".Translate(),
                LetterDefOf.NegativeEvent
            );
        }

        private void GameOver()
        {
            LogDebug("GAME OVER - Killing all pawns and returning to menu");

            if (Current.Game?.Maps != null)
            {
                foreach (Map m in Current.Game.Maps.ToList())
                {
                    if (m != null)
                    {
                        foreach (Pawn pawn in m.mapPawns.AllPawns.ToList())
                        {
                            if (!pawn.Dead)
                                pawn.Kill(null);
                        }
                    }
                }
            }

            GenScene.GoToMainMenu();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref bombardmentActive, "bombardmentActive", false);
            Scribe_Values.Look(ref nextStrikeTick, "nextStrikeTick", 0);
            Scribe_Values.Look(ref strikeCounter, "strikeCounter", 0);
            Scribe_Values.Look(ref nuclearStrikesDone, "nuclearStrikesDone", 0);
            Scribe_Values.Look(ref heatPhaseActive, "heatPhaseActive", false);
            Scribe_Values.Look(ref heatPhaseStartTick, "heatPhaseStartTick", 0);
            Scribe_Values.Look(ref nextColonistCheckTick, "nextColonistCheckTick", 0);
            Scribe_Values.Look(ref gameEndTriggered, "gameEndTriggered", false);
            Scribe_Values.Look(ref finalLetterSent, "finalLetterSent", false);
        }
    }

    public class GameCondition_PlanetaryBombardment : GameCondition
    {
        public override string Label => "HSK_PlanetaryAnnihilation_ConditionLabel".Translate();
        public override string Description => "HSK_PlanetaryAnnihilation_ConditionDescription".Translate();
        public override float TemperatureOffset() => 15f;

        public override void GameConditionTick()
        {
            base.GameConditionTick();
            if (Rand.Chance(0.005f) && SingleMap != null)
            {
                IntVec3 randomCell = CellFinder.RandomCell(SingleMap);
                FireUtility.TryStartFireIn(randomCell, SingleMap, Rand.Range(0.3f, 0.7f), null);
            }
        }
    }
}
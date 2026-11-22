using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace HSK.Watcher.Content.Incident
{
    public class GreenyZyabraMod : Mod
    {
        public GreenyZyabraMod(ModContentPack content) : base(content)
        {
            Log.Message("[GreenyZyabra] Mod loaded successfully.");
        }
    }

    // ========== УТИЛИТЫ ==========

    public static class GreenyZyabraUtils
    {
        public static readonly List<string> TargetNames = new List<string>
        {
            "Andrey Greeny", "Greeny", "Грини", "Grini", "Andrey Grini", "Андрей Грини",
            "Greenie", "Grinny", "Гринни", "Andrey Greenie", "Андрей Гринни", "Гриня",
            "Grinya", "Andrey Grinya", "Андрей Гриня", "Green", "Грин"
        };

        private static readonly Regex PrefixRegex = new Regex(@"^\s*\([^)]+\)\s*", RegexOptions.Compiled);

        public static bool NameMatches(string pawnName, string targetName)
        {
            if (string.IsNullOrEmpty(pawnName))
                return false;

            string cleanedName = PrefixRegex.Replace(pawnName, "").Trim();
            return cleanedName.Equals(targetName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsValidTarget(Pawn pawn)
        {
            if (pawn == null || pawn.gender != Gender.Female)
                return false;

            return TargetNames.Any(name =>
                NameMatches(pawn.Name.ToStringFull, name) ||
                NameMatches(pawn.Name.ToStringShort, name) ||
                NameMatches(pawn.LabelShort, name)
            );
        }
    }

    // ========== ДИАЛОГ ==========

    public class Dialog_SummonGreenyZyabra : Window
    {
        private readonly Map map;
        private readonly Pawn targetPawn;
        private readonly MapComponent_GreenyZyabraCooldown cooldownComp;
        private readonly Action onConfirm;
        private readonly Action onCancel;

        public override Vector2 InitialSize => new Vector2(560f, 310f);

        public Dialog_SummonGreenyZyabra(Map map, Pawn targetPawn,
            MapComponent_GreenyZyabraCooldown cooldownComp, Action onConfirm, Action onCancel)
        {
            this.map = map;
            this.targetPawn = targetPawn;
            this.cooldownComp = cooldownComp;
            this.onConfirm = onConfirm;
            this.onCancel = onCancel;
            this.closeOnClickedOutside = false;
            this.absorbInputAroundWindow = true;
            this.forcePause = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(inRect.x + 10f, inRect.y + 10f, inRect.width - 20f, 32f);
            Widgets.Label(titleRect, "GreenyZyabra_SummonTitle".Translate());
            Text.Font = GameFont.Small;

            Rect textRect = new Rect(inRect.x + 10f, inRect.y + 45f, inRect.width - 20f, 170f);
            string message = "GreenyZyabra_SummonText".Translate(
                targetPawn.Name.ToStringShort,
                targetPawn.gender.GetLabel()
            );
            Widgets.Label(textRect, message);

            float buttonWidth = 180f;
            float buttonHeight = 40f;
            float spacing = 40f;
            float startX = (inRect.width - (buttonWidth * 2 + spacing)) / 2f;
            float buttonY = inRect.yMax - buttonHeight - 15f;

            if (Widgets.ButtonText(new Rect(startX, buttonY, buttonWidth, buttonHeight), "GreenyZyabra_SummonConfirm".Translate()))
            {
                onConfirm?.Invoke();
                Close();
            }

            if (Widgets.ButtonText(new Rect(startX + buttonWidth + spacing, buttonY, buttonWidth, buttonHeight), "GreenyZyabra_SummonCancel".Translate()))
            {
                onCancel?.Invoke();
                Close();
            }
        }
    }

    // ========== WORLD COMPONENT ==========

    public class GreenyZyabraWorldComponent : WorldComponent
    {
        private int lastCheckTick = -1;
        private const int CheckInterval = 30000;
        private const int CooldownTicks = 5400000; // 90 дней

        private bool dialogIsOpen = false;

        public GreenyZyabraWorldComponent(World world) : base(world) { }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            if (Find.TickManager.TicksGame < lastCheckTick + CheckInterval)
                return;

            lastCheckTick = Find.TickManager.TicksGame;

            foreach (Map map in Find.Maps)
            {
                if (!map.IsPlayerHome)
                    continue;

                Pawn targetPawn = map.mapPawns.FreeColonists.FirstOrDefault(p => GreenyZyabraUtils.IsValidTarget(p));

                if (targetPawn != null)
                {
                    ShowSummonDialog(map, targetPawn);
                }
            }
        }

        private void ShowSummonDialog(Map map, Pawn targetPawn)
        {
            MapComponent_GreenyZyabraCooldown cooldownComp = map.GetComponent<MapComponent_GreenyZyabraCooldown>();
            if (cooldownComp == null)
            {
                cooldownComp = new MapComponent_GreenyZyabraCooldown(map);
                map.components.Add(cooldownComp);
            }

            if (Find.TickManager.TicksGame < cooldownComp.lastEventTick + CooldownTicks)
                return;

            if (dialogIsOpen)
                return;

            dialogIsOpen = true;

            Find.WindowStack.Add(new Dialog_SummonGreenyZyabra(
                map,
                targetPawn,
                cooldownComp,
                () => ExecuteEvent(map, targetPawn, cooldownComp),
                () => CancelSummon(map, targetPawn, cooldownComp)
            ));
        }

        private void CancelSummon(Map map, Pawn targetPawn, MapComponent_GreenyZyabraCooldown cooldownComp)
        {
            cooldownComp.lastEventTick = Find.TickManager.TicksGame;

            Log.Message("[GreenyZyabra] Player refused to summon GreenyZyabra. Next attempt in 90 days.");
            Messages.Message("GreenyZyabra_SummonCancelled".Translate(), MessageTypeDefOf.NeutralEvent);

            dialogIsOpen = false;
        }

        public void ExecuteEvent(Map map, Pawn targetPawn, MapComponent_GreenyZyabraCooldown cooldownComp)
        {
            cooldownComp.lastEventTick = Find.TickManager.TicksGame;
            dialogIsOpen = false;

            // 1. Solar eclipse
            GameConditionManager conditionManager = map.GameConditionManager;
            if (conditionManager != null)
            {
                GameCondition eclipse = GameConditionMaker.MakeCondition(GameConditionDefOf.Eclipse, Rand.Range(2500, 7500));
                conditionManager.RegisterCondition(eclipse);
            }

            // 2. Spawn creature
            IntVec3 spawnCell = FindValidSpawnCell(map);
            if (spawnCell.IsValid)
            {
                SpawnGreenyZyabra(map, spawnCell);
            }
            else
            {
                Log.Warning("[GreenyZyabra] Could not find valid spawn cell!");
            }

            // 3. Play sound
            SoundDef sound = DefDatabase<SoundDef>.GetNamed("GreenyZyabra_InvasionSound", false);
            if (sound != null)
            {
                sound.PlayOneShot(SoundInfo.InMap(new TargetInfo(map.Center, map)));
            }

            // 4. Letter
            Find.LetterStack.ReceiveLetter(
                "GreenyZyabraInvasionLabel".Translate(),
                "GreenyZyabraInvasionText".Translate(targetPawn.Name.ToStringShort),
                LetterDefOf.ThreatSmall,
                new TargetInfo(spawnCell.IsValid ? spawnCell : map.Center, map)
            );

            // 5. Message
            Messages.Message("GreenyZyabraEvent_Message_Start".Translate(targetPawn.Name.ToStringShort), MessageTypeDefOf.ThreatBig);
        }

        private void SpawnGreenyZyabra(Map map, IntVec3 spawnCell)
        {
            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamed("GreenyZyabra", false);
            if (kindDef == null)
            {
                Log.Warning("[GreenyZyabra] PawnKindDef 'GreenyZyabra' not found!");
                return;
            }

            Faction faction = Find.FactionManager.FirstFactionOfDef(FactionDefOf.Pirate);
            if (faction == null)
            {
                faction = Faction.OfPlayerSilentFail;
            }

            Pawn pawn = PawnGenerator.GeneratePawn(kindDef, faction);
            if (pawn != null)
            {
                GenSpawn.Spawn(pawn, spawnCell, map);
                pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter);

                if (faction != null && faction != Faction.OfPlayer)
                {
                    faction.TryAffectGoodwillWith(Faction.OfPlayer, -100);
                    faction.SetRelationDirect(Faction.OfPlayer, FactionRelationKind.Hostile);
                }
            }
        }

        private IntVec3 FindValidSpawnCell(Map map)
        {
            IntVec3 result = IntVec3.Invalid;

            if (RCellFinder.TryFindRandomPawnEntryCell(out result, map, CellFinder.EdgeRoadChance_Neutral, true, null))
            {
                if (IsWalkableAndClear(map, result))
                    return result;
            }

            for (int i = 0; i < 200; i++)
            {
                IntVec3 edgeCell = CellFinder.RandomEdgeCell(map);
                if (IsWalkableAndClear(map, edgeCell) && IsReachableFromEdge(map, edgeCell))
                    return edgeCell;
            }

            for (int i = 0; i < 100; i++)
            {
                IntVec3 edgeCell = CellFinder.RandomEdgeCell(map);
                if (CellFinder.TryFindRandomReachableNearbyCell(
                    edgeCell, map, 10f,
                    TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false),
                    c => IsWalkableAndClear(map, c) && IsReachableFromEdge(map, c),
                    null, out IntVec3 nearbyCell))
                {
                    return nearbyCell;
                }
            }

            return IntVec3.Invalid;
        }

        private bool IsWalkableAndClear(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map))
                return false;

            if (!cell.Walkable(map))
                return false;

            RoofDef roof = map.roofGrid.RoofAt(cell);
            if (roof != null && roof.isThickRoof)
                return false;

            List<Thing> things = map.thingGrid.ThingsListAt(cell);
            foreach (Thing thing in things)
            {
                if (thing.def.passability == Traversability.Impassable)
                    return false;
            }

            return true;
        }

        private bool IsReachableFromEdge(Map map, IntVec3 cell)
        {
            if (cell.DistanceToEdge(map) <= 1)
                return true;

            foreach (IntVec3 edgeCell in CellRect.WholeMap(map).EdgeCells)
            {
                if (map.reachability.CanReach(cell, edgeCell, PathEndMode.OnCell,
                    TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false)))
                {
                    return true;
                }
            }

            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastCheckTick, "lastCheckTick", -1);
            Scribe_Values.Look(ref dialogIsOpen, "dialogIsOpen", false);
        }
    }

    // ========== MAP COMPONENT ==========

    public class MapComponent_GreenyZyabraCooldown : MapComponent
    {
        public int lastEventTick = -9999999;

        public MapComponent_GreenyZyabraCooldown(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastEventTick, "lastEventTick", -9999999);
        }
    }

    // ========== INCIDENT WORKER ==========

    public class IncidentWorker_GreenyZyabraInvasion : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (map == null) return false;

            Pawn targetPawn = map.mapPawns.FreeColonists.FirstOrDefault(p => GreenyZyabraUtils.IsValidTarget(p));
            if (targetPawn == null) return false;

            MapComponent_GreenyZyabraCooldown cooldownComp = map.GetComponent<MapComponent_GreenyZyabraCooldown>();
            if (cooldownComp == null)
            {
                cooldownComp = new MapComponent_GreenyZyabraCooldown(map);
                map.components.Add(cooldownComp);
            }

            const int CooldownTicks = 5400000;
            if (Find.TickManager.TicksGame < cooldownComp.lastEventTick + CooldownTicks)
                return false;

            return true;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (map == null) return false;

            Pawn targetPawn = map.mapPawns.FreeColonists.FirstOrDefault(p => GreenyZyabraUtils.IsValidTarget(p));
            if (targetPawn == null) return false;

            MapComponent_GreenyZyabraCooldown cooldownComp = map.GetComponent<MapComponent_GreenyZyabraCooldown>();
            if (cooldownComp == null)
            {
                cooldownComp = new MapComponent_GreenyZyabraCooldown(map);
                map.components.Add(cooldownComp);
            }

            GreenyZyabraWorldComponent worldComp = Find.World.GetComponent<GreenyZyabraWorldComponent>();
            if (worldComp == null)
            {
                Log.Error("[GreenyZyabra] WorldComponent not found! Creating new one...");
                worldComp = new GreenyZyabraWorldComponent(Find.World);
                Find.World.components.Add(worldComp);
            }

            Find.WindowStack.Add(new Dialog_SummonGreenyZyabra(
                map,
                targetPawn,
                cooldownComp,
                () => worldComp.ExecuteEvent(map, targetPawn, cooldownComp),
                () => {
                    cooldownComp.lastEventTick = Find.TickManager.TicksGame;
                    Messages.Message("GreenyZyabra_SummonCancelled".Translate(), MessageTypeDefOf.NeutralEvent);
                }
            ));

            return true;
        }
    }

    // ========== HARMONY PATCHES ==========

    [StaticConstructorOnStartup]
    public static class HarmonyLoader
    {
        static HarmonyLoader()
        {
            try
            {
                var harmony = new Harmony("greenyzyabra.butcher");

                var original = AccessTools.Method(typeof(Corpse), "ButcherProducts");
                var postfix = AccessTools.Method(typeof(Corpse_ButcherProducts_Patch), "Postfix");

                harmony.Patch(original, postfix: new HarmonyMethod(postfix));

                Log.Message("[GreenyZyabra] Harmony patches applied successfully.");
            }
            catch (Exception ex)
            {
                Log.Error($"[GreenyZyabra] Failed to apply Harmony patches: {ex.Message}");
            }
        }
    }

    public static class Corpse_ButcherProducts_Patch
    {
        public static void Postfix(Corpse __instance, Pawn butcher, float efficiency)
        {
            try
            {
                if (__instance.InnerPawn?.def?.defName != "GreenyZyabra") return;

                var comp = __instance.InnerPawn.GetComp<CompSpawnMinifiedOnButcher>();
                if (comp == null || !comp.shouldSpawnCarpet) return;

                var props = comp.Props;
                if (props.buildingDef == null) return;

                comp.shouldSpawnCarpet = false;

                for (int i = 0; i < props.count; i++)
                {
                    Building building = (Building)ThingMaker.MakeThing(props.buildingDef);

                    CompQuality compQuality = building.TryGetComp<CompQuality>();
                    if (compQuality != null)
                    {
                        int skillLevel = butcher?.skills?.GetSkill(SkillDefOf.Artistic)?.Level ?? 3;
                        QualityCategory quality = QualityUtility.GenerateQualityCreatedByPawn(skillLevel, false);
                        compQuality.SetQuality(quality, ArtGenerationContext.Colony);
                    }

                    MinifiedThing minified = building.MakeMinified();
                    if (minified != null)
                    {
                        GenPlace.TryPlaceThing(minified, __instance.Position, __instance.Map, ThingPlaceMode.Near);
                    }
                }

                Messages.Message(
                    "Recovered greenzyabrya carpet from the hide!",
                    new TargetInfo(__instance.Position, __instance.Map),
                    MessageTypeDefOf.PositiveEvent
                );
            }
            catch (Exception ex)
            {
                Log.Error($"[GreenyZyabra] Error in ButcherProducts patch: {ex.Message}");
            }
        }
    }

    // ========== COMP ==========

    public class CompProperties_SpawnMinifiedOnButcher : CompProperties
    {
        public ThingDef buildingDef;
        public int count = 1;

        public CompProperties_SpawnMinifiedOnButcher()
        {
            compClass = typeof(CompSpawnMinifiedOnButcher);
        }
    }

    public class CompSpawnMinifiedOnButcher : ThingComp
    {
        public bool shouldSpawnCarpet = true;

        public CompProperties_SpawnMinifiedOnButcher Props =>
            (CompProperties_SpawnMinifiedOnButcher)props;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref shouldSpawnCarpet, "shouldSpawnCarpet", true);
        }
    }
}
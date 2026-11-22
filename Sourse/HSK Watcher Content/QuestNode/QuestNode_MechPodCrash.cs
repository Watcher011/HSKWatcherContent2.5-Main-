//using RimWorld;
//using RimWorld.Planet;
//using RimWorld.QuestGen;
//using SK;
//using System.Collections.Generic;
//using System.Linq;
//using UnityEngine;
//using Verse;
//using Verse.AI.Group;

//namespace HSK.Watcher.Content
//{
//    public class QuestNode_MechPodCrash : QuestNode
//    {
//        private const string MechKindDefName = "Mech_Crawler";
//        private const int MechCount = 3;
//        private const int VariantCount = 7;

//        protected override void RunInt()
//        {
//            Log.Message("[HSK] === QuestNode_MechPodCrash.RunInt START ===");

//            Slate slate = QuestGen.slate;
//            Map map = slate.Get<Map>("map");
//            if (map == null) map = QuestGen_Get.GetMap();
//            if (map == null)
//            {
//                Log.Warning("[HSK] RunInt: map == null — aborting");
//                return;
//            }
//            Log.Message($"[HSK] RunInt: map = {map}, size = {map.Size}");

//            Pawn refugee = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
//                PawnKindDefOf.SpaceRefugee,
//                forceGenerateNewPawn: true));
//            refugee.guest.Recruitable = true;
//            QuestGen.AddToGeneratedPawns(refugee);
//            if (!refugee.IsWorldPawn())
//            {
//                Find.WorldPawns.PassToWorld(refugee);
//            }
//            Log.Message($"[HSK] RunInt: refugee = {refugee.LabelShort}");

//            List<Pawn> mechs = new List<Pawn>();
//            PawnKindDef mechKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(MechKindDefName)
//                                   ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Scyther");
//            Log.Message($"[HSK] RunInt: mechKind = {(mechKind == null ? "NULL!" : mechKind.defName)}");

//            if (mechKind != null)
//            {
//                for (int i = 0; i < MechCount; i++)
//                {
//                    Pawn mech = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
//                        mechKind,
//                        Faction.OfMechanoids,
//                        forceGenerateNewPawn: true));
//                    QuestGen.AddToGeneratedPawns(mech);
//                    if (!mech.IsWorldPawn())
//                    {
//                        Find.WorldPawns.PassToWorld(mech);
//                    }
//                    mechs.Add(mech);
//                }
//            }
//            Log.Message($"[HSK] RunInt: mechs generated = {mechs.Count}");

//            int crashIndex = Rand.RangeInclusive(1, VariantCount);
//            int breachIndex = Rand.RangeInclusive(1, VariantCount);
//            string name = refugee.LabelShort;
//            string crashLabel = "HSK_MechPodCrash_LetterLabel".Translate().Resolve();
//            string crashText = ("HSK_MechPodCrash_LetterText_" + crashIndex).Translate(name).Resolve();
//            string breachLabel = "HSK_MechPodBreach_LetterLabel".Translate().Resolve();
//            string breachText = ("HSK_MechPodBreach_LetterText_" + breachIndex).Translate(name).Resolve();

//            slate.Set("refugee", refugee);
//            slate.Set("mechs", mechs);
//            slate.Set("crashLabel", crashLabel);
//            slate.Set("crashText", crashText);
//            slate.Set("breachLabel", breachLabel);
//            slate.Set("breachText", breachText);

//            Log.Message($"[HSK] RunInt: crashIndex={crashIndex}, breachIndex={breachIndex}");
//            Log.Message("[HSK] === QuestNode_MechPodCrash.RunInt END ===");
//        }

//        protected override bool TestRunInt(Slate slate)
//        {
//            slate.Set("mechs", new List<Pawn>());
//            slate.Set("crashLabel", "");
//            slate.Set("crashText", "");
//            slate.Set("breachLabel", "");
//            slate.Set("breachText", "");
//            return true;
//        }
//    }

//    public class MechPodFlying : DropPodFlying
//    {
//        private static ThingDef cachedLandedDef;

//        protected override ThingDef LandedThingDef
//        {
//            get
//            {
//                if (cachedLandedDef == null)
//                {
//                    cachedLandedDef = DefDatabase<ThingDef>.GetNamedSilentFail("HSK_MechPodLanded");
//                    Log.Message($"[HSK] MechPodFlying.LandedThingDef requested → {(cachedLandedDef == null ? "NULL! (def not found)" : cachedLandedDef.defName)}");
//                    if (cachedLandedDef != null)
//                    {
//                        Log.Message($"[HSK]   → thingClass = {cachedLandedDef.thingClass}, useHitPoints = {cachedLandedDef.useHitPoints}, comps = {cachedLandedDef.comps?.Count ?? 0}");
//                    }
//                }
//                return cachedLandedDef;
//            }
//        }
//    }

//    public class CompProperties_ReleaseCargoPawns : CompProperties
//    {
//        public bool joinPlayer = true;

//        public CompProperties_ReleaseCargoPawns()
//        {
//            compClass = typeof(CompReleaseCargoPawns);
//        }
//    }

//    public class CompReleaseCargoPawns : ThingComp
//    {
//        private const int TotalTicksForBurn = 180000; // 3 дня
//        private const int TicksPerFuel = 25000;       // 10 часов
//        private const int TotalFuelCount = 10;

//        public List<Pawn> ambushMechs = new List<Pawn>();
//        public string breachLabel;
//        public string breachText;
//        private bool triggered = false;
//        private int timerTicks = TotalTicksForBurn;
//        private int fuelSpawned = 0;
//        private List<IntVec3> fuelCells = new List<IntVec3>();

//        private CompProperties_ReleaseCargoPawns Props => (CompProperties_ReleaseCargoPawns)props;

//        public override void PostSpawnSetup(bool respawningAfterLoad)
//        {
//            base.PostSpawnSetup(respawningAfterLoad);

//            DropPodLanded landed = parent as DropPodLanded;
//            string cargoInfo = landed?.cargo == null
//                ? "NULL"
//                : $"count={landed.cargo.innerContainer.Count} [{string.Join(", ", landed.cargo.innerContainer.Select(t => t.def.defName))}]";
//            string compsInfo = (parent as ThingWithComps)?.AllComps.Select(c => c.GetType().Name).ToCommaList() ?? "none";

//            Log.Message($"[HSK] PostSpawnSetup: def={parent.def.defName}, HP={parent.HitPoints}/{parent.MaxHitPoints}, cargo={cargoInfo}");
//            Log.Message($"[HSK] PostSpawnSetup: comps = {compsInfo}");
//        }

//        public override IEnumerable<Gizmo> CompGetGizmosExtra()
//        {
//            foreach (var g in base.CompGetGizmosExtra()) yield return g;

//            if (!triggered && parent.Spawned && !parent.Destroyed)
//            {
//                yield return new Command_Action
//                {
//                    defaultLabel = "Вскрыть капсулу",
//                    defaultDesc = "Разрезать обшивку и посмотреть, что внутри.",
//                    icon = ContentFinder<Texture2D>.Get("UI/Commands/OpenCasket", false),
//                    action = () =>
//                    {
//                        Log.Message("[HSK] Gizmo: Break open pressed");
//                        parent.Destroy(DestroyMode.KillFinalize);
//                    }
//                };
//            }
//        }
//        public override void CompTick()
//        {
//            base.CompTick();
//            if (triggered) return;
//            if (fuelCells == null) fuelCells = new List<IntVec3>();

//            timerTicks--;

//            int ticksElapsed = TotalTicksForBurn - timerTicks;
//            if (ticksElapsed % 2500 == 0 && ticksElapsed > 0)
//            {
//                Log.Message($"[HSK] CompTick: elapsed={ticksElapsed / 2500}h, timerTicks={timerTicks}, fuelSpawned={fuelSpawned}, triggered={triggered}");
//            }

//            int targetFuel = Mathf.Min(TotalFuelCount, ticksElapsed / TicksPerFuel);
//            int attemptsThisTick = 0;
//            while (fuelSpawned < targetFuel && fuelSpawned < TotalFuelCount && attemptsThisTick < 40)
//            {
//                SpawnOilSmear();
//                attemptsThisTick++;
//            }

//            if (timerTicks <= 0)
//            {
//                Log.Message("[HSK] CompTick: timer expired → IgniteFuelAndBurn");
//                IgniteFuelAndBurn();
//            }
//        }

//        public override void PostDestroy(DestroyMode mode, Map previousMap)
//        {
//            Log.Message($"[HSK] PostDestroy: mode={mode}, triggered={triggered}, previousMap={(previousMap == null ? "NULL" : "OK")}, HP={parent.HitPoints}/{parent.MaxHitPoints}");
//            if (triggered || previousMap == null) return;
//            if (mode == DestroyMode.Deconstruct || mode == DestroyMode.KillFinalize)
//            {
//                Log.Message("[HSK] PostDestroy: calling TriggerAmbush with previousMap");
//                TriggerAmbush(forcedByTimer: false, fallbackMap: previousMap);
//            }
//            else
//            {
//                Log.Warning($"[HSK] PostDestroy: mode {mode} not handled — TriggerAmbush NOT called!");
//            }
//        }

//        private bool SpawnOilSmear()
//        {
//            Map map = parent?.Map;
//            if (map == null)
//            {
//                Log.Warning("[HSK] SpawnOilSmear: map == null");
//                return false;
//            }

//            ThingDef oilDef = DefDatabase<ThingDef>.GetNamedSilentFail("Filth_OilSmear");
//            if (oilDef == null)
//            {
//                Log.Error("[HSK] SpawnOilSmear: Filth_OilSmear def NOT FOUND!");
//                return false;
//            }

//            IntVec3 center = parent.Position;
//            for (int attempt = 0; attempt < 40; attempt++)
//            {
//                IntVec3 cell = CellFinder.RandomClosewalkCellNear(center, map, 5);
//                if (!cell.InBounds(map)) continue;
//                if (cell.Fogged(map)) continue;
//                if (fuelCells.Contains(cell)) continue;

//                if (FilthMaker.TryMakeFilth(cell, map, oilDef))
//                {
//                    fuelCells.Add(cell);
//                    fuelSpawned++;
//                    Log.Message($"[HSK] SpawnOilSmear: oil #{fuelSpawned} at {cell}");
//                    return true;
//                }
//            }
//            Log.Warning($"[HSK] SpawnOilSmear: 40 attempts failed (fuelSpawned={fuelSpawned})");
//            return false;
//        }

//        private void IgniteFuelAndBurn()
//        {
//            if (triggered) return;
//            triggered = true;
//            Log.Message("[HSK] IgniteFuelAndBurn START");

//            Map map = parent?.Map;
//            if (map == null)
//            {
//                Log.Warning("[HSK] IgniteFuelAndBurn: map == null");
//                return;
//            }

//            IntVec3 center = parent.Position;

//            int safety = 100;
//            while (fuelSpawned < TotalFuelCount && safety-- > 0)
//            {
//                if (!SpawnOilSmear()) break;
//            }
//            Log.Message($"[HSK] IgniteFuelAndBurn: final fuelSpawned={fuelSpawned}");

//            DropPodLanded landed = parent as DropPodLanded;
//            Log.Message($"[HSK] IgniteFuelAndBurn: parent as DropPodLanded = {(landed == null ? "NULL" : "OK")}");
//            if (landed?.cargo?.innerContainer != null)
//            {
//                ThingOwner container = landed.cargo.innerContainer;
//                Log.Message($"[HSK] IgniteFuelAndBurn: container.Count = {container.Count}");
//                for (int i = container.Count - 1; i >= 0; i--)
//                {
//                    if (container[i] is Pawn pawn && pawn.RaceProps.IsMechanoid)
//                    {
//                        container.Remove(pawn);
//                        Log.Message($"[HSK] IgniteFuelAndBurn: killing mech {pawn.LabelShort}");
//                        if (!pawn.Dead) pawn.Kill(null, null);
//                    }
//                }
//            }

//            ThingDef fuelDef = DefDatabase<ThingDef>.GetNamedSilentFail("Filth_Fuel");
//            Log.Message($"[HSK] IgniteFuelAndBurn: fuelDef = {(fuelDef == null ? "NULL!" : fuelDef.defName)}");

//            int ignitedCount = 0;
//            foreach (IntVec3 oilCell in fuelCells.ToList())
//            {
//                if (!oilCell.InBounds(map) || oilCell.Fogged(map)) continue;

//                bool ignited = false;
//                if (fuelDef != null)
//                {
//                    foreach (IntVec3 fuelCell in GenAdjFast.AdjacentCells8Way(oilCell).InRandomOrder())
//                    {
//                        if (!fuelCell.InBounds(map) || fuelCell.Fogged(map)) continue;
//                        if (fuelCell.GetFirstThing(map, fuelDef) != null) continue;
//                        if (FilthMaker.TryMakeFilth(fuelCell, map, fuelDef))
//                        {
//                            ignited = true;
//                            ignitedCount++;
//                            break;
//                        }
//                    }
//                }

//                if (!ignited)
//                {
//                    FireUtility.TryStartFireIn(oilCell, map, 1.0f, null);
//                    ignitedCount++;
//                }
//            }
//            Log.Message($"[HSK] IgniteFuelAndBurn: ignited {ignitedCount} oil cells");

//            if (parent.Spawned && !parent.Destroyed)
//            {
//                parent.TryAttachFire(1.0f, null);
//                Log.Message("[HSK] IgniteFuelAndBurn: attached fire to parent");
//            }

//            string finalLabel = breachLabel.NullOrEmpty() ? "Капсула воспламенилась" : breachLabel;
//            string finalText = breachText.NullOrEmpty()
//                ? "Масляные пятна вокруг капсулы воспламенились. Механоиды сгорели внутри."
//                : breachText;

//            Find.LetterStack.ReceiveLetter(finalLabel, finalText, LetterDefOf.NeutralEvent, new TargetInfo(center, map));
//            Log.Message("[HSK] IgniteFuelAndBurn END");
//        }

//        private void TriggerAmbush(bool forcedByTimer, Map fallbackMap = null)
//        {
//            if (triggered)
//            {
//                Log.Message("[HSK] TriggerAmbush: already triggered, skipping");
//                return;
//            }
//            triggered = true;
//            Log.Message($"[HSK] TriggerAmbush START (fallbackMap={(fallbackMap == null ? "NULL" : fallbackMap.ToString())}, forcedByTimer={forcedByTimer})");

//            Map map = parent?.Map ?? parent?.MapHeld ?? fallbackMap;
//            if (map == null)
//            {
//                Log.Error("[HSK] TriggerAmbush: ALL maps null — abort");
//                return;
//            }
//            Log.Message($"[HSK] TriggerAmbush: using map = {map}");

//            IntVec3 center = parent.PositionHeld.IsValid ? parent.PositionHeld : parent.Position;
//            if (!center.IsValid)
//            {
//                Log.Error("[HSK] TriggerAmbush: invalid center position");
//                return;
//            }
//            Log.Message($"[HSK] TriggerAmbush: center = {center}, parent = {parent.def.defName}");

//            DropPodLanded landed = parent as DropPodLanded;
//            Log.Message($"[HSK] TriggerAmbush: parent as DropPodLanded = {(landed == null ? "NULL" : "OK")}");
//            Log.Message($"[HSK] TriggerAmbush: cargo = {(landed?.cargo == null ? "NULL" : "OK")}");
//            Log.Message($"[HSK] TriggerAmbush: innerContainer = {(landed?.cargo?.innerContainer == null ? "NULL" : "OK")}");

//            if (landed?.cargo?.innerContainer != null)
//            {
//                ThingOwner container = landed.cargo.innerContainer;
//                Log.Message($"[HSK] TriggerAmbush: container.Count = {container.Count}");
//                List<Pawn> extractedPawns = new List<Pawn>();

//                for (int i = container.Count - 1; i >= 0; i--)
//                {
//                    if (container[i] is Pawn pawn)
//                    {
//                        container.Remove(pawn);
//                        extractedPawns.Add(pawn);
//                    }
//                }
//                Log.Message($"[HSK] TriggerAmbush: extractedPawns = {extractedPawns.Count}");

//                foreach (var pawn in extractedPawns)
//                {
//                    IntVec3 cell = CellFinder.RandomClosewalkCellNear(center, map, 3);
//                    GenSpawn.Spawn(pawn, cell, map);
//                    Log.Message($"[HSK] TriggerAmbush: spawned {pawn.LabelShort} ({pawn.def.defName}) at {cell}");

//                    if (pawn.RaceProps.Humanlike)
//                    {
//                        if (Props.joinPlayer && pawn.Faction != Faction.OfPlayer)
//                        {
//                            pawn.SetFaction(Faction.OfPlayer);
//                        }
//                    }
//                    else if (pawn.RaceProps.IsMechanoid)
//                    {
//                        if (pawn.Faction != Faction.OfMechanoids)
//                        {
//                            pawn.SetFaction(Faction.OfMechanoids);
//                        }
//                        ambushMechs.Add(pawn);
//                    }
//                }
//            }
//            else
//            {
//                Log.Warning("[HSK] TriggerAmbush: cargo innerContainer NULL — pawns are NOT released");
//            }

//            Log.Message($"[HSK] TriggerAmbush: ambushMechs.Count = {ambushMechs.Count}");

//            SpawnExoticLoot(map, center);

//            if (ambushMechs != null && ambushMechs.Count > 0)
//            {
//                List<Pawn> validMechs = ambushMechs.Where(m => m != null && !m.Dead && m.Spawned).ToList();
//                Log.Message($"[HSK] TriggerAmbush: validMechs = {validMechs.Count}");
//                if (validMechs.Count > 0)
//                {
//                    LordMaker.MakeNewLord(
//                        Faction.OfMechanoids,
//                        new LordJob_AssaultColony(Faction.OfMechanoids, canKidnap: false, canTimeoutOrFlee: false, sappers: false, useAvoidGridSmart: true),
//                        map,
//                        validMechs
//                    );

//                    string finalLabel = breachLabel.NullOrEmpty() ? "Активация засады" : breachLabel;
//                    string finalText = breachText.NullOrEmpty() ? "Механоиды вырвались из капсулы!" : breachText;

//                    Find.LetterStack.ReceiveLetter(
//                        finalLabel,
//                        finalText,
//                        LetterDefOf.PositiveEvent,
//                        new TargetInfo(center, map)
//                    );
//                    Log.Message("[HSK] TriggerAmbush: ambush letter sent");
//                }
//            }
//            Log.Message("[HSK] TriggerAmbush END");
//        }

//        private void SpawnExoticLoot(Map map, IntVec3 center)
//        {
//            Log.Message("[HSK] SpawnExoticLoot START");

//            ThingCategoryDef specialCat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("BuildingsSpecial");
//            Log.Message($"[HSK] SpawnExoticLoot: BuildingsSpecial cat = {(specialCat == null ? "NULL!" : specialCat.defName)}");

//            List<ThingDef> candidates = new List<ThingDef>();
//            if (specialCat != null)
//            {
//                candidates = DefDatabase<ThingDef>.AllDefs
//                    .Where(d => d.building != null
//                                && d.Minifiable
//                                && d.thingCategories != null
//                                && d.thingCategories.Contains(specialCat))
//                    .ToList();
//            }

//            Log.Message($"[HSK] SpawnExoticLoot: filter found {candidates.Count} candidates");

//            if (candidates.NullOrEmpty())
//            {
//                Log.Warning("[HSK] SpawnExoticLoot: filter empty → fallback");
//                candidates = new List<ThingDef>();
//                foreach (string name in new[] { "PsychicEmanator", "VanometricPowerCell", "InfiniteChemreactor" })
//                {
//                    ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
//                    if (d != null && d.Minifiable)
//                    {
//                        candidates.Add(d);
//                        Log.Message($"[HSK] SpawnExoticLoot: fallback + {name}");
//                    }
//                }
//            }

//            if (candidates.NullOrEmpty())
//            {
//                Log.Warning("[HSK] SpawnExoticLoot: NO candidates at all — spawn skipped");
//                return;
//            }

//            ThingDef chosen = candidates.RandomElement();
//            Log.Message($"[HSK] SpawnExoticLoot: chosen = {chosen.defName}");

//            Thing inner = ThingMaker.MakeThing(chosen);
//            if (inner == null)
//            {
//                Log.Warning($"[HSK] SpawnExoticLoot: MakeThing failed for {chosen.defName}");
//                return;
//            }

//            Thing minified = MinifyUtility.MakeMinified(inner);
//            if (minified == null)
//            {
//                Log.Warning($"[HSK] SpawnExoticLoot: Minify failed for {chosen.defName}");
//                return;
//            }
//            Log.Message($"[HSK] SpawnExoticLoot: minified = {minified.def.defName}");

//            IntVec3 cell = CellFinder.RandomClosewalkCellNear(center, map, 10);
//            if (!cell.IsValid || !cell.InBounds(map))
//            {
//                Log.Warning($"[HSK] SpawnExoticLoot: invalid cell {cell}, using center {center}");
//                cell = center;
//            }

//            GenSpawn.Spawn(minified, cell, map);
//            Log.Message($"[HSK] SpawnExoticLoot: SPAWNED {chosen.defName} at {cell} — spawned={minified.Spawned}");
//        }
//        private static bool HasTradeTagSafe(ThingDef d, string tagName)
//        {
//            if (d.tradeTags == null) return false;
//            foreach (object tag in d.tradeTags)
//            {
//                if (tag == null) continue;
//                string name = tag is Def def ? def.defName : tag.ToString();
//                if (name == tagName) return true;
//            }
//            return false;
//        }

//        public override void PostExposeData()
//        {
//            base.PostExposeData();
//            Scribe_Collections.Look(ref ambushMechs, "ambushMechs", LookMode.Reference);
//            Scribe_Values.Look(ref breachLabel, "breachLabel");
//            Scribe_Values.Look(ref breachText, "breachText");
//            Scribe_Values.Look(ref triggered, "triggered", false);
//            Scribe_Values.Look(ref timerTicks, "timerTicks", TotalTicksForBurn);
//            Scribe_Values.Look(ref fuelSpawned, "fuelSpawned", 0);
//            Scribe_Collections.Look(ref fuelCells, "fuelCells", LookMode.Value);
//            if (fuelCells == null) fuelCells = new List<IntVec3>();
//            Log.Message($"[HSK] PostExposeData: triggered={triggered}, timerTicks={timerTicks}, fuelSpawned={fuelSpawned}, fuelCells={fuelCells.Count}");
//        }
//    }

//    public class QuestNode_SpawnSealedPod : QuestNode
//    {
//        protected override void RunInt()
//        {
//            Log.Message("[HSK] === QuestNode_SpawnSealedPod.RunInt START ===");

//            Slate slate = QuestGen.slate;
//            Map map = slate.Get<Map>("map");
//            Pawn refugee = slate.Get<Pawn>("refugee");
//            Log.Message($"[HSK] SpawnSealedPod.RunInt: map={(map == null ? "NULL" : "OK")}, refugee={(refugee == null ? "NULL" : refugee.LabelShort)}");

//            if (map == null || refugee == null)
//            {
//                Log.Warning("[HSK] SpawnSealedPod.RunInt: abort (null map or refugee)");
//                return;
//            }

//            QuestPart_SpawnSealedPod part = new QuestPart_SpawnSealedPod();
//            part.inSignal = slate.Get<string>("inSignal") ?? QuestGen.quest.InitiateSignal;
//            part.mapParent = map.Parent;
//            part.refugee = refugee;
//            part.mechs = slate.Get<List<Pawn>>("mechs") ?? new List<Pawn>();
//            part.breachLabel = slate.Get<string>("breachLabel");
//            part.breachText = slate.Get<string>("breachText");
//            QuestGen.quest.AddPart(part);

//            Log.Message($"[HSK] SpawnSealedPod.RunInt: part added, inSignal={part.inSignal}, mechs={part.mechs.Count}");
//            Log.Message("[HSK] === QuestNode_SpawnSealedPod.RunInt END ===");
//        }

//        protected override bool TestRunInt(Slate slate) => true;
//    }

//    public class QuestPart_SpawnSealedPod : QuestPart
//    {
//        public string inSignal;
//        public MapParent mapParent;
//        public Pawn refugee;
//        public List<Pawn> mechs = new List<Pawn>();
//        public string breachLabel;
//        public string breachText;

//        public override void Notify_QuestSignalReceived(Signal signal)
//        {
//            base.Notify_QuestSignalReceived(signal);
//            Log.Message($"[HSK] NotifySignal: received tag={signal.tag}, expected inSignal={inSignal}");
//            if (signal.tag != inSignal)
//            {
//                Log.Message("[HSK] NotifySignal: tag mismatch — skipping");
//                return;
//            }

//            Log.Message("[HSK] NotifySignal: MATCH → spawning pod");

//            Map map = mapParent?.Map;
//            if (map == null)
//            {
//                Log.Warning("[HSK] NotifySignal: mapParent?.Map == null");
//                return;
//            }
//            if (refugee == null)
//            {
//                Log.Warning("[HSK] NotifySignal: refugee == null");
//                return;
//            }

//            if (refugee.IsWorldPawn())
//            {
//                Find.WorldPawns.RemovePawn(refugee);
//                Log.Message("[HSK] NotifySignal: refugee removed from world pawns");
//            }

//            foreach (var mech in mechs)
//            {
//                if (mech != null && mech.IsWorldPawn())
//                {
//                    Find.WorldPawns.RemovePawn(mech);
//                }
//            }

//            ThingDef podDef = DefDatabase<ThingDef>.GetNamedSilentFail("HSK_MechPodFlying");
//            Log.Message($"[HSK] NotifySignal: podDef = {(podDef == null ? "NULL! (def missing)" : podDef.defName)}, thingClass = {podDef?.thingClass}");

//            if (podDef == null)
//            {
//                Log.Error("[HSK] NotifySignal: HSK_MechPodFlying def MISSING — cannot spawn!");
//                return;
//            }

//            MechPodFlying pod = (MechPodFlying)ThingMaker.MakeThing(podDef);
//            Log.Message($"[HSK] NotifySignal: pod instance type = {pod?.GetType().FullName}");
//            if (pod == null)
//            {
//                Log.Error("[HSK] NotifySignal: ThingMaker.MakeThing returned null!");
//                return;
//            }

//            pod.cargo = new ActiveTransporterInfo();
//            pod.cargo.innerContainer.TryAdd(refugee);
//            Log.Message($"[HSK] NotifySignal: refugee added to cargo, count={pod.cargo.innerContainer.Count}");
//            foreach (var mech in mechs)
//            {
//                if (mech != null)
//                {
//                    pod.cargo.innerContainer.TryAdd(mech);
//                }
//            }
//            Log.Message($"[HSK] NotifySignal: final cargo count = {pod.cargo.innerContainer.Count}");

//            IntVec3 spot = DropCellFinder.RandomDropSpot(map);
//            GenSpawn.Spawn(pod, spot, map);
//            Log.Message($"[HSK] NotifySignal: pod SPAWNED at {spot}, spawned={pod.Spawned}, map={map}");
//        }

//        public override void ExposeData()
//        {
//            base.ExposeData();
//            Scribe_Values.Look(ref inSignal, "inSignal");
//            Scribe_References.Look(ref mapParent, "mapParent");
//            Scribe_References.Look(ref refugee, "refugee");
//            Scribe_Collections.Look(ref mechs, "mechs", LookMode.Reference);
//            Scribe_Values.Look(ref breachLabel, "breachLabel");
//            Scribe_Values.Look(ref breachText, "breachText");
//        }
//    }
//}
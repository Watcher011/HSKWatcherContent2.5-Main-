//using System;
//using System.Collections.Generic;
//using System.Linq;
//using RimWorld;
//using RimWorld.Planet;
//using UnityEngine;
//using Verse;
//using Verse.AI;
//using Verse.AI.Group;

//namespace HSK.Watcher.Content.Incident
//{
//    public static class MaxTacDebugConfig
//    {
//        public static bool DEBUG_MODE = true;

//        public static void Log(string message)
//        {
//            if (DEBUG_MODE)
//            {
//                Verse.Log.Message($"[MaxTac-Debug] {message}");
//            }
//        }
//    }

//    // =====================================================================
//    // 1. КОМПОНЕНТ КАРТЫ: УПРАВЛЕНИЕ МИССИЕЙ И ЭВАКУАЦИЕЙ
//    // =====================================================================
//    public class MapComponent_MaxTacStrike : MapComponent
//    {
//        public static bool StrikeInProgress = false;

//        private List<Pawn> squad = new List<Pawn>();
//        private Lord lord;
//        private Thing shuttle;
//        private Pawn targetPsycho;
//        private bool active = false;
//        private int extractionTicks = 0;
//        private bool shuttleCalledAway = false;

//        public bool Active => active;

//        public MapComponent_MaxTacStrike(Map map) : base(map) { }

//        public override void MapComponentTick()
//        {
//            base.MapComponentTick();

//            if (active || StrikeInProgress)
//            {
//                RunActiveStrikeLogic();
//                return;
//            }

//            if (Find.TickManager.TicksGame % 300 == 0)
//            {
//                CheckAndLaunchStrike();
//            }
//        }

//        private void CheckAndLaunchStrike()
//        {
//            if (map == null || StrikeInProgress) return;

//            Pawn foundPsycho = null;
//            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
//            {
//                if (p == null || !p.InMentalState || IsMaxTacTrooper(p)) continue;

//                bool isCyber = false;
//                if (p.MentalStateDef != null)
//                {
//                    if (p.MentalStateDef == MentalStateDefOf.Berserk) isCyber = true;
//                    else if (p.MentalStateDef.defName != null && p.MentalStateDef.defName.Contains("Cyber")) isCyber = true;
//                }

//                Need cyberNeed = p.needs?.TryGetNeed<Need_CyberPsychosis>();
//                if (cyberNeed != null)
//                {
//                    var prop = cyberNeed.GetType().GetProperty("IsOverThreshold");
//                    if (prop != null && prop.GetValue(cyberNeed) is bool over && over) isCyber = true;
//                }

//                if (isCyber)
//                {
//                    foundPsycho = p;
//                    MaxTacDebugConfig.Log($"Найден киберпсих для зачистки: {p.LabelShort}");
//                    break;
//                }
//            }

//            if (foundPsycho != null)
//            {
//                IncidentWorker_MaxTacPunishment.ExecuteStrike(map, foundPsycho, this);
//            }
//        }

//        public void InitializeStrike(List<Pawn> squadList, Lord strikeLord, Thing shuttleThing, Pawn psycho)
//        {
//            squad = squadList ?? new List<Pawn>();
//            lord = strikeLord;
//            shuttle = shuttleThing;
//            targetPsycho = psycho;
//            active = true;
//            StrikeInProgress = true;
//            extractionTicks = 0;
//            shuttleCalledAway = false;
//            MaxTacDebugConfig.Log($"Инцидент инициализирован. Отряд: {squad.Count}, Цель: {psycho?.LabelShort}");
//        }

//        private void RunActiveStrikeLogic()
//        {
//            if (!active) return;

//            if (Find.TickManager.TicksGame % 120 == 0)
//            {
//                ApplyNetrunnerAura();
//            }

//            if (shuttle != null && (!shuttle.Spawned || shuttle.Destroyed))
//            {
//                shuttle = null;
//            }

//            squad.RemoveAll(p => p == null || p.Dead || p.Destroyed);

//            // УСЛОВИЕ 1: Весь отряд уничтожен — убираем челнок и завершаем
//            if (squad.Count == 0)
//            {
//                MaxTacDebugConfig.Log("Весь отряд Max-Tac уничтожен. Отзыв пустого челнока.");
//                TriggerShuttleLeave();
//                EndStrike();
//                return;
//            }

//            // УСЛОВИЕ 2: Шаттл уничтожен — оперативники отступают пешком
//            if (shuttle == null && !shuttleCalledAway)
//            {
//                MaxTacDebugConfig.Log("Шаттл уничтожен! Отряд переключается на отход через край карты.");
//                shuttleCalledAway = true;
//                SwitchToFleeDuty();
//            }

//            // УСЛОВИЕ 3: Цель нейтрализована
//            bool targetNeutralized = targetPsycho != null && (targetPsycho.Downed || targetPsycho.Dead);

//            if (targetNeutralized && shuttle != null && shuttle.Spawned && !shuttleCalledAway)
//            {
//                var shuttleComp = shuttle.TryGetComp<CompShuttle>();
//                if (shuttleComp != null && !shuttleComp.requiredPawns.Contains(targetPsycho))
//                {
//                    SetupShuttleLoading(shuttleComp);
//                }

//                extractionTicks += 250;

//                bool allInside = squad.All(t => t == null || t.Dead || !t.Spawned) && (targetPsycho == null || !targetPsycho.Spawned);

//                if (allInside || extractionTicks >= 5000)
//                {
//                    MaxTacDebugConfig.Log("Эвакуация: запуск отлета шаттла.");
//                    shuttleCalledAway = true;
//                    TriggerShuttleLeave();
//                    EndStrike();
//                }
//            }
//        }

//        private void SetupShuttleLoading(CompShuttle shuttleComp)
//        {
//            MaxTacDebugConfig.Log("Цель нейтрализована. Настройка погрузки в CompShuttle.");
//            shuttleComp.requiredPawns.Clear();
//            foreach (var trooper in squad)
//            {
//                if (trooper != null && !trooper.Dead)
//                {
//                    shuttleComp.requiredPawns.Add(trooper);
//                }
//            }
//            if (targetPsycho != null)
//            {
//                shuttleComp.requiredPawns.Add(targetPsycho);
//            }
//            shuttleComp.permitShuttle = true;

//            var transComp = shuttle.TryGetComp<CompTransporter>();
//            if (transComp != null && !transComp.LoadingInProgressOrReadyToLaunch)
//            {
//                TransporterUtility.InitiateLoading(Gen.YieldSingle(transComp));
//            }

//            if (lord != null && shuttle != null)
//            {
//                for (int i = 0; i < lord.ownedPawns.Count; i++)
//                {
//                    Pawn trooper = lord.ownedPawns[i];
//                    if (trooper != null && !trooper.Dead && trooper.Spawned)
//                    {
//                        trooper.mindState.duty = new PawnDuty(DutyDefOf.Defend, shuttle.Position, 4f);
//                    }
//                }
//            }

//            Messages.Message("Цель нейтрализована. Ожидание погрузки отряда Max-Tac в челнок.", new TargetInfo(shuttle.Position, map), MessageTypeDefOf.PositiveEvent);
//        }

//        private void TriggerShuttleLeave()
//        {
//            if (shuttle == null || !shuttle.Spawned) return;

//            IntVec3 launchPos = shuttle.Position;
//            Map currentMap = map;

//            var transComp = shuttle.TryGetComp<CompTransporter>();
//            if (transComp != null)
//            {
//                if (targetPsycho != null)
//                {
//                    Thing tThing = targetPsycho.Dead ? (targetPsycho.Corpse ?? (Thing)targetPsycho) : (Thing)targetPsycho;
//                    if (tThing != null && tThing.Spawned)
//                    {
//                        tThing.DeSpawn();
//                        transComp.innerContainer.TryAddOrTransfer(tThing);
//                    }
//                }
//                foreach (var t in squad)
//                {
//                    if (t != null && t.Spawned)
//                    {
//                        t.DeSpawn();
//                        transComp.innerContainer.TryAddOrTransfer(t);
//                    }
//                }
//            }

//            shuttle.Destroy();

//            Messages.Message("Челнок Max-Tac с отрядом и целью покинул сектор.", new TargetInfo(launchPos, currentMap), MessageTypeDefOf.PositiveEvent);
//        }

//        private void SwitchToFleeDuty()
//        {
//            if (lord == null) return;

//            for (int i = 0; i < lord.ownedPawns.Count; i++)
//            {
//                Pawn trooper = lord.ownedPawns[i];
//                if (trooper != null && !trooper.Dead && trooper.Spawned)
//                {
//                    trooper.mindState.duty = new PawnDuty(DutyDefOf.ExitMapBest);
//                    trooper.jobs.EndCurrentJob(JobCondition.InterruptForced);
//                }
//            }

//            Messages.Message("Шаттл Max-Tac уничтожен! Оперативники отступают и покидают сектор пешком.", MessageTypeDefOf.NegativeEvent);
//        }

//        private void EndStrike()
//        {
//            MaxTacDebugConfig.Log("Инцидент полностью завершен. Глобальный флаг сброшен.");
//            active = false;
//            StrikeInProgress = false;
//        }

//        private void ApplyNetrunnerAura()
//        {
//            if (map == null || squad == null || squad.Count == 0) return;
//            HediffDef overrideDef = DefDatabase<HediffDef>.GetNamedSilentFail("WildAIOverride");
//            if (overrideDef == null) return;

//            foreach (var trooper in squad)
//            {
//                if (trooper == null || !trooper.Spawned || trooper.Dead) continue;

//                foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
//                {
//                    if (pawn == null || pawn.Dead || IsMaxTacTrooper(pawn)) continue;

//                    if (pawn.Position.InHorDistOf(trooper.Position, 10f))
//                    {
//                        bool hasCybernetics = pawn.health?.hediffSet?.hediffs.Any(h => h is Hediff_Implant || h is Hediff_AddedPart) ?? false;
//                        bool isPsycho = pawn.InMentalState;

//                        if (hasCybernetics || isPsycho)
//                        {
//                            var existing = pawn.health.hediffSet.GetFirstHediffOfDef(overrideDef);
//                            if (existing == null)
//                            {
//                                pawn.health.AddHediff(overrideDef);
//                            }
//                        }
//                    }
//                }
//            }
//        }

//        private static bool IsMaxTacTrooper(Pawn pawn)
//        {
//            if (pawn == null) return false;
//            NameTriple nameTriple = pawn.Name as NameTriple;
//            return nameTriple != null && nameTriple.First == "Оперативник";
//        }

//        public override void ExposeData()
//        {
//            base.ExposeData();
//            if (squad == null) squad = new List<Pawn>();
//            Scribe_Collections.Look(ref squad, "squad", LookMode.Reference);
//            Scribe_References.Look(ref lord, "lord");
//            Scribe_References.Look(ref shuttle, "shuttle");
//            Scribe_References.Look(ref targetPsycho, "targetPsycho");
//            Scribe_Values.Look(ref active, "active", false);
//            Scribe_Values.Look(ref extractionTicks, "extractionTicks", 0);
//            Scribe_Values.Look(ref shuttleCalledAway, "shuttleCalledAway", false);
//            Scribe_Values.Look(ref StrikeInProgress, "StrikeInProgress", false);
//        }
//    }

//    // =====================================================================
//    // 2. РАБОТНИК ИНЦИДЕНТА
//    // =====================================================================
//    public class IncidentWorker_MaxTacPunishment : IncidentWorker
//    {
//        protected override bool CanFireNowSub(IncidentParms parms)
//        {
//            Map map = parms.target as Map;
//            return map != null && !MapComponent_MaxTacStrike.StrikeInProgress;
//        }

//        protected override bool TryExecuteWorker(IncidentParms parms)
//        {
//            Map map = parms.target as Map;
//            if (map == null || MapComponent_MaxTacStrike.StrikeInProgress) return false;

//            Pawn targetPsycho = null;
//            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
//            {
//                if (p == null || !p.InMentalState) continue;
//                if (p.Name is NameTriple nt && nt.First == "Оперативник") continue;
//                targetPsycho = p;
//                break;
//            }

//            if (targetPsycho == null) return false;

//            MapComponent_MaxTacStrike comp = map.GetComponent<MapComponent_MaxTacStrike>();
//            if (comp != null)
//            {
//                ExecuteStrike(map, targetPsycho, comp);
//                return true;
//            }

//            return false;
//        }

//        public static void ExecuteStrike(Map map, Pawn targetPsycho, MapComponent_MaxTacStrike comp)
//        {
//            if (MapComponent_MaxTacStrike.StrikeInProgress) return;

//            MaxTacDebugConfig.Log($"Запуск ExecuteStrike для цели: {targetPsycho.LabelShort}");

//            Faction maxTacFaction = Find.FactionManager.AllFactionsVisible.FirstOrDefault(f => !f.HostileTo(Faction.OfPlayer) && !f.IsPlayer) ?? Faction.OfEmpire;
//            if (maxTacFaction == null || maxTacFaction == Faction.OfPlayer)
//            {
//                maxTacFaction = Faction.OfAncients;
//            }

//            IntVec3 landingCell;
//            bool foundSpot = CellFinderLoose.TryFindRandomNotEdgeCellWith(12, c =>
//                c.Standable(map) &&
//                !c.Roofed(map) &&
//                map.edificeGrid[c] == null &&
//                GenAdj.OccupiedRect(c, Rot4.North, new IntVec2(5, 3)).All(cell =>
//                    cell.InBounds(map) && cell.Standable(map) && !cell.Roofed(map) && map.edificeGrid[cell] == null
//                ) &&
//                GenRadial.RadialCellsAround(c, 10f, true).All(rc => !rc.InBounds(map) || map.edificeGrid[rc] == null),
//                map, out landingCell);

//            if (!foundSpot)
//            {
//                if (!DropCellFinder.TryFindDropSpotNear(targetPsycho.Position, map, out landingCell, false, true, true, null, true))
//                {
//                    landingCell = DropCellFinder.RandomDropSpot(map);
//                }
//            }

//            MaxTacDebugConfig.Log($"Точка приземления шаттла: {landingCell}");

//            string[] tacticalCallsigns = new[] { "Альфа", "Бета", "Гамма", "Дельта", "Омега" };
//            PawnKindDef soldierKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("AncientSoldier")
//                                      ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("Imperial_Trooper")
//                                      ?? PawnKindDefOf.SpaceRefugee;

//            List<Pawn> squad = new List<Pawn>();
//            for (int i = 0; i < 5; i++)
//            {
//                Pawn trooper = null;
//                try
//                {
//                    PawnGenerationRequest request = new PawnGenerationRequest(
//                        soldierKind,
//                        maxTacFaction,
//                        PawnGenerationContext.NonPlayer,
//                        tile: -1,
//                        forceGenerateNewPawn: true,
//                        allowDead: false,
//                        allowDowned: false,
//                        canGeneratePawnRelations: false,
//                        mustBeCapableOfViolence: true,
//                        colonistRelationChanceFactor: 0f,
//                        allowGay: false,
//                        allowPregnant: false,
//                        allowAddictions: false
//                    );
//                    trooper = PawnGenerator.GeneratePawn(request);
//                }
//                catch (Exception ex)
//                {
//                    MaxTacDebugConfig.Log($"Ошибка генерации: {ex.Message}");
//                }

//                if (trooper == null)
//                {
//                    try
//                    {
//                        trooper = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, maxTacFaction, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: true));
//                    }
//                    catch { }
//                }

//                if (trooper == null) continue;

//                string callsign = (i < tacticalCallsigns.Length) ? tacticalCallsigns[i] : $"Спецназ-{i + 1}";
//                trooper.Name = new NameTriple("Оперативник", callsign, callsign);

//                ClearExistingGear(trooper);

//                Color maxTacGray = new Color(0.4f, 0.45f, 0.5f);
//                TryEquipApparel(trooper, "Helmet_MaxTac", maxTacGray);
//                TryEquipApparel(trooper, "Armor_Siege", maxTacGray);
//                TryEquipApparel(trooper, "CE_Apparel_TacVest", maxTacGray);
//                TryEquipApparel(trooper, "Apparel_TacticalMask", maxTacGray);
//                TryEquipApparel(trooper, "Boots_Combat", maxTacGray);

//                TryEquipWeapon(trooper, "Gun_SawGun");
//                GiveCEAmmoSafe(trooper, new[] { "Ammo_30x173mm_FMJ", "Ammo_30x173mm_HE" }, 120);
//                ForceReloadCEWeapon(trooper);

//                AddImplantToPart(trooper, "ArchotechEye", "Eye", 2);
//                AddImplantToPart(trooper, "ArchotechArm", "Shoulder", 2);
//                AddImplantToPart(trooper, "ArchotechLeg", "Leg", 2);
//                AddImplantToPart(trooper, "GTHeart", "Heart", 1);
//                AddImplantToPart(trooper, "GTLung", "Lung", 2);
//                AddImplantToPart(trooper, "GTKidney", "Kidney", 2);
//                AddImplantToPart(trooper, "GTLiver", "Liver", 1);
//                AddImplantToPart(trooper, "GTEar", "Ear", 2);
//                AddImplantToPart(trooper, "GTSpine", "Spine", 1);
//                AddImplantToPart(trooper, "GTNeck", "Neck", 1);
//                AddImplantToPart(trooper, "GTDentures", "Jaw", 1);
//                AddImplantToPart(trooper, "AIChip", "Brain", 1);
//                AddImplantToPart(trooper, "MindLock", "Brain", 1);
//                AddImplantToPart(trooper, "Painstopper", "Brain", 1);
//                AddImplantToPart(trooper, "DeathAcidifier", "Torso", 1);

//                squad.Add(trooper);
//            }

//            ThingDef incomingDef = DefDatabase<ThingDef>.GetNamed("HSK_MaxTacShuttleIncoming", false) ?? DefDatabase<ThingDef>.GetNamed("TransportPodIncoming");
//            ThingDef shuttleDef = DefDatabase<ThingDef>.GetNamed("HSK_MaxTacShuttleBuilding", false);

//            Thing createdShuttle = null;
//            Skyfaller_MaxTacShuttle skyfaller = SkyfallerMaker.MakeSkyfaller(incomingDef) as Skyfaller_MaxTacShuttle;
//            if (skyfaller != null)
//            {
//                if (shuttleDef != null)
//                {
//                    createdShuttle = ThingMaker.MakeThing(shuttleDef);
//                    createdShuttle.SetFaction(maxTacFaction);
//                    skyfaller.innerContainer.TryAdd(createdShuttle);
//                }
//                foreach (var trooper in squad)
//                {
//                    skyfaller.innerContainer.TryAdd(trooper);
//                }
//                GenSpawn.Spawn(skyfaller, landingCell, map);
//            }

//            if (comp != null)
//            {
//                Lord lord = LordMaker.MakeNewLord(maxTacFaction, new LordJob_MaxTacHunt(targetPsycho), map, squad);
//                comp.InitializeStrike(squad, lord, createdShuttle, targetPsycho);
//            }

//            Find.LetterStack.ReceiveLetter(
//                "HSK_MaxTacLetterLabel".Translate(),
//                string.Format("HSK_MaxTacLetterText".Translate(), landingCell.ToString(), targetPsycho.LabelShort),
//                LetterDefOf.ThreatBig,
//                new TargetInfo(landingCell, map)
//            );
//        }

//        private static void ClearExistingGear(Pawn pawn)
//        {
//            if (pawn.equipment != null) foreach (var eq in pawn.equipment.AllEquipmentListForReading.ToList()) { pawn.equipment.Remove(eq); eq.Destroy(); }
//            if (pawn.apparel != null) foreach (var ap in pawn.apparel.WornApparel.ToList()) { pawn.apparel.Remove(ap); ap.Destroy(); }
//            if (pawn.inventory != null) pawn.inventory.DestroyAll();
//        }

//        private static void TryEquipApparel(Pawn pawn, string defName, Color color)
//        {
//            var apparelDef = DefDatabase<ThingDef>.GetNamed(defName, false);
//            if (apparelDef == null) return;
//            try
//            {
//                Thing apparelThing = apparelDef.MadeFromStuff ? ThingMaker.MakeThing(apparelDef, ThingDefOf.Plasteel) : ThingMaker.MakeThing(apparelDef);
//                if (apparelThing is Apparel apparel)
//                {
//                    pawn.apparel.Wear(apparel);
//                    var comp = apparel.TryGetComp<CompColorable>();
//                    if (comp != null) comp.SetColor(color);
//                }
//                else apparelThing.Destroy();
//            }
//            catch (Exception ex) { MaxTacDebugConfig.Log($"Ошибка экипировки брони {defName}: {ex.Message}"); }
//        }

//        private static void TryEquipWeapon(Pawn pawn, string defName)
//        {
//            var weaponDef = DefDatabase<ThingDef>.GetNamed(defName, false);
//            if (weaponDef == null) return;
//            try
//            {
//                var weapon = (ThingWithComps)ThingMaker.MakeThing(weaponDef);
//                pawn.equipment.AddEquipment(weapon);
//            }
//            catch (Exception ex) { MaxTacDebugConfig.Log($"Ошибка экипировки оружия {defName}: {ex.Message}"); }
//        }

//        private static void GiveCEAmmoSafe(Pawn pawn, string[] possibleAmmoDefs, int count)
//        {
//            ThingDef ammoDef = null;
//            foreach (var defName in possibleAmmoDefs)
//            {
//                ammoDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
//                if (ammoDef != null) break;
//            }

//            if (ammoDef == null)
//            {
//                ammoDef = DefDatabase<ThingDef>.AllDefs.FirstOrDefault(d => d.defName.Contains("30x173") || d.defName.Contains("Ammo"));
//            }

//            if (ammoDef == null || pawn.inventory == null) return;

//            try
//            {
//                int remaining = count;
//                while (remaining > 0)
//                {
//                    var ammo = ThingMaker.MakeThing(ammoDef);
//                    int stack = Mathf.Min(remaining, ammoDef.stackLimit > 0 ? ammoDef.stackLimit : 75);
//                    ammo.stackCount = stack;
//                    if (!pawn.inventory.innerContainer.TryAdd(ammo)) { ammo.Destroy(); break; }
//                    remaining -= stack;
//                }
//            }
//            catch (Exception ex) { MaxTacDebugConfig.Log($"Ошибка при выдаче патронов CE: {ex.Message}"); }
//        }

//        private static void ForceReloadCEWeapon(Pawn pawn)
//        {
//            ThingWithComps weapon = pawn?.equipment?.Primary;
//            if (weapon == null) return;

//            ThingComp ammoComp = weapon.AllComps?.FirstOrDefault(c => c.GetType().Name == "CompAmmoUser");
//            if (ammoComp == null) return;

//            try
//            {
//                var props = ammoComp.props;
//                var magSizeField = props.GetType().GetField("magazineSize");
//                var curMagProp = ammoComp.GetType().GetProperty("CurMagCount");

//                if (magSizeField != null && curMagProp != null)
//                {
//                    int magSize = (int)magSizeField.GetValue(props);
//                    if (magSize > 0)
//                    {
//                        curMagProp.SetValue(ammoComp, magSize);
//                    }
//                }
//            }
//            catch (Exception ex) { MaxTacDebugConfig.Log($"Ошибка автозарядки CE: {ex.Message}"); }
//        }

//        private static void AddImplantToPart(Pawn pawn, string hediffDefName, string bodyPartDefName, int count)
//        {
//            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(hediffDefName);
//            if (hediffDef == null) return;

//            int installed = 0;
//            var parts = pawn.health.hediffSet.GetNotMissingParts()
//                .Where(p => p.def != null && (p.def.defName == bodyPartDefName || p.def.label == bodyPartDefName))
//                .ToList();

//            if (!parts.Any() && pawn.RaceProps.body != null)
//            {
//                parts = pawn.RaceProps.body.AllParts
//                    .Where(p => p.def != null && p.def.defName == bodyPartDefName)
//                    .ToList();
//            }

//            foreach (BodyPartRecord part in parts)
//            {
//                if (installed >= count) break;
//                bool alreadyHas = pawn.health.hediffSet.hediffs.Any(h => h.Part == part && h.def == hediffDef);
//                if (alreadyHas)
//                {
//                    installed++;
//                    continue;
//                }

//                try
//                {
//                    Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn, part);
//                    if (hediff != null)
//                    {
//                        pawn.health.AddHediff(hediff);
//                        installed++;
//                    }
//                }
//                catch (Exception ex) { MaxTacDebugConfig.Log($"Ошибка при установке {hediffDefName}: {ex.Message}"); }
//            }
//        }
//    }

//    // =====================================================================
//    // 3. КАСТОМНЫЙ СКАЙФОЛЛЕР (ШАТТЛ) С ВЫСАДКОЙ ОТРЯДА
//    // =====================================================================
//    public class Skyfaller_MaxTacShuttle : Skyfaller
//    {
//        protected override void SpawnThings()
//        {
//            Map map = base.Map;
//            IntVec3 landing = base.Position;

//            List<Pawn> squad = new List<Pawn>();
//            for (int i = innerContainer.Count - 1; i >= 0; i--)
//            {
//                if (innerContainer[i] is Pawn pawn) squad.Add(pawn);
//            }
//            for (int i = 0; i < squad.Count; i++)
//            {
//                innerContainer.Remove(squad[i]);
//            }

//            base.SpawnThings();

//            ThingDef shuttleDef = DefDatabase<ThingDef>.GetNamed("HSK_MaxTacShuttleBuilding", false);
//            Thing spawnedShuttle = shuttleDef != null
//                ? map.thingGrid.ThingAt(landing, shuttleDef) ?? map.listerThings.ThingsOfDef(shuttleDef).FirstOrDefault(t => t.Spawned && t.Position.InHorDistOf(landing, 3f))
//                : null;

//            IntVec3 center = spawnedShuttle != null ? spawnedShuttle.Position : landing;

//            foreach (var trooper in squad)
//            {
//                if (trooper != null)
//                {
//                    if (CellFinder.TryFindRandomCellNear(center, map, 3, c => c.Standable(map) && c.GetEdifice(map) == null, out var spot))
//                    {
//                        GenSpawn.Spawn(trooper, spot, map, Rot4.Random);
//                    }
//                    else
//                    {
//                        GenSpawn.Spawn(trooper, landing, map, Rot4.Random);
//                    }
//                }
//            }
//        }
//    }

//    // =====================================================================
//    // 4. ИИ ОТРЯДА: ОХОТА НА КИБЕРПСИХА
//    // =====================================================================
//    public class LordJob_MaxTacHunt : LordJob
//    {
//        private Pawn targetPsycho;

//        public LordJob_MaxTacHunt() { }
//        public LordJob_MaxTacHunt(Pawn psycho)
//        {
//            this.targetPsycho = psycho;
//        }

//        public override bool CanOpenAnyDoor(Pawn p) => true;

//        public override StateGraph CreateGraph()
//        {
//            StateGraph graph = new StateGraph();
//            LordToil_MaxTacHunt toilHunt = new LordToil_MaxTacHunt(targetPsycho);
//            graph.AddToil(toilHunt);
//            return graph;
//        }

//        public override void ExposeData()
//        {
//            base.ExposeData();
//            Scribe_References.Look(ref targetPsycho, "targetPsycho");
//        }
//    }

//    public class LordToil_MaxTacHunt : LordToil
//    {
//        private Pawn targetPsycho;

//        public LordToil_MaxTacHunt(Pawn psycho)
//        {
//            this.targetPsycho = psycho;
//        }

//        public override void UpdateAllDuties()
//        {
//            for (int i = 0; i < lord.ownedPawns.Count; i++)
//            {
//                Pawn trooper = lord.ownedPawns[i];
//                if (trooper == null || trooper.Dead) continue;

//                if (trooper.mindState.duty != null && trooper.mindState.duty.def == DutyDefOf.ExitMapBest)
//                    continue;

//                if (targetPsycho != null && !targetPsycho.Dead && !targetPsycho.Downed)
//                {
//                    trooper.mindState.duty = new PawnDuty(DutyDefOf.AssaultThing, targetPsycho);
//                }
//                else
//                {
//                    if (trooper.CurJobDef == JobDefOf.AttackMelee || trooper.CurJobDef == JobDefOf.AttackStatic || trooper.CurJobDef == JobDefOf.CastAbilityOnThing)
//                    {
//                        trooper.jobs.EndCurrentJob(JobCondition.InterruptForced);
//                    }

//                    PawnDuty defendDuty = new PawnDuty(DutyDefOf.Defend, trooper.Position);
//                    defendDuty.radius = 4f;
//                    trooper.mindState.duty = defendDuty;
//                }
//            }
//        }
//    }
//}
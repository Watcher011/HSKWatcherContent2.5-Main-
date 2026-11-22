//using HarmonyLib;
//using RimWorld;
//using System;
//using System.Collections.Generic;
//using System.Reflection;
//using UnityEngine;
//using Verse;
//using Verse.AI.Group;
//using Verse.Sound;

//namespace HSK.Watcher.Content.Incident
//{
//    [StaticConstructorOnStartup]
//    public static class ArtilleryModLoader
//    {
//        static ArtilleryModLoader()
//        {
//            new Harmony("HSK.Watcher.Content.Incident").PatchAll();
//        }
//    }

//    public class IncidentWorker_ArtilleryPreparation : IncidentWorker_RaidEnemy
//    {
//        private const float BombardmentRadius = 25f;
//        private const int SalvoIntervalTicks = 120;
//        private const int FlareToBarrageDelayTicks = 900;
//        private const int RaidDelayTicks = 800;

//        private static readonly string[] CEShellNames = new string[] {
//            "Bullet_155mmHowitzerShell_HE",
//            "Bullet_155mmHowitzerShell_Incendiary",
//            "Bullet_81mmMortarShell_HE",
//        };

//        private static bool? _ceLoaded;
//        private static bool CE_Loaded
//        {
//            get
//            {
//                if (!_ceLoaded.HasValue)
//                    _ceLoaded = ModsConfig.IsActive("CETeam.CombatExtended") || ModsConfig.IsActive("CombatExtended.Continued");
//                return _ceLoaded.Value;
//            }
//        }

//        private static Type _projectileCEType;
//        private static Type ProjectileCE_Type
//        {
//            get
//            {
//                if (_projectileCEType == null && CE_Loaded)
//                    _projectileCEType = Type.GetType("CombatExtended.ProjectileCE, CombatExtended", false);
//                return _projectileCEType;
//            }
//        }

//        protected override bool CanFireNowSub(IncidentParms parms)
//        {
//            if (!base.CanFireNowSub(parms)) return false;
//            Map map = (Map)parms.target;
//            Faction faction = parms.faction;
//            if (faction == null)
//                faction = Find.FactionManager.RandomEnemyFaction(false, false, true, TechLevel.Industrial);
//            if (faction == null) return false;
//            parms.faction = faction;
//            if (faction.def.techLevel < TechLevel.Industrial) return false;
//            IntVec3 dummy;
//            return TryFindBombardmentTarget(map, out dummy);
//        }

//        protected override bool TryExecuteWorker(IncidentParms parms)
//        {
//            Map map = (Map)parms.target;
//            Faction faction = parms.faction;
//            if (faction == null)
//                faction = Find.FactionManager.RandomEnemyFaction(false, false, true, TechLevel.Industrial);
//            if (faction == null) return false;
//            parms.faction = faction;

//            IntVec3 target;
//            if (!TryFindBombardmentTarget(map, out target)) return false;

//            SendInterceptedTransmission(faction, map, target);

//            int totalShells = Rand.Range(4, 15); // 4–14 случайно
//            int salvos = totalShells;
//            int barrageStart = Find.TickManager.TicksGame + FlareToBarrageDelayTicks;

//            ScheduleBarrage(map, faction, target, totalShells, salvos, barrageStart);
//            ScheduleRaid(parms, totalShells, barrageStart + salvos * SalvoIntervalTicks + RaidDelayTicks);

//            return true;
//        }

//        /// <summary>
//        /// Перехваченное сообщение от вражеского корректировщика
//        /// </summary>
//        private void SendInterceptedTransmission(Faction faction, Map map, IntVec3 target)
//        {
//            string coords = FormatCoordinates(target, map);
//            string letterLabel = "LetterLabelArtilleryPrep".Translate(faction.Name);
//            string letterText = "LetterArtilleryPrep".Translate(faction.Name, coords);
//            Find.LetterStack.ReceiveLetter(
//                letterLabel,
//                letterText,
//                LetterDefOf.ThreatBig, new TargetInfo(map.Center, map));
//            // Убрано Messages.Message - не нужно дублировать вверху экрана
//        }

//        /// <summary>
//        /// Форматирует координаты в радиосвязной формат
//        /// </summary>
//        private string FormatCoordinates(IntVec3 cell, Map map)
//        {
//            int sectorX = Mathf.FloorToInt((float)cell.x / 10f);
//            int sectorZ = Mathf.FloorToInt((float)cell.z / 10f);
//            int absSectorX = Mathf.Abs(sectorX);
//            int absSectorZ = Mathf.Abs(sectorZ);
//            char sectorLetter = (char)('A' + (absSectorX % 26));
//            string sectorNumber = (absSectorZ + 1).ToString("D2");
//            int offsetX = Mathf.Abs(cell.x) % 10;
//            int offsetZ = Mathf.Abs(cell.z) % 10;
//            string result = "";
//            result += sectorLetter;
//            result += sectorNumber;
//            result += "-";
//            result += offsetX.ToString();
//            result += "-";
//            result += offsetZ.ToString();
//            return result;
//        }

//        private void ScheduleBarrage(Map map, Faction faction, IntVec3 center, int totalShells, int salvos, int startTick)
//        {
//            List<ThingDef> shellDefs = GetShellDefs();
//            if (shellDefs.Count == 0)
//            {
//                shellDefs = new List<ThingDef>();
//                shellDefs.Add(ThingDefOf.Shell_HighExplosive);
//            }

//            for (int s = 0; s < salvos; s++)
//            {
//                int shellTick = startTick + s * SalvoIntervalTicks + Rand.Range(-20, 20);
//                ThingDef def = shellDefs[Rand.Range(0, shellDefs.Count)];
//                IntVec3 cell = SelectImpactCell(center);
//                ArtilleryScheduler sched = map.GetComponent<ArtilleryScheduler>();
//                if (sched != null) sched.RegisterShell(map, faction, cell, def, shellTick);
//            }

//            // Убрано Messages.Message - не нужно дублировать вверху экрана
//            ArtilleryScheduler barrageSched = map.GetComponent<ArtilleryScheduler>();
//            if (barrageSched != null)
//                barrageSched.RegisterDelayedAction(() =>
//                {
//                    if (center.InBounds(map))
//                    {
//                        // Звук перед началом залпа
//                        SoundDef preImpactSound = DefDatabase<SoundDef>.GetNamed("MortarRound_PreImpact", false);
//                        if (preImpactSound != null)
//                            SoundStarter.PlayOneShot(preImpactSound, new TargetInfo(center, map));
//                    }
//                }, startTick - 60);
//        }

//        private List<ThingDef> GetShellDefs()
//        {
//            List<ThingDef> list = new List<ThingDef>();
//            if (CE_Loaded)
//            {
//                for (int i = 0; i < CEShellNames.Length; i++)
//                {
//                    ThingDef def = DefDatabase<ThingDef>.GetNamed(CEShellNames[i], false);
//                    if (def != null) list.Add(def);
//                }
//            }
//            if (list.Count == 0)
//                list.Add(ThingDefOf.Shell_HighExplosive);
//            return list;
//        }

//        private IntVec3 SelectImpactCell(IntVec3 center)
//        {
//            float angle = Rand.Range(0f, 360f);
//            float dist = Rand.Range(0f, BombardmentRadius);

//            if (Rand.Value < 0.6f)
//            {
//                dist = Rand.Range(BombardmentRadius * 0.5f, BombardmentRadius);
//            }

//            return new IntVec3(
//                Mathf.RoundToInt(center.x + Mathf.Cos(angle * Mathf.Deg2Rad) * dist),
//                0,
//                Mathf.RoundToInt(center.z + Mathf.Sin(angle * Mathf.Deg2Rad) * dist));
//        }

//        private void ScheduleRaid(IncidentParms parms, int totalShells, int raidTick)
//        {
//            if (parms.faction == null)
//            {
//                Log.Warning("[ArtilleryPrep] ScheduleRaid: faction is null, cannot schedule raid");
//                return;
//            }

//            float factor = 1f + totalShells * 0.05f;
//            if (parms.points <= 0)
//                parms.points = StorytellerUtility.DefaultThreatPointsNow(parms.target);
//            parms.points = Mathf.Min(parms.points * factor, 10000f);

//            if (parms.raidArrivalMode == null)
//                parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
//            if (parms.raidStrategy == null)
//                parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;

//            Map map = parms.target as Map;
//            if (map != null)
//            {
//                ArtilleryScheduler sched = map.GetComponent<ArtilleryScheduler>();
//                if (sched != null) sched.RegisterRaid(parms, raidTick);
//            }
//            Log.Message("[ArtilleryPrep] Raid scheduled at tick " + raidTick + " for " + parms.faction.Name + ", points=" + parms.points.ToString("F0"));
//        }

//        private bool TryFindBombardmentTarget(Map map, out IntVec3 target)
//        {
//            target = IntVec3.Invalid;

//            List<Thing> things = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
//            List<Thing> artillery = new List<Thing>();
//            for (int i = 0; i < things.Count; i++)
//            {
//                if (IsArtillery(things[i]) && things[i].Faction == Faction.OfPlayer)
//                    artillery.Add(things[i]);
//            }
//            if (artillery.Count > 0)
//            {
//                target = artillery[Rand.Range(0, artillery.Count)].Position;
//                return true;
//            }

//            HashSet<string> keys = new HashSet<string>();
//            keys.Add("PowerGenerator");
//            keys.Add("GeothermalGenerator");
//            keys.Add("SolarGenerator");
//            keys.Add("Turret_MiniTurret");
//            keys.Add("Battery");

//            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
//            List<Thing> targets = new List<Thing>();
//            for (int i = 0; i < buildings.Count; i++)
//            {
//                Building b = buildings[i];
//                if (keys.Contains(b.def.defName) || (b.def.building != null && b.def.building.ai_combatDangerous))
//                    targets.Add(b);
//            }
//            if (targets.Count > 0)
//            {
//                target = targets[Rand.Range(0, targets.Count)].Position;
//                return true;
//            }

//            List<Pawn> colonists = map.mapPawns.FreeColonists;
//            if (colonists.Count > 0)
//            {
//                int bestCount = 0;
//                IntVec3 bestPos = colonists[0].Position;
//                for (int i = 0; i < colonists.Count; i++)
//                {
//                    int count = 0;
//                    for (int j = 0; j < colonists.Count; j++)
//                    {
//                        if (colonists[i].Position.DistanceTo(colonists[j].Position) < 15f)
//                            count++;
//                    }
//                    if (count > bestCount)
//                    {
//                        bestCount = count;
//                        bestPos = colonists[i].Position;
//                    }
//                }
//                target = bestPos;
//                return true;
//            }

//            return CellFinder.TryFindRandomCellNear(map.Center, map, (int)(map.Size.x * 0.3f), c => c.Standable(map) && !c.Roofed(map), out target);
//        }

//        private bool IsArtillery(Thing t)
//        {
//            if (t == null) return false;
//            if (CE_Loaded)
//            {
//                Type ceTurret = Type.GetType("CombatExtended.CompTurretGunCE, CombatExtended", false);
//                if (ceTurret != null)
//                {
//                    MethodInfo tryGetComp = t.GetType().GetMethod("TryGetComp");
//                    if (tryGetComp != null)
//                    {
//                        MethodInfo generic = tryGetComp.MakeGenericMethod(ceTurret);
//                        if (generic != null && generic.Invoke(t, null) != null) return true;
//                    }
//                }
//            }
//            CompEquippable eq = t.TryGetComp<CompEquippable>();
//            if (eq != null && eq.PrimaryVerb != null && eq.PrimaryVerb.verbProps != null && eq.PrimaryVerb.verbProps.range >= 40f) return true;
//            string n = t.def.defName.ToLower();
//            return n.Contains("mortar") || n.Contains("artillery") || n.Contains("howitzer") || n.Contains("cannon");
//        }

//        public override float BaseChanceThisGame
//        {
//            get
//            {
//                float c = base.BaseChanceThisGame;
//                List<Map> maps = Find.Maps;
//                for (int i = 0; i < maps.Count; i++)
//                {
//                    Map m = maps[i];
//                    if (!m.IsPlayerHome) continue;

//                    List<Thing> things = m.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
//                    int artilleryCount = 0;
//                    for (int j = 0; j < things.Count; j++)
//                    {
//                        if (IsArtillery(things[j]) && things[j].Faction == Faction.OfPlayer)
//                            artilleryCount++;
//                    }
//                    c += artilleryCount * 0.5f;
//                    c += m.wealthWatcher.WealthTotal / 50000f;
//                }
//                return Mathf.Min(c, 8f);
//            }
//        }

//        protected override void ResolveRaidPoints(IncidentParms parms) => base.ResolveRaidPoints(parms);
//    }

//    public class ArtilleryScheduler : MapComponent
//    {
//        private List<ScheduledShell> shells = new List<ScheduledShell>();
//        private List<ScheduledRaid> raids = new List<ScheduledRaid>();
//        private List<DelayedAction> delayed = new List<DelayedAction>();
//        private List<RepeatingAction> repeating = new List<RepeatingAction>();

//        public ArtilleryScheduler(Map map) : base(map) { }

//        public void RegisterShell(Map map, Faction faction, IntVec3 target, ThingDef def, int tick)
//        {
//            shells.Add(new ScheduledShell { Map = map, Faction = faction, TargetCell = target, ShellDef = def, TriggerTick = tick });
//        }

//        public void RegisterRaid(IncidentParms parms, int tick) => raids.Add(new ScheduledRaid { Parms = parms, TriggerTick = tick });
//        public void RegisterDelayedAction(Action action, int tick) => delayed.Add(new DelayedAction { Action = action, TriggerTick = tick });
//        public void RegisterRepeatingAction(Action action, int tick, int interval, int reps) => repeating.Add(new RepeatingAction { Action = action, NextTick = tick, Interval = interval, RemainingRepetitions = reps });

//        public override void MapComponentTick()
//        {
//            base.MapComponentTick();
//            int tick = Find.TickManager.TicksGame;

//            for (int i = delayed.Count - 1; i >= 0; i--)
//            {
//                DelayedAction d = delayed[i];
//                if (d.TriggerTick <= tick)
//                {
//                    try { if (d.Action != null) d.Action(); }
//                    catch (Exception ex) { Log.Warning("[AS] Delayed err: " + ex.Message); }
//                    delayed.RemoveAt(i);
//                }
//            }

//            for (int i = repeating.Count - 1; i >= 0; i--)
//            {
//                RepeatingAction r = repeating[i];
//                if (r.NextTick <= tick)
//                {
//                    try { if (r.Action != null) r.Action(); }
//                    catch (Exception ex) { Log.Warning("[AS] Repeat err: " + ex.Message); }
//                    r.RemainingRepetitions--;
//                    r.NextTick += r.Interval;
//                    if (r.RemainingRepetitions <= 0) repeating.RemoveAt(i);
//                }
//            }

//            for (int i = shells.Count - 1; i >= 0; i--)
//            {
//                ScheduledShell s = shells[i];
//                if (s.TriggerTick <= tick) { FireShell(s); shells.RemoveAt(i); }
//            }

//            for (int i = raids.Count - 1; i >= 0; i--)
//            {
//                ScheduledRaid r = raids[i];
//                if (r.TriggerTick <= tick) { LaunchRaid(r); raids.RemoveAt(i); }
//            }
//        }

//        private void FireShell(ScheduledShell s)
//        {
//            if (s.Map == null || !s.TargetCell.InBounds(s.Map)) return;

//            bool isCE = false;
//            if (CE_Loaded && s.ShellDef.thingClass != null)
//            {
//                Type projType = ProjectileCE_Type;
//                if (projType != null && projType.IsAssignableFrom(s.ShellDef.thingClass))
//                    isCE = true;
//                if (!isCE && s.ShellDef.thingClass.Name.Contains("ProjectileCE"))
//                    isCE = true;
//            }

//            Log.Message("[AS] Firing " + s.ShellDef.defName + " at " + s.TargetCell + ". IsCE=" + isCE);

//            if (isCE)
//                FireCEShell(s);
//            else
//                VanillaExplosion(s);
//        }

//        private void FireCEShell(ScheduledShell s)
//        {
//            try
//            {
//                float radius; int damage; DamageDef damageDef; SoundDef sound; bool applyToNeighbors; float armorPenetration;
//                ReadCEProjectileProperties(s.ShellDef, out radius, out damage, out damageDef, out sound, out applyToNeighbors, out armorPenetration);

//                // Звук свиста снаряда перед ударом
//                SoundDef whistleSound = DefDatabase<SoundDef>.GetNamed("MortarRound_PreImpact", false);
//                if (whistleSound != null)
//                    SoundStarter.PlayOneShot(whistleSound, new TargetInfo(s.TargetCell, s.Map));

//                Vector3 skyPos = new Vector3(s.TargetCell.x, 150f, s.TargetCell.z);
//                FleckMaker.ThrowMetaPuff(skyPos, s.Map);

//                int flightTime = Rand.Range(45, 90);
//                ScheduledShell shellCopy = s;
//                RegisterDelayedAction(() =>
//                {
//                    if (!shellCopy.TargetCell.InBounds(shellCopy.Map)) return;
//                    DoExplosionViaReflection(shellCopy, radius, damage, damageDef, sound, applyToNeighbors, armorPenetration);
//                }, Find.TickManager.TicksGame + flightTime);

//            }
//            catch (Exception ex)
//            {
//                Log.Warning("[AS] CE shell error: " + ex.Message + "\n" + ex.StackTrace);
//                VanillaExplosion(s);
//            }
//        }

//        private void DoExplosionViaReflection(ScheduledShell s, float radius, int damage, DamageDef damageDef,
//            SoundDef sound, bool applyToNeighbors, float armorPenetration)
//        {
//            try
//            {
//                MethodInfo[] methods = typeof(GenExplosion).GetMethods(BindingFlags.Public | BindingFlags.Static);
//                List<MethodInfo> doExplosionMethods = new List<MethodInfo>();
//                for (int i = 0; i < methods.Length; i++)
//                {
//                    if (methods[i].Name == "DoExplosion")
//                        doExplosionMethods.Add(methods[i]);
//                }

//                MethodInfo targetMethod = null;
//                object[] args = null;

//                for (int m = 0; m < doExplosionMethods.Count; m++)
//                {
//                    MethodInfo method = doExplosionMethods[m];
//                    ParameterInfo[] parms = method.GetParameters();

//                    if (parms.Length < 7 || parms[0].ParameterType != typeof(IntVec3))
//                        continue;

//                    args = new object[parms.Length];

//                    for (int i = 0; i < parms.Length; i++)
//                    {
//                        ParameterInfo p = parms[i];
//                        string nameLower = p.Name.ToLower();
//                        switch (nameLower)
//                        {
//                            case "center": args[i] = s.TargetCell; break;
//                            case "map": args[i] = s.Map; break;
//                            case "radius": args[i] = radius; break;
//                            case "damtype": args[i] = damageDef; break;
//                            case "instigator": args[i] = null; break;
//                            case "damamount": args[i] = damage; break;
//                            case "armorpenetration": args[i] = armorPenetration; break;
//                            case "explosionsound": args[i] = sound; break;
//                            case "weapon": args[i] = s.ShellDef; break;
//                            case "projectile": args[i] = null; break;
//                            case "intendedtarget": args[i] = null; break;
//                            case "postexplosionspawnthingdef": args[i] = null; break;
//                            case "postexplosionspawnchance": args[i] = 0f; break;
//                            case "postexplosionspawnthingcount": args[i] = 1; break;
//                            case "postexplosiongastype": args[i] = null; break;
//                            case "applydamagetoexplosioncellsneighbors": args[i] = applyToNeighbors; break;
//                            case "preexplosionspawnthingdef": args[i] = null; break;
//                            case "preexplosionspawnchance": args[i] = 0f; break;
//                            case "preexplosionspawnthingcount": args[i] = 0; break;
//                            case "explosionchancetostartfire": args[i] = 0f; break;
//                            case "explosiondamagefalloff": args[i] = false; break;
//                            case "direction": args[i] = null; break;
//                            case "ignoredthings": args[i] = null; break;
//                            case "affectedpawns": args[i] = null; break;
//                            case "dovisualeffects": args[i] = true; break;
//                            case "propagationspeed": args[i] = 1f; break;
//                            case "excluderadius": args[i] = 0f; break;
//                            case "dosoundeffects": args[i] = true; break;
//                            case "postexplosionspawnthingdefwater": args[i] = null; break;
//                            case "screenshakefactor": args[i] = radius * 0.15f; break;
//                            default:
//                                args[i] = p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
//                                break;
//                        }
//                    }

//                    targetMethod = method;
//                    break;
//                }

//                if (targetMethod != null && args != null)
//                {
//                    targetMethod.Invoke(null, args);
//                    Log.Message("[AS] Explosion at " + s.TargetCell + " via reflection, method with " + args.Length + " params");
//                }
//                else
//                {
//                    throw new InvalidOperationException("No suitable DoExplosion found");
//                }

//                FleckMaker.Static(s.TargetCell.ToVector3(), s.Map, FleckDefOf.ExplosionFlash, radius * 0.5f);
//                FleckMaker.ThrowSmoke(s.TargetCell.ToVector3(), s.Map, radius * 0.6f);

//                if (Find.CurrentMap == s.Map)
//                {
//                    float dist = (s.TargetCell.ToVector3() - Find.CameraDriver.MapPosition.ToVector3()).magnitude;
//                    if (dist < 50f)
//                        Find.CameraDriver.shaker.DoShake(Mathf.Min(radius * 0.15f, 1.5f) * (1f - dist / 50f));
//                }

//                SpawnFragments(s);
//            }
//            catch (Exception ex)
//            {
//                Log.Warning("[AS] DoExplosionViaReflection error: " + ex.Message);
//                DoManualDamage(s, radius, damage, damageDef, armorPenetration);
//            }
//        }

//        private void DoManualDamage(ScheduledShell s, float radius, int damage, DamageDef damageDef, float armorPenetration)
//        {
//            try
//            {
//                foreach (Thing thing in GenRadial.RadialDistinctThingsAround(s.TargetCell, s.Map, radius, true))
//                {
//                    if (thing == null) continue;
//                    try
//                    {
//                        thing.TakeDamage(new DamageInfo(damageDef, damage, armorPenetration, -1f));
//                    }
//                    catch { }
//                }

//                FleckMaker.Static(s.TargetCell.ToVector3(), s.Map, FleckDefOf.ExplosionFlash, radius);
//                FleckMaker.ThrowSmoke(s.TargetCell.ToVector3(), s.Map, radius);

//                if (Find.CurrentMap == s.Map)
//                    Find.CameraDriver.shaker.DoShake(0.5f);
//            }
//            catch (Exception ex)
//            {
//                Log.Warning("[AS] DoManualDamage error: " + ex.Message);
//            }
//        }

//        private void SpawnFragments(ScheduledShell s)
//        {
//            try
//            {
//                CompProperties fragmentsComp = s.ShellDef.GetCompProperties<CompProperties>();
//                if (fragmentsComp == null) return;

//                Type compType = fragmentsComp.GetType();
//                if (!compType.Name.Contains("Fragments")) return;

//                FieldInfo fragmentsField = compType.GetField("fragments");
//                if (fragmentsField == null) return;

//                object fragments = fragmentsField.GetValue(fragmentsComp);
//                if (fragments == null) return;

//                MethodInfo throwMethod = compType.GetMethod("Throw");
//                if (throwMethod != null)
//                {
//                    throwMethod.Invoke(fragmentsComp, new object[] {
//                        s.TargetCell.ToVector3(), s.Map, null
//                    });
//                    Log.Message("[AS] Fragments spawned at " + s.TargetCell);
//                }
//            }
//            catch { }
//        }

//        private void ReadCEProjectileProperties(ThingDef shellDef, out float radius, out int damage,
//            out DamageDef damageDef, out SoundDef sound, out bool applyToNeighbors,
//            out float armorPenetration)
//        {
//            radius = 5.5f;
//            damage = 578;
//            damageDef = DamageDefOf.Bomb;
//            sound = null;
//            applyToNeighbors = true;
//            armorPenetration = 10f;

//            if (!CE_Loaded || shellDef == null) return;

//            try
//            {
//                ProjectileProperties proj = shellDef.projectile;
//                if (proj == null) return;

//                Type projType = proj.GetType();

//                FieldInfo r = projType.GetField("explosionRadius", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
//                if (r != null) radius = (float)r.GetValue(proj);

//                FieldInfo d = projType.GetField("damageAmountBase", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
//                if (d != null) damage = (int)d.GetValue(proj);

//                FieldInfo dd = projType.GetField("damageDef", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
//                if (dd != null) damageDef = dd.GetValue(proj) as DamageDef;

//                FieldInfo s = projType.GetField("soundExplode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
//                if (s != null) sound = s.GetValue(proj) as SoundDef;

//                FieldInfo n = projType.GetField("applyDamageToExplosionCellsNeighbors", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
//                if (n != null) applyToNeighbors = (bool)n.GetValue(proj);

//                MethodInfo getAPMethod = projType.GetMethod("GetArmorPenetration", new Type[] { typeof(Thing) });
//                if (getAPMethod != null)
//                {
//                    armorPenetration = (float)getAPMethod.Invoke(proj, new object[] { null });
//                }
//                else
//                {
//                    MethodInfo getAPMethodNoArgs = projType.GetMethod("GetArmorPenetration", Type.EmptyTypes);
//                    if (getAPMethodNoArgs != null)
//                    {
//                        armorPenetration = (float)getAPMethodNoArgs.Invoke(proj, null);
//                    }
//                }

//                MethodInfo getExplosionAP = projType.GetMethod("GetExplosionArmorPenetration");
//                if (getExplosionAP != null)
//                {
//                    armorPenetration = (float)getExplosionAP.Invoke(proj, null);
//                }

//                FieldInfo shellingField = projType.GetField("shellingProps", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
//                if (shellingField != null)
//                {
//                    object shelling = shellingField.GetValue(proj);
//                    if (shelling != null)
//                    {
//                        Type shellingType = shelling.GetType();
//                        FieldInfo dmgField = shellingType.GetField("damage", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
//                        if (dmgField != null)
//                        {
//                            float shellingDmg = (float)dmgField.GetValue(shelling);
//                            if (shellingDmg > 0) damage = Mathf.RoundToInt(damage * shellingDmg);
//                        }
//                    }
//                }

//                Log.Message("[AS] Read CE props for " + shellDef.defName + ": radius=" + radius + ", damage=" + damage + ", AP=" + armorPenetration);
//            }
//            catch (Exception ex)
//            {
//                Log.Warning("[AS] ReadCEProjectileProperties error: " + ex.Message);
//            }
//        }

//        private void VanillaExplosion(ScheduledShell s)
//        {
//            float radius = Rand.Range(3f, 5f);
//            int dmg = Rand.Range(40, 80);
//            DamageDef dd = DamageDefOf.Bomb;
//            if (s.ShellDef != null && s.ShellDef.defName != null)
//            {
//                if (s.ShellDef.defName.Contains("Incendiary")) dd = DamageDefOf.Flame;
//                else if (s.ShellDef.defName.Contains("EMP")) dd = DamageDefOf.EMP;
//            }

//            try
//            {
//                ProjectileProperties proj = s.ShellDef == null ? null : s.ShellDef.projectile;
//                if (proj != null)
//                {
//                    MethodInfo getDmgMethod = proj.GetType().GetMethod("GetDamageAmount", new Type[] { typeof(Thing) });
//                    if (getDmgMethod != null)
//                    {
//                        dmg = (int)getDmgMethod.Invoke(proj, new object[] { null });
//                    }
//                }
//            }
//            catch { }

//            try
//            {
//                ScheduledShell dummy = new ScheduledShell { TargetCell = s.TargetCell, Map = s.Map, ShellDef = s.ShellDef };
//                DoExplosionViaReflection(dummy, radius, dmg, dd, null, false, 8f);
//            }
//            catch
//            {
//                DoManualDamage(s, radius, dmg, dd, 8f);
//            }

//            FleckMaker.Static(s.TargetCell.ToVector3(), s.Map, FleckDefOf.ExplosionFlash, 2f);
//            FleckMaker.ThrowSmoke(s.TargetCell.ToVector3(), s.Map, 3f);
//        }

//        private void LaunchRaid(ScheduledRaid r)
//        {
//            if (r.Parms == null)
//            {
//                Log.Warning("[AS] LaunchRaid: Parms is null");
//                return;
//            }

//            Map map = r.Parms.target as Map;
//            if (map == null)
//            {
//                Log.Warning("[AS] LaunchRaid: target is null or not a Map");
//                return;
//            }

//            try
//            {
//                if (r.Parms.faction == null)
//                {
//                    Log.Warning("[AS] LaunchRaid: faction is null, aborting");
//                    return;
//                }

//                IncidentWorker worker = IncidentDefOf.RaidEnemy.Worker;
//                IncidentWorker_RaidEnemy raidWorker = worker as IncidentWorker_RaidEnemy;
//                if (raidWorker == null)
//                {
//                    Log.Warning("[AS] LaunchRaid: could not get RaidEnemy worker");
//                    return;
//                }

//                IncidentParms freshParms = new IncidentParms
//                {
//                    target = map,
//                    faction = r.Parms.faction,
//                    points = r.Parms.points,
//                    forced = true
//                };

//                if (r.Parms.raidArrivalMode != null)
//                    freshParms.raidArrivalMode = r.Parms.raidArrivalMode;
//                else
//                    freshParms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;

//                if (r.Parms.raidStrategy != null)
//                    freshParms.raidStrategy = r.Parms.raidStrategy;
//                else
//                    freshParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;

//                if (raidWorker.TryExecute(freshParms))
//                {
//                    // Убрано Messages.Message - не нужно дублировать вверху экрана
//                    Log.Message("[AS] Raid launched successfully: " + freshParms.faction.Name + ", points=" + freshParms.points.ToString("F0"));
//                }
//                else
//                {
//                    Log.Warning("[AS] Raid TryExecute returned false for " + freshParms.faction.Name);
//                }
//            }
//            catch (Exception ex)
//            {
//                Log.Error("[AS] LaunchRaid exception: " + ex.Message + "\n" + ex.StackTrace);
//            }
//        }

//        public override void ExposeData()
//        {
//            base.ExposeData();
//            Scribe_Collections.Look(ref shells, "shells", LookMode.Deep);
//            Scribe_Collections.Look(ref raids, "raids", LookMode.Deep);
//            Scribe_Collections.Look(ref delayed, "delayed", LookMode.Deep);
//            Scribe_Collections.Look(ref repeating, "repeating", LookMode.Deep);
//        }

//        private static bool CE_Loaded => ModsConfig.IsActive("CETeam.CombatExtended") || ModsConfig.IsActive("CombatExtended.Continued");

//        private static Type _projCEType;
//        private static Type ProjectileCE_Type
//        {
//            get
//            {
//                if (_projCEType == null && CE_Loaded)
//                    _projCEType = Type.GetType("CombatExtended.ProjectileCE, CombatExtended", false);
//                return _projCEType;
//            }
//        }
//    }

//    public class ScheduledShell : IExposable
//    {
//        public Map Map; public Faction Faction; public IntVec3 TargetCell; public ThingDef ShellDef; public int TriggerTick;
//        public void ExposeData()
//        {
//            Scribe_References.Look(ref Map, "map"); Scribe_References.Look(ref Faction, "faction");
//            Scribe_Values.Look(ref TargetCell, "targetCell"); Scribe_Defs.Look(ref ShellDef, "shellDef");
//            Scribe_Values.Look(ref TriggerTick, "triggerTick", 0);
//        }
//    }

//    public class ScheduledRaid : IExposable
//    {
//        public IncidentParms Parms; public int TriggerTick;
//        public void ExposeData() { Scribe_Deep.Look(ref Parms, "parms"); Scribe_Values.Look(ref TriggerTick, "triggerTick", 0); }
//    }

//    public class DelayedAction : IExposable
//    {
//        [Unsaved(false)] public Action Action; public int TriggerTick;
//        public void ExposeData() { Scribe_Values.Look(ref TriggerTick, "triggerTick", 0); }
//    }

//    public class RepeatingAction : IExposable
//    {
//        [Unsaved(false)] public Action Action; public int NextTick, Interval, RemainingRepetitions;
//        public void ExposeData()
//        {
//            Scribe_Values.Look(ref NextTick, "nextTick", 0); Scribe_Values.Look(ref Interval, "interval", 60);
//            Scribe_Values.Look(ref RemainingRepetitions, "remainingRepetitions", 0);
//        }
//    }
//}
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace HSK.Watcher.Content.Incident
{
    [StaticConstructorOnStartup]
    public static class ArtilleryModLoader
    {
        static ArtilleryModLoader()
        {
            new Harmony("HSK.Watcher.Content.Incident").PatchAll();
        }
    }

    public class IncidentWorker_ArtilleryPreparation : IncidentWorker_RaidEnemy
    {
        private const float BombardmentRadius = 25f;
        private const int SalvoIntervalTicks = 120;
        private const int FlareToBarrageDelayTicks = 900;
        private const int RaidDelayTicks = 1800;

        private static readonly string[] CEShellNames = new string[] {
            "Bullet_155mmHowitzerShell_HE",
            "Bullet_155mmHowitzerShell_Incendiary",
            "Bullet_81mmMortarShell_HE",
        };

        private static bool? _ceLoaded;
        private static bool CE_Loaded
        {
            get
            {
                if (!_ceLoaded.HasValue)
                    _ceLoaded = ModsConfig.IsActive("CETeam.CombatExtended") || ModsConfig.IsActive("CombatExtended.Continued");
                return _ceLoaded.Value;
            }
        }

        private static Type _projectileCEType;
        private static Type ProjectileCE_Type
        {
            get
            {
                if (_projectileCEType == null && CE_Loaded)
                    _projectileCEType = Type.GetType("CombatExtended.ProjectileCE, CombatExtended", false);
                return _projectileCEType;
            }
        }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            Faction faction = parms.faction;
            if (faction == null)
                faction = Find.FactionManager.RandomEnemyFaction(false, false, true, TechLevel.Industrial);
            if (faction == null) return false;
            parms.faction = faction;
            if (faction.def.techLevel < TechLevel.Industrial) return false;
            IntVec3 dummy;
            return TryFindBombardmentTarget(map, out dummy);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            Faction faction = parms.faction;
            if (faction == null)
                faction = Find.FactionManager.RandomEnemyFaction(false, false, true, TechLevel.Industrial);
            if (faction == null) return false;
            parms.faction = faction;

            IntVec3 target;
            if (!TryFindBombardmentTarget(map, out target)) return false;

            SendInterceptedTransmission(faction, map, target);

            int totalShells = Rand.Range(4, 9);
            int salvos = totalShells;
            int barrageStart = Find.TickManager.TicksGame + FlareToBarrageDelayTicks;

            ScheduleBarrage(map, faction, target, totalShells, salvos, barrageStart);
            ScheduleRaid(parms, totalShells, barrageStart + salvos * SalvoIntervalTicks + RaidDelayTicks);

            return true;
        }

        private void SendInterceptedTransmission(Faction faction, Map map, IntVec3 target)
        {
            string coords = FormatCoordinates(target, map);
            string letterLabel = "LetterLabelArtilleryPrep".Translate(faction.Name);
            string letterText = "LetterArtilleryPrep".Translate(faction.Name, coords);
            Find.LetterStack.ReceiveLetter(
                letterLabel,
                letterText,
                LetterDefOf.ThreatBig, new TargetInfo(map.Center, map));
        }

        private string FormatCoordinates(IntVec3 cell, Map map)
        {
            int sectorX = Mathf.FloorToInt((float)cell.x / 10f);
            int sectorZ = Mathf.FloorToInt((float)cell.z / 10f);
            int absSectorX = Mathf.Abs(sectorX);
            int absSectorZ = Mathf.Abs(sectorZ);
            char sectorLetter = (char)('A' + (absSectorX % 26));
            string sectorNumber = (absSectorZ + 1).ToString("D2");
            int offsetX = Mathf.Abs(cell.x) % 10;
            int offsetZ = Mathf.Abs(cell.z) % 10;
            string result = "";
            result += sectorLetter;
            result += sectorNumber;
            result += "-";
            result += offsetX.ToString();
            result += "-";
            result += offsetZ.ToString();
            return result;
        }

        private void ScheduleBarrage(Map map, Faction faction, IntVec3 center, int totalShells, int salvos, int startTick)
        {
            List<ThingDef> shellDefs = GetShellDefs();
            if (shellDefs.Count == 0)
            {
                shellDefs = new List<ThingDef>();
                shellDefs.Add(ThingDefOf.Shell_HighExplosive);
            }

            for (int s = 0; s < salvos; s++)
            {
                int shellTick = startTick + s * SalvoIntervalTicks + Rand.Range(-20, 20);
                ThingDef def = shellDefs[Rand.Range(0, shellDefs.Count)];
                IntVec3 cell = SelectImpactCell(center);
                ArtilleryScheduler sched = map.GetComponent<ArtilleryScheduler>();
                if (sched != null) sched.RegisterShell(map, faction, cell, def, shellTick);
            }

            ArtilleryScheduler barrageSched = map.GetComponent<ArtilleryScheduler>();
            if (barrageSched != null)
                barrageSched.RegisterDelayedAction(() =>
                {
                    if (center.InBounds(map))
                    {
                        SoundDef preImpactSound = DefDatabase<SoundDef>.GetNamed("MortarRound_PreImpact", false);
                        if (preImpactSound != null)
                            SoundStarter.PlayOneShot(preImpactSound, new TargetInfo(center, map));
                    }
                }, startTick - 60);
        }

        private List<ThingDef> GetShellDefs()
        {
            List<ThingDef> list = new List<ThingDef>();
            if (CE_Loaded)
            {
                for (int i = 0; i < CEShellNames.Length; i++)
                {
                    ThingDef def = DefDatabase<ThingDef>.GetNamed(CEShellNames[i], false);
                    if (def != null) list.Add(def);
                }
            }
            if (list.Count == 0)
                list.Add(ThingDefOf.Shell_HighExplosive);
            return list;
        }

        private IntVec3 SelectImpactCell(IntVec3 center)
        {
            float angle = Rand.Range(0f, 360f);
            float dist = Rand.Range(0f, BombardmentRadius);

            if (Rand.Value < 0.6f)
            {
                dist = Rand.Range(BombardmentRadius * 0.5f, BombardmentRadius);
            }

            return new IntVec3(
                Mathf.RoundToInt(center.x + Mathf.Cos(angle * Mathf.Deg2Rad) * dist),
                0,
                Mathf.RoundToInt(center.z + Mathf.Sin(angle * Mathf.Deg2Rad) * dist));
        }

        private void ScheduleRaid(IncidentParms parms, int totalShells, int raidTick)
        {
            if (parms.faction == null)
            {
                Log.Warning("[ArtilleryPrep] ScheduleRaid: faction is null, cannot schedule raid");
                return;
            }

            float factor = 1f + totalShells * 0.05f;
            if (parms.points <= 0)
                parms.points = StorytellerUtility.DefaultThreatPointsNow(parms.target);
            parms.points = Mathf.Min(parms.points * factor, 10000f);

            if (parms.raidArrivalMode == null)
                parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            if (parms.raidStrategy == null)
                parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;

            Map map = parms.target as Map;
            if (map != null)
            {
                ArtilleryScheduler sched = map.GetComponent<ArtilleryScheduler>();
                if (sched != null) sched.RegisterRaid(parms, raidTick);
            }
            Log.Message("[ArtilleryPrep] Raid scheduled at tick " + raidTick + " for " + parms.faction.Name + ", points=" + parms.points.ToString("F0"));
        }

        private bool TryFindBombardmentTarget(Map map, out IntVec3 target)
        {
            target = IntVec3.Invalid;

            // === ПРИОРИТЕТ 1: Артиллерия игрока ===
            List<Thing> things = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
            List<Thing> artillery = new List<Thing>();
            for (int i = 0; i < things.Count; i++)
            {
                if (IsArtillery(things[i]) && things[i].Faction == Faction.OfPlayer)
                    artillery.Add(things[i]);
            }
            if (artillery.Count > 0)
            {
                target = artillery[Rand.Range(0, artillery.Count)].Position;
                return true;
            }

            // === ПРИОРИТЕТ 2: Здания с тегами ===
            HashSet<string> targetTags = new HashSet<string>();
            targetTags.Add("MachineGun");
            targetTags.Add("Artillery");
            targetTags.Add("Turret");
            targetTags.Add("Mortar");
            targetTags.Add("Howitzer");
            targetTags.Add("Cannon");
            targetTags.Add("GunEmplacement");
            targetTags.Add("HeavyWeapon");

            List<Building> allBuildings = map.listerBuildings.allBuildingsColonist;
            List<Building> taggedTargets = new List<Building>();

            for (int i = 0; i < allBuildings.Count; i++)
            {
                Building b = allBuildings[i];
                if (b.def.building != null && b.def.building.buildingTags != null)
                {
                    List<string> tags = b.def.building.buildingTags;
                    for (int t = 0; t < tags.Count; t++)
                    {
                        if (targetTags.Contains(tags[t]))
                        {
                            taggedTargets.Add(b);
                            break;
                        }
                    }
                }
            }

            if (taggedTargets.Count > 0)
            {
                target = taggedTargets[Rand.Range(0, taggedTargets.Count)].Position;
                return true;
            }

            // === ПРИОРИТЕТ 3: Критическая инфраструктура ===
            HashSet<string> criticalDefs = new HashSet<string>();
            criticalDefs.Add("PowerGenerator");
            criticalDefs.Add("GeothermalGenerator");
            criticalDefs.Add("SolarGenerator");
            criticalDefs.Add("Turret_MiniTurret");
            criticalDefs.Add("Battery");

            List<Thing> criticalTargets = new List<Thing>();
            for (int i = 0; i < allBuildings.Count; i++)
            {
                Building b = allBuildings[i];
                if (criticalDefs.Contains(b.def.defName) || (b.def.building != null && b.def.building.ai_combatDangerous))
                    criticalTargets.Add(b);
            }
            if (criticalTargets.Count > 0)
            {
                target = criticalTargets[Rand.Range(0, criticalTargets.Count)].Position;
                return true;
            }

            // === ПРИОРИТЕТ 4: Колонисты ===
            List<Pawn> colonists = map.mapPawns.FreeColonists;
            if (colonists.Count > 0)
            {
                int bestCount = 0;
                IntVec3 bestPos = colonists[0].Position;
                for (int i = 0; i < colonists.Count; i++)
                {
                    int count = 0;
                    for (int j = 0; j < colonists.Count; j++)
                    {
                        if (colonists[i].Position.DistanceTo(colonists[j].Position) < 15f)
                            count++;
                    }
                    if (count > bestCount)
                    {
                        bestCount = count;
                        bestPos = colonists[i].Position;
                    }
                }
                target = bestPos;
                return true;
            }

            return CellFinder.TryFindRandomCellNear(map.Center, map, (int)(map.Size.x * 0.3f), c => c.Standable(map) && !c.Roofed(map), out target);
        }

        private bool IsArtillery(Thing t)
        {
            if (t == null) return false;
            if (CE_Loaded)
            {
                Type ceTurret = Type.GetType("CombatExtended.CompTurretGunCE, CombatExtended", false);
                if (ceTurret != null)
                {
                    MethodInfo tryGetComp = t.GetType().GetMethod("TryGetComp");
                    if (tryGetComp != null)
                    {
                        MethodInfo generic = tryGetComp.MakeGenericMethod(ceTurret);
                        if (generic != null && generic.Invoke(t, null) != null) return true;
                    }
                }
            }
            CompEquippable eq = t.TryGetComp<CompEquippable>();
            if (eq != null && eq.PrimaryVerb != null && eq.PrimaryVerb.verbProps != null && eq.PrimaryVerb.verbProps.range >= 40f) return true;
            string n = t.def.defName.ToLower();
            return n.Contains("mortar") || n.Contains("artillery") || n.Contains("howitzer") || n.Contains("cannon");
        }

        public override float BaseChanceThisGame
        {
            get
            {
                float c = base.BaseChanceThisGame;
                List<Map> maps = Find.Maps;
                for (int i = 0; i < maps.Count; i++)
                {
                    Map m = maps[i];
                    if (!m.IsPlayerHome) continue;

                    List<Thing> things = m.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
                    int artilleryCount = 0;
                    for (int j = 0; j < things.Count; j++)
                    {
                        if (IsArtillery(things[j]) && things[j].Faction == Faction.OfPlayer)
                            artilleryCount++;
                    }
                    c += artilleryCount * 0.5f;
                    c += m.wealthWatcher.WealthTotal / 50000f;
                }
                return Mathf.Min(c, 8f);
            }
        }

        protected override void ResolveRaidPoints(IncidentParms parms) => base.ResolveRaidPoints(parms);
    }

    public class ArtilleryScheduler : MapComponent
    {
        private List<ScheduledShell> shells = new List<ScheduledShell>();
        private List<ScheduledRaid> raids = new List<ScheduledRaid>();
        private List<DelayedAction> delayed = new List<DelayedAction>();
        private List<RepeatingAction> repeating = new List<RepeatingAction>();

        private const float DefaultShotHeight = 150f;

        private static bool _ceFieldsCached = false;
        private static MethodInfo _ceLaunchMethod;
        private static FieldInfo _ceDestinationField;

        private static Type _projectilePropertiesCEType;

        public ArtilleryScheduler(Map map) : base(map) { }

        public void RegisterShell(Map map, Faction faction, IntVec3 target, ThingDef def, int tick)
        {
            shells.Add(new ScheduledShell { Map = map, Faction = faction, TargetCell = target, ShellDef = def, TriggerTick = tick });
        }

        public void RegisterRaid(IncidentParms parms, int tick) => raids.Add(new ScheduledRaid { Parms = parms, TriggerTick = tick });
        public void RegisterDelayedAction(Action action, int tick) => delayed.Add(new DelayedAction { Action = action, TriggerTick = tick });
        public void RegisterRepeatingAction(Action action, int tick, int interval, int reps) => repeating.Add(new RepeatingAction { Action = action, NextTick = tick, Interval = interval, RemainingRepetitions = reps });

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            int tick = Find.TickManager.TicksGame;

            for (int i = delayed.Count - 1; i >= 0; i--)
            {
                DelayedAction d = delayed[i];
                if (d.TriggerTick <= tick)
                {
                    try { if (d.Action != null) d.Action(); }
                    catch (Exception ex) { Log.Warning("[AS] Delayed err: " + ex.Message); }
                    delayed.RemoveAt(i);
                }
            }

            for (int i = repeating.Count - 1; i >= 0; i--)
            {
                RepeatingAction r = repeating[i];
                if (r.NextTick <= tick)
                {
                    try { if (r.Action != null) r.Action(); }
                    catch (Exception ex) { Log.Warning("[AS] Repeat err: " + ex.Message); }
                    r.RemainingRepetitions--;
                    r.NextTick += r.Interval;
                    if (r.RemainingRepetitions <= 0) repeating.RemoveAt(i);
                }
            }

            for (int i = shells.Count - 1; i >= 0; i--)
            {
                ScheduledShell s = shells[i];
                if (s.TriggerTick <= tick) { FireShell(s); shells.RemoveAt(i); }
            }

            for (int i = raids.Count - 1; i >= 0; i--)
            {
                ScheduledRaid r = raids[i];
                if (r.TriggerTick <= tick) { LaunchRaid(r); raids.RemoveAt(i); }
            }
        }

        private void FireShell(ScheduledShell s)
        {
            if (s.Map == null || !s.TargetCell.InBounds(s.Map)) return;

            bool isCE = false;
            if (CE_Loaded && s.ShellDef.thingClass != null)
            {
                Type projType = ProjectileCE_Type;
                if (projType != null && projType.IsAssignableFrom(s.ShellDef.thingClass))
                    isCE = true;
                if (!isCE && s.ShellDef.thingClass.Name.Contains("ProjectileCE"))
                    isCE = true;
            }

            if (isCE)
                FireCEShellFromEdge(s);
            else
                FireVanillaShell(s);
        }

        private void FireCEShellFromEdge(ScheduledShell s)
        {
            try
            {
                CacheCEFields();

                if (!ValidateCEProjectileDef(s.ShellDef))
                {
                    FireVanillaShell(s);
                    return;
                }

                Vector3 edgeOrigin = CalculateEdgeOrigin(s.TargetCell, s.Map);
                Vector2 origin2D = new Vector2(edgeOrigin.x, edgeOrigin.z);

                float distance = Vector3.Distance(edgeOrigin, s.TargetCell.ToVector3());
                float shotAngle = CalculateCEShotAngle(distance);
                float shotRotation = CalculateCEShotRotation(edgeOrigin, s.TargetCell);
                float shotSpeed = GetCEShellSpeed(s.ShellDef);
                float shotHeight = DefaultShotHeight;

                Thing launcher = CreateFakeLauncher(s.Map, edgeOrigin);
                if (launcher == null)
                {
                    VanillaExplosion(s);
                    return;
                }

                Thing projectile = ThingMaker.MakeThing(s.ShellDef);
                if (projectile == null)
                {
                    launcher.Destroy();
                    VanillaExplosion(s);
                    return;
                }

                GenSpawn.Spawn(projectile, launcher.Position, s.Map);

                if (projectile == null || projectile.Destroyed)
                {
                    launcher.Destroy();
                    VanillaExplosion(s);
                    return;
                }

                if (_ceLaunchMethod != null)
                {
                    try
                    {
                        _ceLaunchMethod.Invoke(projectile, new object[]
                        {
                            launcher, origin2D, shotAngle, shotRotation,
                            shotHeight, shotSpeed, null, distance
                        });
                    }
                    catch (TargetInvocationException)
                    {
                        projectile.Destroy();
                        launcher.Destroy();
                        VanillaExplosion(s);
                        return;
                    }
                }
                else
                {
                    projectile.Destroy();
                    launcher.Destroy();
                    VanillaExplosion(s);
                    return;
                }

                // Fix Destination — CE Launch() calculates it from origin+distance which puts it off-map
                if (_ceDestinationField != null)
                {
                    object dest = _ceDestinationField.GetValue(projectile);
                    Vector2 currentDest = Vector2.zero;
                    if (dest is Vector2) currentDest = (Vector2)dest;
                    else if (dest is Vector3) currentDest = new Vector2(((Vector3)dest).x, ((Vector3)dest).z);

                    bool isOffMap = currentDest.x < 0 || currentDest.x > 250 || currentDest.y < 0 || currentDest.y > 250;
                    if (isOffMap || currentDest == Vector2.zero)
                    {
                        _ceDestinationField.SetValue(projectile, new Vector2(s.TargetCell.x, s.TargetCell.z));
                    }
                }

                RegisterDelayedAction(() =>
                {
                    if (launcher != null && !launcher.Destroyed)
                    {
                        try { launcher.Destroy(); } catch { }
                    }
                }, Find.TickManager.TicksGame + 600);
            }
            catch (Exception ex)
            {
                Log.Warning("[AS] CE shell error: " + ex.Message);
                VanillaExplosion(s);
            }
        }

        private Vector3 CalculateEdgeOrigin(IntVec3 target, Map map)
        {
            Vector3 center = map.Center.ToVector3();
            Vector3 targetV3 = target.ToVector3();
            Vector3 dir = (center - targetV3).normalized;
            if (dir == Vector3.zero) dir = Vector3.forward;

            float tX = dir.x > 0.01f ? (map.Size.x - 1 - center.x) / dir.x :
                       dir.x < -0.01f ? -center.x / dir.x : float.MaxValue;
            float tZ = dir.z > 0.01f ? (map.Size.z - 1 - center.z) / dir.z :
                       dir.z < -0.01f ? -center.z / dir.z : float.MaxValue;

            float t = Mathf.Min(Mathf.Abs(tX), Mathf.Abs(tZ)) * 0.95f;
            Vector3 origin = center + dir * t;
            origin.y = DefaultShotHeight;

            IntVec3 cell = origin.ToIntVec3();
            if (!cell.InBounds(map))
            {
                cell = CellFinder.RandomEdgeCell(map);
                origin = cell.ToVector3Shifted();
                origin.y = DefaultShotHeight;
            }
            return origin;
        }

        private bool ValidateCEProjectileDef(ThingDef shellDef)
        {
            if (shellDef.projectile == null) return false;
            return shellDef.projectile.GetType().Name.Contains("ProjectilePropertiesCE");
        }

        private Thing CreateFakeLauncher(Map map, Vector3 desiredPosition)
        {
            try
            {
                ThingDef filthDef = DefDatabase<ThingDef>.GetNamed("Filth_Blood", false)
                    ?? ThingDefOf.Filth_Blood
                    ?? DefDatabase<ThingDef>.GetNamed("ChunkSandstone", false)
                    ?? DefDatabase<ThingDef>.GetNamed("ChunkGranite", false);

                if (filthDef == null) return null;

                Thing launcher = ThingMaker.MakeThing(filthDef);
                if (launcher == null) return null;

                IntVec3 spawnCell = desiredPosition.ToIntVec3();
                if (!spawnCell.InBounds(map))
                    spawnCell = CellFinder.RandomEdgeCell(map);

                GenSpawn.Spawn(launcher, spawnCell, map);
                return launcher.Spawned ? launcher : null;
            }
            catch
            {
                return null;
            }
        }

        private float GetCEShellSpeed(ThingDef shellDef)
        {
            try
            {
                if (shellDef.projectile != null)
                {
                    float speed = shellDef.projectile.speed;
                    if (speed > 0) return speed;

                    Type ppceType = shellDef.projectile.GetType();
                    if (ppceType.Name.Contains("ProjectilePropertiesCE"))
                    {
                        FieldInfo speedField = ppceType.GetField("speed", BindingFlags.Public | BindingFlags.Instance);
                        if (speedField != null)
                        {
                            float ceSpeed = (float)speedField.GetValue(shellDef.projectile);
                            if (ceSpeed > 0) return ceSpeed;
                        }

                        PropertyInfo speedProp = ppceType.GetProperty("Speed", BindingFlags.Public | BindingFlags.Instance);
                        if (speedProp != null)
                        {
                            float ceSpeed = (float)speedProp.GetValue(shellDef.projectile);
                            if (ceSpeed > 0) return ceSpeed;
                        }
                    }
                }
            }
            catch { }

            string defName = shellDef.defName.ToLower();
            if (defName.Contains("81mm") || defName.Contains("mortar")) return 70f;
            if (defName.Contains("155mm") || defName.Contains("howitzer")) return 120f;
            if (defName.Contains("120mm")) return 95f;
            if (defName.Contains("105mm")) return 110f;
            return 80f;
        }

        private float CalculateCEShotAngle(float distance)
        {
            if (distance > 300f) return Rand.Range(0.30f, 0.50f);
            if (distance > 150f) return Rand.Range(0.40f, 0.60f);
            return Rand.Range(0.50f, 0.70f);
        }

        private float CalculateCEShotRotation(Vector3 origin, IntVec3 target)
        {
            Vector3 dir = target.ToVector3() - origin;
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        private void FireVanillaShell(ScheduledShell s)
        {
            try
            {
                Thing projectileThing = ThingMaker.MakeThing(s.ShellDef);
                if (projectileThing == null)
                {
                    VanillaExplosion(s);
                    return;
                }

                GenPlace.TryPlaceThing(projectileThing, s.TargetCell, s.Map, ThingPlaceMode.Direct);

                Projectile proj = projectileThing as Projectile;
                if (proj != null)
                {
                    Vector3 origin = GetSkyOrigin(s.TargetCell, s.Map);
                    proj.Launch(null, origin, s.TargetCell, s.TargetCell, ProjectileHitFlags.IntendedTarget, false, null, null);
                }
                else
                {
                    VanillaExplosion(s);
                }
            }
            catch
            {
                VanillaExplosion(s);
            }
        }

        private Vector3 GetSkyOrigin(IntVec3 target, Map map)
        {
            float distance = map.Size.x + Rand.Range(20f, 50f);
            float offsetZ = Rand.Range(-20f, 20f);
            return new Vector3(target.x, 150f, target.z - distance + offsetZ);
        }

        private void VanillaExplosion(ScheduledShell s)
        {
            float radius = Rand.Range(3f, 5f);
            int dmg = Rand.Range(40, 80);
            DamageDef dd = DamageDefOf.Bomb;
            if (s.ShellDef?.defName != null)
            {
                if (s.ShellDef.defName.Contains("Incendiary")) dd = DamageDefOf.Flame;
                else if (s.ShellDef.defName.Contains("EMP")) dd = DamageDefOf.EMP;
            }

            try
            {
                ProjectileProperties proj = s.ShellDef?.projectile;
                if (proj != null)
                {
                    MethodInfo getDmgMethod = proj.GetType().GetMethod("GetDamageAmount", new Type[] { typeof(Thing) });
                    if (getDmgMethod != null)
                        dmg = (int)getDmgMethod.Invoke(proj, new object[] { null });
                }
            }
            catch { }

            try
            {
                DoExplosionViaReflection(s, radius, dmg, dd, null, false, 8f);
            }
            catch
            {
                DoManualDamage(s, radius, dmg, dd, 8f);
            }

            FleckMaker.Static(s.TargetCell.ToVector3(), s.Map, FleckDefOf.ExplosionFlash, 2f);
            FleckMaker.ThrowSmoke(s.TargetCell.ToVector3(), s.Map, 3f);
        }

        private void DoExplosionViaReflection(ScheduledShell s, float radius, int damage, DamageDef damageDef,
            SoundDef sound, bool applyToNeighbors, float armorPenetration)
        {
            try
            {
                MethodInfo[] methods = typeof(GenExplosion).GetMethods(BindingFlags.Public | BindingFlags.Static);
                List<MethodInfo> doExplosionMethods = new List<MethodInfo>();
                for (int i = 0; i < methods.Length; i++)
                {
                    if (methods[i].Name == "DoExplosion")
                        doExplosionMethods.Add(methods[i]);
                }

                MethodInfo targetMethod = null;
                object[] args = null;

                for (int m = 0; m < doExplosionMethods.Count; m++)
                {
                    MethodInfo method = doExplosionMethods[m];
                    ParameterInfo[] parms = method.GetParameters();

                    if (parms.Length < 7 || parms[0].ParameterType != typeof(IntVec3))
                        continue;

                    args = new object[parms.Length];

                    for (int i = 0; i < parms.Length; i++)
                    {
                        ParameterInfo p = parms[i];
                        string nameLower = p.Name.ToLower();
                        switch (nameLower)
                        {
                            case "center": args[i] = s.TargetCell; break;
                            case "map": args[i] = s.Map; break;
                            case "radius": args[i] = radius; break;
                            case "damtype": args[i] = damageDef; break;
                            case "instigator": args[i] = null; break;
                            case "damamount": args[i] = damage; break;
                            case "armorpenetration": args[i] = armorPenetration; break;
                            case "explosionsound": args[i] = sound; break;
                            case "weapon": args[i] = s.ShellDef; break;
                            case "projectile": args[i] = null; break;
                            case "intendedtarget": args[i] = null; break;
                            case "postexplosionspawnthingdef": args[i] = null; break;
                            case "postexplosionspawnchance": args[i] = 0f; break;
                            case "postexplosionspawnthingcount": args[i] = 1; break;
                            case "postexplosiongastype": args[i] = null; break;
                            case "applydamagetoexplosioncellsneighbors": args[i] = applyToNeighbors; break;
                            case "preexplosionspawnthingdef": args[i] = null; break;
                            case "preexplosionspawnchance": args[i] = 0f; break;
                            case "preexplosionspawnthingcount": args[i] = 0; break;
                            case "explosionchancetostartfire": args[i] = 0f; break;
                            case "explosiondamagefalloff": args[i] = false; break;
                            case "direction": args[i] = null; break;
                            case "ignoredthings": args[i] = null; break;
                            case "affectedpawns": args[i] = null; break;
                            case "dovisualeffects": args[i] = true; break;
                            case "propagationspeed": args[i] = 1f; break;
                            case "excluderadius": args[i] = 0f; break;
                            case "dosoundeffects": args[i] = true; break;
                            case "postexplosionspawnthingdefwater": args[i] = null; break;
                            case "screenshakefactor": args[i] = radius * 0.15f; break;
                            default:
                                args[i] = p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
                                break;
                        }
                    }

                    targetMethod = method;
                    break;
                }

                if (targetMethod != null && args != null)
                {
                    targetMethod.Invoke(null, args);
                }
                else
                {
                    throw new InvalidOperationException("No suitable DoExplosion found");
                }

                FleckMaker.Static(s.TargetCell.ToVector3(), s.Map, FleckDefOf.ExplosionFlash, radius * 0.5f);
                FleckMaker.ThrowSmoke(s.TargetCell.ToVector3(), s.Map, radius * 0.6f);

                if (Find.CurrentMap == s.Map)
                {
                    float dist = (s.TargetCell.ToVector3() - Find.CameraDriver.MapPosition.ToVector3()).magnitude;
                    if (dist < 50f)
                        Find.CameraDriver.shaker.DoShake(Mathf.Min(radius * 0.15f, 1.5f) * (1f - dist / 50f));
                }

                SpawnFragments(s);
            }
            catch (Exception ex)
            {
                Log.Warning("[AS] Explosion reflection error: " + ex.Message);
                DoManualDamage(s, radius, damage, damageDef, armorPenetration);
            }
        }

        private void DoManualDamage(ScheduledShell s, float radius, int damage, DamageDef damageDef, float armorPenetration)
        {
            try
            {
                foreach (Thing thing in GenRadial.RadialDistinctThingsAround(s.TargetCell, s.Map, radius, true))
                {
                    if (thing == null) continue;
                    try { thing.TakeDamage(new DamageInfo(damageDef, damage, armorPenetration, -1f)); }
                    catch { }
                }

                FleckMaker.Static(s.TargetCell.ToVector3(), s.Map, FleckDefOf.ExplosionFlash, radius);
                FleckMaker.ThrowSmoke(s.TargetCell.ToVector3(), s.Map, radius);

                if (Find.CurrentMap == s.Map)
                    Find.CameraDriver.shaker.DoShake(0.5f);
            }
            catch (Exception ex)
            {
                Log.Warning("[AS] Manual damage error: " + ex.Message);
            }
        }

        private void SpawnFragments(ScheduledShell s)
        {
            try
            {
                foreach (var comp in s.ShellDef.comps)
                {
                    Type compType = comp.GetType();
                    if (compType.Name.Contains("Fragments") || compType.Name.Contains("Fragment"))
                    {
                        MethodInfo throwMethod = compType.GetMethod("Throw", BindingFlags.Public | BindingFlags.Instance);
                        if (throwMethod != null)
                        {
                            throwMethod.Invoke(comp, new object[] { s.TargetCell.ToVector3(), s.Map, null });
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[AS] Fragments error: " + ex.Message);
            }
        }

        private void LaunchRaid(ScheduledRaid r)
        {
            if (r.Parms == null || r.Parms.faction == null)
            {
                Log.Warning("[AS] LaunchRaid: invalid parms");
                return;
            }

            Map map = r.Parms.target as Map;
            if (map == null)
            {
                Log.Warning("[AS] LaunchRaid: target is not a Map");
                return;
            }

            try
            {
                IncidentWorker worker = IncidentDefOf.RaidEnemy.Worker;
                IncidentWorker_RaidEnemy raidWorker = worker as IncidentWorker_RaidEnemy;
                if (raidWorker == null)
                {
                    Log.Warning("[AS] LaunchRaid: could not get RaidEnemy worker");
                    return;
                }

                IncidentParms freshParms = new IncidentParms
                {
                    target = map,
                    faction = r.Parms.faction,
                    points = r.Parms.points,
                    forced = true,
                    raidArrivalMode = r.Parms.raidArrivalMode ?? PawnsArrivalModeDefOf.EdgeWalkIn,
                    raidStrategy = r.Parms.raidStrategy ?? RaidStrategyDefOf.ImmediateAttack
                };

                if (raidWorker.TryExecute(freshParms))
                {
                    Log.Message("[AS] Raid launched: " + freshParms.faction.Name + ", points=" + freshParms.points.ToString("F0"));
                }
                else
                {
                    Log.Warning("[AS] Raid TryExecute returned false for " + freshParms.faction.Name);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[AS] LaunchRaid exception: " + ex.Message);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref shells, "shells", LookMode.Deep);
            Scribe_Collections.Look(ref raids, "raids", LookMode.Deep);
            Scribe_Collections.Look(ref delayed, "delayed", LookMode.Deep);
            Scribe_Collections.Look(ref repeating, "repeating", LookMode.Deep);
        }

        private static bool CE_Loaded => ModsConfig.IsActive("CETeam.CombatExtended") || ModsConfig.IsActive("CombatExtended.Continued");

        private static Type _projCEType;
        private static Type ProjectileCE_Type
        {
            get
            {
                if (_projCEType == null && CE_Loaded)
                    _projCEType = Type.GetType("CombatExtended.ProjectileCE, CombatExtended", false);
                return _projCEType;
            }
        }

        private static void CacheCEFields()
        {
            if (_ceFieldsCached) return;
            _ceFieldsCached = true;

            if (!CE_Loaded) return;

            Type projType = ProjectileCE_Type;
            if (projType == null) return;

            _projectilePropertiesCEType = Type.GetType("CombatExtended.ProjectilePropertiesCE, CombatExtended", false);

            MethodInfo[] methods = projType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (MethodInfo m in methods)
            {
                if (m.Name != "Launch") continue;
                ParameterInfo[] p = m.GetParameters();
                if (p.Length == 8 &&
                    p[0].ParameterType == typeof(Thing) &&
                    p[1].ParameterType == typeof(Vector2) &&
                    p[2].ParameterType == typeof(float) &&
                    p[3].ParameterType == typeof(float) &&
                    p[4].ParameterType == typeof(float) &&
                    p[5].ParameterType == typeof(float) &&
                    p[6].ParameterType == typeof(Thing) &&
                    p[7].ParameterType == typeof(float))
                {
                    _ceLaunchMethod = m;
                    break;
                }
            }

            BindingFlags allFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            _ceDestinationField = projType.GetField("Destination", allFlags);
        }
    }

    public class ScheduledShell : IExposable
    {
        public Map Map; public Faction Faction; public IntVec3 TargetCell; public ThingDef ShellDef; public int TriggerTick;
        public void ExposeData()
        {
            Scribe_References.Look(ref Map, "map"); Scribe_References.Look(ref Faction, "faction");
            Scribe_Values.Look(ref TargetCell, "targetCell"); Scribe_Defs.Look(ref ShellDef, "shellDef");
            Scribe_Values.Look(ref TriggerTick, "triggerTick", 0);
        }
    }

    public class ScheduledRaid : IExposable
    {
        public IncidentParms Parms; public int TriggerTick;
        public void ExposeData() { Scribe_Deep.Look(ref Parms, "parms"); Scribe_Values.Look(ref TriggerTick, "triggerTick", 0); }
    }

    public class DelayedAction : IExposable
    {
        [Unsaved(false)] public Action Action; public int TriggerTick;
        public void ExposeData() { Scribe_Values.Look(ref TriggerTick, "triggerTick", 0); }
    }

    public class RepeatingAction : IExposable
    {
        [Unsaved(false)] public Action Action; public int NextTick, Interval, RemainingRepetitions;
        public void ExposeData()
        {
            Scribe_Values.Look(ref NextTick, "nextTick", 0); Scribe_Values.Look(ref Interval, "interval", 60);
            Scribe_Values.Look(ref RemainingRepetitions, "remainingRepetitions", 0);
        }
    }
}
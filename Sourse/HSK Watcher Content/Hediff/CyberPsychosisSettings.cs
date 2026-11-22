//using HarmonyLib;
//using LudeonTK;
//using RimWorld;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Reflection;
//using UnityEngine;
//using Verse;
//using Verse.AI;

//namespace HSK.Watcher.Content
//{
//    // ==================== SETTINGS ====================
//    public class CyberPsychosisModSettings : ModSettings
//    {
//        public bool cyberPsychosisEnabled = true;
//        public float thresholdMultiplier = 1.0f;
//        public int baseThreshold = 120;
//        public float psychosisGainRate = 0.5f;
//        public int checkIntervalTicks = 2500;
//        public bool debugMode = false;

//        public override void ExposeData()
//        {
//            Scribe_Values.Look(ref cyberPsychosisEnabled, "cyberPsychosisEnabled", true);
//            Scribe_Values.Look(ref thresholdMultiplier, "thresholdMultiplier", 1.0f);
//            Scribe_Values.Look(ref baseThreshold, "baseThreshold", 120);
//            Scribe_Values.Look(ref psychosisGainRate, "psychosisGainRate", 0.5f);
//            Scribe_Values.Look(ref checkIntervalTicks, "checkIntervalTicks", 2500);
//            Scribe_Values.Look(ref debugMode, "debugMode", false);
//            base.ExposeData();
//        }
//    }

//    // ==================== MOD ====================
//    public class CyberPsychosisMod : Mod
//    {
//        public static CyberPsychosisModSettings settings;
//        public static CyberPsychosisMod Instance;
//        public static bool DebugMode => settings?.debugMode ?? false;

//        public CyberPsychosisMod(ModContentPack content) : base(content)
//        {
//            Instance = this;
//            settings = GetSettings<CyberPsychosisModSettings>();
//            LoadImplantPoints(content.RootDir);
//            Log.Message("[CyberPsychosis] Mod initialized. Enabled: " + settings.cyberPsychosisEnabled);
//        }

//        private void LoadImplantPoints(string rootDir)
//        {
//            string filePath = Path.Combine(rootDir, "ImplantPoints_CyberPsychosis.xml");

//            if (!File.Exists(filePath))
//            {
//                Log.Warning("[CyberPsychosis] Implant points file not found: " + filePath);
//                return;
//            }

//            try
//            {
//                string[] lines = File.ReadAllLines(filePath);
//                Need_CyberPsychosis.ImplantPoints.Clear();

//                foreach (string rawLine in lines)
//                {
//                    string line = rawLine.Trim();
//                    if (string.IsNullOrEmpty(line))
//                        continue;
//                    if (line.StartsWith("<?") || line.StartsWith("<!") || line.Contains("<!--") || line.Contains("-->"))
//                        continue;
//                    if (line.StartsWith("<Implants") || line.StartsWith("</Implants"))
//                        continue;

//                    int ltOpen = line.IndexOf('<');
//                    if (ltOpen < 0) continue;
//                    int gtOpen = line.IndexOf('>', ltOpen);
//                    if (gtOpen < 0) continue;

//                    int ltClose = line.LastIndexOf('<');
//                    if (ltClose <= gtOpen) continue;

//                    string tagName = line.Substring(ltOpen + 1, gtOpen - ltOpen - 1).Trim();
//                    int spaceIdx = tagName.IndexOf(' ');
//                    if (spaceIdx > 0)
//                        tagName = tagName.Substring(0, spaceIdx);

//                    string valueStr = line.Substring(gtOpen + 1, ltClose - gtOpen - 1).Trim();

//                    if (float.TryParse(valueStr,
//                        System.Globalization.NumberStyles.Float,
//                        System.Globalization.CultureInfo.InvariantCulture,
//                        out float points))
//                    {
//                        Need_CyberPsychosis.ImplantPoints[tagName] = points;
//                    }
//                    else
//                    {
//                        Log.Warning("[CyberPsychosis] Cannot parse points for '" + tagName + "': '" + valueStr + "'");
//                    }
//                }

//                Log.Message("[CyberPsychosis] Loaded " + Need_CyberPsychosis.ImplantPoints.Count + " implant definitions from XML.");
//            }
//            catch (Exception ex)
//            {
//                Log.Error("[CyberPsychosis] Failed to load implant points: " + ex);
//            }
//        }

//        public override void DoSettingsWindowContents(Rect inRect)
//        {
//            Listing_Standard listingStandard = new Listing_Standard();
//            listingStandard.Begin(inRect);

//            listingStandard.CheckboxLabeled("Enable_Cyber_Psychosis_System".Translate(), ref settings.cyberPsychosisEnabled,
//                "Enable_Cyber_Psychosis_System_Tip".Translate());

//            listingStandard.CheckboxLabeled("Debug_Mode".Translate(), ref settings.debugMode,
//                "Debug_Mode_Tip".Translate());

//            if (settings.cyberPsychosisEnabled)
//            {
//                listingStandard.Label("Base_Threshold".Translate() + ": " + settings.baseThreshold);
//                settings.baseThreshold = (int)listingStandard.Slider(settings.baseThreshold, 50, 500);

//                listingStandard.Label("Threshold_Multiplier".Translate() + ": " + settings.thresholdMultiplier.ToString("F2"));
//                settings.thresholdMultiplier = listingStandard.Slider(settings.thresholdMultiplier, 0.5f, 2.0f);

//                listingStandard.Label("Psychosis_Gain_Rate".Translate() + ": " + settings.psychosisGainRate.ToString("F2"));
//                settings.psychosisGainRate = listingStandard.Slider(settings.psychosisGainRate, 0.1f, 2.0f);

//                listingStandard.Label("Check_Interval_(ticks)".Translate() + ": " + settings.checkIntervalTicks);
//                settings.checkIntervalTicks = (int)listingStandard.Slider(settings.checkIntervalTicks, 60, 10000);
//            }

//            listingStandard.End();
//            base.DoSettingsWindowContents(inRect);
//        }

//        public override string SettingsCategory()
//        {
//            return "Cyber_Psychosis".Translate();
//        }
//    }

//    // ==================== LOGGER ====================
//    public static class CyberPsychosisLogger
//    {
//        public static void Log(string message)
//        {
//            if (CyberPsychosisMod.DebugMode)
//                Verse.Log.Message("[CyberPsychosis] " + message);
//        }

//        public static void LogWarning(string message)
//        {
//            Verse.Log.Warning("[CyberPsychosis] " + message);
//        }

//        public static void LogError(string message)
//        {
//            Verse.Log.Error("[CyberPsychosis] " + message);
//        }
//    }

//    // ==================== HEDIFF COMPS ====================
//    public class HediffCompProperties_CyberPsychosisBlocker : HediffCompProperties
//    {
//        public float durationHours = 24f;

//        public HediffCompProperties_CyberPsychosisBlocker()
//        {
//            compClass = typeof(HediffComp_CyberPsychosisBlocker);
//        }
//    }

//    public class HediffComp_CyberPsychosisBlocker : HediffComp
//    {
//        public HediffCompProperties_CyberPsychosisBlocker Props => (HediffCompProperties_CyberPsychosisBlocker)props;

//        private int ticksRemaining = 0;

//        public override void CompPostMake()
//        {
//            base.CompPostMake();
//            ticksRemaining = (int)(Props.durationHours * 2500f);
//        }

//        public override void CompPostTick(ref float severityAdjustment)
//        {
//            base.CompPostTick(ref severityAdjustment);

//            if (ticksRemaining > 0)
//            {
//                ticksRemaining--;
//                parent.Severity = (float)ticksRemaining / (Props.durationHours * 2500f);
//            }
//        }

//        public override string CompTipStringExtra
//        {
//            get
//            {
//                float hoursRemaining = ticksRemaining / 2500f;
//                float daysRemaining = hoursRemaining / 24f;

//                if (daysRemaining >= 1f)
//                    return "\n\n" + "Time_remaining".Translate() + ": " + daysRemaining.ToString("F1") + " " + "days".Translate();
//                else
//                    return "\n\n" + "Time_remaining".Translate() + ": " + hoursRemaining.ToString("F0") + " " + "hours".Translate();
//            }
//        }

//        public override void CompExposeData()
//        {
//            base.CompExposeData();
//            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 0);
//        }
//    }

//    public class HediffCompProperties_PermanentCyberBlocker : HediffCompProperties
//    {
//        public float humanityRestorePerDay = 0.1f;

//        public HediffCompProperties_PermanentCyberBlocker()
//        {
//            compClass = typeof(HediffComp_PermanentCyberBlocker);
//        }
//    }

//    public class HediffComp_PermanentCyberBlocker : HediffComp
//    {
//        public HediffCompProperties_PermanentCyberBlocker Props => (HediffCompProperties_PermanentCyberBlocker)props;

//        private int ticksSinceLastRestore = 0;

//        public override void CompPostTick(ref float severityAdjustment)
//        {
//            base.CompPostTick(ref severityAdjustment);

//            ticksSinceLastRestore++;
//            if (ticksSinceLastRestore >= 2500)
//            {
//                ticksSinceLastRestore = 0;
//                RestoreHumanity();
//            }
//        }

//        private void RestoreHumanity()
//        {
//            Need_CyberPsychosis need = parent.pawn.needs?.TryGetNeed<Need_CyberPsychosis>();
//            if (need != null && need.CurLevel < 1.0f)
//            {
//                float restoreAmount = Props.humanityRestorePerDay / 24f;
//                need.CurLevel = Mathf.Min(1.0f, need.CurLevel + restoreAmount);

//                if (CyberPsychosisMod.DebugMode)
//                {
//                    CyberPsychosisLogger.Log(string.Format("Permanent blocker restored {0:F2}% humanity to {1}",
//                        restoreAmount * 100, parent.pawn.Name));
//                }
//            }
//        }

//        public override string CompTipStringExtra
//        {
//            get
//            {
//                return "\n\n<color=green>" + "Permanently_prevents_cyber_psychosis".Translate() + "</color>\n" + "Slowly_restores_humanity".Translate();
//            }
//        }

//        public override void CompExposeData()
//        {
//            base.CompExposeData();
//            Scribe_Values.Look(ref ticksSinceLastRestore, "ticksSinceLastRestore", 0);
//        }
//    }

//    // ==================== HEDIFFS ====================
//    public class Hediff_CyberPsychosisBlocker : HediffWithComps
//    {
//        public override bool ShouldRemove
//        {
//            get
//            {
//                if (!CyberPsychosisMod.settings.cyberPsychosisEnabled)
//                    return true;
//                return Severity <= 0.001f;
//            }
//        }
//    }

//    public class Hediff_PermanentCyberPsychosisBlocker : HediffWithComps
//    {
//        public override bool ShouldRemove => false;
//    }

//    // ==================== УНИВЕРСАЛЬНЫЙ МЕНЕДЖЕР (ДЛЯ ВСЕХ ПЕШЕК) ====================
//    public class Hediff_CyberPsychosisManager : HediffWithComps
//    {
//        private float cachedTotalPoints = 0f;
//        private int lastCheckTick = 0;
//        private float humanity = 1.0f;

//        public float Humanity => humanity;
//        public float TotalPoints => cachedTotalPoints;

//        public override void Tick()
//        {
//            base.Tick();

//            if (pawn == null || pawn.Dead || !pawn.RaceProps.Humanlike) return;

//            if (Find.TickManager.TicksGame - lastCheckTick > 2500)
//            {
//                lastCheckTick = Find.TickManager.TicksGame;
//                RecalculateAndProcess();
//            }
//        }

//        private void RecalculateAndProcess()
//        {
//            float total = 0f;
//            if (pawn.health?.hediffSet?.hediffs != null)
//            {
//                foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
//                {
//                    if (hediff?.def?.defName != null && Need_CyberPsychosis.ImplantPoints.TryGetValue(hediff.def.defName, out float points))
//                    {
//                        total += points;
//                    }
//                }
//            }

//            // Учитываем шлем Макс-Так (-200 очков)
//            if (pawn.apparel?.WornApparel != null)
//            {
//                foreach (Apparel app in pawn.apparel.WornApparel)
//                {
//                    if (app?.def?.defName == "Helmet_MaxTac")
//                    {
//                        total -= 200f;
//                    }
//                }
//            }

//            cachedTotalPoints = Mathf.Max(0f, Mathf.Round(total));

//            float baseVal = CyberPsychosisMod.settings?.baseThreshold ?? 120f;
//            float mult = CyberPsychosisMod.settings?.thresholdMultiplier ?? 1.0f;
//            float threshold = baseVal * mult;

//            if (pawn.story?.traits != null)
//            {
//                if (pawn.story.traits.HasTrait(DefDatabase<TraitDef>.GetNamed("BodyPurist", false)))
//                    threshold = 20f;
//                else if (pawn.story.traits.HasTrait(DefDatabase<TraitDef>.GetNamed("BodyModder", false)))
//                    threshold += 200f;
//            }

//            bool hasPermanentBlocker = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("PermanentCyberPsychosisBlocker")) != null;
//            bool hasTempBlocker = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("CyberPsychosisBlocker")) != null;

//            if (hasPermanentBlocker || hasTempBlocker)
//            {
//                humanity = 1.0f;
//                Severity = 0f;
//                RemoveCombatBuff();
//                return;
//            }

//            if (cachedTotalPoints > threshold)
//            {
//                float overflow = cachedTotalPoints / threshold;
//                float severityFactor = 1.0f + humanity;
//                float decay = 0.0049f * (CyberPsychosisMod.settings?.psychosisGainRate ?? 0.5f) * overflow * severityFactor;

//                humanity = Mathf.Max(0f, humanity - decay);
//            }
//            else
//            {
//                humanity = Mathf.Min(1.0f, humanity + 0.01f);
//            }

//            Severity = 1f - humanity;
//            UpdateCyberPsychosisBuff(threshold);

//            // Срыв только на крайней стадии (человечность <= 10% / Severity >= 0.9)
//            if (humanity <= 0.1f || Severity >= 0.9f)
//            {
//                TriggerCyberPsychosisBreak();
//            }
//        }

//        private void UpdateCyberPsychosisBuff(float threshold)
//        {
//            HediffDef buffDef = HediffDef.Named("CyberPsychosisCombatBuff");
//            if (buffDef == null) return;

//            bool isPsychopath = pawn.story?.traits != null && pawn.story.traits.HasTrait(TraitDefOf.Psychopath);

//            if (cachedTotalPoints > threshold || humanity < 0.5f)
//            {
//                Hediff existingBuff = pawn.health.hediffSet.GetFirstHediffOfDef(buffDef);
//                if (existingBuff == null)
//                {
//                    existingBuff = HediffMaker.MakeHediff(buffDef, pawn);
//                    pawn.health.AddHediff(existingBuff);
//                }

//                float severityMultiplier = isPsychopath ? 1.5f : 1.0f;
//                existingBuff.Severity = Mathf.Clamp01((1f - humanity) * severityMultiplier);
//            }
//            else
//            {
//                RemoveCombatBuff();
//            }
//        }

//        private void RemoveCombatBuff()
//        {
//            HediffDef buffDef = HediffDef.Named("CyberPsychosisCombatBuff");
//            if (buffDef == null) return;
//            Hediff existingBuff = pawn.health.hediffSet.GetFirstHediffOfDef(buffDef);
//            if (existingBuff != null)
//            {
//                pawn.health.RemoveHediff(existingBuff);
//            }
//        }

//        private void TriggerCyberPsychosisBreak()
//        {
//            if (pawn.InMentalState || pawn.Downed || pawn.Dead) return;

//            MentalStateDef stateDef = DefDatabase<MentalStateDef>.GetNamed("Berserk", false);

//            if (stateDef != null && pawn.mindState?.mentalStateHandler != null)
//            {
//                pawn.mindState.mentalStateHandler.TryStartMentalState(stateDef, "Cyber psychosis berserk", true);

//                HediffDef buffDef = HediffDef.Named("CyberPsychosisCombatBuff");
//                if (buffDef != null)
//                {
//                    Hediff existingBuff = pawn.health.hediffSet.GetFirstHediffOfDef(buffDef);
//                    if (existingBuff == null)
//                    {
//                        existingBuff = HediffMaker.MakeHediff(buffDef, pawn);
//                        pawn.health.AddHediff(existingBuff);
//                    }

//                    bool isPsychopath = pawn.story?.traits != null && pawn.story.traits.HasTrait(TraitDefOf.Psychopath);
//                    existingBuff.Severity = isPsychopath ? 1.0f : 0.8f;
//                }

//                if (pawn.Faction != Faction.OfPlayer)
//                {
//                    CyberPsychosisLogger.Log(string.Format("Raid/Enemy {0} went berserk due to extreme cyber psychosis!", pawn.Name));
//                }
//                else
//                {
//                    Messages.Message("CyberPsychosis_MentalBreak".Translate(pawn.Name), pawn, MessageTypeDefOf.NegativeEvent);
//                }
//            }
//        }

//        public override void ExposeData()
//        {
//            base.ExposeData();
//            Scribe_Values.Look(ref cachedTotalPoints, "cachedTotalPoints", 0f);
//            Scribe_Values.Look(ref humanity, "humanity", 1.0f);
//            Scribe_Values.Look(ref lastCheckTick, "lastCheckTick", 0);
//        }
//    }

//    // ==================== БОЕВОЙ БАФФ С ИЗМЕНЕНИЕМ ГЛАЗ ====================
//    public class Hediff_CyberPsychosisBuff : HediffWithComps
//    {
//        public override void PostAdd(DamageInfo? dinfo)
//        {
//            base.PostAdd(dinfo);

//            if (pawn != null && Current.ProgramState == ProgramState.Playing)
//            {
//                pawn.Drawer?.renderer?.SetAllGraphicsDirty();
//                PortraitsCache.SetDirty(pawn);
//            }

//            if (pawn?.story?.traits != null && pawn.story.traits.HasTrait(TraitDefOf.Psychopath))
//            {
//                CyberPsychosisLogger.Log(string.Format("{0} is a Psychopath. Cyberpsychosis enhancement activated!", pawn.Name));
//            }
//        }

//        public override void PostRemoved()
//        {
//            base.PostRemoved();

//            if (pawn != null && Current.ProgramState == ProgramState.Playing)
//            {
//                pawn.Drawer?.renderer?.SetAllGraphicsDirty();
//                PortraitsCache.SetDirty(pawn);
//            }
//        }
//    }

//    // ==================== ДИКИЙ ИСКИН ====================
  

//    // ==================== NEED (ДЛЯ КОЛОНИСТОВ) ====================
//    public class Need_CyberPsychosis : Need
//    {
//        private const float MAX_HUMANITY = 1.0f;
//        private const float BASE_DECAY = 0.0049f;
//        private const int LOW_HUMANITY_THRESHOLD_TICKS = 3600000;

//        public static Dictionary<string, float> ImplantPoints = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

//        private static TraitDef TraitImmune;
//        private static TraitDef TraitHollowed;
//        private static TraitDef TraitBodyPurist;
//        private static TraitDef TraitBodyModder;

//        private float cachedTotalPoints = 0f;
//        private int lastRecalculateTick = 0;
//        private bool defsResolved = false;
//        private int killDesireTicks = 0;
//        private bool forceRecalculate = false;

//        private int lowHumanityTicks = 0;
//        private bool hollowedTriggered = false;

//        private float humanityLevel = 1.0f;
//        private bool humanityInitialized = false;

//        private List<string> lastFoundImplants = new List<string>();

//        public new float CurLevel
//        {
//            get { return humanityLevel; }
//            set
//            {
//                humanityLevel = Mathf.Clamp01(value);
//                base.CurLevel = humanityLevel;
//            }
//        }

//        public float PsychosisSeverity => 1f - humanityLevel;
//        public float HumanityPercent => humanityLevel * 100f;
//        public float TotalAugmentationPoints => cachedTotalPoints;

//        public bool HasIronWill
//        {
//            get
//            {
//                if (pawn?.story?.traits == null) return false;
//                if (TraitImmune == null) TraitImmune = DefDatabase<TraitDef>.GetNamed("CyberPsychosisImmune", false);
//                return TraitImmune != null && pawn.story.traits.HasTrait(TraitImmune);
//            }
//        }

//        public bool IsHollowed
//        {
//            get
//            {
//                if (pawn?.story?.traits == null) return false;
//                if (TraitHollowed == null) TraitHollowed = DefDatabase<TraitDef>.GetNamed("Hollowed", false);
//                return TraitHollowed != null && pawn.story.traits.HasTrait(TraitHollowed);
//            }
//        }

//        public float CurrentThreshold
//        {
//            get
//            {
//                float baseVal = CyberPsychosisMod.settings?.baseThreshold ?? 120f;
//                float mult = CyberPsychosisMod.settings?.thresholdMultiplier ?? 1.0f;
//                float threshold = baseVal * mult;

//                if (pawn?.story?.traits != null)
//                {
//                    if (TraitBodyPurist == null) TraitBodyPurist = DefDatabase<TraitDef>.GetNamed("BodyPurist", false);
//                    if (TraitBodyPurist != null && pawn.story.traits.HasTrait(TraitBodyPurist))
//                        return 20f;

//                    if (TraitBodyModder == null) TraitBodyModder = DefDatabase<TraitDef>.GetNamed("BodyModder", false);
//                    if (TraitBodyModder != null && pawn.story.traits.HasTrait(TraitBodyModder))
//                        threshold += 200f;
//                }

//                return threshold;
//            }
//        }

//        public float PointsUntilPsychosis => Mathf.Max(0f, CurrentThreshold - cachedTotalPoints);
//        public bool IsOverThreshold => cachedTotalPoints > CurrentThreshold;
//        public bool HasKillDesire => PsychosisSeverity >= 0.9f && killDesireTicks > 0;

//        public bool HasPermanentBlocker => pawn.health?.hediffSet?.GetFirstHediffOfDef(
//            HediffDef.Named("PermanentCyberPsychosisBlocker")) != null;

//        public float EstimatedDaysToEmpty
//        {
//            get
//            {
//                if (!IsOverThreshold || humanityLevel <= 0.01f || HasPermanentBlocker) return 0f;

//                float gainRate = CyberPsychosisMod.settings?.psychosisGainRate ?? 0.5f;
//                float overflowMultiplier = Mathf.Max(cachedTotalPoints / CurrentThreshold, 1.0f);
//                float decreasePerCheck = BASE_DECAY * gainRate * overflowMultiplier;

//                int checkInterval = CyberPsychosisMod.settings?.checkIntervalTicks ?? 2500;
//                float checksNeeded = humanityLevel / decreasePerCheck;
//                float ticksNeeded = checksNeeded * checkInterval;

//                return ticksNeeded / 60000f;
//            }
//        }

//        public Need_CyberPsychosis(Pawn pawn) : base(pawn)
//        {
//            threshPercents = new List<float> { 0.25f, 0.5f, 0.75f };

//            humanityLevel = MAX_HUMANITY;
//            base.CurLevel = MAX_HUMANITY;
//            humanityInitialized = true;
//        }

//        public override void SetInitialLevel()
//        {
//            humanityLevel = MAX_HUMANITY;
//            base.CurLevel = MAX_HUMANITY;
//            humanityInitialized = true;
//        }

//        public void ForceRecalculate()
//        {
//            forceRecalculate = true;
//        }

//        public override void NeedInterval()
//        {
//            if (!humanityInitialized || humanityLevel > 1.01f || humanityLevel < 0f)
//            {
//                humanityLevel = MAX_HUMANITY;
//                base.CurLevel = MAX_HUMANITY;
//                humanityInitialized = true;
//            }

//            if (!CyberPsychosisMod.settings.cyberPsychosisEnabled)
//                return;

//            if (HasIronWill)
//            {
//                if (humanityLevel < MAX_HUMANITY)
//                {
//                    humanityLevel = MAX_HUMANITY;
//                    base.CurLevel = MAX_HUMANITY;
//                }
//                killDesireTicks = 0;
//                lowHumanityTicks = 0;
//                return;
//            }

//            if (!defsResolved)
//            {
//                if (DefDatabase<PawnCapacityDef>.DefCount == 0)
//                    return;
//                defsResolved = true;
//            }

//            if (killDesireTicks > 0)
//                killDesireTicks--;

//            int currentTick = Find.TickManager.TicksGame;
//            int intervalTicks = CyberPsychosisMod.settings?.checkIntervalTicks ?? 2500;

//            if (lastRecalculateTick == 0)
//                lastRecalculateTick = currentTick;

//            if (!IsHollowed && !HasPermanentBlocker)
//            {
//                if (humanityLevel < 0.25f)
//                {
//                    lowHumanityTicks += intervalTicks;
//                    if (lowHumanityTicks >= LOW_HUMANITY_THRESHOLD_TICKS)
//                    {
//                        TryBecomeHollowed();
//                    }
//                }
//                else
//                {
//                    lowHumanityTicks = Mathf.Max(0, lowHumanityTicks - intervalTicks / 2);
//                }
//            }

//            if ((currentTick - lastRecalculateTick >= intervalTicks) || forceRecalculate)
//            {
//                lastRecalculateTick = currentTick;
//                forceRecalculate = false;
//                RecalculatePoints();
//                UpdatePsychosis();
//            }
//        }

//        private void RecalculatePoints()
//        {
//            if (pawn == null || pawn.Dead) return;

//            lastFoundImplants.Clear();
//            float total = 0f;

//            if (pawn.health?.hediffSet?.hediffs != null)
//            {
//                foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
//                {
//                    if (hediff?.def?.defName == null) continue;

//                    if (ImplantPoints.TryGetValue(hediff.def.defName, out float points))
//                    {
//                        total += points;
//                        lastFoundImplants.Add(string.Format("{0} ({1})", hediff.def.label ?? hediff.def.defName, points));
//                    }
//                }
//            }

//            // Учитываем шлем Макс-Так (-200 очков)
//            if (pawn.apparel?.WornApparel != null)
//            {
//                foreach (Apparel app in pawn.apparel.WornApparel)
//                {
//                    if (app?.def?.defName == "Helmet_MaxTac")
//                    {
//                        total -= 200f;
//                        lastFoundImplants.Add(string.Format("{0} (-200)", app.def.label ?? "MaxTac Helmet"));
//                    }
//                }
//            }

//            cachedTotalPoints = Mathf.Max(0f, Mathf.Round(total));
//        }

//        private void UpdatePsychosis()
//        {
//            if (HasPermanentBlocker)
//            {
//                if (humanityLevel < MAX_HUMANITY)
//                {
//                    humanityLevel = Mathf.Min(MAX_HUMANITY, humanityLevel + 0.01f);
//                    base.CurLevel = humanityLevel;
//                }
//                killDesireTicks = 0;
//                return;
//            }

//            Hediff blocker = pawn.health?.hediffSet?.GetFirstHediffOfDef(
//                HediffDef.Named("CyberPsychosisBlocker"));

//            if (blocker != null)
//            {
//                if (humanityLevel < MAX_HUMANITY)
//                {
//                    humanityLevel = Mathf.Min(MAX_HUMANITY, humanityLevel + 0.05f);
//                    base.CurLevel = humanityLevel;
//                }
//                killDesireTicks = 0;
//                return;
//            }

//            if (!IsOverThreshold)
//            {
//                if (humanityLevel < MAX_HUMANITY)
//                {
//                    humanityLevel = Mathf.Min(MAX_HUMANITY, humanityLevel + 0.02f);
//                    base.CurLevel = humanityLevel;
//                }
//                killDesireTicks = 0;
//                return;
//            }

//            float gainRate = CyberPsychosisMod.settings?.psychosisGainRate ?? 0.5f;
//            float overflowMultiplier = Mathf.Max(cachedTotalPoints / CurrentThreshold, 1.0f);
//            float severityFactor = 1.0f + (1.0f - humanityLevel) * 2.0f;
//            float decreaseAmount = BASE_DECAY * gainRate * overflowMultiplier * severityFactor;

//            humanityLevel = Mathf.Max(0f, humanityLevel - decreaseAmount);
//            base.CurLevel = humanityLevel;

//            if (PsychosisSeverity >= 0.9f && killDesireTicks <= 0)
//            {
//                killDesireTicks = 2500;
//                Messages.Message("CyberPsychosis_KillDesire".Translate(pawn.Name), pawn, MessageTypeDefOf.NegativeEvent);
//            }

//            CheckForMentalBreak();
//        }

//        private void CheckForMentalBreak()
//        {
//            if (PsychosisSeverity < 0.9f) return;
//            TriggerMentalBreak();
//        }

//        private void TriggerMentalBreak()
//        {
//            if (pawn.InMentalState || pawn.Downed || pawn.Dead) return;

//            MentalStateDef stateDef = DefDatabase<MentalStateDef>.GetNamed("Berserk", false);

//            if (stateDef != null && pawn?.mindState?.mentalStateHandler != null)
//            {
//                pawn.mindState.mentalStateHandler.TryStartMentalState(stateDef, "Cyber psychosis berserk", true);
//                Messages.Message("CyberPsychosis_MentalBreak".Translate(pawn.Name), pawn, MessageTypeDefOf.NegativeEvent);
//            }
//        }

//        public void TryBecomeHollowed()
//        {
//            if (hollowedTriggered) return;
//            if (TraitHollowed == null) TraitHollowed = DefDatabase<TraitDef>.GetNamed("Hollowed", false);
//            if (TraitHollowed == null || HasIronWill) return;

//            if (TraitImmune == null) TraitImmune = DefDatabase<TraitDef>.GetNamed("CyberPsychosisImmune", false);
//            if (TraitImmune != null && pawn.story.traits.HasTrait(TraitImmune))
//            {
//                Trait existing = pawn.story.traits.allTraits.FirstOrDefault(t => t.def == TraitImmune);
//                if (existing != null) pawn.story.traits.RemoveTrait(existing);
//            }

//            pawn.story.traits.GainTrait(new Trait(TraitHollowed, 0));
//            hollowedTriggered = true;
//            lowHumanityTicks = 0;

//            AlterPersonality();
//            RegenerateSkills();

//            Messages.Message("CyberPsychosis_BecameHollowed".Translate(pawn.Name), pawn, MessageTypeDefOf.NegativeEvent);
//        }

//        private void AlterPersonality()
//        {
//            if (pawn.story?.traits?.allTraits == null) return;
//            var removable = pawn.story.traits.allTraits.Where(t => t.def != TraitHollowed).ToList();
//            int removeCount = Mathf.Min(2, removable.Count);
//            for (int i = 0; i < removeCount; i++)
//            {
//                Trait toRemove = removable.RandomElement();
//                if (toRemove != null)
//                {
//                    pawn.story.traits.RemoveTrait(toRemove);
//                    removable.Remove(toRemove);
//                }
//            }
//        }

//        private void RegenerateSkills()
//        {
//            if (pawn.skills?.skills == null) return;
//            foreach (SkillRecord skill in pawn.skills.skills)
//            {
//                skill.Level = Rand.Range(1, 5);
//                skill.passion = (Passion)Rand.Range(0, 3);
//                skill.xpSinceLastLevel = 0f;
//                skill.xpSinceMidnight = 0f;
//            }
//        }

//        public override string GetTipString()
//        {
//            string status = "";
//            string killWarning = "";
//            string timeEst = "";
//            string blockerInfo = "";
//            string implantList = "";
//            string immunityInfo = "";

//            if (HasIronWill)
//                immunityInfo = "\n<color=green>♦ " + "Iron_Will_Immune".Translate() + "</color>";
//            else if (IsHollowed)
//                immunityInfo = "\n<color=red>♦ " + "Hollowed_Lost_Self".Translate() + "</color>";

//            if (HasPermanentBlocker)
//            {
//                blockerInfo = "\n<color=green>♦ " + "Neural_Stabilizer_active".Translate() + "</color>";
//                status = "Protected".Translate();
//            }
//            else if (humanityLevel > 0.75f)
//                status = "Stable".Translate();
//            else if (humanityLevel > 0.5f)
//                status = "Warning".Translate();
//            else if (humanityLevel > 0.25f)
//                status = "Critical".Translate();
//            else
//            {
//                status = "DANGER".Translate();
//                if (HasKillDesire)
//                    killWarning = "\n<color=red>" + "KILL_DESIRE_ACTIVE".Translate() + "</color>";
//            }

//            if (IsOverThreshold && humanityLevel > 0.01f && !HasPermanentBlocker)
//            {
//                timeEst = "\n" + "Est_time_to_empty".Translate() + ": " + EstimatedDaysToEmpty.ToString("F1") + " " + "days".Translate();
//            }

//            string thresholdInfo = IsOverThreshold
//                ? "<color=red>" + "Over_THRESHOLD".Translate() + "!</color>\n" + "Points".Translate() + ": " + cachedTotalPoints.ToString("F0") + "/" + CurrentThreshold.ToString("F0")
//                : "Points".Translate() + ": " + cachedTotalPoints.ToString("F0") + "/" + CurrentThreshold.ToString("F0") + " (" + "safe".Translate() + ")";

//            if (lastFoundImplants.Count > 0)
//            {
//                implantList = "\n\n" + "Active_implants".Translate() + ":";
//                foreach (string s in lastFoundImplants)
//                    implantList += "\n  • " + s;
//            }

//            return string.Format("{0}: {1:P0} ({2:F0}% {3})\n{4}\n{5}: {6}{7}{8}{9}{10}{11}",
//                LabelCap,
//                humanityLevel,
//                humanityLevel * 100,
//                "Humanity".Translate().ToLower(),
//                thresholdInfo,
//                "Status".Translate(),
//                status,
//                killWarning,
//                blockerInfo,
//                immunityInfo,
//                timeEst,
//                implantList);
//        }

//        public override void ExposeData()
//        {
//            Scribe_Values.Look(ref humanityLevel, "humanityLevel", 1.0f);
//            Scribe_Values.Look(ref humanityInitialized, "humanityInitialized", false);
//            Scribe_Values.Look(ref lastRecalculateTick, "lastRecalculateTick", 0);
//            Scribe_Values.Look(ref lowHumanityTicks, "lowHumanityTicks", 0);
//            Scribe_Values.Look(ref hollowedTriggered, "hollowedTriggered", false);

//            if (Scribe.mode == LoadSaveMode.Saving)
//            {
//                base.CurLevel = humanityLevel;
//            }

//            base.ExposeData();

//            if (Scribe.mode == LoadSaveMode.PostLoadInit)
//            {
//                if (!humanityInitialized || humanityLevel < 0.01f)
//                {
//                    humanityLevel = MAX_HUMANITY;
//                    humanityInitialized = true;
//                }
//                base.CurLevel = humanityLevel;
//            }

//            Scribe_Values.Look(ref cachedTotalPoints, "cachedTotalPoints", 0f);
//            Scribe_Values.Look(ref defsResolved, "defsResolved", false);
//            Scribe_Values.Look(ref killDesireTicks, "killDesireTicks", 0);
//            Scribe_Values.Look(ref forceRecalculate, "forceRecalculate", false);
//        }
//    }

//    // ==================== THOUGHT WORKERS ====================
//    public class ThoughtWorker_CyberPsychosis : ThoughtWorker
//    {
//        protected override ThoughtState CurrentStateInternal(Pawn p)
//        {
//            if (!CyberPsychosisMod.settings.cyberPsychosisEnabled || p?.needs == null)
//                return ThoughtState.Inactive;

//            Need_CyberPsychosis need = p.needs.TryGetNeed<Need_CyberPsychosis>();
//            if (need == null) return ThoughtState.Inactive;

//            float severity = need.PsychosisSeverity;
//            if (severity <= 0.01f) return ThoughtState.Inactive;

//            int stage = severity >= 0.9f ? 3 : (severity >= 0.6f ? 2 : 1);
//            return ThoughtState.ActiveAtStage(stage);
//        }
//    }

//    public class ThoughtWorker_KillDesire : ThoughtWorker
//    {
//        protected override ThoughtState CurrentStateInternal(Pawn p)
//        {
//            if (!CyberPsychosisMod.settings.cyberPsychosisEnabled || p?.needs == null)
//                return ThoughtState.Inactive;

//            Need_CyberPsychosis need = p.needs.TryGetNeed<Need_CyberPsychosis>();
//            return (need != null && need.HasKillDesire) ? ThoughtState.ActiveDefault : ThoughtState.Inactive;
//        }
//    }

//    // ==================== COMP USE EFFECT ====================
//    public class CompUseEffect_CyberPsychosisBlocker : CompUseEffect
//    {
//        public override AcceptanceReport CanBeUsedBy(Pawn p)
//        {
//            if (!CyberPsychosisMod.settings.cyberPsychosisEnabled)
//                return "Cyber psychosis system is disabled in mod settings.";
//            return true;
//        }

//        public override void DoEffect(Pawn usedBy)
//        {
//            base.DoEffect(usedBy);
//            ApplyBlocker(usedBy);
//        }

//        private void ApplyBlocker(Pawn target)
//        {
//            Hediff existing = target.health?.hediffSet?.GetFirstHediffOfDef(
//                HediffDef.Named("CyberPsychosisBlocker"));
//            if (existing != null)
//                target.health.RemoveHediff(existing);

//            Hediff blocker = HediffMaker.MakeHediff(
//                HediffDef.Named("CyberPsychosisBlocker"), target);
//            blocker.Severity = 1f;
//            target.health.AddHediff(blocker);

//            Need_CyberPsychosis need = target.needs?.TryGetNeed<Need_CyberPsychosis>();
//            if (need != null)
//                need.CurLevel = 1f;

//            Messages.Message("CyberPsychosis_BlockerTaken".Translate(target.Name), target, MessageTypeDefOf.PositiveEvent);
//        }
//    }

//    // ==================== RECIPE ====================
//    public class Recipe_InstallPermanentCyberBlocker : Recipe_Surgery
//    {
//        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
//        {
//            if (!base.AvailableOnNow(thing, part)) return false;
//            Pawn pawn = thing as Pawn;
//            if (pawn == null) return false;

//            Hediff existing = pawn.health?.hediffSet?.GetFirstHediffOfDef(
//                HediffDef.Named("PermanentCyberPsychosisBlocker"));
//            return existing == null;
//        }

//        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
//        {
//            base.ApplyOnPawn(pawn, part, billDoer, ingredients, bill);

//            if (pawn.health != null)
//            {
//                Hediff tempBlocker = pawn.health.hediffSet?.GetFirstHediffOfDef(
//                    HediffDef.Named("CyberPsychosisBlocker"));
//                if (tempBlocker != null)
//                    pawn.health.RemoveHediff(tempBlocker);

//                Hediff permanent = HediffMaker.MakeHediff(
//                    HediffDef.Named("PermanentCyberPsychosisBlocker"), pawn, part);
//                permanent.Severity = 1f;
//                pawn.health.AddHediff(permanent);

//                Need_CyberPsychosis need = pawn.needs?.TryGetNeed<Need_CyberPsychosis>();
//                if (need != null)
//                    need.CurLevel = 1f;

//                Messages.Message("CyberPsychosis_PermanentProtection".Translate(pawn.Name), pawn, MessageTypeDefOf.PositiveEvent);
//            }
//        }
//    }

//    // ==================== HARMONY PATCHES ====================
//    [StaticConstructorOnStartup]
//    public static class CyberPsychosisHarmonyPatches
//    {
//        static CyberPsychosisHarmonyPatches()
//        {
//            var harmony = new Harmony("HSK.Watcher.CyberPsychosis");
//            harmony.PatchAll();
//            CyberPsychosisLogger.Log("Harmony patches initialized");
//        }
//    }

//    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
//    public static class Patch_PawnSpawnSetup
//    {
//        [HarmonyPostfix]
//        public static void Postfix(Pawn __instance, Map map, bool respawningAfterLoad)
//        {
//            if (!CyberPsychosisMod.settings.cyberPsychosisEnabled) return;
//            if (__instance == null || !__instance.RaceProps.Humanlike) return;

//            HediffDef managerDef = HediffDef.Named("CyberPsychosisManagerHediff");
//            if (managerDef != null && __instance.health?.hediffSet?.GetFirstHediffOfDef(managerDef) == null)
//            {
//                Hediff hediff = HediffMaker.MakeHediff(managerDef, __instance);
//                __instance.health.AddHediff(hediff);
//            }
//        }
//    }

//    [HarmonyPatch(typeof(HediffSet))]
//    [HarmonyPatch("AddDirect")]
//    public static class Patch_HediffAdded
//    {
//        [HarmonyPostfix]
//        public static void Postfix(Hediff hediff, Pawn ___pawn)
//        {
//            if (!CyberPsychosisMod.settings.cyberPsychosisEnabled || ___pawn == null || !___pawn.RaceProps.Humanlike)
//                return;

//            if (hediff?.def?.defName == null) return;

//            if (Need_CyberPsychosis.ImplantPoints.ContainsKey(hediff.def.defName))
//            {
//                Need_CyberPsychosis need = ___pawn.needs?.TryGetNeed<Need_CyberPsychosis>();
//                if (need != null)
//                {
//                    need.ForceRecalculate();
//                }
//            }
//        }
//    }

//    [HarmonyPatch(typeof(Pawn_JobTracker))]
//    [HarmonyPatch("DetermineNextJob")]
//    public static class Patch_KillDesireJob
//    {
//        [HarmonyPrefix]
//        public static bool Prefix(Pawn_JobTracker __instance, ref ThinkResult __result, Pawn ___pawn)
//        {
//            if (!CyberPsychosisMod.settings.cyberPsychosisEnabled)
//                return true;

//            if (___pawn.Downed || ___pawn.Dead)
//                return true;

//            Need_CyberPsychosis need = ___pawn.needs?.TryGetNeed<Need_CyberPsychosis>();
//            if (need == null || !need.HasKillDesire)
//                return true;

//            Pawn victim = FindVictim(___pawn);
//            if (victim != null)
//            {
//                Job attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, victim);
//                attackJob.killIncappedTarget = true;
//                __result = new ThinkResult(attackJob, null, null, true);
//                Messages.Message("CyberPsychosis_Attack".Translate(___pawn.Name, victim.Name), ___pawn, MessageTypeDefOf.NegativeEvent);
//                return false;
//            }

//            return true;
//        }

//        private static Pawn FindVictim(Pawn attacker)
//        {
//            if (attacker.Map == null) return null;

//            Pawn closestVictim = null;
//            float closestDist = float.MaxValue;

//            foreach (Pawn pawn in attacker.Map.mapPawns.AllPawnsSpawned)
//            {
//                if (pawn == attacker) continue;
//                if (pawn.Dead || pawn.Downed) continue;
//                if (!pawn.RaceProps.Humanlike) continue;
//                if (pawn.Faction != attacker.Faction) continue;

//                float dist = attacker.Position.DistanceToSquared(pawn.Position);
//                if (dist < closestDist && dist < 900f)
//                {
//                    closestDist = dist;
//                    closestVictim = pawn;
//                }
//            }

//            return closestVictim;
//        }
//    }
//}
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
namespace HSK.Watcher.Content.Compes
{
    // ==========================================
    // 1. КОМПОНЕНТ ТРИГГЕРА КОМЫ С ОТСЛЕЖИВАНИЕМ КОЛИЧЕСТВА
    // ==========================================
    public class HediffCompProperties_RegenerationComaTrigger : HediffCompProperties
    {
        public HediffCompProperties_RegenerationComaTrigger()
        {
            this.compClass = typeof(HediffComp_RegenerationComaTrigger);
        }

        public string regenerationHediffDef = "AbnormalRegenerationComa";
        public string blissLobotomyDef = "AbnormalBlissLobotomy";
        public float healthThreshold = 0.15f;
        public int checkIntervalTicks = 60;
        public int cooldownDays = 7;
        public int maxComas = 3;
    }

    public class HediffComp_RegenerationComaTrigger : HediffComp
    {
        private HediffCompProperties_RegenerationComaTrigger Props =>
            (HediffCompProperties_RegenerationComaTrigger)this.props;

        private int tickCounter = 0;
        private bool isInComa = false;
        private int comaCount = 0;
        private bool isLobotomized = false;
        private int cooldownTicksRemaining = 0;
        private bool isOnCooldown = false;

        public override void CompPostMake()
        {
            base.CompPostMake();
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (this.parent?.pawn == null) return;
            var pawn = this.parent.pawn;

            if (isInComa) return;
            if (pawn.Dead) return;
            if (isLobotomized) return;

            if (isOnCooldown)
            {
                cooldownTicksRemaining--;
                if (cooldownTicksRemaining <= 0)
                {
                    isOnCooldown = false;
                }
                return;
            }

            if (comaCount >= Props.maxComas)
            {
                if (!isLobotomized)
                {
                    AddLobotomy(pawn);
                    isLobotomized = true;
                }
                return;
            }

            tickCounter++;
            if (tickCounter < Props.checkIntervalTicks) return;
            tickCounter = 0;

            CheckForRegeneration(pawn);
        }

        private void CheckForRegeneration(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return;

            if (pawn.health.hediffSet.HasHediff(HediffDef.Named(Props.regenerationHediffDef))) return;
            if (isOnCooldown) return;
            if (isLobotomized) return;
            if (comaCount >= Props.maxComas) return;

            float healthPercent = pawn.health.summaryHealth.SummaryHealthPercent;

            bool hasLethalInjuries = HasLethalInjuries(pawn);
            bool isBleedingOut = pawn.health.hediffSet.BleedRateTotal > 0.5f;
            bool isCriticallyInjured = healthPercent < Props.healthThreshold;

            bool hasLethalCondition = pawn.health.hediffSet.hediffs.Any(h =>
                h.def.defName == "BloodLoss" && h.Severity > 0.8f ||
                h.def.defName == "PainShock" ||
                h.def.defName == "Heatstroke" && h.Severity > 0.8f ||
                h.def.defName == "Hypothermia" && h.Severity > 0.8f ||
                h.def.defName == "Toxicity" && h.Severity > 0.8f
            );

            bool shouldTrigger = hasLethalInjuries || isBleedingOut || isCriticallyInjured || hasLethalCondition;

            if (shouldTrigger)
            {
                TriggerRegenerationComa(pawn);
            }
        }

        private bool HasLethalInjuries(Pawn pawn)
        {
            if (pawn.health.hediffSet == null) return false;

            var heartDef = DefDatabase<BodyPartDef>.GetNamedSilentFail("Heart");
            var lungDef = DefDatabase<BodyPartDef>.GetNamedSilentFail("Lung");

            var vitalParts = new List<BodyPartDef>();
            if (heartDef != null) vitalParts.Add(heartDef);
            if (lungDef != null) vitalParts.Add(lungDef);

            foreach (var injury in pawn.health.hediffSet.hediffs)
            {
                if (injury is Hediff_Injury && injury.Part != null)
                {
                    if (vitalParts.Contains(injury.Part.def))
                    {
                        if (injury.Severity > 0.5f) return true;
                    }
                }
            }

            return false;
        }

        private void TriggerRegenerationComa(Pawn pawn)
        {
            try
            {
                if (pawn.Dead) return;

                var comaDef = HediffDef.Named(Props.regenerationHediffDef);
                if (comaDef == null)
                {
                    Log.Error($"[RegenerationComaTrigger] Could not find {Props.regenerationHediffDef}");
                    return;
                }

                var oldComa = pawn.health.hediffSet.GetFirstHediffOfDef(comaDef);
                if (oldComa != null)
                {
                    pawn.health.RemoveHediff(oldComa);
                }

                var comaHediff = HediffMaker.MakeHediff(comaDef, pawn);
                pawn.health.AddHediff(comaHediff);
                isInComa = true;

                Messages.Message(
                    $"{pawn.Name} has entered a regeneration coma!",
                    pawn,
                    MessageTypeDefOf.NeutralEvent
                );
            }
            catch (Exception ex)
            {
                Log.Error($"[RegenerationComaTrigger] Error: {ex.Message}");
            }
        }

        public void OnRegenerationComplete(Pawn pawn)
        {
            isInComa = false;
            comaCount++;

            if (comaCount >= Props.maxComas)
            {
                if (!isLobotomized)
                {
                    AddLobotomy(pawn);
                    isLobotomized = true;
                }
                return;
            }

            isOnCooldown = true;
            cooldownTicksRemaining = Props.cooldownDays * 60000;

            Messages.Message(
                $"{pawn.Name} has recovered from regeneration coma. Gland recharging for {Props.cooldownDays} days.",
                pawn,
                MessageTypeDefOf.PositiveEvent
            );
        }

        private void AddLobotomy(Pawn pawn)
        {
            try
            {
                if (string.IsNullOrEmpty(Props.blissLobotomyDef)) return;

                var lobotomyDef = HediffDef.Named(Props.blissLobotomyDef);
                if (lobotomyDef == null) return;

                var lobotomy = HediffMaker.MakeHediff(lobotomyDef, pawn);
                pawn.health.AddHediff(lobotomy);
                isLobotomized = true;

                Messages.Message(
                    $"{pawn.Name} has been lobotomized by the parasite after {comaCount} comas!",
                    pawn,
                    MessageTypeDefOf.NegativeEvent
                );
            }
            catch (Exception ex)
            {
                Log.Error($"[RegenerationComaTrigger] AddLobotomy error: {ex.Message}");
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref isInComa, "isInComa", false);
            Scribe_Values.Look(ref comaCount, "comaCount", 0);
            Scribe_Values.Look(ref isLobotomized, "isLobotomized", false);
            Scribe_Values.Look(ref isOnCooldown, "isOnCooldown", false);
            Scribe_Values.Look(ref cooldownTicksRemaining, "cooldownTicksRemaining", 0);
        }

        public override string CompTipStringExtra
        {
            get
            {
                try
                {
                    if (this.parent?.pawn == null) return "";

                    string result = "";

                    if (isLobotomized)
                    {
                        result += $"\n\n{"Lobotomized"}";
                        return result;
                    }

                    if (isOnCooldown)
                    {
                        float daysLeft = cooldownTicksRemaining / 60000f;
                        result += $"\n\nCooldown: {daysLeft:F1} days";
                    }
                    else if (comaCount >= Props.maxComas)
                    {
                        result += $"\n\nDepleted";
                    }
                    else
                    {
                        result += $"\n\nReady";
                    }

                    result += $"\nComas: {comaCount}/{Props.maxComas}";

                    return result;
                }
                catch
                {
                    return "";
                }
            }
        }
    }

    // ==========================================
    // 2. КОМПОНЕНТ ДЛЯ РЕГЕНЕРАЦИИ В КОМЕ
    // ==========================================
    public class HediffCompProperties_RegenerationComa : HediffCompProperties
    {
        public HediffCompProperties_RegenerationComa()
        {
            this.compClass = typeof(HediffComp_RegenerationComa);
        }

        public int regenerationTicks = 420000;
        public float regenerationRate = 0.15f;
        public float healRate = 0.2f;
        public bool showRemainingTime = true;
    }

    public class HediffComp_RegenerationComa : HediffComp
    {
        private HediffCompProperties_RegenerationComa Props =>
            (HediffCompProperties_RegenerationComa)this.props;

        private int ticksSinceStart = 0;
        private bool regenerationComplete = false;

        public override void CompPostMake()
        {
            base.CompPostMake();
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (this.parent?.pawn == null || this.parent.pawn.Dead) return;
            if (regenerationComplete) return;

            ticksSinceStart++;

            if (ticksSinceStart % 60 == 0)
            {
                RegeneratePawn();
            }

            if (ticksSinceStart >= Props.regenerationTicks)
            {
                CompleteRegeneration();
            }
        }

        private void RegeneratePawn()
        {
            var pawn = this.parent.pawn;
            if (pawn == null) return;

            var bleedingInjuries = pawn.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(h => h.Bleeding)
                .ToList();

            foreach (var injury in bleedingInjuries)
            {
                injury.Severity = 0f;
                pawn.health.RemoveHediff(injury);
            }

            foreach (var injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())
            {
                injury.Severity -= Props.healRate;
                if (injury.Severity <= 0)
                {
                    pawn.health.RemoveHediff(injury);
                }
            }

            foreach (var hediff in pawn.health.hediffSet.hediffs.ToList())
            {
                if (hediff.def.defName != "AbnormalRegenerationComa" &&
                    hediff.def.defName != "AbnormalRegenerationGland" &&
                    hediff.def.defName != "AbnormalBlissLobotomy" &&
                    hediff.def.defName != "ResurrectionComa" &&
                    hediff.def.isBad &&
                    !hediff.def.defName.Contains("Death"))
                {
                    hediff.Severity -= Props.regenerationRate * 0.5f;
                    if (hediff.Severity <= 0)
                    {
                        pawn.health.RemoveHediff(hediff);
                    }
                }
            }

            pawn.health.hediffSet.hediffs
                .Where(h => h.def.defName == "BloodLoss")
                .ToList()
                .ForEach(h => h.Severity -= Props.regenerationRate * 2f);

            RestoreMissingParts(pawn);
        }

        private void RestoreMissingParts(Pawn pawn)
        {
            var health = pawn.health;
            if (health == null) return;

            var missingParts = health.hediffSet.GetMissingPartsCommonAncestors();

            foreach (var partInfo in missingParts)
            {
                var part = partInfo.Part;
                if (part == null) continue;

                var vitalDefs = new List<string> { "Heart", "Lung", "Liver", "Kidney", "Brain" };
                if (vitalDefs.Contains(part.def.defName))
                {
                    var woundDef = DefDatabase<HediffDef>.GetNamedSilentFail("Wound");
                    if (woundDef == null)
                    {
                        woundDef = DefDatabase<HediffDef>.GetNamedSilentFail("Injury");
                    }
                    if (woundDef == null)
                    {
                        woundDef = HediffDefOf.BloodLoss;
                    }

                    if (woundDef != null)
                    {
                        var newPart = HediffMaker.MakeHediff(woundDef, pawn, part);
                        newPart.Severity = 0.01f;
                        health.AddHediff(newPart);
                    }
                }
            }
        }

        private void CompleteRegeneration()
        {
            var pawn = this.parent.pawn;
            if (pawn == null) return;

            regenerationComplete = true;
            pawn.health.RemoveHediff(this.parent);

            var trigger = pawn.health.hediffSet.hediffs
                .Select(h => h.TryGetComp<HediffComp_RegenerationComaTrigger>())
                .FirstOrDefault(c => c != null);

            if (trigger != null)
            {
                trigger.OnRegenerationComplete(pawn);
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksSinceStart, "ticksSinceStart", 0);
            Scribe_Values.Look(ref regenerationComplete, "regenerationComplete", false);
        }

        public override string CompTipStringExtra
        {
            get
            {
                if (!Props.showRemainingTime || regenerationComplete) return "";
                float remaining = (Props.regenerationTicks - ticksSinceStart) / 60000f;
                return $"\n\nRemaining: {remaining:F1} days";
            }
        }
    }

    // ==========================================
    // 3. КОМПОНЕНТ САМОУНИЧТОЖЕНИЯ ПРИ УДАЛЕНИИ
    // ==========================================
    public class HediffCompProperties_SelfDestructOnRemove : HediffCompProperties
    {
        public HediffCompProperties_SelfDestructOnRemove()
        {
            this.compClass = typeof(HediffComp_SelfDestructOnRemove);
        }

        public string destructionMessage = "The void takes what was promised. Consciousness erased. Nervous system burned to nothing.";
    }

    public class HediffComp_SelfDestructOnRemove : HediffComp
    {
        private HediffCompProperties_SelfDestructOnRemove Props =>
            (HediffCompProperties_SelfDestructOnRemove)this.props;

        public void TriggerDestruction()
        {
            if (this.parent?.pawn == null) return;

            var pawn = this.parent.pawn;

            var brainDef = DefDatabase<BodyPartDef>.GetNamedSilentFail("Brain");
            if (brainDef != null)
            {
                var brain = pawn.health.hediffSet.GetNotMissingParts()
                    .FirstOrDefault(p => p.def == brainDef);

                if (brain != null)
                {
                    var woundDef = DefDatabase<HediffDef>.GetNamedSilentFail("Wound");
                    if (woundDef == null)
                    {
                        woundDef = DefDatabase<HediffDef>.GetNamedSilentFail("Injury");
                    }
                    if (woundDef == null)
                    {
                        woundDef = HediffDefOf.BloodLoss;
                    }

                    var wound = HediffMaker.MakeHediff(woundDef, pawn, brain);
                    wound.Severity = 1f;
                    pawn.health.AddHediff(wound);
                }
            }

            if (!pawn.Dead)
            {
                pawn.Kill(null);
            }

            Messages.Message(
                Props.destructionMessage,
                pawn,
                MessageTypeDefOf.NegativeEvent
            );
        }
    }
}
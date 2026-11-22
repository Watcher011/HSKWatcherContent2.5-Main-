using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace HSK.Watcher.Content.Compes
{
    public class HediffCompProperties_RandomStatProsthetic : HediffCompProperties
    {
        public HediffCompProperties_RandomStatProsthetic()
        {
            this.compClass = typeof(HediffComp_RandomStatProsthetic);
        }

        public float manipMin = 0f;
        public float manipMax = 0f;
        public float consciousMin = 0f;
        public float consciousMax = 0f;
        public float movingMin = 0f;
        public float movingMax = 0f;
        public float sightMin = 0f;
        public float sightMax = 0f;
        public float hearingMin = 0f;
        public float hearingMax = 0f;
        public float breathingMin = 0f;
        public float breathingMax = 0f;
        public float bloodFiltrationMin = 0f;
        public float bloodFiltrationMax = 0f;
        public float bloodPumpingMin = 0f;
        public float bloodPumpingMax = 0f;

        public float beautyMin = 0f;
        public float beautyMax = 0f;
        public float breakMin = 0f;
        public float breakMax = 0f;
        public float painMin = 0f;
        public float painMax = 0f;
        public float hungerMin = 0f;
        public float hungerMax = 0f;

        public bool generateRandomLabel = true;
        public string labelPrefix = "Abnormal";
    }

    public class Hediff_RandomStatProsthetic : Hediff_AddedPart
    {
        private HediffStage uniqueStage = null;

        public float generatedManipulation = 0f;
        public float generatedConsciousness = 0f;
        public float generatedMoving = 0f;
        public float generatedSight = 0f;
        public float generatedHearing = 0f;
        public float generatedBreathing = 0f;
        public float generatedBloodFiltration = 0f;
        public float generatedBloodPumping = 0f;
        public float generatedBeauty = 0f;
        public float generatedMentalBreak = 0f;
        public float generatedPain = 0f;
        public float generatedHunger = 0f;
        public string generatedLabel = "";
        public bool statsGenerated = false;

        private HediffCompProperties_RandomStatProsthetic cachedProps = null;

        private HediffCompProperties_RandomStatProsthetic Props
        {
            get
            {
                if (cachedProps == null)
                {
                    cachedProps = GetCompProperties();
                }
                return cachedProps;
            }
        }

        private HediffCompProperties_RandomStatProsthetic GetCompProperties()
        {
            if (comps == null) return null;

            for (int i = 0; i < comps.Count; i++)
            {
                if (comps[i] is HediffComp_RandomStatProsthetic comp)
                {
                    return comp.props as HediffCompProperties_RandomStatProsthetic;
                }
            }
            return null;
        }

        public override HediffStage CurStage
        {
            get
            {
                if (uniqueStage != null)
                {
                    return uniqueStage;
                }
                return base.CurStage;
            }
        }

        public override void PostMake()
        {
            base.PostMake();

            if (this.pawn != null && !this.pawn.Dead && !statsGenerated)
            {
                GenerateRandomStats();
                CreateUniqueStage();

                if (Props != null && Props.generateRandomLabel)
                {
                    GenerateRandomLabel();
                }

                statsGenerated = true;
                this.pawn.health.Notify_HediffChanged(this);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref generatedManipulation, "generatedManipulation", 0f);
            Scribe_Values.Look(ref generatedConsciousness, "generatedConsciousness", 0f);
            Scribe_Values.Look(ref generatedMoving, "generatedMoving", 0f);
            Scribe_Values.Look(ref generatedSight, "generatedSight", 0f);
            Scribe_Values.Look(ref generatedHearing, "generatedHearing", 0f);
            Scribe_Values.Look(ref generatedBreathing, "generatedBreathing", 0f);
            Scribe_Values.Look(ref generatedBloodFiltration, "generatedBloodFiltration", 0f);
            Scribe_Values.Look(ref generatedBloodPumping, "generatedBloodPumping", 0f);
            Scribe_Values.Look(ref generatedBeauty, "generatedBeauty", 0f);
            Scribe_Values.Look(ref generatedMentalBreak, "generatedMentalBreak", 0f);
            Scribe_Values.Look(ref generatedPain, "generatedPain", 0f);
            Scribe_Values.Look(ref generatedHunger, "generatedHunger", 0f);
            Scribe_Values.Look(ref generatedLabel, "generatedLabel", "");
            Scribe_Values.Look(ref statsGenerated, "statsGenerated", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                CreateUniqueStage();
                if (this.pawn != null)
                {
                    this.pawn.health.Notify_HediffChanged(this);
                }
            }
        }

        private void GenerateRandomStats()
        {
            if (Props == null) return;

            if (Props.manipMax != 0f || Props.manipMin != 0f)
                generatedManipulation = Rand.Range(Props.manipMin, Props.manipMax);
            if (Props.consciousMax != 0f || Props.consciousMin != 0f)
                generatedConsciousness = Rand.Range(Props.consciousMin, Props.consciousMax);
            if (Props.movingMax != 0f || Props.movingMin != 0f)
                generatedMoving = Rand.Range(Props.movingMin, Props.movingMax);
            if (Props.sightMax != 0f || Props.sightMin != 0f)
                generatedSight = Rand.Range(Props.sightMin, Props.sightMax);
            if (Props.hearingMax != 0f || Props.hearingMin != 0f)
                generatedHearing = Rand.Range(Props.hearingMin, Props.hearingMax);
            if (Props.breathingMax != 0f || Props.breathingMin != 0f)
                generatedBreathing = Rand.Range(Props.breathingMin, Props.breathingMax);
            if (Props.bloodFiltrationMax != 0f || Props.bloodFiltrationMin != 0f)
                generatedBloodFiltration = Rand.Range(Props.bloodFiltrationMin, Props.bloodFiltrationMax);
            if (Props.bloodPumpingMax != 0f || Props.bloodPumpingMin != 0f)
                generatedBloodPumping = Rand.Range(Props.bloodPumpingMin, Props.bloodPumpingMax);

            if (Props.beautyMax != 0f || Props.beautyMin != 0f)
                generatedBeauty = Rand.Range(Props.beautyMin, Props.beautyMax);
            if (Props.breakMax != 0f || Props.breakMin != 0f)
                generatedMentalBreak = Rand.Range(Props.breakMin, Props.breakMax);
            if (Props.painMax != 0f || Props.painMin != 0f)
                generatedPain = Rand.Range(Props.painMin, Props.painMax);
            if (Props.hungerMax != 0f || Props.hungerMin != 0f)
                generatedHunger = Rand.Range(Props.hungerMin, Props.hungerMax);

            generatedManipulation = (float)Math.Round(generatedManipulation, 2);
            generatedConsciousness = (float)Math.Round(generatedConsciousness, 2);
            generatedMoving = (float)Math.Round(generatedMoving, 2);
            generatedSight = (float)Math.Round(generatedSight, 2);
            generatedHearing = (float)Math.Round(generatedHearing, 2);
            generatedBreathing = (float)Math.Round(generatedBreathing, 2);
            generatedBloodFiltration = (float)Math.Round(generatedBloodFiltration, 2);
            generatedBloodPumping = (float)Math.Round(generatedBloodPumping, 2);
            generatedBeauty = (float)Math.Round(generatedBeauty, 2);
            generatedMentalBreak = (float)Math.Round(generatedMentalBreak, 2);
            generatedPain = (float)Math.Round(generatedPain, 2);
            generatedHunger = (float)Math.Round(generatedHunger, 2);
        }

        private void CreateUniqueStage()
        {
            uniqueStage = new HediffStage();

            if (def.stages != null && def.stages.Count > 0)
            {
                CopyBaseStageFields(def.stages[0], uniqueStage);
            }

            uniqueStage.statOffsets = new List<StatModifier>();
            uniqueStage.capMods = new List<PawnCapacityModifier>();

            if (generatedBeauty != 0f)
            {
                uniqueStage.statOffsets.Add(new StatModifier
                {
                    stat = StatDefOf.PawnBeauty,
                    value = generatedBeauty
                });
            }
            if (generatedMentalBreak != 0f)
            {
                uniqueStage.statOffsets.Add(new StatModifier
                {
                    stat = StatDefOf.MentalBreakThreshold,
                    value = generatedMentalBreak
                });
            }

            TryAddCapMod(uniqueStage, PawnCapacityDefOf.Manipulation, generatedManipulation);
            TryAddCapMod(uniqueStage, PawnCapacityDefOf.Consciousness, generatedConsciousness);
            TryAddCapMod(uniqueStage, PawnCapacityDefOf.Moving, generatedMoving);
            TryAddCapMod(uniqueStage, PawnCapacityDefOf.Sight, generatedSight);
            TryAddCapMod(uniqueStage, PawnCapacityDefOf.Hearing, generatedHearing);
            TryAddCapMod(uniqueStage, PawnCapacityDefOf.Breathing, generatedBreathing);
            TryAddCapMod(uniqueStage, PawnCapacityDefOf.BloodFiltration, generatedBloodFiltration);
            TryAddCapMod(uniqueStage, PawnCapacityDefOf.BloodPumping, generatedBloodPumping);

            if (generatedPain != 0f)
                uniqueStage.painOffset = generatedPain;
            if (generatedHunger != 0f)
                uniqueStage.hungerRateFactor = 1f + generatedHunger;

            uniqueStage.painFactor = 1f;

            if (!string.IsNullOrEmpty(generatedLabel))
            {
                uniqueStage.label = generatedLabel;
            }
        }

        private void TryAddCapMod(HediffStage stage, PawnCapacityDef capacityDef, float value)
        {
            if (stage == null || capacityDef == null || value == 0f) return;

            stage.capMods.Add(new PawnCapacityModifier
            {
                capacity = capacityDef,
                offset = value
            });
        }

        private void CopyBaseStageFields(HediffStage source, HediffStage target)
        {
            if (source == null || target == null) return;

            Type stageType = typeof(HediffStage);
            FieldInfo[] fields = stageType.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (FieldInfo field in fields)
            {
                try
                {
                    if (field.FieldType.IsGenericType &&
                        field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        continue;
                    }

                    object value = field.GetValue(source);
                    field.SetValue(target, value);
                }
                catch { }
            }
        }

        private void GenerateRandomLabel()
        {
            string[] prefixes = {
                "Ancient", "Twisted", "Living", "Crimson", "Void",
                "Ethereal", "Savage", "Radiant", "Shadow", "Arcane",
                "Corrupted", "Blessed", "Cursed", "Primal", "Abyssal"
            };

            string[] suffixes = {
                "Limb", "Prosthetic", "Appendage", "Extension",
                "Awakening", "Mutation", "Rebirth", "Evolution"
            };

            generatedLabel = prefixes.RandomElement() + " " + suffixes.RandomElement();

            var labelField = typeof(Hediff).GetField("label",
                BindingFlags.Public | BindingFlags.Instance);
            if (labelField != null)
            {
                labelField.SetValue(this, generatedLabel);
            }

            if (uniqueStage != null)
            {
                uniqueStage.label = generatedLabel;
            }
        }

        public override string TipStringExtra
        {
            get
            {
                try
                {
                    string text = "";

                    if (!string.IsNullOrEmpty(generatedLabel))
                    {
                        text += "\n\n<i>" + generatedLabel + "</i>";
                    }

                    text += "\n\n" + "HSK_Watcher_GeneratedParameters".Translate();

                    if (generatedManipulation != 0f)
                        text += "\n  " + "HSK_Watcher_Manipulation".Translate() + ": " + FormatPercent(generatedManipulation);
                    if (generatedConsciousness != 0f)
                        text += "\n  " + "HSK_Watcher_Consciousness".Translate() + ": " + FormatPercent(generatedConsciousness);
                    if (generatedMoving != 0f)
                        text += "\n  " + "HSK_Watcher_Moving".Translate() + ": " + FormatPercent(generatedMoving);
                    if (generatedSight != 0f)
                        text += "\n  " + "HSK_Watcher_Sight".Translate() + ": " + FormatPercent(generatedSight);
                    if (generatedHearing != 0f)
                        text += "\n  " + "HSK_Watcher_Hearing".Translate() + ": " + FormatPercent(generatedHearing);
                    if (generatedBreathing != 0f)
                        text += "\n  " + "HSK_Watcher_Breathing".Translate() + ": " + FormatPercent(generatedBreathing);
                    if (generatedBloodFiltration != 0f)
                        text += "\n  " + "HSK_Watcher_BloodFiltration".Translate() + ": " + FormatPercent(generatedBloodFiltration);
                    if (generatedBloodPumping != 0f)
                        text += "\n  " + "HSK_Watcher_BloodPumping".Translate() + ": " + FormatPercent(generatedBloodPumping);

                    if (generatedBeauty != 0f)
                        text += "\n  " + "HSK_Watcher_Beauty".Translate() + ": " + generatedBeauty.ToString("F2");
                    if (generatedMentalBreak != 0f)
                        text += "\n  " + "HSK_Watcher_MentalBreakThreshold".Translate() + ": " + FormatPercent(generatedMentalBreak);
                    if (generatedPain != 0f)
                        text += "\n  " + "HSK_Watcher_Pain".Translate() + ": +" + (generatedPain * 100f).ToString("F0") + "%";
                    if (generatedHunger != 0f)
                        text += "\n  " + "HSK_Watcher_HungerRate".Translate() + ": x" + (1f + generatedHunger).ToString("F2");

                    return text;
                }
                catch
                {
                    return "";
                }
            }
        }

        private string FormatPercent(float value)
        {
            if (value > 0)
                return "+" + (value * 100f).ToString("F0") + "%";
            return (value * 100f).ToString("F0") + "%";
        }
    }

    public class HediffComp_RandomStatProsthetic : HediffComp
    {
        public HediffCompProperties_RandomStatProsthetic Props =>
            (HediffCompProperties_RandomStatProsthetic)this.props;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HSK.Watcher.Content.Compes
{
    // ==========================================
    // 1. КЛАСС ДЛЯ НАСТРОЕК ИЗ XML
    // ==========================================
    public class HediffCompProperties_RandomStatProsthetic : HediffCompProperties
    {
        public HediffCompProperties_RandomStatProsthetic()
        {
            this.compClass = typeof(HediffComp_RandomStatProsthetic);
        }

        // Настройки для генерации случайных параметров
        public float manipMin = 0.1f;
        public float manipMax = 0.8f;
        public float consciousMin = -0.15f;
        public float consciousMax = -0.01f;
        public float beautyMin = -0.2f;
        public float beautyMax = -0.05f;
        public float breakMin = -0.04f;
        public float breakMax = -0.01f;
        public float painMin = 0.01f;
        public float painMax = 0.05f;
        public float hungerMin = 0.01f;
        public float hungerMax = 0.05f;

        // Можно ли генерировать случайное название
        public bool generateRandomLabel = true;
    }

    // ==========================================
    // 2. УНИВЕРСАЛЬНЫЙ КОМПОНЕНТ ДЛЯ ЛЮБЫХ ПРОТЕЗОВ
    // ==========================================
    public class HediffComp_RandomStatProsthetic : HediffComp
    {
        // Доступ к настройкам из XML
        public HediffCompProperties_RandomStatProsthetic Props =>
            (HediffCompProperties_RandomStatProsthetic)this.props;

        // Сохраняем сгенерированные параметры
        private float generatedManipulation = 0.3f;
        private float generatedConsciousness = -0.01f;
        private float generatedBeauty = -0.1f;
        private float generatedMentalBreak = -0.01f;
        private float generatedPain = 0.01f;
        private float generatedHunger = 0.01f;
        private string generatedLabel = "";
        private bool statsGenerated = false;

        // ==========================================
        // 3. ОСНОВНЫЕ МЕТОДЫ
        // ==========================================
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);

            if (this.parent.pawn != null && !this.parent.pawn.Dead && !statsGenerated)
            {
                GenerateRandomStats();
                ApplyStatsToStage();

                // Генерируем название только если включено в настройках
                if (Props.generateRandomLabel)
                {
                    GenerateRandomLabel();
                }

                statsGenerated = true;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref generatedManipulation, "generatedManipulation", 0.3f);
            Scribe_Values.Look(ref generatedConsciousness, "generatedConsciousness", -0.01f);
            Scribe_Values.Look(ref generatedBeauty, "generatedBeauty", -0.1f);
            Scribe_Values.Look(ref generatedMentalBreak, "generatedMentalBreak", -0.01f);
            Scribe_Values.Look(ref generatedPain, "generatedPain", 0.01f);
            Scribe_Values.Look(ref generatedHunger, "generatedHunger", 0.01f);
            Scribe_Values.Look(ref generatedLabel, "generatedLabel", "");
            Scribe_Values.Look(ref statsGenerated, "statsGenerated", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ApplyStatsToStage();
                if (this.parent != null && !string.IsNullOrEmpty(generatedLabel))
                {
                    this.parent.label = generatedLabel;
                }
            }
        }

        // ==========================================
        // 4. ГЕНЕРАЦИЯ ПАРАМЕТРОВ
        // ==========================================
        private void GenerateRandomStats()
        {
            try
            {
                generatedManipulation = Rand.Range(Props.manipMin, Props.manipMax);
                generatedConsciousness = Rand.Range(Props.consciousMin, Props.consciousMax);
                generatedBeauty = Rand.Range(Props.beautyMin, Props.beautyMax);
                generatedMentalBreak = Rand.Range(Props.breakMin, Props.breakMax);
                generatedPain = Rand.Range(Props.painMin, Props.painMax);
                generatedHunger = Rand.Range(Props.hungerMin, Props.hungerMax);

                generatedManipulation = (float)Math.Round(generatedManipulation, 2);
                generatedConsciousness = (float)Math.Round(generatedConsciousness, 2);
                generatedBeauty = (float)Math.Round(generatedBeauty, 2);
                generatedMentalBreak = (float)Math.Round(generatedMentalBreak, 2);
                generatedPain = (float)Math.Round(generatedPain, 2);
                generatedHunger = (float)Math.Round(generatedHunger, 2);

                if (this.parent.pawn != null)
                {
                    Log.Message($"{GetBaseLabel()} installed on {parent.pawn.Name}: " +
                               $"Manipulation: +{generatedManipulation * 100}%, " +
                               $"Consciousness: {generatedConsciousness * 100}%, " +
                               $"Beauty: {generatedBeauty}, " +
                               $"MentalBreak: {generatedMentalBreak}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error generating random stats: {ex.Message}");
            }
        }

        private void GenerateRandomLabel()
        {
            try
            {
                // Получаем базовое название из XML
                string baseLabel = GetBaseLabel();

                // Массивы для генерации случайного названия
                string[] prefixes = {
                    "Ancient", "Twisted", "Living", "Crimson", "Void",
                    "Ethereal", "Savage", "Radiant", "Shadow", "Arcane",
                    "Corrupted", "Blessed", "Cursed", "Primal", "Abyssal"
                };

                string[] suffixes = {
                    "Limb", "Prosthetic", "Appendage", "Extension",
                    "Awakening", "Mutation", "Rebirth", "Evolution"
                };

                // Генерируем новое название
                string newLabel = $"{prefixes.RandomElement()} {suffixes.RandomElement()}";
                generatedLabel = newLabel;

                if (this.parent != null)
                {
                    this.parent.label = generatedLabel;
                }

                Log.Message($"Renamed {baseLabel} to {generatedLabel}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error generating label: {ex.Message}");
            }
        }

        // ==========================================
        // 5. ПРИМЕНЕНИЕ ПАРАМЕТРОВ
        // ==========================================
        private void ApplyStatsToStage()
        {
            try
            {
                if (this.parent == null || this.parent.CurStage == null)
                    return;

                var stage = this.parent.CurStage;

                // Применяем statOffsets
                if (stage.statOffsets == null)
                    stage.statOffsets = new List<StatModifier>();

                // Красота
                var beautyMod = stage.statOffsets.FirstOrDefault(m => m.stat == StatDefOf.PawnBeauty);
                if (beautyMod == null)
                {
                    beautyMod = new StatModifier { stat = StatDefOf.PawnBeauty };
                    stage.statOffsets.Add(beautyMod);
                }
                beautyMod.value = generatedBeauty;

                // Порог нервного срыва
                var breakMod = stage.statOffsets.FirstOrDefault(m => m.stat == StatDefOf.MentalBreakThreshold);
                if (breakMod == null)
                {
                    breakMod = new StatModifier { stat = StatDefOf.MentalBreakThreshold };
                    stage.statOffsets.Add(breakMod);
                }
                breakMod.value = generatedMentalBreak;

                // Применяем capMods
                if (stage.capMods == null)
                    stage.capMods = new List<PawnCapacityModifier>();

                // Манипуляция
                var manipMod = stage.capMods.FirstOrDefault(m => m.capacity == PawnCapacityDefOf.Manipulation);
                if (manipMod == null)
                {
                    manipMod = new PawnCapacityModifier { capacity = PawnCapacityDefOf.Manipulation };
                    stage.capMods.Add(manipMod);
                }
                manipMod.offset = generatedManipulation;

                // Сознание
                var consciousMod = stage.capMods.FirstOrDefault(m => m.capacity == PawnCapacityDefOf.Consciousness);
                if (consciousMod == null)
                {
                    consciousMod = new PawnCapacityModifier { capacity = PawnCapacityDefOf.Consciousness };
                    stage.capMods.Add(consciousMod);
                }
                consciousMod.offset = generatedConsciousness;

                // Применяем другие параметры
                stage.painOffset = generatedPain;
                stage.hungerRateFactor = generatedHunger;

                // Обновляем состояние персонажа
                if (this.parent.pawn != null)
                    this.parent.pawn.health.Notify_HediffChanged(this.parent);
            }
            catch (Exception ex)
            {
                Log.Error($"Error applying stats: {ex.Message}");
            }
        }

        // ==========================================
        // 6. ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ==========================================
        private string GetBaseLabel()
        {
            try
            {
                // Пытаемся получить название из def
                if (this.parent.def != null && !string.IsNullOrEmpty(this.parent.def.label))
                {
                    return this.parent.def.label;
                }

                // Если def нет или label пустой, используем defName
                if (this.parent.def != null && !string.IsNullOrEmpty(this.parent.def.defName))
                {
                    return this.parent.def.defName;
                }

                return "Prosthetic";
            }
            catch
            {
                return "Prosthetic";
            }
        }

        // ==========================================
        // 7. ОТОБРАЖЕНИЕ В ИНТЕРФЕЙСЕ
        // ==========================================
        public override string CompTipStringExtra
        {
            get
            {
                try
                {
                    // Используем сгенерированное название или базовое
                    string displayName = !string.IsNullOrEmpty(generatedLabel) ? generatedLabel : GetBaseLabel();

                    string text = $"\n\n=== {displayName} ===";
                    text += $"\n  Манипуляция: +{generatedManipulation * 100:F0}%";
                    text += $"\n  Сознание: {generatedConsciousness * 100:F0}%";
                    text += $"\n  Красота: {generatedBeauty:F2}";
                    text += $"\n  Порог срыва: {generatedMentalBreak:F2}";
                    text += $"\n  Боль: {generatedPain:F2}";
                    text += $"\n  Голод: x{generatedHunger:F2}";
                    return text;
                }
                catch
                {
                    return "";
                }
            }
        }
    }
}
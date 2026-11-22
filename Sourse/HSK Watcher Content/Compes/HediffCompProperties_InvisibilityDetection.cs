using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Reflection;

namespace HSK.Watcher.Content.Compes
{
    public class HediffCompProperties_InvisibilityDetection : HediffCompProperties
    {
        public HediffCompProperties_InvisibilityDetection()
        {
            this.compClass = typeof(HediffComp_InvisibilityDetection);
        }

        public float detectionRadius = 15f;
        public float detectionChance = 0.8f;
    }

    public class HediffComp_InvisibilityDetection : HediffComp
    {
        public HediffCompProperties_InvisibilityDetection Props =>
            (HediffCompProperties_InvisibilityDetection)this.props;

        private int updateTick = 0;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (this.parent?.pawn == null || this.parent.pawn.Dead)
                return;

            updateTick++;
            if (updateTick < 30) return;
            updateTick = 0;

            DetectInvisibleEntities();
        }

        private void DetectInvisibleEntities()
        {
            var pawn = this.parent.pawn;
            if (pawn.Map == null) return;

            foreach (var target in pawn.Map.mapPawns.AllPawnsSpawned)
            {
                if (target == pawn || target.Dead)
                    continue;

                float distSq = pawn.Position.DistanceToSquared(target.Position);
                float radiusSq = Props.detectionRadius * Props.detectionRadius;

                if (distSq > radiusSq)
                    continue;

                if (IsInvisible(target))
                {
                    if (Rand.Chance(Props.detectionChance))
                    {
                        DisruptInvisibility(target);
                        break;
                    }
                }
            }
        }

        private bool IsInvisible(Pawn pawn)
        {
            if (pawn.health?.hediffSet == null)
                return false;

            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.def.defName == "HoraxianInvisibility" ||
                    hediff.def.defName == "PsychicInvisibility")
                {
                    return true;
                }

                var invisComp = hediff.TryGetComp<HediffComp_Invisibility>();
                if (invisComp != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void DisruptInvisibility(Pawn target)
        {
            if (target.health?.hediffSet == null)
                return;

            // Собираем хедиффы для удаления в отдельный список
            List<Hediff> toRemove = new List<Hediff>();
            List<HediffComp_Invisibility> toDisrupt = new List<HediffComp_Invisibility>();

            foreach (var hediff in target.health.hediffSet.hediffs)
            {
                if (hediff.def.defName == "HoraxianInvisibility" ||
                    hediff.def.defName == "PsychicInvisibility")
                {
                    toRemove.Add(hediff);
                    continue;
                }

                var invisComp = hediff.TryGetComp<HediffComp_Invisibility>();
                if (invisComp != null)
                {
                    toDisrupt.Add(invisComp);
                }
            }

            // Удаляем собранные хедиффы
            foreach (var hediff in toRemove)
            {
                target.health.RemoveHediff(hediff);
            }

            // Прерываем компоненты невидимости
            foreach (var invisComp in toDisrupt)
            {
                try
                {
                    var disruptMethod = typeof(HediffComp_Invisibility).GetMethod("Disrupt",
                        BindingFlags.Public | BindingFlags.Instance);

                    if (disruptMethod != null)
                    {
                        disruptMethod.Invoke(invisComp, null);
                    }
                    else
                    {
                        // Если нет метода Disrupt, удаляем хедифф
                        var parentHediff = invisComp.parent;
                        if (parentHediff != null && parentHediff.pawn != null)
                        {
                            parentHediff.pawn.health.RemoveHediff(parentHediff);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[AbnormalEyes] Error disrupting invisibility: {ex.Message}");
                }
            }

            if (toRemove.Count > 0 || toDisrupt.Count > 0)
            {
                Messages.Message(
                    $"{this.parent.pawn.Name} detected and disrupted {target.Name}'s invisibility!",
                    target,
                    MessageTypeDefOf.PositiveEvent
                );

                Log.Message($"[AbnormalEyes] {this.parent.pawn.Name} disrupted invisibility of {target.Name}");
            }
        }

        public override string CompTipStringExtra
        {
            get
            {
                try
                {
                    if (this.parent?.pawn == null)
                        return "";

                    return $"\n\n{"HSK.AbnormalEyes.DetectsInvisible".Translate()} ({Props.detectionRadius} {"HSK.AbnormalEyes.Units".Translate()})";
                }
                catch
                {
                    return "";
                }
            }
        }
    }
}
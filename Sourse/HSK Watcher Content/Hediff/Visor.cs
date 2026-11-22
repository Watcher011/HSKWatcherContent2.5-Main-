using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSK.Watcher.VisorUniqueSystem
{
    // ============================================
    // RENDER NODE PROPERTIES
    // ============================================
    public class PawnRenderNodeProperties_Visor : PawnRenderNodeProperties
    {
        public PawnRenderNodeProperties_Visor()
        {
            visibleFacing = new List<Rot4>
            {
                Rot4.North,
                Rot4.South,
                Rot4.East,
                Rot4.West
            };
            workerClass = typeof(PawnRenderNodeWorker_Visor);
            nodeClass = typeof(PawnRenderNode);
        }
    }

    // ============================================
    // RENDER WORKER
    // ============================================
    public class PawnRenderNodeWorker_Visor : PawnRenderNodeWorker
    {
        private static HediffDef _visorDefCache;

        private static HediffDef VisorDef
        {
            get
            {
                if (_visorDefCache == null)
                {
                    _visorDefCache = DefDatabase<HediffDef>.GetNamedSilentFail("HSK_WatcherVisor");
                }
                return _visorDefCache;
            }
        }

        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
            {
                return false;
            }

            if (parms.pawn == null || parms.pawn.Dead)
            {
                return false;
            }

            HediffDef def = VisorDef;
            return def != null && parms.pawn.health.hediffSet.HasHediff(def);
        }
    }

    // ============================================
    // RECIPE: Установка импланта через Мозг
    // ============================================
    public class Recipe_InstallWatcherVisor : Recipe_Surgery
    {
        private static HediffDef _eyeDefCache;
        private static bool _eyeDefChecked;

        private static HediffDef EyeDef
        {
            get
            {
                if (!_eyeDefChecked)
                {
                    _eyeDefCache = DefDatabase<HediffDef>.GetNamedSilentFail("HSK_WatcherVisorEye");
                    _eyeDefChecked = true;
                }
                return _eyeDefCache;
            }
        }

        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (pawn.health.hediffSet.HasHediff(recipe.addsHediff))
            {
                yield break;
            }

            BodyPartDef brainDef = DefDatabase<BodyPartDef>.GetNamedSilentFail("Brain");
            if (brainDef == null)
            {
                yield break;
            }

            foreach (BodyPartRecord brain in pawn.RaceProps.body.GetPartsWithDef(brainDef))
            {
                if (!pawn.health.hediffSet.PartIsMissing(brain))
                {
                    yield return brain;
                }
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer != null)
            {
                if (CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }

                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            }

            // 1. Устанавливаем главный хеддиф в Мозг
            if (part != null && !pawn.health.hediffSet.HasHediff(recipe.addsHediff, part))
            {
                pawn.health.AddHediff(recipe.addsHediff, part);
            }

            // 2. Устанавливаем хеддифы-индикаторы в оба глаза
            IEnumerable<BodyPartRecord> eyeParts = pawn.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Eye);
            List<BodyPartRecord> eyes = eyeParts.ToList();
            HediffDef secondary = EyeDef ?? recipe.addsHediff;

            foreach (BodyPartRecord eye in eyes)
            {
                if (!pawn.health.hediffSet.PartIsMissing(eye))
                {
                    if (billDoer != null && billDoer.Map != null)
                    {
                        MedicalRecipesUtility.SpawnNaturalPartIfClean(pawn, eye, billDoer.Position, billDoer.Map);
                        MedicalRecipesUtility.SpawnThingsFromHediffs(pawn, eye, billDoer.Position, billDoer.Map);
                    }

                    pawn.TakeDamage(new DamageInfo(DamageDefOf.SurgicalCut, 99999f, 999f, -1f, null, eye));
                }

                IEnumerable<Hediff> allHediffs = pawn.health.hediffSet.hediffs;
                List<Hediff> leftovers = allHediffs
                    .Where(h => h.Part == eye && h is Hediff_AddedPart)
                    .ToList();

                foreach (Hediff leftover in leftovers)
                {
                    pawn.health.RemoveHediff(leftover);
                }

                if (pawn.health.hediffSet.PartIsMissing(eye))
                {
                    pawn.health.RestorePart(eye, null, true);
                }

                pawn.health.AddHediff(secondary, eye);
            }
        }
    }

    // ============================================
    // УТИЛИТА СВЯЗИ (Контроль целостности)
    // ============================================
    public static class VisorLinkUtility
    {
        public static bool IsVisorDef(HediffDef def)
        {
            return def != null && (def.defName == "HSK_WatcherVisor" || def.defName == "HSK_WatcherVisorEye");
        }

        public static bool NeedsResolve(Pawn pawn, Hediff visor)
        {
            if (pawn == null || visor == null || visor.pawn == null)
            {
                return false;
            }

            if (pawn.health.InPainShock || pawn.stances?.curStance is Stance_Busy)
            {
                return false;
            }

            IEnumerable<Hediff> pawnHediffs = pawn.health.hediffSet.hediffs;
            bool hasMain = pawnHediffs.Any(h => h.def.defName == "HSK_WatcherVisor");

            IEnumerable<BodyPartRecord> eyeParts = pawn.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Eye);
            List<BodyPartRecord> eyes = eyeParts.ToList();

            int eyeCountWithIndicator = eyes.Count(e => pawnHediffs.Any(h => h.Part == e && h.def.defName == "HSK_WatcherVisorEye"));

            if (!hasMain || eyeCountWithIndicator < eyes.Count)
            {
                return true;
            }

            return false;
        }

        public static void Resolve(Hediff visor)
        {
            Pawn pawn = visor?.pawn;
            if (pawn == null || pawn.Dead || pawn.health == null)
            {
                return;
            }

            IEnumerable<Hediff> pawnHediffs = pawn.health.hediffSet.hediffs;
            List<Hediff> allVisors = pawnHediffs
                .Where(h => IsVisorDef(h.def))
                .ToList();

            if (allVisors.Count == 0)
            {
                return;
            }

            List<BodyPartRecord> eyePartsToDamage = allVisors
                .Where(h => h.Part != null && h.Part.def == BodyPartDefOf.Eye && !pawn.health.hediffSet.PartIsMissing(h.Part))
                .Select(h => h.Part)
                .Distinct()
                .ToList();

            foreach (var h in allVisors)
            {
                pawn.health.RemoveHediff(h);
            }

            foreach (var part in eyePartsToDamage)
            {
                pawn.health.AddHediff(HediffDefOf.MissingBodyPart, part);
            }
        }
    }

    public class HediffCompProperties_VisorLink : HediffCompProperties
    {
        public HediffCompProperties_VisorLink()
        {
            compClass = typeof(HediffComp_VisorLink);
        }
    }

    public class HediffComp_VisorLink : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || !pawn.IsHashIntervalTick(30))
            {
                return;
            }

            if (VisorLinkUtility.NeedsResolve(pawn, parent))
            {
                GameComponent_VisorLink.Enqueue(parent);
            }
        }
    }

    public class GameComponent_VisorLink : GameComponent
    {
        private static readonly List<Hediff> _pendingVisors = new List<Hediff>();

        public GameComponent_VisorLink(Game game) { }

        public static void Enqueue(Hediff visor)
        {
            if (!_pendingVisors.Contains(visor))
            {
                _pendingVisors.Add(visor);
            }
        }

        public override void GameComponentTick()
        {
            if (_pendingVisors.Count == 0)
            {
                return;
            }

            List<Hediff> work = new List<Hediff>(_pendingVisors);
            _pendingVisors.Clear();

            foreach (Hediff visor in work)
            {
                VisorLinkUtility.Resolve(visor);
            }
        }
    }
}
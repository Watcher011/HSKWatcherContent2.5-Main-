//using RimWorld;
//using Verse;

//namespace HSK.Watcher.Content
//{
//    public class Hediff_WildAIOverride : HediffWithComps
//    {
//        public override void PostAdd(DamageInfo? dinfo)
//        {
//            base.PostAdd(dinfo);
//            SuppressOtherAugmentations();
//        }

//        public override void Tick()
//        {
//            base.Tick();
//            // Каждые несколько тиков принудительно зачищаем сторонние боевые баффы и оверклокинги, 
//            // так как ИСКИН «отключает» любые усиления.
//            if (pawn.IsHashIntervalTick(250))
//            {
//                SuppressOtherAugmentations();
//            }
//        }

//        private void SuppressOtherAugmentations()
//        {
//            if (pawn?.health?.hediffSet == null) return;

//            // Удаляем боевой бафф киберпсихоза, если он активен, так как ИСКИН глушит оверклокинг
//            Hediff combatBuff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("CyberPsychosisCombatBuff"));
//            if (combatBuff != null)
//            {
//                pawn.health.RemoveHediff(combatBuff);
//                CyberPsychosisLogger.Log($"[WildAI] Suppressed cybernetic overdrive on {pawn.Name} due to Wild AI override.");
//            }
//        }
//    }
//}
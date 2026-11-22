using RimWorld;
using Verse;

namespace HSK.Watcher.Content
{
    public class CompProperties_SpawnCorpse : CompProperties
    {
        public PawnKindDef pawnKind;
        public int count = 1;

        // На сколько дней трупы "перегнили" сверх начала гниения.
        // 0 = только начали гнить, 5 = сильно разложившиеся.
        // Иссушенными (dessicated) станут примерно после 7.5 дней сверх начала.
        public float extraRotDays = 3f;

        public CompProperties_SpawnCorpse()
        {
            compClass = typeof(CompSpawnCorpse);
        }
    }

    public class CompSpawnCorpse : ThingComp
    {
        private CompProperties_SpawnCorpse Props => (CompProperties_SpawnCorpse)props;

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (previousMap == null || Props.pawnKind == null) return;

            IntVec3 pos = parent.Position;
            for (int i = 0; i < Props.count; i++)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(Props.pawnKind, null);
                GenSpawn.Spawn(pawn, pos, previousMap);
                pawn.Kill(null);

                Corpse corpse = pawn.Corpse;
                CompRottable rot = corpse?.GetComp<CompRottable>();
                if (rot != null)
                {
                    rot.RotProgress = rot.PropsRot.TicksToRotStart
                                      + Props.extraRotDays * 60000f;
                }
            }
        }
    }
}
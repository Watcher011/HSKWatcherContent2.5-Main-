using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace HSK.Watcher.Content.Buildings
{
    public class Building_PrimitiveRadioStation : Building_CommsConsole
    {
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn myPawn)
        {
            // Проверяем возможность использования
            if (!CanUseCommsNow)
            {
                yield return new FloatMenuOption("Cannot use radio: no power", null);
                yield break;
            }

            if (!myPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Some))
            {
                yield return new FloatMenuOption("CannotUseNoPath".Translate(), null);
                yield break;
            }

            // Получаем только наземные фракции (без космических кораблей)
            var commTargets = Find.FactionManager.AllFactionsVisibleInViewOrder
                .Where(f => !f.temporary && !f.IsPlayer && !f.def.hidden)
                .Cast<ICommunicable>();

            foreach (ICommunicable commTarget in commTargets)
            {
                FloatMenuOption option = commTarget.CommFloatMenuOption(this, myPawn);
                if (option != null)
                {
                    yield return option;
                }
            }
        }

        // Переопределяем метод получения целей связи
        public new IEnumerable<ICommunicable> GetCommTargets(Pawn myPawn)
        {
            return Find.FactionManager.AllFactionsVisibleInViewOrder
                .Where(f => !f.temporary && !f.IsPlayer && !f.def.hidden)
                .Cast<ICommunicable>();
        }
    }
}
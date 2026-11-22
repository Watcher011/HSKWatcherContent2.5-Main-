using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSK.Watcher.Content
{
    // Один визит сборщика податей.
    public class TributeVisit : IExposable
    {
        public List<Pawn> traders = new List<Pawn>();
        public int mapId = -1;
        public bool favorGained;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref traders, "traders", LookMode.Reference);
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref favorGained, "favorGained", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (traders == null)
                {
                    traders = new List<Pawn>();
                }
                traders.RemoveAll(p => p == null);
            }
        }
    }

    // Следит за визитами: если сборщик ушёл, а очки привилегий (favor) Империи
    // за визит не начислялись - штраф к отношениям с Империей.
    public class GameComponent_TributeCollector : GameComponent
    {
        // Штраф к отношениям с Империей (отрицательное число).
        public static int GoodwillPenalty = -10;
        private const int CheckInterval = 250;

        private List<TributeVisit> visits = new List<TributeVisit>();

        public GameComponent_TributeCollector(Game game)
        {
        }

        public static GameComponent_TributeCollector Instance
        {
            get { return Current.Game == null ? null : Current.Game.GetComponent<GameComponent_TributeCollector>(); }
        }

        public void Register(Map map, IEnumerable<Pawn> pawns)
        {
            List<Pawn> traders = pawns.Where(p => p != null && p.TraderKind != null).ToList();
            if (map == null || traders.Count == 0)
            {
                return;
            }
            visits.Add(new TributeVisit { traders = traders, mapId = map.uniqueID });
        }

        // Вызывается из патча GainFavor.
        public void NotifyFavorGained()
        {
            for (int i = 0; i < visits.Count; i++)
            {
                visits[i].favorGained = true;
            }
        }

        public override void GameComponentTick()
        {
            if (visits.Count == 0 || Find.TickManager.TicksGame % CheckInterval != 0)
            {
                return;
            }
            for (int i = visits.Count - 1; i >= 0; i--)
            {
                TributeVisit v = visits[i];
                if (!IsFinished(v))
                {
                    continue;
                }
                visits.RemoveAt(i);
            }
        }

        // true - визит закончен (штраф применён или отменён), запись можно удалять.
        private bool IsFinished(TributeVisit v)
        {
            Faction empire = Faction.OfEmpire;
            if (empire == null || v.traders.Count == 0 || !Find.Maps.Any(m => m.uniqueID == v.mapId))
            {
                return true;
            }
            // убит, захвачен или перешёл к игроку - штрафа нет
            if (v.traders.Any(p => p.Dead || p.Destroyed || p.Faction != empire))
            {
                return true;
            }
            // всё ещё на карте
            if (v.traders.Any(p => p.Spawned))
            {
                return false;
            }
            // все сборщики ушли
            if (!v.favorGained && !empire.HostileTo(Faction.OfPlayer))
            {
                ApplyPenalty(empire);
            }
            return true;
        }

        private void ApplyPenalty(Faction empire)
        {
            Faction.OfPlayer.TryAffectGoodwillWith(empire, GoodwillPenalty, canSendMessage: false, canSendHostilityLetter: true);
            Find.LetterStack.ReceiveLetter(
                "HSK_TributeCollectorEmptyLabel".Translate(),
                "HSK_TributeCollectorEmptyText".Translate(empire.Named("FACTION"), (-GoodwillPenalty).Named("PENALTY")),
                LetterDefOf.NegativeEvent);
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref visits, "tributeVisits", LookMode.Deep);
            if (visits == null)
            {
                visits = new List<TributeVisit>();
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class TributeCollectorPatches
    {
        static TributeCollectorPatches()
        {
            Harmony harmony = new Harmony("HSK.Watcher.TributeCollectorPenalty");
            harmony.Patch(
                AccessTools.Method(typeof(Pawn_RoyaltyTracker), "GainFavor"),
                postfix: new HarmonyMethod(typeof(TributeCollectorPatches), nameof(GainFavorPostfix)));
        }

        public static void GainFavorPostfix(Faction faction, int amount)
        {
            if (amount > 0 && faction != null && faction == Faction.OfEmpire)
            {
                GameComponent_TributeCollector comp = GameComponent_TributeCollector.Instance;
                if (comp != null)
                {
                    comp.NotifyFavorGained();
                }
            }
        }
    }
}
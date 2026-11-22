using System.Linq;
using RimWorld;
using Verse;

namespace HSK.Watcher.GameComponents
{
    public class GameComponent_AbominationWatcher : GameComponent
    {
        private int lastCheckTick = -1;
        private bool eventTriggered = false;
        private bool gameEndScheduled = false;
        private int gameEndTick = 0;

        private const int CheckIntervalTicks = 6000; // 6000 тиков = 100 секунд
        private const int AbominationLimit = 12;
        private const int DelayBeforeGameEnd = 300; // 300 тиков = 5 секунд задержки
        private const string AbominationDefName = "Abomination";

        public GameComponent_AbominationWatcher(Game game) { }

        public override void GameComponentTick()
        {
            // Если конец игры запланирован, проверяем не пора ли завершить
            if (gameEndScheduled)
            {
                if (Find.TickManager.TicksGame >= gameEndTick)
                {
                    gameEndScheduled = false;
                    GameOver();
                }
                return;
            }

            if (eventTriggered) return;

            if (Find.TickManager.TicksGame % CheckIntervalTicks != 0) return;

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick == lastCheckTick) return;

            lastCheckTick = currentTick;
            CheckForAbominationOverrun();
        }

        private void CheckForAbominationOverrun()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            int count = map.mapPawns.AllPawnsSpawned
                .Count(p => p.def.defName == AbominationDefName && !p.Dead);

            if (count > AbominationLimit)
            {
                TriggerGameEnd(map, count);
                eventTriggered = true;
            }
        }

        private void TriggerGameEnd(Map map, int count)
        {
            if (map == null) return;

            // Формируем текст письма с подстановкой числа
            string letterLabel = "HSK.AbominationOverrun.LetterLabel".Translate();
            string letterTextRaw = "HSK.AbominationOverrun.LetterText".Translate();
            string letterText = string.Format(letterTextRaw, count);

            // Отправляем письмо
            Find.LetterStack.ReceiveLetter(
                letterLabel,
                letterText,
                LetterDefOf.ThreatBig
            );

            // Планируем конец игры через N тиков
            gameEndScheduled = true;
            gameEndTick = Find.TickManager.TicksGame + DelayBeforeGameEnd;
        }

        private void GameOver()
        {
            // Показываем диалог перед выходом
            DiaNode dialogNode = new DiaNode("HSK.AbominationOverrun.DialogText".Translate());
            DiaOption endGameOption = new DiaOption("HSK.AbominationOverrun.EndButton".Translate());
            endGameOption.resolveTree = true;
            endGameOption.action = () => {
                // Убиваем всех пешек
                if (Current.Game?.Maps != null)
                {
                    foreach (Map m in Current.Game.Maps.ToList())
                    {
                        if (m != null)
                        {
                            foreach (Pawn pawn in m.mapPawns.AllPawns.ToList())
                            {
                                if (!pawn.Dead)
                                    pawn.Kill(null);
                            }
                        }
                    }
                }
                GenScene.GoToMainMenu();
            };
            dialogNode.options.Add(endGameOption);
            Find.WindowStack.Add(new Dialog_NodeTree(dialogNode, true, false, null));
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref lastCheckTick, "lastCheckTick", -1);
            Scribe_Values.Look(ref eventTriggered, "eventTriggered", false);
            Scribe_Values.Look(ref gameEndScheduled, "gameEndScheduled", false);
            Scribe_Values.Look(ref gameEndTick, "gameEndTick", 0);
        }
    }
}
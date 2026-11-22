// ============================================================
// Cannibal Frenzy — нервный срыв у каннибала, давно не евшего человечину.
// Каннибал нападает на колониста; когда тот упал — у него удаляется часть тела.
// Совместимо с C# 7.3 (без ??=, switch-выражений и т.п.).
//
// ОТЛАДКА: подробный лог пишется, пока CannibalFrenzyDebug.Enabled = true.
// Выключить на лету: Dev Mode → Debug actions → «Watcher / Cannibal» →
// «Toggle debug log». Выключить насовсем — поставить false в CannibalFrenzyDebug.
// ============================================================

using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI;

namespace HSK.Watcher.Content
{
    /// <summary>
    /// Отладочный лог каннибальского срыва с общим выключателем.
    /// Предупреждения о реальных сбоях (Log.Warning / Log.Error) от выключателя
    /// не зависят — они пишутся всегда.
    /// </summary>
    public static class CannibalFrenzyDebug
    {
        // Главный выключатель отладки. true — подробный лог в консоль.
        // Значение по умолчанию при запуске игры; на лету меняется debug action'ом.
        public static bool Enabled = true;

        public static void Msg(string message)
        {
            if (!Enabled) return;
            Log.Message("[CannibalFrenzy] " + message);
        }

        public static string Name(Pawn pawn)
        {
            return pawn != null ? pawn.LabelShort : "null";
        }
    }

    /// <summary>
    /// Поиск цели и выбор части тела для удаления.
    /// </summary>
    public static class CannibalFrenzyUtility
    {
        // Какие части тела может потерять жертва (уровень 1). Сравниваем не defName
        // целиком, а его «слова» (CamelCase / подчёркивания разбираются на токены), —
        // так подходят и "Arm", и "LeftArm", и "SK_Arm_Left", но НЕ "Heart"
        // (в нём нет токена "ear"). Кисти/стопы отдельно не нужны: они пропадают
        // вместе с рукой/ногой.
        private static readonly HashSet<string> RemovableTokens = new HashSet<string>
        {
            "shoulder", "arm", "leg", "eye", "ear", "tongue", "kidney", "lung"
        };

        // Парные органы удаляем только если останется второй — чтобы не убить жертву сразу.
        private static readonly HashSet<string> PairedTokens = new HashSet<string>
        {
            "kidney", "lung"
        };

        /// <summary>
        /// Разбивает defName на слова в нижнем регистре: "LeftArm" -> left, arm.
        /// </summary>
        private static List<string> Tokens(string defName)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(defName)) return result;

            System.Text.RegularExpressions.MatchCollection matches =
                System.Text.RegularExpressions.Regex.Matches(defName, "[A-Z]+(?![a-z])|[A-Z]?[a-z]+|[0-9]+");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                result.Add(match.Value.ToLowerInvariant());
            }

            return result;
        }

        private static bool HasToken(BodyPartDef def, HashSet<string> tokens)
        {
            if (def == null) return false;

            List<string> words = Tokens(def.defName);
            for (int i = 0; i < words.Count; i++)
            {
                if (tokens.Contains(words[i])) return true;
            }

            return false;
        }

        // Детей не трогаем.
        private const float MinVictimAgeYears = 14f;

        // В TraitDefOf поля Cannibal может не быть (зависит от версии), поэтому
        // ищем черту по имени и кэшируем.
        private static TraitDef cannibalTrait;
        private static bool cannibalTraitResolved;

        public static bool IsCannibal(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null) return false;

            if (!cannibalTraitResolved)
            {
                cannibalTraitResolved = true;
                cannibalTrait = DefDatabase<TraitDef>.GetNamedSilentFail("Cannibal");
                CannibalFrenzyDebug.Msg("черта «Cannibal» " + (cannibalTrait != null ? "найдена" : "НЕ найдена"));
                if (cannibalTrait == null)
                {
                    Log.Warning("[Watcher] CannibalFrenzy: не найдена черта характера «Cannibal» — " +
                        "срыв происходить не будет.");
                }
            }

            return cannibalTrait != null && pawn.story.traits.HasTrait(cannibalTrait);
        }

        public static bool IsValidTarget(Pawn cannibal, Pawn target)
        {
            if (cannibal == null || target == null) return false;
            if (target == cannibal || target.Dead || target.Destroyed || !target.Spawned) return false;
            if (target.Map != cannibal.Map) return false;
            if (target.ageTracker != null && target.ageTracker.AgeBiologicalYearsFloat < MinVictimAgeYears) return false;
            return true;
        }

        /// <summary>
        /// Случайный достижимый колонист на той же карте (не упавший). Предпочитает
        /// тех, у кого нет черты Каннибал, но если таких нет — подойдёт любой.
        /// </summary>
        public static Pawn TryFindTarget(Pawn cannibal)
        {
            if (cannibal == null || cannibal.Map == null) return null;

            List<Pawn> preferred = new List<Pawn>();
            List<Pawn> fallback = new List<Pawn>();

            List<Pawn> colonists = cannibal.Map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn p = colonists[i];
                if (!IsValidTarget(cannibal, p) || p.Downed) continue;
                if (!cannibal.CanReach(p, PathEndMode.Touch, Danger.Deadly)) continue;

                if (IsCannibal(p)) fallback.Add(p);
                else preferred.Add(p);
            }

            List<Pawn> pool = preferred.Count > 0 ? preferred : fallback;
            if (pool.Count == 0) return null;
            return pool.RandomElement();
        }

        /// <summary>
        /// Выбирает часть тела из белого списка, которая ещё не отсутствует.
        /// </summary>
        public static BodyPartRecord PickPartToRemove(Pawn victim)
        {
            if (victim == null || victim.health == null || victim.health.hediffSet == null) return null;

            HediffSet hediffSet = victim.health.hediffSet;
            List<BodyPartRecord> notMissing = hediffSet.GetNotMissingParts().ToList();
            List<BodyPartRecord> candidates = new List<BodyPartRecord>();

            // Уровень 1: руки, ноги, глаза, уши, почки, лёгкие (по словам в defName).
            for (int i = 0; i < notMissing.Count; i++)
            {
                BodyPartRecord part = notMissing[i];
                if (part.def == null || !HasToken(part.def, RemovableTokens)) continue;

                if (HasToken(part.def, PairedTokens))
                {
                    int sameCount = notMissing.Count(p => p.def == part.def);
                    if (sameCount < 2) continue;
                }

                candidates.Add(part);
            }

            if (candidates.Count > 0)
            {
                BodyPartRecord chosen = candidates.RandomElement();
                CannibalFrenzyDebug.Msg("выбор части тела: defName=" + chosen.def.defName + ", название=" + chosen.Label
                    + ", кандидатов: " + candidates.Count);
                return chosen;
            }

            // Уровень 2 (запасной, не зависит от названий): «концевые» наружные части —
            // пальцы, уши, глаза, нос, челюсть и т. п. Они не жизненно важны: у корня тела
            // (parent == null) и у частей с вложенными частями (голова, шея, туловище)
            // дети есть, поэтому они сюда не попадают; внутренние органы — не Outside.
            for (int i = 0; i < notMissing.Count; i++)
            {
                BodyPartRecord part = notMissing[i];
                if (part.def == null || part.parent == null) continue;
                if (part.depth != BodyPartDepth.Outside) continue;
                if (part.parts != null && part.parts.Count > 0) continue;

                candidates.Add(part);
            }

            if (candidates.Count > 0)
            {
                BodyPartRecord chosen = candidates.RandomElement();
                CannibalFrenzyDebug.Msg("запасной выбор части тела: defName=" + chosen.def.defName + ", название=" + chosen.Label
                    + ", кандидатов: " + candidates.Count);
                return chosen;
            }

            // Ничего не нашли — пишем в лог, из чего состоит тело (это нужно знать, чтобы
            // поправить список). Предупреждение пишется всегда, не зависит от отладки.
            string bodyName = victim.RaceProps != null && victim.RaceProps.body != null
                ? victim.RaceProps.body.defName
                : "?";
            string partNames = string.Join(", ",
                notMissing.Where(p => p.def != null).Select(p => p.def.defName).Distinct().ToArray());
            Log.Warning("[Watcher] CannibalFrenzy: у " + victim.LabelShort + " не найдено ни одной части тела для откусывания. " +
                "Тело: " + bodyName + ". Части: " + partNames);
            return null;
        }

        // Сколько сытости (0..1) даёт одна «трапеза».
        private const float NutritionGain = 0.25f;

        /// <summary>
        /// Вызывается в конце работы «откусить»: удаляет выбранную часть тела,
        /// шлёт письмо, кормит каннибала, даёт ему «довольное» воспоминание и
        /// сбрасывает таймер голода. Флаг mealDone выставляется первым, чтобы срыв
        /// гарантированно закончился, даже если что-то дальше пойдёт не так.
        /// </summary>
        public static void FinishMeal(Pawn cannibal, Pawn victim, BodyPartRecord part)
        {
            if (cannibal == null) return;

            MentalState_CannibalFrenzy state = cannibal.MentalState as MentalState_CannibalFrenzy;
            if (state != null)
            {
                state.mealDone = true;
            }

            CannibalFrenzyDebug.Msg("FinishMeal: каннибал=" + CannibalFrenzyDebug.Name(cannibal)
                + ", жертва=" + CannibalFrenzyDebug.Name(victim)
                + ", срыв найден=" + (state != null));

            if (victim == null || victim.Dead || victim.health == null || victim.health.hediffSet == null)
            {
                CannibalFrenzyDebug.Msg("FinishMeal: жертва мертва или недоступна — часть тела не удаляем.");
                return;
            }

            // Часть могла пропасть, пока шла трапеза, — тогда берём другую.
            BodyPartRecord toRemove = part;
            if (toRemove == null || !victim.health.hediffSet.GetNotMissingParts().Contains(toRemove))
            {
                toRemove = PickPartToRemove(victim);
            }

            CannibalFrenzyDebug.Msg("FinishMeal: часть тела для удаления — "
                + (toRemove != null ? toRemove.Label : "нет подходящей"));

            if (toRemove != null)
            {
                string partLabel = toRemove.Label;
                victim.health.AddHediff(HediffDefOf.MissingBodyPart, toRemove);

                Find.LetterStack.ReceiveLetter(
                    "Watcher_CannibalFrenzy_MutilationLabel".Translate(),
                    "Watcher_CannibalFrenzy_MutilationText".Translate(cannibal.LabelShortCap, victim.LabelShortCap, partLabel),
                    LetterDefOf.NegativeEvent,
                    new LookTargets(victim)
                );
            }

            // Сытость.
            if (cannibal.needs != null && cannibal.needs.food != null)
            {
                Need_Food food = cannibal.needs.food;
                float foodBefore = food.CurLevel;
                food.CurLevel = System.Math.Min(food.MaxLevel, food.CurLevel + NutritionGain);
                CannibalFrenzyDebug.Msg(string.Format("FinishMeal: сытость {0:F2} -> {1:F2}", foodBefore, food.CurLevel));
            }

            // Воспоминание «ел человечину» (для каннибала — приятное).
            ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("AteHumanlikeMeatDirectCannibal");
            if (thought == null)
            {
                thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("AteHumanlikeMeatDirect");
            }

            CannibalFrenzyDebug.Msg("FinishMeal: воспоминание — "
                + (thought != null ? thought.defName : "ThoughtDef не найден"));

            if (thought != null && cannibal.needs != null && cannibal.needs.mood != null
                && cannibal.needs.mood.thoughts != null && cannibal.needs.mood.thoughts.memories != null)
            {
                cannibal.needs.mood.thoughts.memories.TryGainMemory(thought);
            }

            // Сброс счётчика «давно не ел человечину».
            GameComponent_CannibalHunger hunger = Current.Game != null
                ? Current.Game.GetComponent<GameComponent_CannibalHunger>()
                : null;
            if (hunger != null)
            {
                hunger.NotifyAteHuman(cannibal);
            }
            else
            {
                CannibalFrenzyDebug.Msg("FinishMeal: GameComponent_CannibalHunger не найден, таймер голода не сброшен.");
            }
        }
    }

    /// <summary>
    /// Нужен, чтобы срыв не мог начаться, если нет подходящей цели.
    /// </summary>
    public class MentalStateWorker_CannibalFrenzy : MentalStateWorker
    {
        public override bool StateCanOccur(Pawn pawn)
        {
            return base.StateCanOccur(pawn) && CannibalFrenzyUtility.TryFindTarget(pawn) != null;
        }
    }

    /// <summary>
    /// Сам срыв. Логика шага вызывается из GameComponent_CannibalHunger (каждые
    /// несколько тиков) — так не нужно переопределять MentalStateTick, сигнатура
    /// которого отличается между версиями RimWorld.
    /// </summary>
    public class MentalState_CannibalFrenzy : MentalState
    {
        public Pawn target;

        // true, когда каннибал закончил есть (работа Watcher_CannibalEatBodyPart
        // выставляет это в конце) — только после этого срыв может завершиться.
        public bool mealDone;

        // Сколько раз пытались запустить работу «откусить». Страховка от зацикливания,
        // если работу что-то постоянно прерывает (например, жертву уже спасают).
        private int eatAttempts;
        private const int MaxEatAttempts = 5;

        private const string EatJobDefName = "Watcher_CannibalEatBodyPart";

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref target, "cannibalFrenzyTarget");
            Scribe_Values.Look(ref mealDone, "cannibalFrenzyMealDone", false);
            Scribe_Values.Look(ref eatAttempts, "cannibalFrenzyEatAttempts", 0);
        }

        public void DoFrenzyStep()
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned) return;

            if (pawn.Downed)
            {
                CannibalFrenzyDebug.Msg(CannibalFrenzyDebug.Name(pawn) + ": каннибал сам упал — срыв завершается.");
                RecoverFromState();
                return;
            }

            // Выбираем (или заменяем) цель.
            if (!CannibalFrenzyUtility.IsValidTarget(pawn, target))
            {
                target = CannibalFrenzyUtility.TryFindTarget(pawn);
                if (target == null)
                {
                    CannibalFrenzyDebug.Msg(CannibalFrenzyDebug.Name(pawn) + ": подходящей цели нет — срыв завершается.");
                    RecoverFromState();
                    return;
                }

                CannibalFrenzyDebug.Msg(CannibalFrenzyDebug.Name(pawn) + ": выбрана цель " + CannibalFrenzyDebug.Name(target));
            }

            // Жертва упала — каннибал не уходит, а откусывает часть тела отдельной
            // работой с прогресс-баром. Пока он ест, срыв не заканчивается, поэтому
            // он не бросает жертву и не несёт её в кровать.
            if (target.Downed)
            {
                if (mealDone)
                {
                    CannibalFrenzyDebug.Msg(CannibalFrenzyDebug.Name(pawn) + ": доел — срыв завершается.");
                    RecoverFromState();
                    return;
                }

                Job running = pawn.CurJob;
                if (running != null && running.def != null && running.def.defName == EatJobDefName)
                {
                    return; // уже ест
                }

                JobDef eatJobDef = DefDatabase<JobDef>.GetNamedSilentFail(EatJobDefName);
                if (eatJobDef == null)
                {
                    Log.Warning("[Watcher] CannibalFrenzy: не найден JobDef «" + EatJobDefName + "» — " +
                        "каннибал не сможет откусить часть тела, срыв завершается.");
                    RecoverFromState();
                    return;
                }

                if (eatAttempts >= MaxEatAttempts)
                {
                    CannibalFrenzyDebug.Msg(CannibalFrenzyDebug.Name(pawn) + ": исчерпаны попытки начать трапезу ("
                        + MaxEatAttempts + ") — срыв завершается.");
                    RecoverFromState();
                    return;
                }

                eatAttempts++;
                CannibalFrenzyDebug.Msg(CannibalFrenzyDebug.Name(pawn) + ": " + CannibalFrenzyDebug.Name(target)
                    + " упал(а) — запускаю работу «откусить», попытка " + eatAttempts + "/" + MaxEatAttempts);
                Job eatJob = JobMaker.MakeJob(eatJobDef, target);
                pawn.jobs.StartJob(eatJob, JobCondition.InterruptForced);
                return;
            }

            // Продолжаем атаку.
            Job cur = pawn.CurJob;
            if (cur == null || cur.def != JobDefOf.AttackMelee || cur.targetA.Thing != target)
            {
                CannibalFrenzyDebug.Msg(CannibalFrenzyDebug.Name(pawn) + ": атакует " + CannibalFrenzyDebug.Name(target));
                Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
                job.canBashDoors = true;
                pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            }
        }
    }

    /// <summary>
    /// Работа «откусить часть тела»: каннибал подходит к упавшему, некоторое время
    /// ест (идёт прогресс-бар, на полу появляется кровь), и только в конце у жертвы
    /// пропадает часть тела. Жертва резервируется, чтобы её не унесли в кровать
    /// другие колонисты, пока идёт «трапеза».
    /// </summary>
    public class JobDriver_CannibalEatBodyPart : JobDriver
    {
        private const TargetIndex VictimInd = TargetIndex.A;

        // Сколько тиков длится «трапеза» (2500 тиков = 1 игровой час).
        private const int EatDurationTicks = 600;
        // Как часто на полу рядом с жертвой появляется кровь.
        private const int BloodIntervalTicks = 120;

        private BodyPartRecord part;

        private Pawn Victim
        {
            get { return job.GetTarget(VictimInd).Thing as Pawn; }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            bool reserved = pawn.Reserve(Victim, job, 1, -1, null, errorOnFailed);
            CannibalFrenzyDebug.Msg(CannibalFrenzyDebug.Name(pawn) + ": резерв жертвы " + CannibalFrenzyDebug.Name(Victim)
                + (reserved ? " получен" : " НЕ получен (жертву уже кто-то спасает?)"));
            return reserved;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.AddFinishAction(delegate
            {
                Pawn v = Victim;
                string victimState = v == null ? "null" : (v.Dead ? "мертва" : (v.Downed ? "лежит" : "на ногах"));
                CannibalFrenzyDebug.Msg(CannibalFrenzyDebug.Name(pawn) + ": работа «откусить» закончена, жертва: " + victimState);
            });

            this.FailOnDespawnedOrNull(VictimInd);
            this.FailOn(delegate
            {
                Pawn v = Victim;
                return v == null || v.Dead || !v.Downed;
            });

            yield return Toils_Goto.GotoThing(VictimInd, PathEndMode.Touch);

            Toil eat = ToilMaker.MakeToil("CannibalEatBodyPart");
            eat.initAction = delegate
            {
                if (part == null)
                {
                    part = CannibalFrenzyUtility.PickPartToRemove(Victim);
                }

                // Если съедобной части не нашлось — короткая пауза и конец.
                pawn.jobs.curDriver.ticksLeftThisToil = part != null ? EatDurationTicks : 60;

                CannibalFrenzyDebug.Msg(CannibalFrenzyDebug.Name(pawn) + ": начинает есть, часть тела — "
                    + (part != null ? part.Label : "нет подходящей") + ", тиков: " + pawn.jobs.curDriver.ticksLeftThisToil);
            };
            eat.tickAction = delegate
            {
                Pawn v = Victim;
                if (v == null) return;

                pawn.rotationTracker.FaceTarget(v);

                if (pawn.IsHashIntervalTick(BloodIntervalTicks) && v.Map != null)
                {
                    FilthMaker.TryMakeFilth(v.Position, v.Map, ThingDefOf.Filth_Blood);
                }
            };
            eat.handlingFacing = true;
            eat.defaultCompleteMode = ToilCompleteMode.Delay;
            eat.WithProgressBarToilDelay(VictimInd);
            yield return eat;

            Toil finish = ToilMaker.MakeToil("CannibalFinishMeal");
            finish.initAction = delegate
            {
                CannibalFrenzyUtility.FinishMeal(pawn, Victim, part);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }

    /// <summary>
    /// Отслеживает, когда каждый каннибал последний раз ел человечину, запускает срыв
    /// и подгоняет шаги активных срывов. Создаётся движком автоматически (в т.ч. для
    /// уже существующих сейвов).
    /// </summary>
    public class GameComponent_CannibalHunger : GameComponent
    {
        // ---- Настройки ----
        private const int CheckIntervalTicks = 250;
        private const int FrenzyStepIntervalTicks = 15;
        // Сколько дней без человечины до начала риска срыва.
        private const int HungerThresholdDays = 12;
        // После порога срыв происходит в среднем раз в столько дней.
        private const float FrenzyMtbDays = 1.5f;

        private const string MentalStateDefName = "Watcher_CannibalFrenzy";

        private Dictionary<Pawn, int> lastAteHumanTick = new Dictionary<Pawn, int>();
        private List<Pawn> tmpKeys;
        private List<int> tmpValues;

        // Любое из этих воспоминаний значит, что пешка ела человеческое мясо
        // (напрямую или в составе блюда, с чертой Каннибал или без неё).
        private static readonly string[] HumanMeatThoughtDefNames =
        {
            "AteHumanlikeMeatDirect",
            "AteHumanlikeMeatDirectCannibal",
            "AteHumanlikeMeatAsIngredient",
            "AteHumanlikeMeatAsIngredientCannibal"
        };

        private static readonly List<ThoughtDef> humanMeatThoughts = new List<ThoughtDef>();
        private static bool thoughtsResolved;

        public GameComponent_CannibalHunger(Game game)
        {
        }

        /// <summary>
        /// Отметить, что каннибал только что ел человечину (сбрасывает отсчёт голода).
        /// </summary>
        public void NotifyAteHuman(Pawn pawn)
        {
            if (pawn == null) return;
            lastAteHumanTick[pawn] = Find.TickManager.TicksGame;
            CannibalFrenzyDebug.Msg("отсчёт голода сброшен для " + CannibalFrenzyDebug.Name(pawn));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref lastAteHumanTick, "lastAteHumanTick",
                LookMode.Reference, LookMode.Value, ref tmpKeys, ref tmpValues);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (lastAteHumanTick == null)
                {
                    lastAteHumanTick = new Dictionary<Pawn, int>();
                }

                List<Pawn> bad = lastAteHumanTick.Keys.Where(p => p == null || p.Dead || p.Destroyed).ToList();
                for (int i = 0; i < bad.Count; i++)
                {
                    lastAteHumanTick.Remove(bad[i]);
                }
            }
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            int now = Find.TickManager.TicksGame;

            if (now % FrenzyStepIntervalTicks == 0)
            {
                ProcessActiveFrenzies();
            }

            if (now % CheckIntervalTicks == 0)
            {
                CheckCannibals(now);
            }
        }

        private void ProcessActiveFrenzies()
        {
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                List<Pawn> colonists = new List<Pawn>(maps[m].mapPawns.FreeColonistsSpawned);
                for (int i = 0; i < colonists.Count; i++)
                {
                    MentalState_CannibalFrenzy state = colonists[i].MentalState as MentalState_CannibalFrenzy;
                    if (state != null)
                    {
                        state.DoFrenzyStep();
                    }
                }
            }
        }

        private void CheckCannibals(int now)
        {
            MentalStateDef frenzyDef = DefDatabase<MentalStateDef>.GetNamedSilentFail(MentalStateDefName);
            if (frenzyDef == null) return;

            ResolveThoughts();

            int thresholdTicks = HungerThresholdDays * GenDate.TicksPerDay;

            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                List<Pawn> colonists = new List<Pawn>(maps[m].mapPawns.FreeColonistsSpawned);
                for (int i = 0; i < colonists.Count; i++)
                {
                    Pawn pawn = colonists[i];
                    if (!CannibalFrenzyUtility.IsCannibal(pawn))
                        continue;

                    // Первый раз видим этого каннибала — даём полный запас времени.
                    int last;
                    if (!lastAteHumanTick.TryGetValue(pawn, out last))
                    {
                        last = now;
                        lastAteHumanTick[pawn] = last;
                    }

                    // Если недавно ел человечину — ванильные воспоминания это покажут.
                    int ateTick = GetLastAteTickFromMemories(pawn, now);
                    if (ateTick > last)
                    {
                        last = ateTick;
                        lastAteHumanTick[pawn] = last;
                    }

                    if (now - last < thresholdTicks) continue;
                    if (pawn.InMentalState || pawn.Downed || !pawn.Awake()) continue;
                    if (!Rand.MTBEventOccurs(FrenzyMtbDays, GenDate.TicksPerDay, CheckIntervalTicks)) continue;

                    bool started = pawn.mindState.mentalStateHandler.TryStartMentalState(
                        frenzyDef,
                        reason: "Watcher_CannibalFrenzy_Reason".Translate(),
                        forced: false);

                    CannibalFrenzyDebug.Msg(string.Format("{0}: {1:F1} дн. без человечины — попытка срыва: {2}",
                        CannibalFrenzyDebug.Name(pawn), (now - last) / (float)GenDate.TicksPerDay,
                        started ? "запущен" : "не удалось"));

                    if (started)
                    {
                        // Сбрасываем отсчёт, чтобы срывы не шли подряд.
                        lastAteHumanTick[pawn] = now;
                    }
                }
            }
        }

        private static void ResolveThoughts()
        {
            if (thoughtsResolved) return;
            thoughtsResolved = true;

            humanMeatThoughts.Clear();
            for (int i = 0; i < HumanMeatThoughtDefNames.Length; i++)
            {
                ThoughtDef def = DefDatabase<ThoughtDef>.GetNamedSilentFail(HumanMeatThoughtDefNames[i]);
                if (def != null)
                {
                    humanMeatThoughts.Add(def);
                }
            }

            CannibalFrenzyDebug.Msg("ThoughtDef человечины найдено: " + humanMeatThoughts.Count + " из "
                + HumanMeatThoughtDefNames.Length + " (" + string.Join(", ",
                    humanMeatThoughts.Select(t => t.defName).ToArray()) + ")");

            if (humanMeatThoughts.Count == 0)
            {
                Log.Warning("[Watcher] CannibalFrenzy: не найдены ThoughtDef «ate humanlike meat» — " +
                    "определить, когда каннибал ел человечину, не получится, срывы будут идти по таймеру.");
            }
        }

        /// <summary>
        /// Возвращает игровой тик последнего поедания человечины, судя по воспоминаниям
        /// каннибала (-1, если таких воспоминаний нет).
        /// </summary>
        private static int GetLastAteTickFromMemories(Pawn pawn, int now)
        {
            if (pawn.needs == null || pawn.needs.mood == null || pawn.needs.mood.thoughts == null
                || pawn.needs.mood.thoughts.memories == null)
                return -1;

            List<Thought_Memory> memories = pawn.needs.mood.thoughts.memories.Memories;
            if (memories == null) return -1;

            int best = -1;
            for (int i = 0; i < memories.Count; i++)
            {
                Thought_Memory memory = memories[i];
                if (memory.def == null) continue;
                if (!humanMeatThoughts.Contains(memory.def)) continue;

                int ateTick = now - memory.age;
                if (ateTick > best) best = ateTick;
            }

            return best;
        }

        /// <summary>
        /// Пишет в лог, сколько дней каждый каннибал-колонист не ел человечину.
        /// Вызывается debug action'ом, поэтому от выключателя отладки не зависит.
        /// </summary>
        public void LogStatus()
        {
            int now = Find.TickManager.TicksGame;
            ResolveThoughts();

            int found = 0;
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                List<Pawn> colonists = new List<Pawn>(maps[m].mapPawns.FreeColonistsSpawned);
                for (int i = 0; i < colonists.Count; i++)
                {
                    Pawn pawn = colonists[i];
                    if (!CannibalFrenzyUtility.IsCannibal(pawn)) continue;
                    found++;

                    int last;
                    bool tracked = lastAteHumanTick.TryGetValue(pawn, out last);
                    if (!tracked) last = now;

                    int ateTick = GetLastAteTickFromMemories(pawn, now);
                    if (ateTick > last) last = ateTick;

                    float days = (now - last) / (float)GenDate.TicksPerDay;
                    Log.Message(string.Format(
                        "[CannibalFrenzy] {0}: {1:F1} дн. без человечины (порог {2}), в срыве: {3}, отслеживается: {4}",
                        pawn.LabelShort, days, HungerThresholdDays, pawn.InMentalState, tracked));
                }
            }

            if (found == 0)
            {
                Log.Message("[CannibalFrenzy] Каннибалов-колонистов на картах нет.");
            }
        }
    }

    /// <summary>
    /// Действия в меню Dev Mode → Debug actions → «Watcher / Cannibal».
    /// </summary>
    public static class CannibalFrenzy_DebugActions
    {
        [DebugAction("Watcher / Cannibal", "Toggle debug log", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ToggleDebugLog()
        {
            CannibalFrenzyDebug.Enabled = !CannibalFrenzyDebug.Enabled;
            Log.Message("[CannibalFrenzy] debug log: " + (CannibalFrenzyDebug.Enabled ? "ON" : "OFF"));
        }

        [DebugAction("Watcher / Cannibal", "Force frenzy (selected pawn)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceFrenzy()
        {
            List<Pawn> selected = Find.Selector.SelectedPawns;
            if (selected.Count == 0)
            {
                Log.Warning("[CannibalFrenzy] Debug Action: не выбрано ни одной пешки.");
                return;
            }

            MentalStateDef def = DefDatabase<MentalStateDef>.GetNamedSilentFail("Watcher_CannibalFrenzy");
            if (def == null)
            {
                Log.Warning("[CannibalFrenzy] Debug Action: не найден MentalStateDef Watcher_CannibalFrenzy.");
                return;
            }

            foreach (Pawn pawn in selected)
            {
                if (pawn.mindState == null || pawn.mindState.mentalStateHandler == null) continue;

                bool ok = pawn.mindState.mentalStateHandler.TryStartMentalState(def, reason: "debug", forced: true);
                Log.Message("[CannibalFrenzy] Debug Action: срыв для " + pawn.LabelShort + " — "
                    + (ok ? "запущен" : "не удалось запустить"));
            }
        }

        [DebugAction("Watcher / Cannibal", "Log hunger status", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogHungerStatus()
        {
            GameComponent_CannibalHunger comp = Current.Game != null
                ? Current.Game.GetComponent<GameComponent_CannibalHunger>()
                : null;

            if (comp == null)
            {
                Log.Warning("[CannibalFrenzy] Debug Action: GameComponent_CannibalHunger не найден.");
                return;
            }

            comp.LogStatus();
        }
    }
}
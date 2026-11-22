using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace HSK.Watcher.Content.Incident
{
    [StaticConstructorOnStartup]
    public class Building_Portal_RickMorty : Building
    {
        private float spinPosition;
        private float ticksAlive;
        private float motedelay;
        private static readonly Material BladesMat = MaterialPool.MatFrom("Things/Building/Genstep/BlueportalRM");

        // Звук, проигрываемый в момент появления из портала.
        private static readonly SoundDef ArrivalSound = DefDatabase<SoundDef>.GetNamed("RM_PortalArrival", false);

        private float phaseTimer;
        private int phase;

        private Pawn rick;
        private Pawn morty;
        private Building portal2;
        private IntVec3 rickApproachCell;
        private IntVec3 mortyApproachCell;

        // === ЗАЩИТА В 3 МИНУТЫ ===
        // Общая страховка: оба портала закроются не позже чем через
        // 3 минуты после открытия портала A. 180 * 60 = 10800 тиков.
        private const int PortalLifetime = 10800;

        // Флаг, что портал B уже создан (для синхронизации времени жизни)
        private bool portal2Created;

        // === ПАРАМЕТРЫ ПОШАГОВОГО СПАВНА СУЩЕСТВ ===
        private int creaturesToSpawn;               // сколько ещё осталось заспавнить
        private float creatureSpawnTimer;           // таймер между спавнами
        private string chosenCreatureDefName;       // выбранный вид для ЭТОГО портала
        private int chosenCreatureCount;            // сколько штук выбранного вида
        private const int CreatureSpawnInterval = 60; // 60 тиков = 1 сек между существами

        // === СПИСОК ВОЗМОЖНЫХ СУЩЕСТВ ===
        // Вес - относительный. Чем больше, тем чаще выпадает.
        // Замените defName на свои (например, из мода на динозавров).
        private static readonly List<CreatureVariant> CreatureVariants = new List<CreatureVariant>
        {
            new CreatureVariant { defName = "FRaptor",    weight = 40f, count = 2 },
            new CreatureVariant { defName = "Terramorph", weight = 30f, count = 3 },
            new CreatureVariant { defName = "Lacosdile",     weight = 20f, count = 5 },
            new CreatureVariant { defName = "Nutcracker",       weight = 10f, count = 2 },
        };

        // === ВАРИАНТ СУЩЕСТВА С ВЕСОМ ===
        private class CreatureVariant
        {
            public string defName;   // defName PawnKindDef
            public float weight;     // шанс (относительный вес)
            public int count;        // сколько спавнить
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            motedelay = Rand.Range(2, 6);
            // ticksAlive инициализируется только для портала A.
            // Для портала B ticksAlive будет установлен извне через SetLifetime().
            if (ticksAlive <= 0f)
                ticksAlive = PortalLifetime;
            phaseTimer = 0f;
            phase = 0;
            portal2Created = false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksAlive, "ticksAlive");
            Scribe_Values.Look(ref phaseTimer, "phaseTimer");
            Scribe_Values.Look(ref phase, "phase");
            Scribe_Values.Look(ref rickApproachCell, "rickApproachCell");
            Scribe_Values.Look(ref mortyApproachCell, "mortyApproachCell");
            Scribe_Values.Look(ref portal2Created, "portal2Created");
            Scribe_Values.Look(ref creaturesToSpawn, "creaturesToSpawn");
            Scribe_Values.Look(ref creatureSpawnTimer, "creatureSpawnTimer");
            Scribe_Values.Look(ref chosenCreatureDefName, "chosenCreatureDefName");
            Scribe_Values.Look(ref chosenCreatureCount, "chosenCreatureCount");
            Scribe_Deep.Look(ref rick, "rick");
            Scribe_Deep.Look(ref morty, "morty");
            Scribe_Deep.Look(ref portal2, "portal2");
        }

        // === СИНХРОНИЗАЦИЯ ВРЕМЕНИ ЖИЗНИ ПОРТАЛА B ===
        public void SetLifetime(float remainingTicks)
        {
            ticksAlive = remainingTicks;
        }

        // Helper: safely fetch a TraitDef by defName
        private static TraitDef GetTrait(string defName)
        {
            TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            if (def == null)
                Log.Warning($"[RickMortyPortal] TraitDef '{defName}' not found.");
            return def;
        }

        // Helper: safely fetch a HediffDef by defName
        private static HediffDef GetHediff(string defName)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            if (def == null)
                Log.Warning($"[RickMortyPortal] HediffDef '{defName}' not found.");
            return def;
        }

        // Helper: safely fetch a GeneDef by defName
        private static GeneDef GetGene(string defName)
        {
            GeneDef def = DefDatabase<GeneDef>.GetNamedSilentFail(defName);
            if (def == null)
                Log.Warning($"[RickMortyPortal] GeneDef '{defName}' not found.");
            return def;
        }

        // Forces a pawn to walk toward the given cell
        private static void SendPawnTo(Pawn pawn, IntVec3 cell)
        {
            if (pawn == null || pawn.Destroyed || !pawn.Spawned || pawn.jobs == null)
                return;

            Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            job.checkOverrideOnExpire = false;
            pawn.jobs.StartJob(job, JobCondition.InterruptForced, null, resumeCurJobAfterwards: false);
        }

        // Пешка должна ФИЗИЧЕСКИ стоять на клетке
        private static bool IsStandingOn(Pawn pawn, IntVec3 cell)
        {
            if (pawn == null || pawn.Destroyed || !pawn.Spawned)
                return true;

            return pawn.Position == cell;
        }

        // Держит пешку "на привязи" у нужной клетки
        private static void KeepPawnAt(Pawn pawn, IntVec3 targetCell)
        {
            if (pawn == null || pawn.Destroyed || !pawn.Spawned || pawn.jobs == null)
                return;

            Job curJob = pawn.CurJob;
            bool alreadyLeashedHere = curJob != null
                && curJob.targetA.Cell == targetCell
                && (curJob.def == JobDefOf.Goto || curJob.def == JobDefOf.Wait);

            if (alreadyLeashedHere)
                return;

            if (IsStandingOn(pawn, targetCell))
            {
                Job waitJob = JobMaker.MakeJob(JobDefOf.Wait, targetCell);
                waitJob.expiryInterval = 60;
                waitJob.checkOverrideOnExpire = false;
                pawn.jobs.StartJob(waitJob, JobCondition.InterruptForced, null, resumeCurJobAfterwards: false);
            }
            else
            {
                SendPawnTo(pawn, targetCell);
            }
        }

        // Подбирает две РАЗНЫЕ проходимые клетки рядом с центром
        private static void GetTwoWalkableCellsNear(IntVec3 center, Map map, out IntVec3 first, out IntVec3 second)
        {
            List<IntVec3> found = new List<IntVec3>();

            if (center.InBounds(map) && center.Standable(map))
                found.Add(center);

            foreach (IntVec3 adj in GenAdjFast.AdjacentCells8Way(center))
            {
                if (found.Count >= 2)
                    break;
                if (adj.InBounds(map) && adj.Standable(map) && !found.Contains(adj))
                    found.Add(adj);
            }

            first = found.Count > 0 ? found[0] : center;
            second = found.Count > 1 ? found[1] : first;
        }

        // Полностью очищает пешке гены
        private static void ClearAllGenes(Pawn pawn)
        {
            if (pawn.genes == null)
                return;

            pawn.genes.Endogenes.Clear();
            pawn.genes.Xenogenes.Clear();
        }

        // Helper: creates an apparel item, wears it, THEN applies the color
        private static void MakeAndWear(Pawn pawn, string thingDefName, Color color)
        {
            ThingDef apparelDef = ThingDef.Named(thingDefName);
            if (apparelDef == null || !apparelDef.IsApparel)
            {
                Log.Warning($"[RickMortyPortal] Apparel def '{thingDefName}' not found.");
                return;
            }

            ThingDef stuff = apparelDef.MadeFromStuff ? GenStuff.DefaultStuffFor(apparelDef) : null;
            Apparel apparel = (Apparel)ThingMaker.MakeThing(apparelDef, stuff);

            pawn.apparel.Wear(apparel, false);
            apparel.SetColor(color, reportFailure: false);
        }

        private void CreateRick()
        {
            PawnKindDef rickKind = PawnKindDef.Named("Rick");
            if (rickKind == null)
            {
                Log.Error("PawnKindDef 'Rick' not found!");
                return;
            }

            rick = PawnGenerator.GeneratePawn(rickKind, null);

            // === РИК ВСЕГДА МУЖЧИНА ===
            rick.gender = Gender.Male;

            // === ПАРАМЕТРЫ РИКА ===
            rick.Name = new NameTriple("Rick", "Rick", "Sanchez");
            rick.ageTracker.AgeBiologicalTicks = 144000189L; // ~45 лет
            rick.story.bodyType = BodyTypeDefOf.Male;
            rick.story.hairDef = DefDatabase<HairDef>.GetNamed("Hair_Mess");
            rick.story.HairColor = new Color(0.580f, 0.757f, 0.910f); // Голубоватый

            // Первозданный набор генов
            if (rick.genes != null)
            {
                ClearAllGenes(rick);
                GeneDef melaninWhite = GetGene("Skin_Melanin1");
                if (melaninWhite != null)
                    rick.genes.AddGene(melaninWhite, false);
                rick.genes.SetXenotypeDirect(XenotypeDefOf.Baseliner);
            }

            // Цвет кожи через AlienRaces
            CompColorable colorable = rick.TryGetComp<CompColorable>();
            if (colorable != null)
            {
                Color skinColor = new Color(0.98f, 0.98f, 0.98f);
                colorable.SetColor(skinColor);
            }

            // Борода - SideWhiskers
            rick.style.beardDef = DefDatabase<BeardDef>.GetNamed("SideWhiskers");

            // Татуировки - нет
            rick.style.FaceTattoo = TattooDefOf.NoTattoo_Face;
            rick.style.BodyTattoo = TattooDefOf.NoTattoo_Body;

            // Черты характера
            rick.story.traits = new TraitSet(rick);
            TraitDef nervesDef = GetTrait("Nerves");
            if (nervesDef != null)
                rick.story.traits.GainTrait(new Trait(nervesDef, -2));

            // Похмелье
            HediffDef hangoverDef = GetHediff("Hangover");
            if (hangoverDef != null)
                rick.health.AddHediff(hangoverDef);

            // Убираем фракцию игрока
            rick.SetFaction(null);

            if (rick.guest != null)
                rick.guest.SetGuestStatus(null);

            if (rick.playerSettings != null)
                rick.playerSettings.hostilityResponse = HostilityResponseMode.Ignore;

            // Удаляем всю стандартную одежду
            rick.apparel?.DestroyAll();

            // Одежда
            MakeAndWear(rick, "Apparel_RickCoat", new Color(0.75f, 0.76f, 0.82f));
            MakeAndWear(rick, "Bottoms_Combat", new Color(0.27f, 0.39f, 0.71f));
            MakeAndWear(rick, "Apparel_BasicShirt", new Color(0.95f, 0.95f, 0.95f));

            GenSpawn.Spawn(rick, base.Position + new IntVec3(1, 0, 0), base.Map);
            rick.jobs?.StopAll();

            Log.Message("Rick spawned: " + rick.Name);
        }

        private void CreateMorty()
        {
            PawnKindDef mortyKind = PawnKindDef.Named("Morty");
            if (mortyKind == null)
            {
                Log.Error("PawnKindDef 'Morty' not found!");
                return;
            }

            morty = PawnGenerator.GeneratePawn(mortyKind, null);

            // === МОРТИ ВСЕГДА МУЖЧИНА ===
            morty.gender = Gender.Male;

            // === ПАРАМЕТРЫ МОРТИ ===
            morty.Name = new NameTriple("Morty", "Morty", "Smith");
            morty.ageTracker.AgeBiologicalTicks = 50400566L; // ~14 лет
            morty.story.bodyType = BodyTypeDefOf.Thin;
            morty.story.hairDef = DefDatabase<HairDef>.GetNamed("ShortCut");
            morty.story.HairColor = new Color(0.282f, 0.200f, 0.125f); // Тёмно-коричневый

            // Первозданный набор генов
            if (morty.genes != null)
            {
                ClearAllGenes(morty);
                GeneDef melaninLight = GetGene("Skin_Melanin1");
                if (melaninLight != null)
                    morty.genes.AddGene(melaninLight, false);
                morty.genes.SetXenotypeDirect(XenotypeDefOf.Baseliner);
            }

            // Цвет кожи через AlienRaces
            CompColorable colorable = morty.TryGetComp<CompColorable>();
            if (colorable != null)
            {
                Color skinColor = new Color(0.949f, 0.929f, 0.878f);
                colorable.SetColor(skinColor);
            }

            // Борода - нет
            morty.style.beardDef = BeardDefOf.NoBeard;

            // Татуировки - нет
            morty.style.FaceTattoo = TattooDefOf.NoTattoo_Face;
            morty.style.BodyTattoo = TattooDefOf.NoTattoo_Body;

            // Черты характера
            morty.story.traits = new TraitSet(morty);

            TraitDef naiveDef = GetTrait("Naive");
            if (naiveDef != null)
                morty.story.traits.GainTrait(new Trait(naiveDef, 0));

            TraitDef nervesDef = GetTrait("Nerves");
            if (nervesDef != null)
                morty.story.traits.GainTrait(new Trait(nervesDef, -1));

            TraitDef delicateDef = GetTrait("Delicate");
            if (delicateDef != null)
                morty.story.traits.GainTrait(new Trait(delicateDef, 0));

            TraitDef painThresholdDef = GetTrait("PainThreshold");
            if (painThresholdDef != null)
                morty.story.traits.GainTrait(new Trait(painThresholdDef, 1));

            // Убираем фракцию игрока
            morty.SetFaction(null);

            if (morty.guest != null)
                morty.guest.SetGuestStatus(null);

            if (morty.playerSettings != null)
                morty.playerSettings.hostilityResponse = HostilityResponseMode.Ignore;

            // Удаляем всю стандартную одежду
            morty.apparel?.DestroyAll();

            // Одежда
            MakeAndWear(morty, "Apparel_MortyShirt", new Color(1.000f, 0.840f, 0.000f));
            MakeAndWear(morty, "Bottoms_Combat", new Color(0.27f, 0.39f, 0.71f));

            GenSpawn.Spawn(morty, base.Position + new IntVec3(-1, 0, 0), base.Map);
            morty.jobs?.StopAll();

            Log.Message("Morty spawned: " + morty.Name);
        }

        // Расстояние между порталом A и порталом B
        private const int Portal2MinDistance = 10;
        private const int Portal2MaxDistance = 15;

        private void CreatePortal2()
        {
            IntVec3 portal2Pos = IntVec3.Invalid;

            for (int i = 0; i < 200; i++)
            {
                IntVec3 testPos = base.Position + new IntVec3(
                    Rand.Range(-Portal2MaxDistance, Portal2MaxDistance + 1),
                    0,
                    Rand.Range(-Portal2MaxDistance, Portal2MaxDistance + 1));

                float dist = Mathf.Abs(testPos.x - base.Position.x) + Mathf.Abs(testPos.z - base.Position.z);
                if (dist >= Portal2MinDistance && dist <= Portal2MaxDistance && testPos.InBounds(base.Map) && testPos.Walkable(base.Map))
                {
                    bool occupied = false;
                    List<Thing> things = testPos.GetThingList(base.Map);
                    foreach (Thing thing in things)
                    {
                        if (thing.def.rotatable)
                        {
                            occupied = true;
                            break;
                        }
                    }

                    if (!occupied)
                    {
                        portal2Pos = testPos;
                        break;
                    }
                }
            }

            if (portal2Pos == IntVec3.Invalid)
            {
                portal2Pos = base.Position + new IntVec3(Portal2MinDistance, 0, 0);
            }

            // CREATE PORTAL B - DO NOT SPAWN PAWNS!
            ThingDef portalDef = ThingDef.Named("PortalRM");
            portal2 = (Building)GenSpawn.Spawn(portalDef, portal2Pos, base.Map);

            if (portal2 is Building_Portal_RickMorty portal2Script)
            {
                portal2Script.DisablePawnSpawning();
                // === СИНХРОНИЗАЦИЯ: передаём порталу B остаток времени жизни портала A ===
                portal2Script.SetLifetime(ticksAlive);
            }

            portal2Created = true;

            // Берём по отдельной проходимой клетке рядом с порталом B
            GetTwoWalkableCellsNear(portal2Pos, base.Map, out rickApproachCell, out mortyApproachCell);
            SendPawnTo(rick, rickApproachCell);
            SendPawnTo(morty, mortyApproachCell);

            Log.Message("Portal2 created at: " + portal2Pos);
        }

        // Method to disable pawn spawning
        private bool pawnSpawningEnabled = true;

        public void DisablePawnSpawning()
        {
            pawnSpawningEnabled = false;
        }

        private bool HasBothArrivedAtPortal2()
        {
            if (portal2 == null)
                return true;

            // Портал B закрывается только когда оба ФИЗИЧЕСКИ стоят на своей клетке
            return IsStandingOn(rick, rickApproachCell) && IsStandingOn(morty, mortyApproachCell);
        }

        private void MoveRickAndMortyToPortal2()
        {
            if (rick != null && !rick.Destroyed)
            {
                rick.Destroy();
            }
            if (morty != null && !morty.Destroyed)
            {
                morty.Destroy();
            }

            // Close portal B
            if (portal2 != null && !portal2.Destroyed)
            {
                portal2.Destroy();
            }
        }

        // === ЗАПУСК ПОШАГОВОГО СПАВНА С ВЫБОРОМ ВИДА ПО ВЕСАМ ===
        private void StartSpawningCreatures()
        {
            if (CreatureVariants == null || CreatureVariants.Count == 0)
            {
                Log.Error("[RickMortyPortal] CreatureVariants is empty!");
                creaturesToSpawn = 0;
                return;
            }

            // Считаем суммарный вес всех валидных вариантов
            float totalWeight = 0f;
            List<CreatureVariant> validVariants = new List<CreatureVariant>();
            foreach (CreatureVariant v in CreatureVariants)
            {
                if (v == null || v.weight <= 0f)
                    continue;
                // Проверяем, что PawnKindDef существует
                if (PawnKindDef.Named(v.defName) == null)
                {
                    Log.Warning($"[RickMortyPortal] CreatureVariant '{v.defName}' not found, skipping.");
                    continue;
                }
                validVariants.Add(v);
                totalWeight += v.weight;
            }

            if (validVariants.Count == 0)
            {
                Log.Error("[RickMortyPortal] No valid CreatureVariant found!");
                creaturesToSpawn = 0;
                return;
            }

            // Выбираем один вид по взвешенному случайному броску
            float roll = Rand.Range(0f, totalWeight);
            float accum = 0f;
            CreatureVariant chosen = validVariants[validVariants.Count - 1]; // фолбэк
            foreach (CreatureVariant v in validVariants)
            {
                accum += v.weight;
                if (roll <= accum)
                {
                    chosen = v;
                    break;
                }
            }

            chosenCreatureDefName = chosen.defName;
            chosenCreatureCount = Mathf.Max(1, chosen.count);
            creaturesToSpawn = chosenCreatureCount;
            creatureSpawnTimer = 0f; // первое существо появится сразу

            Log.Message($"[RickMortyPortal] Chosen creature: {chosenCreatureDefName} x{chosenCreatureCount}");
        }

        // === СПАВН ОДНОГО СУЩЕСТВА (вид выбран заранее) ===
        private void SpawnOneCreature()
        {
            if (creaturesToSpawn <= 0)
                return;

            // Используем заранее выбранный вид (не пересчитываем веса каждый раз!)
            PawnKindDef creatureKind = null;

            if (!string.IsNullOrEmpty(chosenCreatureDefName))
                creatureKind = PawnKindDef.Named(chosenCreatureDefName);

            // Фолбэк, если PawnKindDef исчез (мод выгрузили и т.п.)
            if (creatureKind == null)
            {
                Log.Warning($"[RickMortyPortal] PawnKindDef '{chosenCreatureDefName}' not found, falling back to 'Megascarab'.");
                creatureKind = PawnKindDef.Named("Megascarab");
            }

            if (creatureKind == null)
            {
                Log.Error("[RickMortyPortal] No valid creature PawnKindDef found!");
                creaturesToSpawn = 0;
                return;
            }

            IntVec3 spawnPos = base.Position;

            if (!spawnPos.InBounds(base.Map) || !spawnPos.Walkable(base.Map))
            {
                spawnPos = CellFinder.RandomClosewalkCellNear(base.Position, base.Map, 3);
            }

            Pawn creature = PawnGenerator.GeneratePawn(creatureKind, null);
            GenSpawn.Spawn(creature, spawnPos, base.Map);

            // === ЭФФЕКТ ПРОХОЖДЕНИЯ ПОРТАЛА ===
          
            FleckMaker.ThrowTornadoDustPuff(
             base.DrawPos,
             base.Map,
             Rand.Range(2, 4),
             new Color(0.3f, 0.5f, 1f)
            );

            // Делаем существо враждебным
            if (creature.mindState?.mentalStateHandler != null)
            {
                creature.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter);
            }

            creaturesToSpawn--;

            // Сообщение показываем только при первом существе
            if (creaturesToSpawn == chosenCreatureCount - 1)
            {
                Messages.Message("RMPortal.HostileMessage".Translate(), MessageTypeDefOf.ThreatBig);
            }
        }

        protected override void Tick()
        {
            // Анимация вращения и пылевые эффекты работают ВСЕГДА
            HandleAnimation();
            motedelay -= 1f;
            if (motedelay <= 0f)
            {
                FleckMaker.ThrowTornadoDustPuff(base.DrawPos, base.Map, Rand.Range(1, 3), new Color(0.3f, 0.5f, 1f));
                motedelay = Rand.Range(10, 20);
            }

            // Портал B не ведёт фазовую логику - только таймер жизни
            if (!pawnSpawningEnabled)
            {
                ticksAlive -= 1f;
                if (ticksAlive <= 0f)
                {
                    Destroy();
                }
                return;
            }

            // === ОБЩИЙ ДЕКРЕМЕНТ ЗАЩИТЫ 3 МИНУТЫ ДЛЯ ПОРТАЛА A ===
            ticksAlive -= 1f;

            if (phase == 0 && phaseTimer >= 120f) // 2 sec - spawn Rick and Morty
            {
                CreateRick();
                CreateMorty();

                ArrivalSound?.PlayOneShot(SoundInfo.InMap(new TargetInfo(base.Position, base.Map)));

                phaseTimer = 0f;
                phase = 1;
            }
            else if (phase == 1 && phaseTimer >= 240f) // 4 sec - create portal B
            {
                CreatePortal2();
                phaseTimer = 0f;
                phase = 2;
            }
            else if (phase == 2)
            {
                // Держим обоих у портала B каждый тик
                if (portal2 != null)
                {
                    KeepPawnAt(rick, rickApproachCell);
                    KeepPawnAt(morty, mortyApproachCell);
                }

                // Портал B закрывается ТОЛЬКО когда оба физически стоят на клетках.
                // Если не дойдут - сработает общая 3-минутная защита (ticksAlive).
                if (HasBothArrivedAtPortal2())
                {
                    MoveRickAndMortyToPortal2();
                    phaseTimer = 0f;
                    phase = 3;

                    // Запуск пошагового спавна существ (выбор вида по весам)
                    StartSpawningCreatures();
                }
            }
            else if (phase == 3)
            {
                // === ПОШАГОВЫЙ СПАВН СУЩЕСТВ ===
                if (creaturesToSpawn > 0)
                {
                    creatureSpawnTimer -= 1f;
                    if (creatureSpawnTimer <= 0f)
                    {
                        SpawnOneCreature();
                        creatureSpawnTimer = CreatureSpawnInterval;
                    }
                }
                else
                {
                    // Все существа заспавнены - переходим к следующей фазе
                    phaseTimer = 0f;
                    phase = 4;
                }
            }
            else if (phase == 4 && phaseTimer >= 180f) // 3 sec - close portal A
            {
                ticksAlive = 0f;
            }

            phaseTimer += 1f;

            // === УНИЧТОЖЕНИЕ ПОРТАЛА A И ПОРТАЛА B ПО ОБЩЕЙ ЗАЩИТЕ ===
            if (ticksAlive <= 0f)
            {
                if (portal2 != null && !portal2.Destroyed)
                    portal2.Destroy();
                Destroy();
            }
        }

        private void HandleAnimation()
        {
            if (Find.TickManager.TicksGame % 5 == 0)
                spinPosition += 8f;
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(
                s: new Vector3(def.size.x, 0f, def.size.z),
                pos: DrawPos + Vector3.up + Altitudes.AltIncVect,
                q: spinPosition.ToQuat()
            );
            Graphics.DrawMesh(MeshPool.plane10, matrix, BladesMat, 0);
        }
    }

    // ============================================================
    // INCIDENT WORKER - RARE EVENT
    // ============================================================
    public class IncidentWorker_RickMortyPortal : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (map.mapPawns.FreeColonists.Count == 0)
                return false;

            IntVec3 spawnPos = FindSpawnPosition(map);
            if (spawnPos == IntVec3.Invalid)
                return false;

            return true;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;

            IntVec3 spawnPos = FindSpawnPosition(map);
            if (spawnPos == IntVec3.Invalid)
                return false;

            ThingDef portalDef = ThingDef.Named("PortalRM");
            Building portal = (Building)GenSpawn.Spawn(portalDef, spawnPos, map);

            Find.LetterStack.ReceiveLetter(
                "RMPortal.LetterLabel".Translate(),
                "RMPortal.LetterText".Translate(),
                LetterDefOf.PositiveEvent,
                new TargetInfo(spawnPos, map)
            );

            return true;
        }

        // Радиус поиска точки открытия портала вокруг базы игрока / центра карты
        private const int SearchRadius = 30;

        private IntVec3 FindSpawnPosition(Map map)
        {
            IntVec3 center = GetBaseOrMapCenter(map);

            // Сначала пробуем рядом с базой (или её заменителем)
            if (CellFinder.TryFindRandomCellNear(center, map, SearchRadius, cell => IsValidPortalCell(map, cell), out IntVec3 result))
                return result;

            // Фолбэк: если рядом с базой не нашлось места - пробуем от центра карты
            if (center != map.Center &&
                CellFinder.TryFindRandomCellNear(map.Center, map, SearchRadius, cell => IsValidPortalCell(map, cell), out result))
                return result;

            return IntVec3.Invalid;
        }

        // Находит "центр базы" игрока
        private IntVec3 GetBaseOrMapCenter(Map map)
        {
            if (map.areaManager?.Home != null && map.areaManager.Home.TrueCount > 0)
            {
                long sumX = 0, sumZ = 0;
                int count = 0;
                foreach (IntVec3 cell in map.areaManager.Home.ActiveCells)
                {
                    sumX += cell.x;
                    sumZ += cell.z;
                    count++;
                }
                if (count > 0)
                    return new IntVec3((int)(sumX / count), 0, (int)(sumZ / count));
            }

            int colonistCount = 0;
            long colSumX = 0, colSumZ = 0;
            foreach (Pawn pawn in map.mapPawns.FreeColonists)
            {
                colSumX += pawn.Position.x;
                colSumZ += pawn.Position.z;
                colonistCount++;
            }
            if (colonistCount > 0)
                return new IntVec3((int)(colSumX / colonistCount), 0, (int)(colSumZ / colonistCount));

            return map.Center;
        }

        // Проверяет, что в клетке реально можно поставить портал
        private bool IsValidPortalCell(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map) || cell.Fogged(map) || !cell.Standable(map))
                return false;

            ThingDef portalDef = ThingDef.Named("PortalRM");
            if (portalDef == null)
                return false;

            AcceptanceReport report = GenConstruct.CanPlaceBlueprintAt(portalDef, cell, Rot4.North, map);
            return report.Accepted;
        }
    }
}
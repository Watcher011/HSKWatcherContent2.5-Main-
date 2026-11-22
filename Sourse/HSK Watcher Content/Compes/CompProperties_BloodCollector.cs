//using System;
//using System.Collections.Generic;
//using System.Linq;
//using RimWorld;
//using UnityEngine;
//using Verse;

//namespace HSK.Watcher.Content.Buildings
//{
//    public class Building_BloodCollectorFlask : Building
//    {
//        // === Определение стадий заполнения ===
//        private enum FillStage
//        {
//            Empty,      // 0%
//            Twenty,     // 20%
//            Fifty,      // 50%
//            Full        // 100%
//        }

//        // Текущее количество крови (единицы)
//        private float bloodAmount = 0f;

//        // Максимальная вместимость
//        private const float MaxCapacity = 100f;

//        // Радиус сбора крови
//        private const float CollectionRadius = 15f;

//        // Интервал сбора (тики)
//        private const int CollectionInterval = 60; // ~1 секунда

//        // Таймер сбора
//        private int collectionTicker = 0;

//        // Текущая стадия заполнения
//        private FillStage currentStage = FillStage.Empty;

//        // Кэш графики
//        private Graphic cachedGraphic;

//        // Флаг вызова вампиров
//        private bool vampiresSummoned = false;

//        // Время исчезновения вампиров
//        private int vampiresExpireTick = 0;

//        // Список призванных вампиров
//        private List<Pawn> summonedVampires = new List<Pawn>();

//        // === Свойства ===
//        public float BloodAmount => bloodAmount;
//        public float FillPercent => bloodAmount / MaxCapacity;
//        public bool IsFull => FillPercent >= 0.99f;
//        public bool VampiresActive => vampiresSummoned && Find.TickManager.TicksGame < vampiresExpireTick;

//        // === Инициализация ===
//        public override void SpawnSetup(Map map, bool respawningAfterLoad)
//        {
//            base.SpawnSetup(map, respawningAfterLoad);
//            UpdateTexture();
//            Log.Message($"[BloodCollector] Колба установлена на позиции {Position}. Текущее заполнение: {FillPercent:P0}");
//        }

//        // === Тик (логика сбора) ===
//        protected override void Tick()
//        {
//            base.Tick();

//            if (!this.Spawned) return;

//            // Сбор крови с интервалом
//            collectionTicker++;
//            if (collectionTicker >= CollectionInterval)
//            {
//                collectionTicker = 0;
//                CollectBloodFromMap();
//            }

//            // Обновление текстуры
//            FillStage newStage = GetFillStage();
//            if (newStage != currentStage)
//            {
//                currentStage = newStage;
//                UpdateTexture();
//            }

//            // Проверка активности вампиров
//            if (vampiresSummoned && Find.TickManager.TicksGame >= vampiresExpireTick)
//            {
//                RemoveVampires();
//            }
//        }

//        // === Сбор крови с карты ===
//        private void CollectBloodFromMap()
//        {
//            if (IsFull) return;

//            float bloodGathered = 0f;

//            // Поиск всех пятен крови в радиусе через логгинг
//            CellRect rect = CellRect.CenteredOn(Position, (int)CollectionRadius);
//            List<Thing> filthList = new List<Thing>();

//            // Собираем все пятна в радиусе
//            foreach (IntVec3 cell in rect.Cells)
//            {
//                if (!cell.InBounds(this.Map)) continue;

//                List<Thing> thingList = cell.GetThingList(this.Map);
//                foreach (Thing thing in thingList)
//                {
//                    if (thing is Filth filth)
//                    {
//                        if (filth.def.defName == "Filth_DarkBlood" ||
//                            filth.def.defName == "Filth_Blood" ||
//                            filth.def.defName == "Filth_BloodInsect")
//                        {
//                            filthList.Add(filth);
//                        }
//                    }
//                }
//            }

//            foreach (Filth filth in filthList)
//            {
//                if (bloodAmount + bloodGathered >= MaxCapacity)
//                {
//                    bloodGathered = MaxCapacity - bloodAmount;
//                    break;
//                }

//                // Количество крови за одну единицу Filth
//                float bloodValue = 0.5f;
//                bloodGathered += bloodValue;
//                filth.Destroy();
//            }

//            if (bloodGathered > 0)
//            {
//                bloodAmount += bloodGathered;
//                bloodAmount = Mathf.Min(bloodAmount, MaxCapacity);
//                Log.Message($"[BloodCollector] Собрано {bloodGathered} крови. Всего: {bloodAmount:F1}/{MaxCapacity:F0} ({FillPercent:P0})");
//            }
//        }

//        // === Получение стадии заполнения ===
//        private FillStage GetFillStage()
//        {
//            float percent = FillPercent;

//            if (percent >= 0.99f) return FillStage.Full;
//            if (percent >= 0.50f) return FillStage.Fifty;
//            if (percent >= 0.20f) return FillStage.Twenty;
//            return FillStage.Empty;
//        }

//        // === Обновление текстуры ===
//        private void UpdateTexture()
//        {
//            string texturePath = GetTexturePath();
//            Log.Message($"[BloodCollector] Обновление текстуры: {texturePath}, заполнение: {FillPercent:P0}");

//            cachedGraphic = GraphicDatabase.Get<Graphic_Single>(texturePath, ShaderDatabase.Cutout, drawSize: Vector2.one, color: Color.white);
//        }

//        // === Путь к текстуре ===
//        private string GetTexturePath()
//        {
//            string basePath = "Things/Buildings/BloodCollectorFlask";

//            switch (GetFillStage())
//            {
//                case FillStage.Empty: return $"{basePath}_Empty";
//                case FillStage.Twenty: return $"{basePath}_20";
//                case FillStage.Fifty: return $"{basePath}_50";
//                case FillStage.Full: return $"{basePath}_100";
//                default: return $"{basePath}_Empty";
//            }
//        }

//        // === Графика ===
//        public override Graphic Graphic
//        {
//            get
//            {
//                if (cachedGraphic == null)
//                {
//                    UpdateTexture();
//                }
//                return cachedGraphic;
//            }
//        }

//        // === Добавление крови (тест) ===
//        public void AddBlood(float amount)
//        {
//            float oldPercent = FillPercent;
//            bloodAmount = Mathf.Min(bloodAmount + amount, MaxCapacity);

//            Log.Message($"[BloodCollector] Добавлено {amount} крови. Было: {oldPercent:P0}, Стало: {FillPercent:P0}");

//            FillStage newStage = GetFillStage();
//            if (newStage != currentStage)
//            {
//                currentStage = newStage;
//                UpdateTexture();
//            }
//        }

//        // === Очистка колбы ===
//        public void ClearBlood()
//        {
//            bloodAmount = 0f;
//            currentStage = GetFillStage();
//            UpdateTexture();
//            Log.Message($"[BloodCollector] Колба очищена. Заполнение: {FillPercent:P0}");
//        }

//        // === Вызов вампиров ===
//        private void SummonVampires()
//        {
//            if (!IsFull)
//            {
//                Messages.Message("Колба не заполнена полностью!", MessageTypeDefOf.RejectInput, historical: false);
//                return;
//            }

//            if (vampiresSummoned)
//            {
//                Messages.Message("Вампиры уже защищают колонию!", MessageTypeDefOf.RejectInput, historical: false);
//                return;
//            }

//            Log.Message("[BloodCollector] Призыв вампиров!");

//            // Спавн 2-3 вампиров
//            int vampireCount = UnityEngine.Random.Range(2, 4);

//            for (int i = 0; i < vampireCount; i++)
//            {
//                // Создаём позицию для спавна рядом с колбой
//                IntVec3 spawnPos = this.Position + new IntVec3(UnityEngine.Random.Range(-3, 4), 0, UnityEngine.Random.Range(-3, 4));

//                if (!spawnPos.InBounds(this.Map))
//                {
//                    spawnPos = this.Position;
//                }

//                // Создаём вампира (Sanguophage)
//                PawnKindDef vampireKind = PawnKindDef.Named("Sanguophage");
//                if (vampireKind == null)
//                {
//                    Log.Error("[BloodCollector] Не найден Sanguophage! Установлен ли Biotech?");
//                    return;
//                }

//                Pawn vampire = PawnGenerator.GeneratePawn(vampireKind, Faction.OfPlayer);
//                GenSpawn.Spawn(vampire, spawnPos, this.Map);

//                // Добавляем хедафф временного защитника
//                HediffDef tempHediffDef = HediffDef.Named("Temporary");
//                if (tempHediffDef != null)
//                {
//                    Hediff tempGuardian = HediffMaker.MakeHediff(tempHediffDef, vampire);
//                    tempGuardian.Severity = 1f;
//                    vampire.health.AddHediff(tempGuardian);
//                }

//                summonedVampires.Add(vampire);
//                Log.Message($"[BloodCollector] Призван вампир: {vampire.Name}");
//            }

//            vampiresSummoned = true;
//            vampiresExpireTick = Find.TickManager.TicksGame + 60000; // 1 день = 60000 тиков

//            // Очищаем колбу после вызова
//            bloodAmount = 0f;
//            currentStage = GetFillStage();
//            UpdateTexture();

//            Messages.Message($"Призваны {vampireCount} вампира-защитника! Они будут защищать колонию в течение дня.",
//                MessageTypeDefOf.PositiveEvent, historical: false);
//        }

//        // === Удаление вампиров ===
//        private void RemoveVampires()
//        {
//            Log.Message("[BloodCollector] Время защиты истекло, вампиры уходят...");

//            foreach (Pawn vampire in summonedVampires)
//            {
//                if (vampire != null && !vampire.Dead)
//                {
//                    // Удаляем хедафф и выселяем
//                    HediffDef tempHediffDef = HediffDef.Named("Temporary");
//                    if (tempHediffDef != null)
//                    {
//                        Hediff tempHediff = vampire.health.hediffSet.GetFirstHediffOfDef(tempHediffDef);
//                        if (tempHediff != null)
//                        {
//                            vampire.health.RemoveHediff(tempHediff);
//                        }
//                    }

//                    // Отправляем вампира в путешествие (исчезает)
//                    vampire.DeSpawn();
//                }
//            }

//            summonedVampires.Clear();
//            vampiresSummoned = false;

//            Messages.Message("Вампиры-защитники покинули колонию.", MessageTypeDefOf.NeutralEvent, historical: false);
//        }

//        // === Gizmo кнопки ===
//        public override IEnumerable<Gizmo> GetGizmos()
//        {
//            foreach (Gizmo gizmo in base.GetGizmos())
//            {
//                yield return gizmo;
//            }

//            // Кнопка добавления 10% крови (тест)
//            yield return new Command_Action
//            {
//                defaultLabel = "AddBloodCommand".Translate(),
//                defaultDesc = "AddBloodCommandDesc".Translate(),
//                action = () => AddBlood(10f)
//            };

//            // Кнопка очистки колбы (тест)
//            yield return new Command_Action
//            {
//                defaultLabel = "ClearBloodCommand".Translate(),
//                defaultDesc = "ClearBloodCommandDesc".Translate(),
//                action = () => ClearBlood()
//            };

//            // Кнопка информации
//            yield return new Command_Action
//            {
//                defaultLabel = "FlaskInfoCommand".Translate(),
//                defaultDesc = "FlaskInfoCommandDesc".Translate(),
//                action = () => Messages.Message(string.Format("FlaskInfoMessage".Translate(), FillPercent.ToString("P0"), bloodAmount, MaxCapacity),
//                    MessageTypeDefOf.TaskCompletion, historical: false)
//            };

//            // Кнопка вызова вампиров (только при 100%)
//            if (IsFull && !vampiresSummoned)
//            {
//                yield return new Command_Action
//                {
//                    defaultLabel = "SummonVampiresCommand".Translate(),
//                    defaultDesc = "SummonVampiresCommandDesc".Translate(),
//                    action = () => SummonVampires()
//                };
//            }

//            // Индикатор активных вампиров (просто информационная кнопка)
//            if (VampiresActive)
//            {
//                int remainingTicks = vampiresExpireTick - Find.TickManager.TicksGame;
//                int remainingHours = remainingTicks / 2500; // 2500 тиков = 1 час

//                yield return new Command_Action
//                {
//                    defaultLabel = string.Format("VampiresActiveCommand".Translate(), remainingHours, summonedVampires.Count),
//                    defaultDesc = "VampiresActiveCommandDesc".Translate(),
//                    action = () => { } // Пустое действие, просто информационная кнопка
//                };
//            }
//        }

//        // === Инспекция ===
//        public override string GetInspectString()
//        {
//            string baseString = base.GetInspectString();
//            string bloodInfo = string.Format("FlaskBloodAmount".Translate(), bloodAmount, MaxCapacity, FillPercent);

//            if (VampiresActive)
//            {
//                int remainingTicks = vampiresExpireTick - Find.TickManager.TicksGame;
//                int remainingHours = remainingTicks / 2500;
//                bloodInfo += $"\n{string.Format("VampiresActiveInfo".Translate(), remainingHours, summonedVampires.Count)}";
//            }

//            return $"{baseString}\n{bloodInfo}";
//        }

//        // === Сохранение ===
//        public override void ExposeData()
//        {
//            base.ExposeData();
//            Scribe_Values.Look(ref bloodAmount, "bloodAmount", 0f);
//            Scribe_Values.Look(ref currentStage, "currentStage", FillStage.Empty);
//            Scribe_Values.Look(ref vampiresSummoned, "vampiresSummoned", false);
//            Scribe_Values.Look(ref vampiresExpireTick, "vampiresExpireTick", 0);
//            Scribe_Collections.Look(ref summonedVampires, "summonedVampires", LookMode.Reference);
//        }
//    }

//    // === Класс мода ===
//    public class BloodCollectorMod : Mod
//    {
//        public BloodCollectorMod(ModContentPack content) : base(content)
//        {
//            Log.Message("[BloodCollector] Мод колбы для сбора крови загружен!");
//        }
//    }
//}
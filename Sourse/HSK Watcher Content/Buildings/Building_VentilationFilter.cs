using RimWorld;
using Verse;
using System.Collections.Generic;
using System;

namespace HSK.Watcher.Content
{
    public class Building_VentilationFilter : Building
    {
        public int cleanRatePerTick = 2;
        private int tickCounter = 0;
        private const int CheckInterval = 10;

        private Queue<IntVec3> cellsToClean = new Queue<IntVec3>();
        private Room cachedRoom = null;
        private int cachedRoomUpdateTick = -1;
        private int totalGasCleaned = 0;
        private int lastCleanCount = 0;
        private bool isWorking = false;

        // Только чёрный дым (CE)
        private static readonly string[] GasDefNamesToRemove = new string[]
        {
            "Gas_BlackSmoke"
        };

        protected override void Tick()
        {
            base.Tick();

            tickCounter++;
            if (tickCounter < CheckInterval) return;
            tickCounter = 0;

            CompRefuelable refuelableComp = this.GetComp<CompRefuelable>();
            if (refuelableComp == null || !refuelableComp.HasFuel)
            {
                if (isWorking) isWorking = false;
                return;
            }

            // Обновляем комнату
            if (cachedRoomUpdateTick < 0 || Find.TickManager.TicksGame - cachedRoomUpdateTick > 200)
            {
                UpdateRoomCache();
            }

            // Проверка: фильтр должен быть в комнате
            if (cachedRoom == null)
            {
                if (isWorking) isWorking = false;
                return;
            }

            // Проверка: комната должна быть закрытой (не улица)
            if (IsRoomOutdoor(cachedRoom))
            {
                if (isWorking) isWorking = false;
                return;
            }

            if (cellsToClean.Count == 0)
            {
                if (isWorking) isWorking = false;
                return;
            }

            int cleaned = 0;
            int attempts = 0;
            int maxAttempts = cellsToClean.Count * 2;

            while (cleaned < cleanRatePerTick && cellsToClean.Count > 0 && attempts < maxAttempts)
            {
                attempts++;
                IntVec3 cell = cellsToClean.Dequeue();

                if (cell.InBounds(this.Map))
                {
                    if (CleanCell(cell))
                    {
                        cleaned++;
                        totalGasCleaned++;
                    }
                }

                cellsToClean.Enqueue(cell);
            }

            if (cleaned > 0)
            {
                isWorking = true;
                lastCleanCount = cleaned;
                FleckMaker.ThrowAirPuffUp(this.Position.ToVector3(), this.Map);
                refuelableComp.Notify_UsedThisTick();
            }
            else
            {
                isWorking = false;
            }
        }

        private bool IsRoomOutdoor(Room room)
        {
            if (room == null) return true;

            // Способ 1: Проверка роли комнаты
            try
            {
                if (room.Role != null)
                {
                    string roleDefName = room.Role.defName ?? "";
                    string roleLabel = room.Role.label ?? "";

                    // Проверяем разные варианты названий для улицы
                    if (roleDefName.ToLower().Contains("outdoor") ||
                        roleDefName.ToLower().Contains("outside") ||
                        roleDefName.ToLower().Contains("outdoors") ||
                        roleLabel.ToLower().Contains("outdoor") ||
                        roleLabel.ToLower().Contains("outside") ||
                        roleLabel.ToLower().Contains("улица"))
                    {
                        return true;
                    }
                }
            }
            catch { }

            // Способ 2: Проверка через количество крыш
            int roofedCount = 0;
            int totalCount = 0;
            int checkLimit = Math.Min(room.CellCount, 100);

            foreach (var cell in room.Cells)
            {
                totalCount++;
                if (this.Map.roofGrid.Roofed(cell))
                {
                    roofedCount++;
                }
                if (totalCount >= checkLimit) break;
            }

            // Если менее 30% клеток имеют крышу - считаем открытым пространством
            if (totalCount > 0 && (float)roofedCount / totalCount < 0.3f)
            {
                return true;
            }

            // Способ 3: Проверка на наличие дверей
            bool hasDoor = false;
            int doorCheckCount = 0;
            foreach (var cell in room.Cells)
            {
                doorCheckCount++;
                if (doorCheckCount > 50) break;

                List<Thing> thingsAtCell = this.Map.thingGrid.ThingsListAt(cell);
                foreach (var thing in thingsAtCell)
                {
                    if (thing.def != null && thing.def.category == ThingCategory.Building)
                    {
                        // Проверяем, является ли здание дверью
                        if (thing.def.building != null)
                        {
                            // В разных версиях RimWorld свойства могут называться по-разному
                            // Пробуем несколько вариантов
                            try
                            {
                                // Вариант 1: IsDoor (с большой буквы)
                                var isDoorProp = thing.def.building.GetType().GetProperty("IsDoor");
                                if (isDoorProp != null && (bool)isDoorProp.GetValue(thing.def.building))
                                {
                                    hasDoor = true;
                                    break;
                                }
                            }
                            catch { }

                            try
                            {
                                // Вариант 2: isDoor (с маленькой буквы)
                                var isDoorField = thing.def.building.GetType().GetField("isDoor");
                                if (isDoorField != null && (bool)isDoorField.GetValue(thing.def.building))
                                {
                                    hasDoor = true;
                                    break;
                                }
                            }
                            catch { }

                            try
                            {
                                // Вариант 3: door (поле)
                                var doorField = thing.def.building.GetType().GetField("door");
                                if (doorField != null && (bool)doorField.GetValue(thing.def.building))
                                {
                                    hasDoor = true;
                                    break;
                                }
                            }
                            catch { }

                            // Вариант 4: Проверка по defName (самый надёжный способ)
                            if (thing.def.defName.ToLower().Contains("door") ||
                                thing.def.defName.ToLower().Contains("gate") ||
                                thing.def.defName.ToLower().Contains("дверь"))
                            {
                                hasDoor = true;
                                break;
                            }
                        }
                    }
                }
                if (hasDoor) break;
            }

            // Если нет дверей и нет крыши - это улица
            if (!hasDoor && (float)roofedCount / totalCount < 0.5f)
            {
                return true;
            }

            return false;
        }

        private void UpdateRoomCache()
        {
            Room newRoom = this.Position.GetRoom(this.Map);
            if (newRoom != cachedRoom)
            {
                cachedRoom = newRoom;
                cachedRoomUpdateTick = Find.TickManager.TicksGame;
                cellsToClean.Clear();

                if (cachedRoom != null && !IsRoomOutdoor(cachedRoom))
                {
                    foreach (var cell in cachedRoom.Cells)
                    {
                        cellsToClean.Enqueue(cell);
                    }
                }
            }
        }

        private bool CleanCell(IntVec3 cell)
        {
            if (this.Map == null) return false;

            bool cleaned = false;
            List<Thing> toRemove = new List<Thing>();

            List<Thing> thingsAtCell = this.Map.thingGrid.ThingsListAt(cell);
            if (thingsAtCell == null || thingsAtCell.Count == 0) return false;

            foreach (Thing thing in thingsAtCell)
            {
                if (thing?.def == null) continue;

                // Проверка по defName (чёрный дым CE)
                if (Array.Exists(GasDefNamesToRemove, name => thing.def.defName == name))
                {
                    toRemove.Add(thing);
                    cleaned = true;
                }
            }

            foreach (Thing gas in toRemove)
            {
                gas.Destroy(DestroyMode.Vanish);
            }

            return cleaned;
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();

            CompRefuelable refuelableComp = this.GetComp<CompRefuelable>();
            if (refuelableComp != null)
            {
                text += $"\nТопливо: {refuelableComp.Fuel:0.##}/{refuelableComp.Props.fuelCapacity:0.##}";
                text += !refuelableComp.HasFuel ? "\nСтатус: нет топлива!" :
                        isWorking ? "\nСтатус: очистка чёрного дыма" : "\nСтатус: ожидание (дыма нет)";
            }

            Room room = GetRoom();
            if (room != null)
            {
                if (IsRoomOutdoor(room))
                {
                    text += "\n❌ Не в закрытом помещении!";
                }
                else
                {
                    text += $"\nОчищает комнату: {room.Role?.label ?? "комната"}";
                    text += $"\nПлощадь комнаты: {room.CellCount} клеток";
                    text += $"\nСкорость очистки: {cleanRatePerTick} клеток/сек";

                    if (totalGasCleaned > 0)
                    {
                        text += $"\nВсего очищено дыма: {totalGasCleaned}";
                        text += $"\nПоследняя очистка: {lastCleanCount} дыма";
                    }
                }
            }
            else
            {
                text += "\n❌ Не в комнате! Постройте в помещении.";
            }

            return text;
        }

        private Room GetRoom()
        {
            if (cachedRoomUpdateTick < 0 || Find.TickManager.TicksGame - cachedRoomUpdateTick > 200)
            {
                UpdateRoomCache();
            }
            return cachedRoom;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cleanRatePerTick, "cleanRatePerTick", 2);
            Scribe_Values.Look(ref totalGasCleaned, "totalGasCleaned", 0);
            Scribe_Values.Look(ref lastCleanCount, "lastCleanCount", 0);
            Scribe_Values.Look(ref isWorking, "isWorking", false);
        }

        public void ResetStats()
        {
            totalGasCleaned = 0;
            lastCleanCount = 0;
        }
    }
}
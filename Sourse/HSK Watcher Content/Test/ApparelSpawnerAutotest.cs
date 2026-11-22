using System.Linq;
using Verse;
using RimWorld;
using LudeonTK;

namespace HSK.Watcher.Content
{
    public static class ApparelSpawnerAutotest
    {
        // Полная очистка зоны: сносит стены, скалы, постройки, пешек и вещи
        private static void ClearArea(IntVec3 center, Map map, int clearRadius = 8)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, clearRadius, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                // 1. Убираем крышу над зоной теста, чтобы ничего не висело в воздухе
                if (map.roofGrid != null && map.roofGrid.Roofed(cell))
                {
                    map.roofGrid.SetRoof(cell, null);
                }

                // 2. Удаляем все вещи, пешек и постройки (стены, двери, турели и т.д.)
                var thingList = cell.GetThingList(map).ToList();
                foreach (Thing thing in thingList)
                {
                    if (thing == null || thing.Destroyed) continue;

                    // Если это постройка или стена — используем Kill(), чтобы игра корректно удалила её из сетки зданий
                    if (thing is Building building)
                    {
                        building.Destroy(DestroyMode.Vanish);
                        continue;
                    }

                    // Пешки
                    if (thing is Pawn pawn)
                    {
                        pawn.Destroy(DestroyMode.Vanish);
                        continue;
                    }

                    // Предметы, растения и всё остальное
                    thing.Destroy(DestroyMode.Vanish);
                }

                // 3. Убираем природные скалы/руины (горную породу), если они есть на клетке
                Building edifice = cell.GetEdifice(map);
                if (edifice != null && !edifice.Destroyed)
                {
                    edifice.Destroy(DestroyMode.Vanish);
                }

                // 4. Сбрасываем ландшафт (террейн) на стандартную почву
                if (map.terrainGrid != null)
                {
                    map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
                }
            }
        }

        // Метод для спавна всей одежды на карте
        public static void SpawnAllApparel()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Log.Error("[HSK.Watcher] Не удалось найти активную карту для спавна одежды.");
                return;
            }

            // Определяем центр спавна (позиция курсора мыши или центр карты)
            IntVec3 spawnCenter = UI.MouseCell();
            if (!spawnCenter.IsValid || !spawnCenter.InBounds(map))
            {
                spawnCenter = map.Center;
            }

            // Очищаем зону перед спавном
            ClearArea(spawnCenter, map, 10);

            // Получаем все ThingDef, являющиеся одеждой, исключая сгенерированные игрой дубликаты
            var apparelDefs = DefDatabase<ThingDef>.AllDefs
                .Where(def => def.IsApparel && !def.generated)
                .ToList();

            int spawnedCount = 0;
            int radius = 1;

            foreach (var apparelDef in apparelDefs)
            {
                IntVec3 spawnLoc = CellFinder.RandomClosewalkCellNear(spawnCenter, map, radius * 2, (IntVec3 c) => c.Walkable(map));

                if (spawnLoc.IsValid && spawnLoc.InBounds(map))
                {
                    Thing apparel = ThingMaker.MakeThing(apparelDef, apparelDef.MadeFromStuff ? GenStuff.RandomStuffFor(apparelDef) : null);

                    if (apparel.TryGetComp<CompQuality>() is CompQuality compQuality)
                    {
                        compQuality.SetQuality(QualityCategory.Normal, ArtGenerationContext.Colony);
                    }

                    GenSpawn.Spawn(apparel, spawnLoc, map);
                    spawnedCount++;
                }
            }

            Messages.Message(
                $"[HSK.Watcher] Спавн завершен. Создано предметов одежды: {spawnedCount} из {apparelDefs.Count}",
                MessageTypeDefOf.TaskCompletion,
                false
            );
        }

        public static void SpawnAllCEGear()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Log.Error("[HSK.Watcher] Не удалось найти активную карту для спавна оружия и боеприпасов.");
                return;
            }

            // Определяем центр спавна (позиция курсора мыши или центр карты)
            IntVec3 spawnCenter = UI.MouseCell();
            if (!spawnCenter.IsValid || !spawnCenter.InBounds(map))
            {
                spawnCenter = map.Center;
            }

            // Очищаем зону перед спавном
            ClearArea(spawnCenter, map, 10);

            // 1. Собираем всё оружие
            var weaponDefs = DefDatabase<ThingDef>.AllDefs
                .Where(def => def.IsWeapon && !def.generated)
                .ToList();

            // 2. Собираем все боеприпасы Combat Extended
            var ammoDefs = DefDatabase<ThingDef>.AllDefs
                .Where(def => !def.generated && (def.defName.StartsWith("Ammo_") || def.IsNutritionGivingIngestible == false && def.thingClass?.FullName?.Contains("Ammo") == true))
                .ToList();

            int spawnedCount = 0;
            int radius = 1;

            // Спавним оружие
            foreach (var weaponDef in weaponDefs)
            {
                IntVec3 spawnLoc = CellFinder.RandomClosewalkCellNear(spawnCenter, map, radius * 2, (IntVec3 c) => c.Walkable(map));
                if (spawnLoc.IsValid && spawnLoc.InBounds(map))
                {
                    Thing weapon = ThingMaker.MakeThing(weaponDef, weaponDef.MadeFromStuff ? GenStuff.RandomStuffFor(weaponDef) : null);

                    if (weapon.TryGetComp<CompQuality>() is CompQuality compQuality)
                    {
                        compQuality.SetQuality(QualityCategory.Normal, ArtGenerationContext.Colony);
                    }

                    GenSpawn.Spawn(weapon, spawnLoc, map);
                    spawnedCount++;
                }
            }

            // Спавним боеприпасы CE
            foreach (var ammoDef in ammoDefs)
            {
                IntVec3 spawnLoc = CellFinder.RandomClosewalkCellNear(spawnCenter, map, radius * 2, (IntVec3 c) => c.Walkable(map));
                if (spawnLoc.IsValid && spawnLoc.InBounds(map))
                {
                    Thing ammo = ThingMaker.MakeThing(ammoDef);
                    ammo.stackCount = ammo.def.stackLimit > 50 ? 50 : ammo.def.stackLimit;

                    GenSpawn.Spawn(ammo, spawnLoc, map);
                    spawnedCount++;
                }
            }

            Messages.Message(
                $"[HSK.Watcher] Спавн завершен. Создано единиц оружия и патронов CE: {spawnedCount}",
                MessageTypeDefOf.TaskCompletion,
                false
            );
        }

        [DebugAction("Autotests", "Spawn All CE Weapons & Ammo (HSK)", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSpawnAllCEGear()
        {
            SpawnAllCEGear();
        }

        [DebugAction("Autotests", "Spawn All Apparel (HSK)", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSpawnAllApparel()
        {
            SpawnAllApparel();
        }
    }
}
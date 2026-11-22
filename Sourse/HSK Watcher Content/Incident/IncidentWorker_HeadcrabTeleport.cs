using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace HSK.Watcher.Content.Headcrab
{
    // ============================================
    // INCIDENT: Портал Ламар (Half-Life референс)
    // ============================================
    public class IncidentWorker_LamarPortal : IncidentWorker
    {
        public static readonly string HeadcrabPawnKindDefName = "Headcrab";
        public static readonly string InsectFactionDefName = "Insect";

        // DefName предмета который выпадет (можно изменить)
        public static readonly string DropItemDefName = "HSK_GordonCrowbar";

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            // Нужна хотя бы одна свободная клетка внутри базы
            return map.mapPawns.FreeColonists.Any(c => c.Spawned && !c.Dead) &&
                   GetValidPortalCell(map).IsValid;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;

            // Находим клетку для портала (внутри или рядом с базой)
            IntVec3 portalCell = GetValidPortalCell(map);
            if (!portalCell.IsValid)
            {
                Log.Warning("[LamarPortal] Could not find valid cell for portal!");
                return false;
            }

            // Создаём эффект телепортации (вспышка)
            FleckMaker.Static(portalCell, map, FleckDefOf.ExplosionFlash, 5f);

            // Дополнительные эффекты — искры и дым
            for (int i = 0; i < 8; i++)
            {
                Vector3 randomOffset = new Vector3(Rand.Range(-0.5f, 0.5f), 0f, Rand.Range(-0.5f, 0.5f));
                FleckMaker.ThrowSmoke(portalCell.ToVector3Shifted() + randomOffset, map, 1f);
            }

            // Эффект входа в портал (если есть Royalty DLC)
            if (FleckDefOf.PsycastSkipFlashEntry != null)
            {
                FleckMaker.Static(portalCell, map, FleckDefOf.PsycastSkipFlashEntry, 3f);
            }

            // Звук телепортации (если есть)
            SoundDef teleportSound = DefDatabase<SoundDef>.GetNamed("TeleportOut", false);
            if (teleportSound != null)
            {
                teleportSound.PlayOneShot(SoundInfo.InMap(new TargetInfo(portalCell, map)));
            }

            // Спавним Хедкраба с эффектом телепортации
            Faction insectFaction = Find.FactionManager.FirstFactionOfDef(
                FactionDef.Named(InsectFactionDefName));
            if (insectFaction == null)
            {
                insectFaction = Find.FactionManager.AllFactions.FirstOrDefault(f =>
                    !f.IsPlayer && !f.defeated && f.def.defName.Contains("Insect"));
            }

            PawnKindDef headcrabKind = DefDatabase<PawnKindDef>.GetNamed(HeadcrabPawnKindDefName, false);
            if (headcrabKind != null)
            {
                Pawn headcrab = PawnGenerator.GeneratePawn(headcrabKind, insectFaction);
                GenSpawn.Spawn(headcrab, portalCell, map);

                // Эффект появления — используем существующий FleckDefOf
                FleckMaker.Static(headcrab.Position, map, FleckDefOf.ExplosionFlash, 3f);

               

                // Регистрируем в трекере
                var tracker = map.GetComponent<MapComponent_HeadcrabTracker>();
                if (tracker == null)
                {
                    tracker = new MapComponent_HeadcrabTracker(map);
                    map.components.Add(tracker);
                }
                tracker.RegisterHeadcrab(headcrab);
            }

            // Спавним предмет (лекарство/другое)
            ThingDef itemDef = DefDatabase<ThingDef>.GetNamed(DropItemDefName, false);
            if (itemDef != null)
            {
                Thing item = ThingMaker.MakeThing(itemDef);
                if (item != null)
                {
                    GenPlace.TryPlaceThing(item, portalCell, map, ThingPlaceMode.Near);
                    FleckMaker.Static(item.Position, map, FleckDefOf.ExplosionFlash, 1.5f);
                }
            }

            // Отправляем письмо
            SendStandardLetter(
                "LetterLabelLamarPortal".Translate(),
                "LetterLamarPortal".Translate(),
                LetterDefOf.ThreatSmall,
                parms,
                new TargetInfo(portalCell, map)
            );

            return true;
        }

        // Находим валидную клетку для портала (внутри домашней зоны или рядом с колонистами)
        private IntVec3 GetValidPortalCell(Map map)
        {
            List<Pawn> colonists = map.mapPawns.FreeColonists
                .Where(c => c.Spawned && !c.Dead)
                .ToList();

            if (colonists.Count == 0) return IntVec3.Invalid;

            // Пробуем найти клетку рядом с колонистами (в пределах 10 клеток)
            for (int attempt = 0; attempt < 50; attempt++)
            {
                Pawn randomColonist = colonists[Rand.Range(0, colonists.Count)];

                IntVec3 candidate = randomColonist.Position + new IntVec3(
                    Rand.Range(-8, 9),
                    0,
                    Rand.Range(-8, 9)
                );

                if (IsValidPortalCell(candidate, map))
                {
                    return candidate;
                }
            }

            // Fallback — рандомная клетка в домашней зоне
            if (map.areaManager.Home != null && map.areaManager.Home.ActiveCells.Any())
            {
                List<IntVec3> homeCells = map.areaManager.Home.ActiveCells.ToList();
                for (int i = 0; i < 20; i++)
                {
                    IntVec3 candidate = homeCells[Rand.Range(0, homeCells.Count)];
                    if (IsValidPortalCell(candidate, map))
                        return candidate;
                }
            }

            return IntVec3.Invalid;
        }

        // Проверка валидности клетки
        private bool IsValidPortalCell(IntVec3 cell, Map map)
        {
            if (!cell.IsValid) return false;
            if (!cell.InBounds(map)) return false;
            if (!cell.Standable(map)) return false;
            if (cell.Fogged(map)) return false;
            if (cell.Impassable(map)) return false;

            // Проверяем, что клетка не заблокирована постройками
            foreach (Thing thing in cell.GetThingList(map))
            {
                if (thing.def.IsBuildingArtificial && thing.def.passability == Traversability.Impassable)
                    return false;
            }

            return true;
        }
    }
}
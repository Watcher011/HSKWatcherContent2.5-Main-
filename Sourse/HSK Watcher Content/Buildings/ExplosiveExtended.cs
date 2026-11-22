using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace HSK.Watcher.Content.Buildings
{
    public class CompProperties_ExplosiveWatcher : CompProperties_Explosive
    {
        // Используем строковый идентификатор вместо прямого Def
        public string explosiveDamageTypeDefName = "Bomb";

        // Остальные поля оставляем как есть
        public new float explosiveRadius = 4.5f;
        public new int damageAmountBase = 90;
        public new float armorPenetrationBase = 0.25f;

        public float detectionRadius = 5f;
        public bool autoDetonateOnEnemy = true;
        public bool detonateOnAlly = false;
        public bool detonateOnAnimals = false;
        public bool detonateOnNeutral = false;

        public Color allyColor = Color.green;
        public Color enemyColor = Color.red;
        public Color neutralColor = Color.yellow;

        // Кэш для resolved DamageDef
        [Unsaved]
        private DamageDef resolvedDamageType;

        // Свойство для безопасного получения DamageDef
        public DamageDef ExplosiveDamageType
        {
            get
            {
                if (resolvedDamageType == null && explosiveDamageTypeDefName != null)
                {
                    resolvedDamageType = DefDatabase<DamageDef>.GetNamedSilentFail(explosiveDamageTypeDefName);
                    if (resolvedDamageType == null)
                    {
                        //Log.Warning($"HSK.Watcher: DamageDef '{explosiveDamageTypeDefName}' not found, defaulting to 'Bomb'");
                        resolvedDamageType = DamageDefOf.Bomb;
                    }
                }
                return resolvedDamageType;
            }
        }

        public CompProperties_ExplosiveWatcher()
        {
            compClass = typeof(CompExplosiveWatcher);
        }

        // Метод для ResolveReferences (если нужно)
        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);

            // Инициализируем кэш заранее
            var _ = ExplosiveDamageType; // Это вызовет инициализацию
        }
    }

    public class CompExplosiveWatcher : CompExplosive
    {
        private new CompProperties_ExplosiveWatcher Props =>
            (CompProperties_ExplosiveWatcher)props;

        private bool enemyDetected;
        private bool allyDetected;
        private bool neutralDetected;
        private int lastCheckTick;
        private const int CHECK_INTERVAL = 30;

        public override void CompTick()
        {
            base.CompTick();

            if (Find.TickManager.TicksGame <= lastCheckTick + CHECK_INTERVAL) return;

            CheckEntitiesInRadius();
            lastCheckTick = Find.TickManager.TicksGame;

            if (Props.autoDetonateOnEnemy && enemyDetected)
            {
                DoInstantExplosion();
            }
        }

        private void CheckEntitiesInRadius()
        {
            enemyDetected = false;
            allyDetected = false;
            neutralDetected = false;

            if (parent.Map == null || !parent.Spawned) return;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(parent.Position,
                                                                   Props.detectionRadius,
                                                                   useCenter: true))
            {
                if (!cell.InBounds(parent.Map)) continue;

                foreach (Thing t in cell.GetThingList(parent.Map))
                {
                    if (t == parent) continue;
                    if (t is Pawn p) CheckPawn(p);
                    if (ShouldDetonateNow()) return;
                }
            }
        }

        private void CheckPawn(Pawn pawn)
        {
            if (pawn.Dead || pawn.Downed) return;

            if (pawn.RaceProps.Animal)
            {
                if (!Props.detonateOnAnimals) return;

                if (pawn.Faction == Faction.OfPlayer) allyDetected = true;
                else if (pawn.Faction?.HostileTo(Faction.OfPlayer) ?? false) enemyDetected = true;
                else neutralDetected = true;
                return;
            }

            if (pawn.Faction == null) return;

            if (pawn.Faction == Faction.OfPlayer) allyDetected = true;
            else if (pawn.Faction.HostileTo(Faction.OfPlayer)) enemyDetected = true;
            else neutralDetected = true;
        }

        private bool ShouldDetonateNow()
        {
            return (enemyDetected && Props.autoDetonateOnEnemy) ||
                   (allyDetected && Props.detonateOnAlly) ||
                   (neutralDetected && Props.detonateOnNeutral);
        }

        private void DoInstantExplosion()
        {
            if (parent.Map == null) return;

            // Используем безопасное свойство вместо прямого поля
            GenExplosion.DoExplosion(
                center: parent.Position,
                map: parent.Map,
                radius: Props.explosiveRadius,
                damType: Props.ExplosiveDamageType, // ← Используем свойство
                instigator: parent,
                damAmount: Props.damageAmountBase,
                armorPenetration: Props.armorPenetrationBase,
                explosionSound: null,
                weapon: null,
                projectile: null,
                intendedTarget: null,
                postExplosionSpawnThingDef: null,
                postExplosionSpawnChance: 0f,
                postExplosionSpawnThingCount: 1,
                applyDamageToExplosionCellsNeighbors: false,
                preExplosionSpawnThingDef: null,
                preExplosionSpawnChance: 0f,
                preExplosionSpawnThingCount: 1,
                chanceToStartFire: 0f,
                damageFalloff: false,
                direction: null,
                ignoredThings: null);

            parent.Destroy();
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (!Find.Selector.IsSelected(parent)) return;

            Color ringColor = enemyDetected ? Props.enemyColor :
                               allyDetected ? Props.allyColor :
                               neutralDetected ? Props.neutralColor :
                               new Color(0.3f, 0.3f, 0.3f, 0.2f);

            GenDraw.DrawRadiusRing(parent.Position, Props.detectionRadius, ringColor);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;

            if (enemyDetected && !Props.autoDetonateOnEnemy)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Взорвать (враг обнаружен)",
                    defaultDesc = "Немедленный взрыв из-за врага в радиусе",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Detonate", true),
                    action = DoInstantExplosion
                };
            }

            yield return new Command_Action
            {
                defaultLabel = "Ручной взрыв",
                defaultDesc = "Принудительно активировать взрыв",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack", true),
                action = DoInstantExplosion
            };

            if (DebugSettings.godMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Тест: Проверить радиус",
                    defaultDesc = "Запустить проверку радиуса сейчас",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack", true),
                    action = () =>
                    {
                        CheckEntitiesInRadius();
                        Messages.Message(
                            $"Проверка: Враг={enemyDetected}  Союзник={allyDetected}",
                            MessageTypeDefOf.NeutralEvent);
                    }
                };
            }
        }

        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned) return base.CompInspectStringExtra();

            string status = enemyDetected ? "<color=red>● КРАСНЫЙ: Враг обнаружен!</color>" :
                           allyDetected ? "<color=green>● ЗЕЛЕНЫЙ: Союзник в радиусе</color>" :
                           neutralDetected ? "<color=yellow>● ЖЕЛТЫЙ: Нейтрал в радиусе</color>" :
                           "○ Радиус чист";

            return $"Радиус обнаружения: {Props.detectionRadius:F1}\n{status}";
        }
    }
}
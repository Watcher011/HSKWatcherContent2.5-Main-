//using HarmonyLib;
//using LudeonTK;
//using RimWorld;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reflection;
//using UnityEngine;
//using Verse;

//namespace HSK.Watcher.Content
//{
//    // ============================================
//    // КАСТОМНЫЙ ThingDef ДЛЯ ГАЗА
//    // ============================================
//    public class CustomGasDef : ThingDef
//    {
//        public Color gasColor = new Color(1f, 1f, 1f, 0.5f);
//        public HediffDef hediffToApply;
//        public float hediffSeverityPerSecond = 0.01f;
//        public float hediffMaxSeverity = 1.0f;
//        public float hediffApplyChance = 1.0f;
//        public int tickInterval = 60;
//        public int gasDurationTicks = 2500;
//        public float density = 1.0f;
//        public SoundDef enterSound;
//        public EffecterDef effecterDef;
//    }

//    // ============================================
//    // КАСТОМНЫЙ ГАЗ — наследуем Thing вместо Gas
//    // ============================================
//    public class CustomGas : Thing
//    {
//        private CustomGasDef customDef;
//        private int age;
//        private Graphic cachedGraphic;

//        public override Graphic Graphic
//        {
//            get
//            {
//                if (customDef != null && cachedGraphic == null)
//                {
//                    try
//                    {
//                        Color c = customDef.gasColor;
//                        float alpha = c.a * customDef.density;
//                        if (customDef.gasDurationTicks > 0 && age > 0)
//                        {
//                            float lifeFactor = 1f - (float)age / customDef.gasDurationTicks;
//                            alpha *= Mathf.Max(lifeFactor, 0f);
//                        }
//                        Color drawColor = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));

//                        if (def.graphicData == null)
//                        {
//                            Log.Warning("[HSK.Watcher.CustomGas] def.graphicData is null");
//                            return base.Graphic;
//                        }

//                        string texPath = def.graphicData.texPath ?? "Things/Gas/Puff";
//                        cachedGraphic = GraphicDatabase.Get(
//                            typeof(Graphic_Gas),
//                            texPath,
//                            ShaderDatabase.Transparent,
//                            def.graphicData.drawSize,
//                            drawColor,
//                            drawColor
//                        );
//                    }
//                    catch (Exception ex)
//                    {
//                        Log.Warning($"[HSK.Watcher.CustomGas] Graphic failed: {ex}");
//                        return base.Graphic;
//                    }
//                    return cachedGraphic;
//                }
//                return base.Graphic;
//            }
//        }

//        public override void SpawnSetup(Map map, bool respawningAfterLoad)
//        {
//            Log.Message($"[HSK.Watcher.CustomGas] === SpawnSetup START ===");
//            Log.Message($"[HSK.Watcher.CustomGas] def={def?.defName}, defType={def?.GetType().Name}");

//            customDef = def as CustomGasDef;

//            if (customDef == null && !respawningAfterLoad)
//            {
//                Log.Error($"[HSK.Watcher.CustomGas] CRITICAL: def is NOT CustomGasDef! Destroying.");
//                this.Destroy();
//                return;
//            }

//            age = 0;

//            try
//            {
//                base.SpawnSetup(map, respawningAfterLoad);
//                Log.Message("[HSK.Watcher.CustomGas] === SpawnSetup SUCCESS ===");
//            }
//            catch (Exception ex)
//            {
//                Log.Error($"[HSK.Watcher.CustomGas] base.SpawnSetup FAILED: {ex}");
//                throw;
//            }
//        }

//        // ИСПРАВЛЕНО: protected override вместо public override
//        protected override void Tick()
//        {
//            base.Tick();

//            if (Destroyed) return;

//            age++;

//            if (customDef != null && customDef.gasDurationTicks > 0 && age >= customDef.gasDurationTicks)
//            {
//                Destroy();
//                return;
//            }

//            if (customDef != null && age % customDef.tickInterval == 0)
//            {
//                ApplyHediffs();
//            }
//        }

//        private void ApplyHediffs()
//        {
//            if (customDef?.hediffToApply == null) return;

//            List<Thing> things = Map.thingGrid.ThingsListAt(Position);
//            foreach (Thing t in things)
//            {
//                if (t is Pawn p && p.RaceProps.IsFlesh)
//                {
//                    if (Rand.Value > customDef.hediffApplyChance) continue;

//                    Hediff h = p.health.hediffSet.GetFirstHediffOfDef(customDef.hediffToApply);
//                    if (h == null)
//                    {
//                        h = HediffMaker.MakeHediff(customDef.hediffToApply, p);
//                        h.Severity = customDef.hediffSeverityPerSecond;
//                        p.health.AddHediff(h);
//                    }
//                    else
//                    {
//                        h.Severity = Mathf.Min(
//                            h.Severity + customDef.hediffSeverityPerSecond,
//                            customDef.hediffMaxSeverity
//                        );
//                    }
//                }
//            }
//        }

//        public override void ExposeData()
//        {
//            base.ExposeData();
//            Scribe_Values.Look(ref age, "customGasAge", 0);
//        }
//    }

//    // ============================================
//    // ХЕЛПЕР ДЛЯ СПАВНА ГАЗА
//    // ============================================
//    public static class CustomGasExplosionHelper
//    {
//        public static void SpawnCustomGas(IntVec3 center, Map map, CustomGasDef gasDef, float radius)
//        {
//            if (gasDef == null || map == null) return;

//            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
//            {
//                if (!cell.InBounds(map) || !cell.Walkable(map)) continue;

//                bool hasGas = false;
//                List<Thing> things = map.thingGrid.ThingsListAt(cell);
//                foreach (Thing t in things)
//                {
//                    if (t.def.category == ThingCategory.Gas) { hasGas = true; break; }
//                }
//                if (hasGas) continue;

//                float dist = cell.DistanceTo(center);
//                float chance = 1f - (dist / radius) * 0.5f;
//                chance = Mathf.Clamp01(chance);

//                if (Rand.Value <= chance)
//                {
//                    try
//                    {
//                        Thing gas = ThingMaker.MakeThing(gasDef);
//                        if (gas != null)
//                            GenPlace.TryPlaceThing(gas, cell, map, ThingPlaceMode.Direct);
//                    }
//                    catch (Exception ex)
//                    {
//                        Log.Error($"[HSK.Watcher.CustomGas] Failed to spawn gas at {cell}: {ex}");
//                    }
//                }
//            }
//        }
//    }

//    // ============================================
//    // HARMONY ПАТЧ: BulletCE.Impact
//    // ============================================
//    [HarmonyPatch]
//    public static class Patch_BulletCE_Impact_SpawnGas
//    {
//        static MethodBase TargetMethod()
//        {
//            Type bulletCEType = AccessTools.TypeByName("CombatExtended.BulletCE");
//            if (bulletCEType != null)
//            {
//                return AccessTools.Method(bulletCEType, "Impact", new[] { typeof(Thing) });
//            }
//            return null;
//        }

//        public static void Postfix(Thing __instance)
//        {
//            if (__instance == null) return;

//            if (__instance.def.defName != "MyCustom_Proj_GrenadeHealingGas")
//                return;

//            Map map = __instance.Map;
//            IntVec3 position = __instance.Position;

//            Log.Message($"[HSK.Watcher.CustomGas] === BulletCE Impact: {__instance.def.defName} at {position}, map={map != null}");

//            if (map == null)
//            {
//                Thing launcher = GetLauncher(__instance);
//                if (launcher != null)
//                {
//                    map = launcher.Map;
//                    Log.Message($"[HSK.Watcher.CustomGas] Got map from launcher: {map != null}");
//                }
//            }

//            if (map == null)
//            {
//                Log.Error("[HSK.Watcher.CustomGas] Map is null! Cannot spawn gas.");
//                return;
//            }

//            var gasDef = DefDatabase<ThingDef>.GetNamed("MyCustom_HealingGas", false) as CustomGasDef;
//            if (gasDef == null)
//            {
//                Log.Error("[HSK.Watcher.CustomGas] MyCustom_HealingGas def not found!");
//                return;
//            }

//            Log.Message($"[HSK.Watcher.CustomGas] Spawning gas at {position}, radius=4");
//            CustomGasExplosionHelper.SpawnCustomGas(position, map, gasDef, 4f);
//        }

//        private static Thing GetLauncher(Thing projectile)
//        {
//            try
//            {
//                FieldInfo field = projectile.GetType().GetField("launcher",
//                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
//                if (field != null) return field.GetValue(projectile) as Thing;

//                PropertyInfo prop = projectile.GetType().GetProperty("Launcher",
//                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
//                if (prop != null) return prop.GetValue(projectile) as Thing;
//            }
//            catch { }
//            return null;
//        }
//    }

//    // ============================================
//    // КНОПКИ В МЕНЮ ОТЛАДКИ
//    // ============================================
//    public static class CustomGasDebugActions
//    {
//        private static List<CustomGasDef> GetCustomGasDefs()
//        {
//            List<CustomGasDef> result = new List<CustomGasDef>();
//            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
//            {
//                if (def is CustomGasDef customGas)
//                    result.Add(customGas);
//            }
//            return result;
//        }

//        [DebugAction("General", "Add custom gas...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
//        private static void AddCustomGas()
//        {
//            List<CustomGasDef> gasDefs = GetCustomGasDefs();
//            if (gasDefs.Count == 0)
//            {
//                Messages.Message("No CustomGasDefs found!", MessageTypeDefOf.RejectInput);
//                return;
//            }

//            List<DebugMenuOption> options = new List<DebugMenuOption>();

//            foreach (CustomGasDef gasDef in gasDefs)
//            {
//                CustomGasDef localDef = gasDef;
//                options.Add(new DebugMenuOption(localDef.label, DebugMenuOptionMode.Action, delegate
//                {
//                    List<DebugMenuOption> radiusOptions = new List<DebugMenuOption>();
//                    float[] radii = new float[] { 1f, 2f, 3f, 4f, 5f, 7f, 10f };

//                    foreach (float radius in radii)
//                    {
//                        float localRadius = radius;
//                        radiusOptions.Add(new DebugMenuOption(
//                            "Radius: " + radius,
//                            DebugMenuOptionMode.Action,
//                            delegate
//                            {
//                                DebugTools.curTool = new DebugTool(
//                                    "Place " + localDef.label,
//                                    delegate
//                                    {
//                                        Map map = Find.CurrentMap;
//                                        if (map == null) return;

//                                        IntVec3 center = UI.MouseCell();
//                                        CustomGasExplosionHelper.SpawnCustomGas(center, map, localDef, localRadius);

//                                        Messages.Message(
//                                            "Added " + localDef.label + " (radius " + localRadius + ")",
//                                            MessageTypeDefOf.TaskCompletion
//                                        );
//                                    }
//                                );
//                            }
//                        ));
//                    }

//                    Find.WindowStack.Add(new Dialog_DebugOptionListLister(radiusOptions));
//                }));
//            }

//            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
//        }

//        [DebugAction("General", "Clear all custom gas", allowedGameStates = AllowedGameStates.PlayingOnMap)]
//        private static void ClearAllCustomGas()
//        {
//            Map map = Find.CurrentMap;
//            if (map == null) return;

//            int cleared = 0;
//            List<Thing> thingsToDestroy = new List<Thing>();

//            foreach (Thing thing in map.listerThings.AllThings)
//            {
//                if (thing is CustomGas)
//                    thingsToDestroy.Add(thing);
//            }

//            foreach (Thing thing in thingsToDestroy)
//            {
//                thing.Destroy();
//                cleared++;
//            }

//            Messages.Message("Cleared " + cleared + " custom gas cells", MessageTypeDefOf.TaskCompletion);
//        }

//        [DebugAction("General", "Test spawn gas at cursor", allowedGameStates = AllowedGameStates.PlayingOnMap)]
//        private static void TestSpawnGasAtCursor()
//        {
//            Map map = Find.CurrentMap;
//            if (map == null) return;

//            var gasDefs = GetCustomGasDefs();
//            if (gasDefs.Count == 0)
//            {
//                Messages.Message("No CustomGasDefs found!", MessageTypeDefOf.RejectInput);
//                return;
//            }

//            var gasDef = gasDefs[0];
//            IntVec3 center = UI.MouseCell();

//            try
//            {
//                Thing testThing = ThingMaker.MakeThing(gasDef);
//                if (testThing != null)
//                {
//                    GenPlace.TryPlaceThing(testThing, center, map, ThingPlaceMode.Direct);
//                    Messages.Message("Test spawn succeeded!", MessageTypeDefOf.TaskCompletion);
//                }
//                else
//                {
//                    Log.Error("[HSK.Watcher.CustomGas] ThingMaker.MakeThing returned null!");
//                }
//            }
//            catch (Exception ex)
//            {
//                Log.Error($"[HSK.Watcher.CustomGas] TEST SPAWN FAILED: {ex}");
//            }
//        }
//    }

//    // ============================================
//    // МОД ИНИЦИАЛИЗАЦИЯ
//    // ============================================
//    [StaticConstructorOnStartup]
//    public static class CustomGasModInitializer
//    {
//        static CustomGasModInitializer()
//        {
//            try
//            {
//                Harmony harmony = new Harmony("HSK.Watcher.CustomGas");

//                var patch = typeof(Patch_BulletCE_Impact_SpawnGas);
//                var targetMethod = (MethodBase)patch.GetMethod("TargetMethod", BindingFlags.Static | BindingFlags.NonPublic)
//                    ?.Invoke(null, null);

//                if (targetMethod != null)
//                {
//                    harmony.Patch(targetMethod, postfix: new HarmonyMethod(patch.GetMethod("Postfix")));
//                    Log.Message("[HSK.Watcher.CustomGas] Patched BulletCE.Impact");
//                }
//                else
//                {
//                    Log.Warning("[HSK.Watcher.CustomGas] Could not find BulletCE.Impact to patch");
//                }

//                Log.Message("[HSK.Watcher.CustomGas] Harmony patches applied successfully.");
//            }
//            catch (Exception ex)
//            {
//                Log.Error("[HSK.Watcher.CustomGas] Failed to apply Harmony patches: " + ex);
//            }
//        }
//    }
//}

using HarmonyLib;
using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace HSK.Watcher.Content
{
    // ==================== КАСТОМНЫЙ ГАЗ (Thing) ====================
    public class CustomGas : Thing
    {
        private int destroyTick;
        public float graphicRotation;
        public float graphicRotationSpeed;
        private CustomGasConfig Config => def.GetModExtension<CustomGasConfig>();

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                int expireTicks = GenTicks.SecondsToTicks((float)(Config?.expireSeconds ?? 30));
                destroyTick = Find.TickManager.TicksGame + expireTicks;
            }
            graphicRotationSpeed = Rand.Range(-10f, 10f) / 60f;
        }

        protected override void Tick()
        {
            base.Tick();
            if (Find.TickManager.TicksGame >= destroyTick)
            {
                Destroy();
                return;
            }
            graphicRotation += graphicRotationSpeed;
            if (GenTicks.TicksGame % 60 == 0)
            {
                ApplyEffect();
            }
        }

        private void ApplyEffect()
        {
            if (Config == null) return;

            List<Thing> things = Position.GetThingList(Map);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is Pawn pawn && pawn.RaceProps.IsFlesh && !pawn.Dead)
                {
                    // Мгновенное убийство насекомых
                    if (Config.instantKillInsects && pawn.RaceProps.Insect)
                    {
                        pawn.Kill(null);
                        continue;
                    }

                    // Остальным — хедифф (если указан)
                    if (Config.hediffDef == null) continue;

                    float protection = GetGasProtection(pawn);
                    float effectiveSeverity = Config.severityPerTick * (1f - protection);
                    if (effectiveSeverity <= 0f) continue;

                    Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(Config.hediffDef);
                    if (h == null)
                    {
                        h = HediffMaker.MakeHediff(Config.hediffDef, pawn);
                        h.Severity = effectiveSeverity;
                        pawn.health.AddHediff(h);
                    }
                    else
                    {
                        h.Severity = Mathf.Min(h.Severity + effectiveSeverity, Config.maxSeverity);
                    }
                }
            }
        }

        private float GetGasProtection(Pawn pawn)
        {
            float protection = 0f;
            StatDef toxicRes = DefDatabase<StatDef>.GetNamed("ToxicEnvironmentResistance", false);
            if (toxicRes != null)
                protection = Mathf.Max(protection, pawn.GetStatValue(toxicRes, true));

            StatDef gasProt = DefDatabase<StatDef>.GetNamed("SmokeSensitivity", false);
            if (gasProt != null)
            {
                float sens = pawn.GetStatValue(gasProt, true);
                protection = Mathf.Max(protection, 1f - sens);
            }
            return Mathf.Clamp01(protection);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref destroyTick, "destroyTick", 0);
            Scribe_Values.Look(ref graphicRotation, "graphicRotation", 0f);
            Scribe_Values.Look(ref graphicRotationSpeed, "graphicRotationSpeed", 0f);
        }
    }

    // ==================== КОНФИГ ГАЗА (ModExtension) ====================
    public class CustomGasConfig : DefModExtension
    {
        public HediffDef hediffDef;
        public float severityPerTick = 0.02f;
        public float maxSeverity = 1.0f;
        public int expireSeconds = 30;
        public Color drawColor = Color.white;
        public float drawAlpha = 0.5f;
        public bool instantKillInsects = false;
    }

    // ==================== КАСТОМНЫЙ ГРАФИК ГАЗА ====================
    public class Graphic_CustomGasCloud : Graphic_Single
    {
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            if (thing == null) return;
            CustomGasConfig config = thingDef.GetModExtension<CustomGasConfig>();
            Texture2D tex = MatSingle.mainTexture as Texture2D;
            if (tex == null) return;

            Rand.PushState();
            Rand.Seed = thing.thingIDNumber.GetHashCode();

            float angle = Rand.Range(0, 360);
            if (thing is CustomGas gas) angle += gas.graphicRotation;

            Vector3 pos = thing.TrueCenter() + new Vector3(Rand.Range(-0.45f, 0.45f), 0f, Rand.Range(-0.45f, 0.45f));
            Vector3 s = new Vector3(Rand.Range(0.8f, 1.2f) * drawSize.x, 0f, Rand.Range(0.8f, 1.2f) * drawSize.y);

            Color color = config?.drawColor ?? Color.white;
            color.a = config?.drawAlpha ?? 0.5f;

            Material mat = MaterialPool.MatFrom(tex, ShaderDatabase.GasRotating, color);

            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(pos, Quaternion.AngleAxis(angle, Vector3.up), s);
            Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0);

            Rand.PopState();
        }
    }

    // ==================== КОНФИГ СНАРЯДА (ModExtension) ====================
    public class GasProjectileConfig : DefModExtension
    {
        public string gasDefName = "MyCustom_Gas_Healing";
        public float radius = 4f;
        public int gasCount = 40;
    }

    // ==================== ПАТЧ НА ВЗРЫВ ГРАНАТЫ CE ====================
    [HarmonyPatch]
    public static class Patch_ProjectileCE_Explode_SpawnCustomGas
    {
        static MethodBase TargetMethod()
        {
            Type projectileCEType = AccessTools.TypeByName("CombatExtended.ProjectileCE");
            if (projectileCEType != null)
            {
                MethodInfo method = AccessTools.Method(projectileCEType, "Explode");
                if (method != null) return method;
            }

            Type bulletCEType = AccessTools.TypeByName("CombatExtended.BulletCE");
            if (bulletCEType != null)
            {
                return AccessTools.Method(bulletCEType, "Impact", new[] { typeof(Thing) });
            }

            return null;
        }

        public static void Postfix(Thing __instance)
        {
            if (__instance == null) return;

            GasProjectileConfig projConfig = __instance.def.GetModExtension<GasProjectileConfig>();
            if (projConfig == null) return;

            Map map = __instance.Map;
            IntVec3 position = __instance.Position;

            if (map == null)
            {
                Thing launcher = GetLauncher(__instance);
                if (launcher != null) map = launcher.Map;
            }

            if (map == null) return;

            ThingDef gasDef = DefDatabase<ThingDef>.GetNamed(projConfig.gasDefName, false);
            if (gasDef == null)
            {
                Log.Error("[HSK.Watcher.CustomGas] Gas def not found: " + projConfig.gasDefName);
                return;
            }

            List<IntVec3> cells = GenRadial.RadialCellsAround(position, projConfig.radius, true)
                .Where(c => c.InBounds(map) && c.Walkable(map))
                .ToList();

            for (int i = 0; i < projConfig.gasCount && cells.Count > 0; i++)
            {
                IntVec3 cell = cells[Rand.Range(0, cells.Count)];
                Thing gas = ThingMaker.MakeThing(gasDef);
                GenSpawn.Spawn(gas, cell, map);
            }
        }

        private static Thing GetLauncher(Thing projectile)
        {
            try
            {
                PropertyInfo prop = projectile.GetType().GetProperty("Launcher",
                    BindingFlags.Public | BindingFlags.Instance);
                if (prop != null) return prop.GetValue(projectile) as Thing;

                FieldInfo field = projectile.GetType().GetField("launcher",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (field != null) return field.GetValue(projectile) as Thing;
            }
            catch { }
            return null;
        }
    }

    // ==================== DEBUG ACTIONS ====================
    public static class CustomGasDebugActions
    {
        [DebugAction("General", "Spawn custom gas", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSpawnGas()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            ThingDef gasDef = DefDatabase<ThingDef>.AllDefsListForReading
                .FirstOrDefault(d => d.thingClass == typeof(CustomGas));

            if (gasDef == null)
            {
                Messages.Message("No custom gas defs found", MessageTypeDefOf.RejectInput);
                return;
            }

            IntVec3 center = UI.MouseCell();
            for (int i = 0; i < 20; i++)
            {
                IntVec3 cell = center + GenRadial.RadialPattern[Rand.Range(0, GenRadial.NumCellsInRadius(3f))];
                if (cell.InBounds(map) && cell.Walkable(map))
                {
                    Thing gas = ThingMaker.MakeThing(gasDef);
                    GenSpawn.Spawn(gas, cell, map);
                }
            }
            Messages.Message("Spawned " + gasDef.label, MessageTypeDefOf.TaskCompletion);
        }

        [DebugAction("General", "Clear all custom gas", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugClearGas()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            foreach (Thing thing in map.listerThings.AllThings.ToList())
            {
                if (thing is CustomGas)
                {
                    thing.Destroy();
                }
            }
            Messages.Message("Cleared all custom gas", MessageTypeDefOf.TaskCompletion);
        }
    }

    // ==================== INITIALIZER ====================
    [StaticConstructorOnStartup]
    public static class CustomGasModInitializer
    {
        static CustomGasModInitializer()
        {
            try
            {
                Harmony harmony = new Harmony("HSK.Watcher.CustomGas");

                var patch = typeof(Patch_ProjectileCE_Explode_SpawnCustomGas);
                var target = (MethodBase)patch.GetMethod("TargetMethod", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
                if (target != null)
                {
                    harmony.Patch(target, postfix: new HarmonyMethod(patch.GetMethod("Postfix")));
                    Log.Message("[HSK.Watcher.CustomGas] Patched: " + target.DeclaringType?.Name + "." + target.Name);
                }
                else
                {
                    Log.Warning("[HSK.Watcher.CustomGas] TargetMethod not found.");
                }

                Log.Message("[HSK.Watcher.CustomGas] Initialized.");
            }
            catch (Exception ex)
            {
                Log.Error("[HSK.Watcher.CustomGas] Init failed: " + ex);
            }
        }
    }
}
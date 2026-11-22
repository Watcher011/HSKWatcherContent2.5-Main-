//using RimWorld;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.Rendering;
//using Verse;



//namespace HSK.Watcher.Content
//{
//    // =====================================================================
//    // 1. CompProperties прожектора (XML-параметры)
//    // =====================================================================
//    public class CompProperties_Spotlight : CompProperties
//    {
//        public float beamAngle = 45f;
//        public float beamLength = 10f;
//        public float sourceOffset = 0.5f;
//        public Color beamColor = new Color(1f, 0.98f, 0.85f, 0.75f);
//        public float brightness = 1.6f;

//        public bool rotatable = true;
//        public float rotationSpeed = 90f;
//        public float rotationStep = 45f;

//        public float angleOffset = 0f;
//        public float angleNorth = 0f;
//        public float angleEast = 90f;
//        public float angleSouth = 180f;
//        public float angleWest = 270f;

//        /// <summary>
//        /// true  — прожектор реально освещает через фантомные эмиттеры,
//        ///         зарегистрированные в ванильном GlowGrid;
//        /// false — только визуальный луч, освещение не трогаем.
//        /// </summary>
//        public bool affectsLight = false;

//        // --- Параметры фантомных эмиттеров ---
//        public string emitterDefName = "HSK_SpotlightEmitter";
//        public int emitterRows = 5;
//        public float emitterSpacing = 1.6f;

//        // TTL-страховка (в тиках) для фантомов: пока лампа жива и
//        // работает, она продлевает жизнь своих фантомов каждый тик на
//        // это значение вперёд. Если продление прекращается (лампа
//        // уничтожена/сломалась и штатная очистка не сработала), общий
//        // сборщик на карте уберёт их не позже чем через этот срок.
//        // Ставьте с запасом — намного больше интервала обычной проверки
//        // сборщика (по умолчанию 500 тиков).
//        public int phantomLifespanTicks = 900; // ~15 игровых секунд

//        public float AngleForRotation(Rot4 rot)
//        {
//            float baseAngle;
//            if (rot == Rot4.North) baseAngle = angleNorth;
//            else if (rot == Rot4.East) baseAngle = angleEast;
//            else if (rot == Rot4.South) baseAngle = angleSouth;
//            else if (rot == Rot4.West) baseAngle = angleWest;
//            else baseAngle = rot.AsAngle;
//            return baseAngle + angleOffset;
//        }

//        public CompProperties_Spotlight()
//        {
//            compClass = typeof(CompSpotlight);
//        }
//    }

//    // =====================================================================
//    // 2. CompProperties фантомного эмиттера
//    //    Наследуемся от CompProperties_Glower, чтобы переиспользовать
//    //    все ванильные поля (glowRadius, glowColor, overlightRadius).
//    // =====================================================================
//    public class CompProperties_SpotlightEmitter : CompProperties_Glower
//    {
//        public CompProperties_SpotlightEmitter()
//        {
//            compClass = typeof(CompGlowerSpotlight);
//        }
//    }

//    // =====================================================================
//    // 3. CompGlower фантомного эмиттера
//    //    Голая обёртка: реальная регистрация в GlowGrid происходит в
//    //    ванильном CompGlower.PostSpawnSetup / PostDestroy. Нам нужен
//    //    только ownerThingID, чтобы находить свои эмиттеры после load,
//    //    и expiresAtTick — TTL-страховка на случай осиротения (см.
//    //    MapComponent_PhantomLifespan ниже).
//    // =====================================================================
//    public class CompGlowerSpotlight : CompGlower
//    {
//        public int ownerThingID = -1;

//        // Абсолютный игровой тик, до которого этот фантом ещё считается
//        // "живым". Хозяин (CompSpotlight) продлевает его каждый тик, пока
//        // работает. Если продление прекращается — общий сборщик на карте
//        // (один таймер на всех, не по одному на фантом) рано или поздно
//        // его уничтожит.
//        public int expiresAtTick = -1;

//        // Начальный грейс-период (тики) для фантома, у которого ещё НЕТ
//        // назначенного expiresAtTick — покрывает как самый первый момент
//        // после спавна нового фантома (хозяин ещё не успел продлить), так
//        // и уже осиротевшие фантомы из старых сейвов (сохранены до того,
//        // как в моде появился expiresAtTick вообще — при загрузке у них
//        // будет дефолтное -1). Если у фантома есть живой хозяин, он
//        // продлит expiresAtTick раньше, чем истечёт этот грейс-период.
//        // Если хозяина нет — фантом сам себя обнулит по истечении срока.
//        private const int InitialGraceTicks = 900; // ~15 игровых секунд

//        public override void PostSpawnSetup(bool respawningAfterLoad)
//        {
//            base.PostSpawnSetup(respawningAfterLoad);

//            if (expiresAtTick <= 0)
//                expiresAtTick = Find.TickManager.TicksGame + InitialGraceTicks;

//            PhantomLifespanTracker.Register(parent.Map, this);
//        }

//        public override void PostDestroy(DestroyMode mode, Map previousMap)
//        {
//            PhantomLifespanTracker.Deregister(previousMap, this);
//            base.PostDestroy(mode, previousMap);
//        }

//        public override void PostExposeData()
//        {
//            base.PostExposeData();
//            Scribe_Values.Look(ref ownerThingID, "hskOwnerThingID", -1);
//            Scribe_Values.Look(ref expiresAtTick, "hskExpiresAtTick", -1);
//        }
//    }

//    // =====================================================================
//    // 3b. Общий (один на карту) сборщик просроченных фантомов — страховка
//    //     на случай, если владелец не смог штатно уничтожить свои
//    //     эмиттеры (мод-конфликт, ошибка, повреждённый сейв и т.п.).
//    //     Именно ОДИН периодический таймер на карту, а не тикер на
//    //     каждый отдельный фантом (у самих фантомов tickerType=Never).
//    // =====================================================================
//    public class MapComponent_PhantomLifespan : MapComponent
//    {
//        public readonly HashSet<CompGlowerSpotlight> tracked = new HashSet<CompGlowerSpotlight>();

//        private int nextSweepTick;
//        private const int SweepIntervalTicks = 500; // ~8 игровых секунд

//        public MapComponent_PhantomLifespan(Map map) : base(map)
//        {
//        }

//        public override void MapComponentTick()
//        {
//            base.MapComponentTick();

//            int now = Find.TickManager.TicksGame;
//            if (now < nextSweepTick)
//                return;
//            nextSweepTick = now + SweepIntervalTicks;

//            if (tracked.Count == 0)
//                return;

//            // Копия — Destroy() у фантома вызовет его PostDestroy, а тот
//            // сам уберёт себя из tracked (мутация коллекции во время
//            // перебора недопустима).
//            var snapshot = new List<CompGlowerSpotlight>(tracked);
//            for (int i = 0; i < snapshot.Count; i++)
//            {
//                CompGlowerSpotlight c = snapshot[i];
//                if (c?.parent == null || c.parent.Destroyed)
//                {
//                    tracked.Remove(c);
//                    continue;
//                }

//                if (c.expiresAtTick > 0 && now > c.expiresAtTick)
//                    c.parent.Destroy();
//            }
//        }
//    }

//    public static class PhantomLifespanTracker
//    {
//        public static void Register(Map map, CompGlowerSpotlight comp)
//        {
//            if (map == null)
//                return;

//            var tracker = map.GetComponent<MapComponent_PhantomLifespan>();
//            if (tracker == null)
//            {
//                tracker = new MapComponent_PhantomLifespan(map);
//                map.components.Add(tracker);
//            }

//            tracker.tracked.Add(comp);
//        }

//        public static void Deregister(Map map, CompGlowerSpotlight comp)
//        {
//            if (map == null)
//                return;

//            map.GetComponent<MapComponent_PhantomLifespan>()?.tracked.Remove(comp);
//        }
//    }

//    // =====================================================================
//    // 4. CompSpotlight — мозг прожектора
//    //    - рисует визуальный меш конуса;
//    //    - держит фантомные эмиттеры, регистрируемые в ванильном GlowGrid;
//    //    - никаких Harmony-патчей.
//    // =====================================================================
//    public class CompSpotlight : ThingComp
//    {
//        private float currentAngle;
//        private float targetAngle;

//        private Mesh beamMesh;
//        private Material beamMaterial;

//        private Vector3 beamOrigin;
//        private float dirX, dirZ;

//        private readonly List<Thing> phantoms = new List<Thing>();
//        private bool phantomsInitialized;
//        private int phantomUpdateCooldown;
//        private const int PhantomUpdateIntervalTicks = 15;

//        private CompPowerTrader powerComp;
//        private bool cachedPowered = true;

//        // Меш кэшируется по форме конуса (угол+длина), общий для всех
//        // прожекторов с одинаковыми параметрами — но НЕ один слот на
//        // весь мод, чтобы разные по форме прожекторы не вытесняли кэш
//        // друг друга каждый кадр.
//        private static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

//        public CompProperties_Spotlight Props => (CompProperties_Spotlight)props;

//        // ------------------------------------------------------------------
//        // Жизненный цикл
//        // ------------------------------------------------------------------
//        public override void PostSpawnSetup(bool respawningAfterLoad)
//        {
//            base.PostSpawnSetup(respawningAfterLoad);

//            if (!respawningAfterLoad)
//            {
//                currentAngle = Props.AngleForRotation(parent.Rotation);
//                targetAngle = currentAngle;
//            }

//            BuildMaterial();
//            RefreshGeometry();

//            powerComp = parent.GetComp<CompPowerTrader>();
//            cachedPowered = IsPowered();

//            // Фантомы инициализируем на первом тике — при загрузке карты
//            // Thing'ы создаются в произвольном порядке.
//            phantomsInitialized = false;
//        }

//        /// <summary>
//        /// true, если у здания нет компонента питания вообще (тогда
//        /// считаем его всегда "включённым"), либо он есть и реально
//        /// подаёт питание (учитывает и рубильник CompFlickable, если он
//        /// есть — CompPowerTrader.PowerOn уже сам это учитывает).
//        /// </summary>
//        private bool IsPowered()
//        {
//            return powerComp == null || powerComp.PowerOn;
//        }

//        public override void PostDestroy(DestroyMode mode, Map previousMap)
//        {
//            DestroyAllPhantomsIncludingOrphaned(previousMap);
//            base.PostDestroy(mode, previousMap);
//        }

//        public override void PostExposeData()
//        {
//            base.PostExposeData();
//            Scribe_Values.Look(ref currentAngle, "spotlightAngle", 0f);
//            Scribe_Values.Look(ref targetAngle, "spotlightTargetAngle", 0f);
//        }

//        // ------------------------------------------------------------------
//        // Тик
//        // ------------------------------------------------------------------
//        public override void CompTick()
//        {
//            base.CompTick();

//            if (!parent.Spawned)
//                return;

//            // Вращение луча (и пересчёт его геометрии) должно работать
//            // ВСЕГДА, даже если affectsLight = false — иначе чисто
//            // визуальный (без реального света) прожектор не смог бы
//            // поворачиваться гизмо вообще.
//            bool wasRotating = !Mathf.Approximately(currentAngle, targetAngle);

//            if (wasRotating)
//            {
//                if (Props.rotationSpeed <= 0f)
//                    currentAngle = targetAngle;
//                else
//                    currentAngle = Mathf.MoveTowardsAngle(
//                        currentAngle, targetAngle, Props.rotationSpeed / 60f);

//                RefreshGeometry();
//            }

//            // Питание проверяем всегда, независимо от affectsLight — оно
//            // определяет ещё и видимость самого нарисованного луча
//            // в PostDraw (визуальный конус без реального света тоже не
//            // должен светиться, пока лампа выключена/обесточена).
//            cachedPowered = IsPowered();

//            if (!Props.affectsLight)
//            {
//                if (phantoms.Count > 0)
//                    DestroyAllPhantoms();
//                return;
//            }

//            if (!cachedPowered)
//            {
//                if (phantoms.Count > 0)
//                    DestroyAllPhantoms();
//                // На следующем восстановлении питания пересоздадим заново.
//                phantomsInitialized = false;
//                return;
//            }

//            if (!phantomsInitialized)
//            {
//                phantomsInitialized = true;
//                AdoptOrSpawnPhantoms();
//                return;
//            }

//            if (wasRotating)
//            {
//                if (phantomUpdateCooldown > 0)
//                    phantomUpdateCooldown--;

//                if (phantomUpdateCooldown <= 0)
//                {
//                    RepositionPhantoms();
//                    phantomUpdateCooldown = PhantomUpdateIntervalTicks;
//                }
//            }
//            else if (phantomUpdateCooldown > 0)
//            {
//                phantomUpdateCooldown = 0;
//                RepositionPhantoms();
//            }

//            RenewPhantomLifespans();
//        }

//        /// <summary>
//        /// Продлевает TTL всех своих фантомов ещё на phantomLifespanTicks
//        /// вперёд. Дёшево — только запись int, без пере-регистрации в
//        /// GlowGrid. Вызывается каждый тик, пока лампа жива, запитана и
//        /// фантомы уже усыновлены/заспавнены.
//        /// </summary>
//        private void RenewPhantomLifespans()
//        {
//            if (phantoms.Count == 0)
//                return;

//            int expiry = Find.TickManager.TicksGame + Props.phantomLifespanTicks;
//            for (int i = 0; i < phantoms.Count; i++)
//            {
//                var c = phantoms[i]?.TryGetComp<CompGlowerSpotlight>();
//                if (c != null)
//                    c.expiresAtTick = expiry;
//            }
//        }

//        // ------------------------------------------------------------------
//        // Отрисовка луча
//        // ------------------------------------------------------------------
//        public override void PostDraw()
//        {
//            base.PostDraw();

//            if (!cachedPowered)
//                return;

//            if (beamMesh == null)
//                beamMesh = GetOrBuildBeamMesh(Props.beamAngle, Props.beamLength);

//            Vector3 origin = beamOrigin;
//            origin.y = AltitudeLayer.MetaOverlays.AltitudeFor();
//            Quaternion rot = Quaternion.Euler(0f, currentAngle, 0f);
//            Matrix4x4 matrix = Matrix4x4.TRS(origin, rot, Vector3.one);
//            Graphics.DrawMesh(beamMesh, matrix, beamMaterial, 0);
//        }

//        private void BuildMaterial()
//        {
//            Color c = Props.beamColor;
//            float b = Mathf.Max(0.01f, Props.brightness);
//            Color bright = new Color(
//                Mathf.Clamp01(c.r * b),
//                Mathf.Clamp01(c.g * b),
//                Mathf.Clamp01(c.b * b),
//                Mathf.Clamp01(c.a));

//            beamMaterial = new Material(ShaderDatabase.Transparent)
//            {
//                color = bright,
//                renderQueue = 3000
//            };
//            // Аддитивный блендинг — луч «светится», а не затемняет.
//            beamMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
//            beamMaterial.SetInt("_DstBlend", (int)BlendMode.One);
//            beamMaterial.SetInt("_ZWrite", 0);
//        }

//        private static Mesh GetOrBuildBeamMesh(float angleDeg, float length)
//        {
//            string key = angleDeg.ToString("F1") + "_" + length.ToString("F2");
//            if (meshCache.TryGetValue(key, out Mesh cached))
//                return cached;

//            Mesh mesh = BuildConeMesh(angleDeg, length);
//            meshCache[key] = mesh;
//            return mesh;
//        }

//        private static Mesh BuildConeMesh(float angleDegrees, float length)
//        {
//            var mesh = new Mesh();
//            int segments = Mathf.Clamp(Mathf.RoundToInt(angleDegrees / 5f), 4, 32);
//            float halfAngle = angleDegrees * 0.5f * Mathf.Deg2Rad;

//            var verts = new Vector3[segments + 2];
//            var uvs = new Vector2[verts.Length];
//            var tris = new int[segments * 3];

//            verts[0] = Vector3.zero;
//            uvs[0] = new Vector2(0.5f, 0f);

//            for (int i = 0; i <= segments; i++)
//            {
//                float t = (float)i / segments;
//                float a = Mathf.Lerp(-halfAngle, halfAngle, t);
//                verts[i + 1] = new Vector3(Mathf.Sin(a) * length, 0f, Mathf.Cos(a) * length);
//                uvs[i + 1] = new Vector2(t, 1f);
//            }

//            for (int i = 0; i < segments; i++)
//            {
//                int ti = i * 3;
//                tris[ti] = 0;
//                tris[ti + 1] = i + 1;
//                tris[ti + 2] = i + 2;
//            }

//            mesh.vertices = verts;
//            mesh.triangles = tris;
//            mesh.uv = uvs;
//            mesh.RecalculateNormals();
//            mesh.RecalculateBounds();
//            return mesh;
//        }

//        // ------------------------------------------------------------------
//        // Геометрия
//        // ------------------------------------------------------------------
//        private void RefreshGeometry()
//        {
//            float rad = currentAngle * Mathf.Deg2Rad;
//            dirX = Mathf.Sin(rad);
//            dirZ = Mathf.Cos(rad);

//            Vector3 center = parent.TrueCenter();
//            beamOrigin = new Vector3(
//                center.x + dirX * Props.sourceOffset,
//                0f,
//                center.z + dirZ * Props.sourceOffset);
//        }

//        // ------------------------------------------------------------------
//        // Фантомные эмиттеры
//        // ------------------------------------------------------------------
//        private void AdoptOrSpawnPhantoms()
//        {
//            phantoms.Clear();

//            ThingDef def = DefDatabase<ThingDef>.GetNamed(Props.emitterDefName, false);
//            if (def == null)
//            {
//                Log.Warning("[HSK.Watcher] emitterDefName не найден: " + Props.emitterDefName);
//                return;
//            }

//            int ownerId = parent.thingIDNumber;
//            Map map = parent.Map;

//            foreach (Thing t in map.listerThings.ThingsOfDef(def))
//            {
//                if (t == null || t.Destroyed) continue;
//                var c = t.TryGetComp<CompGlowerSpotlight>();
//                if (c != null && c.ownerThingID == ownerId)
//                    phantoms.Add(t);
//            }

//            if (phantoms.Count == 0)
//                SpawnPhantoms(def);
//            else
//                RepositionPhantoms();
//        }

//        private void SpawnPhantoms(ThingDef def)
//        {
//            List<IntVec3> positions = ComputePhantomPositions();
//            Map map = parent.Map;
//            int ownerId = parent.thingIDNumber;

//            for (int i = 0; i < positions.Count; i++)
//            {
//                IntVec3 pos = positions[i];
//                if (!pos.InBounds(map))
//                    continue;

//                Thing t = ThingMaker.MakeThing(def);
//                var c = t.TryGetComp<CompGlowerSpotlight>();
//                if (c != null)
//                    c.ownerThingID = ownerId;

//                try
//                {
//                    GenSpawn.Spawn(t, pos, map, WipeMode.Vanish);
//                    phantoms.Add(t);
//                }
//                catch
//                {
//                    // Клетка занята — пропускаем, свет там не появится.
//                }
//            }
//        }

//        private void RepositionPhantoms()
//        {
//            if (phantoms.Count == 0)
//                return;

//            List<IntVec3> positions = ComputePhantomPositions();

//            // Все кандидаты могли отфильтроваться (например, весь конус
//            // в эту секунду упирается в стены) — тогда расставлять
//            // некуда. Раньше это падало с IndexOutOfRangeException на
//            // positions[positions.Count - 1] при positions.Count == 0.
//            if (positions.Count == 0)
//                return;

//            Map map = parent.Map;
//            GlowGrid grid = map.glowGrid;

//            for (int i = 0; i < phantoms.Count; i++)
//            {
//                Thing t = phantoms[i];
//                if (t == null || t.Destroyed)
//                    continue;

//                IntVec3 newPos = i < positions.Count
//                    ? positions[i]
//                    : positions[positions.Count - 1];

//                if (!newPos.InBounds(map) || t.Position == newPos)
//                    continue;

//                var c = t.TryGetComp<CompGlowerSpotlight>();
//                if (c != null)
//                    grid.DeRegisterGlower(c);

//                t.Position = newPos;

//                if (c != null)
//                    grid.RegisterGlower(c);
//            }
//        }

//        private List<IntVec3> ComputePhantomPositions()
//        {
//            var result = new List<IntVec3>();

//            float half = Props.beamAngle * 0.5f * Mathf.Deg2Rad;
//            float length = Props.beamLength;
//            int rows = Mathf.Max(1, Props.emitterRows);
//            float spacing = Mathf.Max(0.5f, Props.emitterSpacing);
//            Map map = parent.Map;

//            for (int row = 0; row < rows; row++)
//            {
//                float t = (row + 0.5f) / rows;
//                float dist = t * length;
//                float halfWidth = Mathf.Tan(half) * dist;

//                int count = Mathf.Max(1, Mathf.RoundToInt((halfWidth * 2f) / spacing) + 1);

//                for (int i = 0; i < count; i++)
//                {
//                    float lt = count == 1 ? 0f : (i / (float)(count - 1)) * 2f - 1f;
//                    float offset = lt * halfWidth;

//                    float px = beamOrigin.x + dirX * dist - dirZ * offset;
//                    float pz = beamOrigin.z + dirZ * dist + dirX * offset;

//                    IntVec3 cell = new IntVec3(Mathf.RoundToInt(px), 0, Mathf.RoundToInt(pz));

//                    // Не ставим фантом туда, где уже стоит любая постройка
//                    // (стена, дверь и т.п.) — иначе GenSpawn с WipeMode.Vanish
//                    // снесёт её, чтобы освободить место под наш эмиттер.
//                    // Клетке со стеной собственный точечный источник и не
//                    // нужен — она и так непроходима и не должна освещаться
//                    // "изнутри".
//                    if (cell.InBounds(map) && cell.GetEdifice(map) == null)
//                        result.Add(cell);
//                }
//            }

//            return result;
//        }

//        private void DestroyAllPhantoms()
//        {
//            for (int i = 0; i < phantoms.Count; i++)
//            {
//                Thing t = phantoms[i];
//                if (t != null && !t.Destroyed)
//                    t.Destroy();
//            }
//            phantoms.Clear();
//        }

//        /// <summary>
//        /// То же самое, но НЕ полагается на кэшированный список phantoms —
//        /// заново сканирует карту по emitterDefName/ownerThingID. Нужно
//        /// на случай, если лампу удалили до того, как отработал первый
//        /// тик после спавна/загрузки: в этот момент phantoms ещё пуст,
//        /// хотя реальные эмиттеры с прошлой сессии уже загружены и
//        /// зарегистрированы в GlowGrid — иначе они остаются сиротами.
//        /// </summary>
//        private void DestroyAllPhantomsIncludingOrphaned(Map map)
//        {
//            DestroyAllPhantoms();

//            if (map == null)
//                return;

//            ThingDef def = DefDatabase<ThingDef>.GetNamed(Props.emitterDefName, false);
//            if (def == null)
//                return;

//            int ownerId = parent.thingIDNumber;

//            // Копия списка — уничтожение из listerThings во время
//            // перебора самого listerThings небезопасно.
//            List<Thing> found = new List<Thing>(map.listerThings.ThingsOfDef(def));
//            for (int i = 0; i < found.Count; i++)
//            {
//                Thing t = found[i];
//                if (t == null || t.Destroyed)
//                    continue;

//                var c = t.TryGetComp<CompGlowerSpotlight>();
//                if (c != null && c.ownerThingID == ownerId)
//                    t.Destroy();
//            }
//        }

//        // ------------------------------------------------------------------
//        // Гизмо
//        // ------------------------------------------------------------------
//        public override IEnumerable<Gizmo> CompGetGizmosExtra()
//        {
//            foreach (var g in base.CompGetGizmosExtra())
//                yield return g;

//            if (!Props.rotatable)
//                yield break;

//            yield return new Command_Action
//            {
//                defaultLabel = "Повернуть по часовой",
//                defaultDesc = "Повернуть луч на " + Props.rotationStep + "° по часовой стрелке.",
//                icon = TexCommand.ForbidOff,
//                action = delegate
//                {
//                    targetAngle = Mathf.Repeat(targetAngle + Props.rotationStep, 360f);
//                }
//            };

//            yield return new Command_Action
//            {
//                defaultLabel = "Повернуть против часовой",
//                defaultDesc = "Повернуть луч на " + Props.rotationStep + "° против часовой стрелки.",
//                icon = TexCommand.ForbidOff,
//                action = delegate
//                {
//                    targetAngle = Mathf.Repeat(targetAngle - Props.rotationStep, 360f);
//                }
//            };
//        }
//    }
//}
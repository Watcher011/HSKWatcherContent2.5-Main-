using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSK.Watcher.Content
{
    /// <summary>
    /// Для orbital-трейдеров (TraderKindDef.orbital = true) имя корабля обычно
    /// генерируется процедурно (NameGenerator) при создании TradeShip и никак
    /// не связано с TraderKindDef.label. Этот патч принудительно подставляет
    /// фиксированное имя из словаря FixedShipNames для перечисленных defName —
    /// после этого оно уже используется везде (письма о прибытии, окно торговли),
    /// так как подмена происходит сразу после конструктора, до первого чтения имени.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Watcher_FixedTraderShipNames
    {
        // defName TraderKindDef -> фиксированное имя корабля.
        // ВАЖНО: TradeShip.FullTitle / GetCallLabel() сами склеивают
        // "name + " (" + def.label + ")"" — поэтому здесь только имя самого
        // корабля, а описание трейдера ("Planet Cracker") должно быть в
        // TraderKindDef.label в XML, а не дублироваться тут же.
        // Чтобы добавить ещё один трейдер с заданным именем — просто добавьте строку сюда.
        private static readonly Dictionary<string, string> FixedShipNames = new Dictionary<string, string>
        {
            { "PlanetCrackerGoodsTrader", "U.S.G Ishimura" },
        };

        private static FieldInfo cachedNameField;

        static Watcher_FixedTraderShipNames()
        {
            var harmony = new Harmony("Watcher.FixedTraderShipNames");
            PatchTradeShipConstructor(harmony);
        }

        private static void PatchTradeShipConstructor(Harmony harmony)
        {
            try
            {
                ConstructorInfo ctor = FindTradeShipConstructor();
                if (ctor == null)
                {
                    Log.Error("[Watcher] FixedTraderShipNames: не найден конструктор TradeShip(TraderKindDef, ...) " +
                        "рефлексией — возможно, класс TradeShip изменился в этой версии игры. Патч не применён.");
                    return;
                }

                MethodInfo postfix = typeof(Watcher_FixedTraderShipNames).GetMethod(
                    nameof(ApplyFixedNamePostfix), BindingFlags.Static | BindingFlags.NonPublic);

                harmony.Patch(ctor, postfix: new HarmonyMethod(postfix));
            }
            catch (Exception ex)
            {
                Log.Error($"[Watcher] FixedTraderShipNames: ошибка при патче конструктора TradeShip: {ex}");
            }
        }

        /// <summary>
        /// Ищем публичный конструктор TradeShip, первым параметром принимающий
        /// TraderKindDef — не завязываемся на точное число/порядок остальных
        /// параметров, чтобы патч не сломался, если в другой версии добавили,
        /// скажем, необязательный Faction.
        /// </summary>
        private static ConstructorInfo FindTradeShipConstructor()
        {
            foreach (ConstructorInfo ctor in typeof(TradeShip).GetConstructors(
                BindingFlags.Public | BindingFlags.Instance))
            {
                ParameterInfo[] parameters = ctor.GetParameters();
                if (parameters.Length > 0 && parameters[0].ParameterType == typeof(TraderKindDef))
                {
                    return ctor;
                }
            }

            return null;
        }

        /// <summary>
        /// Поле "name" объявлено не в TradeShip, а в базовом классе PassingShip
        /// (используется напрямую внутри TradeShip без "this.", значит доступно по
        /// наследованию). Проходим по цепочке типов TradeShip -> PassingShip -> ...,
        /// беря "name" типа string на каждом уровне через DeclaredOnly — так патч
        /// сработает независимо от того, public оно, protected или private в базовом
        /// классе.
        /// </summary>
        private static FieldInfo FindNameField()
        {
            if (cachedNameField != null) return cachedNameField;

            Type type = typeof(TradeShip);
            while (type != null && type != typeof(object))
            {
                FieldInfo field = type.GetField("name",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                if (field != null && field.FieldType == typeof(string))
                {
                    cachedNameField = field;
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        /// <summary>
        /// Постфикс конструктора. Читаем TraderKindDef напрямую из уже
        /// проинициализированного __instance.def — так не важно, как называется
        /// параметр конструктора в конкретной версии игры, лишь бы TradeShip.def
        /// (публичное поле, используемое повсеместно в торговом UI) существовал.
        /// </summary>
        private static void ApplyFixedNamePostfix(TradeShip __instance)
        {
            TraderKindDef traderDef = __instance?.def;
            if (traderDef == null) return;

            if (!FixedShipNames.TryGetValue(traderDef.defName, out string fixedName))
                return;

            FieldInfo nameField = FindNameField();
            if (nameField == null)
            {
                Log.Error("[Watcher] FixedTraderShipNames: не найдено поле имени в TradeShip рефлексией — " +
                    "структура класса могла измениться, фиксированное имя не применено.");
                return;
            }

            nameField.SetValue(__instance, fixedName);
        }
    }
}
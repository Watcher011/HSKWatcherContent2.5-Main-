using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace HSK.Watcher.Content.Incident
{
    /// <summary>
    /// "Босс-рейд" неандертальцев: обычная группа налётчиков-неандертальцев
    /// плюс один Mech_GreatPlankBeast той же фракции, идущий с ними в одном Lord'е.
    /// Спавн муффало при смерти меха реализован через deathAction в XML.
    /// </summary>
    public class IncidentWorker_GreatPlankBeastRaid : IncidentWorker_RaidEnemy
    {
        // Замените на реальные defName, если они отличаются в вашей сборке.
        private const string NeanderthalFactionDefName = "TribeRough";
        private const string MechDefName = "Mech_GreatPlankBeast";

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;

            if (parms.faction == null)
            {
                parms.faction = Find.FactionManager.AllFactionsListForReading
                    .FirstOrDefault(f => !f.IsPlayer && !f.Hidden
                        && f.def.defName == NeanderthalFactionDefName);
            }

            if (parms.faction == null)
            {
                Log.Warning($"[HSK.Watcher] Не найдена фракция '{NeanderthalFactionDefName}' для {nameof(IncidentWorker_GreatPlankBeastRaid)}.");
                return false;
            }

            int lordsBefore = map.lordManager.lords.Count;

            // Стандартная логика IncidentWorker_RaidEnemy: считает очки, генерирует пешек,
            // создаёт Lord (обычно LordJob_AssaultColony) и спавнит рейд.
            if (!base.TryExecuteWorker(parms))
            {
                return false;
            }

            Lord raidLord = map.lordManager.lords
                .Skip(lordsBefore)
                .FirstOrDefault(l => l.faction == parms.faction)
                ?? map.lordManager.lords.LastOrDefault(l => l.faction == parms.faction);

            PawnKindDef mechKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(MechDefName);
            if (mechKind == null)
            {
                Log.Warning($"[HSK.Watcher] PawnKindDef '{MechDefName}' не найден — рейд прошёл без босса.");
                return true;
            }

            IntVec3 spawnSpot = ResolveSpawnSpot(map, raidLord);

            Pawn mech = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                mechKind,
                parms.faction,
                PawnGenerationContext.NonPlayer,
                map.Tile,
                forceGenerateNewPawn: true,
                canGeneratePawnRelations: false));

            GenSpawn.Spawn(mech, spawnSpot, map);

            if (raidLord != null)
            {
                raidLord.AddPawn(mech);
            }

            SendBossLetter(map, mech);

            return true;
        }

        private IntVec3 ResolveSpawnSpot(Map map, Lord raidLord)
        {
            if (raidLord != null && raidLord.ownedPawns.Count > 0)
            {
                Pawn anyRaider = raidLord.ownedPawns[0];
                if (CellFinder.TryFindRandomCellNear(anyRaider.Position, map, 3,
                        c => c.Standable(map) && !c.Fogged(map), out IntVec3 nearCell))
                {
                    return nearCell;
                }
                return anyRaider.Position;
            }

            return DropCellFinder.RandomDropSpot(map);
        }

        private void SendBossLetter(Map map, Pawn mech)
        {
            string label = "HSK_GreatPlankBeastRaid_LetterLabel".Translate();
            string text = "HSK_GreatPlankBeastRaid_LetterText".Translate(mech.Named("MECH"));

            Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.ThreatBig, new LookTargets(mech));
        }
    }
}
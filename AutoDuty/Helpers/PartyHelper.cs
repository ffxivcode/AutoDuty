using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoDuty.Helpers
{
    using Dalamud.Game.ClientState.Objects.SubKinds;
    using Dalamud.Game.ClientState.Objects.Types;
    using Dalamud.Game.ClientState.Party;
    using ECommons;
    using ECommons.DalamudServices;
    using ECommons.GameFunctions;
    using ECommons.PartyFunctions;
    using FFXIVClientStructs.FFXIV.Client.UI.Misc;

    public static class PartyHelper
    {
        public static IGameObject? Self =>
            Player.Available ? Player.Object : null;

        public static IGameObject? PartyMember1 => GetPartyMemberInSlot(1);
        public static IGameObject? PartyMember2 => GetPartyMemberInSlot(2);
        public static IGameObject? PartyMember3 => GetPartyMemberInSlot(3);
        public static IGameObject? PartyMember4 => GetPartyMemberInSlot(4);
        public static IGameObject? PartyMember5 => GetPartyMemberInSlot(5);
        public static IGameObject? PartyMember6 => GetPartyMemberInSlot(6);
        public static IGameObject? PartyMember7 => GetPartyMemberInSlot(7);
        public static IGameObject? PartyMember8 => GetPartyMemberInSlot(8);


        public static IGameObject? GetPartyMemberInSlot(int slot) =>
            slot switch
            {
                < 1 or > 8 => null,
                1 => Self,
                _ => PronounHelper.GetIGameObjectFromPronounID(42 + slot),
            };

        public static List<IBattleChara> GetPartyMembers()
        {
            List<IBattleChara> party = [];
            for (int i = 1; i <= 8; i++)
            {
                IGameObject? member = PronounHelper.GetIGameObjectFromPronounID(42 + i);

                if (member is IBattleChara battleChara)
                {
                    party.Add(battleChara);
                }
            }

            return party;
        }



        private static DateTime partyCombatCheckTime = DateTime.Now;
        private static bool     partyInCombat;

        private static readonly TimeSpan partyCombatCheckInterval = TimeSpan.FromMilliseconds(500);

        public static unsafe bool PartyInCombat()
        {
            if (!PlayerHelper.IsReady)
                return false;

            if (DateTime.Now.Subtract(partyCombatCheckTime).TotalSeconds < partyCombatCheckInterval.TotalSeconds)
                return partyInCombat;

            List<IBattleChara> members = GetPartyMembers();
            if (!partyInCombat && members.Any(x => x.Struct()->InCombat))
                partyInCombat = true;
            else if (!members.Any(x => x.Struct()->InCombat))
                partyInCombat = false;

            Svc.Log.Debug("InCombatCheck: " + partyInCombat);

            partyCombatCheckTime = DateTime.Now;

            return partyInCombat;
        }

        private const  byte     DEAD_THRESHOLD      = 5;
        private static byte     deadCounter         = 0;
        private static DateTime partyDeathCheckTime = DateTime.Now;

        private static readonly TimeSpan partyDeathCheckInterval = TimeSpan.FromMilliseconds(500);

        public static unsafe bool PartyDead()
        {
            if (DateTime.Now.Subtract(partyDeathCheckTime).TotalSeconds < partyDeathCheckInterval.TotalSeconds)
                return deadCounter >= DEAD_THRESHOLD;

            List<IBattleChara> members = GetPartyMembers();
            bool               dead    = members.TrueForAll(x => x.Struct()->IsDead());

            if (dead)
            {
                if (deadCounter < byte.MaxValue - 1)
                    deadCounter++;
            }
            else
            {
                deadCounter = 0;
            }

            partyDeathCheckTime = DateTime.Now;

            return deadCounter >= DEAD_THRESHOLD;
        }

        public static bool IsPartyMember(ulong? cid)
        {
            if (cid == null || !PlayerHelper.IsReady)
                return false;

            return UniversalParty.Members.Any(upm => upm.ContentID == cid);
        }
    }
}
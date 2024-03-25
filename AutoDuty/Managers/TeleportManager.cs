using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game;
using ECommons.GameHelpers;

namespace AutoDuty.Managers
{
    internal class TeleportManager
    {
        public unsafe static void Teleport(uint aetheryteId, byte subindex = 0)
        {
            if (Player.Object == null)
                return;

            ActionManager.Instance()->GetActionStatus(ActionType.Action, 5);

            Telepo.Instance()->Teleport(aetheryteId, subindex);
        }

        public static void TeleportCity(uint city)
        {
            switch (city)
            {
                //Limsa
                case 129:
                    Teleport(8);
                    break;
                //Uldah
                case 130:
                    Teleport(9);
                    break;
                //Gridania
                case 132:
                    Teleport(2);
                    break;
                default:
                    break;
            }
        }
    }
}

using AutoDuty.Windows;
using System.Numerics;

namespace AutoDuty.Helpers
{
    internal static class SummoningBellHelper
    {
        internal static uint SummoningBellDataIds(uint territoryType)
        {
            return territoryType switch
            {
                0 => 2000403, //Inn
                1 => 196630, //Apartment
                2 => 196630, //Personal_Home
                3 => 196630, //FC_Estate
                129 => 2000401, //Limsa_Lominsa_Lower_Decks
                133 => 2000401, //Old_Gridania
                131 => 2000401, //Uldah_Steps_of_Thal
                419 => 2000401, //The_Pillars
                635 => 2000441, //Rhalgrs_Reach
                628 => 2000441, //Kugane
                759 => 2006565, //The_Doman_Enclave
                819 => 2010284, //The_Crystarium
                820 => 2010284, //Eulmore
                962 => 2000441, //Old_Sharlayan
                963 => 2000441, //Radz_at_Han
                1185 => 2000441, //Tuliyollal
                1186 => 2000441, //Nexus_Arcade
                _ => 0
            };
        }

        private static Vector3 SummoningBellVector3s(uint territoryType)
        {
            return territoryType switch
            {
                129 => new(-123.88806f, 17.990356f, 21.469421f),
                133 => new(171.00781f, 15.487854f, -101.487854f),
                131 => new(148.91272f, 3.982544f, -44.205383f),
                419 => new(-151.1712f, -12.64978f, -11.7647705f),
                635 => new(-57.63336f, -0.015319824f, 49.30188f),
                628 => new(19.394226f, 4.043579f, 53.025024f),
                759 => new(60.56299f, -0.015319824f, -3.982666f),
                819 => new(-69.840576f, -7.7058716f, 123.49121f),
                820 => new(7.1869507f, 83.17688f, 31.448853f),
                962 => new(42.09961f, 2.517002f, -39.414062f),
                963 => new(26.749023f, -0.015319824f, -53.696533f),
                1185 => new(18.57019f, -14.023071f, 120.408936f),
                1186 => new(-151.59845f, 0.59503174f, -15.304871f),
                _ => Vector3.Zero 
            };
        }

        internal static void Invoke(SummoningBellLocations summoningBellLocation) 
        {
            switch (AutoDuty.Plugin.Configuration.PreferredSummoningBellEnum)
            {
                case SummoningBellLocations.Inn:
                    GotoInnHelper.Invoke();
                    break;
                case SummoningBellLocations.Apartment:
                    GotoHousingHelper.Invoke((Housing)SummoningBellLocations.Apartment);
                    break;
                case SummoningBellLocations.Personal_Home:
                    GotoHousingHelper.Invoke((Housing)SummoningBellLocations.Personal_Home);
                    break;
                case SummoningBellLocations.FC_Estate:
                    GotoHousingHelper.Invoke((Housing)SummoningBellLocations.FC_Estate);
                    break;
                default:
                    GotoHelper.Invoke((uint)AutoDuty.Plugin.Configuration.PreferredSummoningBellEnum, SummoningBellVector3s((uint)summoningBellLocation), 0.25f, 4);
                    break;
            }
        }
    }
}

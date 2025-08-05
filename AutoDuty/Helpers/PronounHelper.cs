using System;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

// ReSharper disable UnusedType.Global
// ReSharper disable MemberCanBePrivate.Global

namespace AutoDuty.Helpers;

// Straight from Wrath
public static unsafe class PronounHelper
{

    // They/Them, personally

    public static bool FullyReady    => PronounsReady && MethodsReady;
    public static bool PronounsReady => PronounModule.Value               != IntPtr.Zero;
    public static bool MethodsReady  => GetGameObjectPointerFromPronounID != null;

    public static PronounModule* Module => (PronounModule*)PronounModule.Value;

    private static readonly Lazy<IntPtr> PronounModule = new(() =>
                                                             {
                                                                 var ptr = Framework.Instance()->GetUIModule()->GetPronounModule();
                                                                 return ptr != null ? (IntPtr)ptr : IntPtr.Zero;
                                                             });

    // Signature for PronounModule::GetGameObjectByPronounId.
    private static readonly delegate* unmanaged<IntPtr, uint, GameObject*> GetGameObjectPointerFromPronounID = InitializePronounDelegate();

    private static delegate* unmanaged<IntPtr, uint, GameObject*> InitializePronounDelegate()
    {
        try
        {
            const string signature =
                "E8 ?? ?? ?? ?? 48 8B D8 48 85 C0 0F 85 ?? ?? ?? ?? 8D 4F DD";
            var address = Svc.SigScanner.ScanText(signature);
            return (delegate* unmanaged<IntPtr, uint, GameObject*>)address;
        }
        catch (Exception ex)
        {
            ex.Log();
            return null;
        }
    }

    /// <summary>
    ///     Gets a game object by pronoun ID (e.g., 44–50 for party members 2–8).
    /// </summary>
    /// <param name="id">The pronoun ID.</param>
    /// <returns>An IGameObject if found; otherwise, null.</returns>
    public static IGameObject? GetIGameObjectFromPronounID(int id)
    {
        if (!FullyReady) return null;

        try
        {
            uint uID = (uint)id;
            return ObjectHelper.GetObjectFrom(GetGameObjectPointerFromPronounID(PronounModule.Value, uID));
        }
        catch (Exception ex)
        {
            ex.Log();
            return null;
        }
    }
}
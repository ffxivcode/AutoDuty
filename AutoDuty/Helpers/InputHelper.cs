using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Keys;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace AutoDuty;

internal unsafe static class InputHelper
{
    internal static void SetKeyValue(VirtualKey virtualKey, KeyStateFlags keyStateFlag) => (*(int*)(Svc.SigScanner.Module.BaseAddress + Marshal.ReadInt32(Svc.SigScanner.ScanText("48 8D 0C 85 ?? ?? ?? ?? 8B 04 31 85 C2 0F 85") + 0x4) + (4 * (*(byte*)(Svc.SigScanner.Module.BaseAddress + Marshal.ReadInt32(Svc.SigScanner.ScanText("0F B6 94 33 ?? ?? ?? ?? 84 D2") + 0x4) + (int)virtualKey))))) = (int)keyStateFlag;

    internal static bool IsKeyPressed(VirtualKey virtualKey) => Svc.KeyState[virtualKey];
}

//Entire file from vnavmesh
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace AutoDuty.External;

[StructLayout(LayoutKind.Explicit, Size = 0x2B0)]
public unsafe struct CameraEx
{
    [FieldOffset(0x130)] public float DirH; // 0 is north, increases CW
    [FieldOffset(0x134)] public float DirV; // 0 is horizontal, positive is looking up, negative looking down
    [FieldOffset(0x138)] public float InputDeltaHAdjusted;
    [FieldOffset(0x13C)] public float InputDeltaVAdjusted;
    [FieldOffset(0x140)] public float InputDeltaH;
    [FieldOffset(0x144)] public float InputDeltaV;
    [FieldOffset(0x148)] public float DirVMin; // -85deg by default
    [FieldOffset(0x14C)] public float DirVMax; // +45deg by default
}

public unsafe class OverrideCamera : IDisposable
{
    internal void Face(Vector3 pos)
    {
        Enabled = true;
        SpeedH = SpeedV = 360.Degrees();
        DesiredAzimuth = Angle.FromDirectionXZ(pos - Player.Object.Position) + 180.Degrees();
        DesiredAltitude = -30.Degrees();
    }

    public bool Enabled
    {
        get => _rmiCameraHook.IsEnabled;
        set
        {
            if (value)
                _rmiCameraHook.Enable();
            else
                _rmiCameraHook.Disable();
        }
    }

    public bool IgnoreUserInput; // if true - override even if user tries to change camera orientation, otherwise override only if user does nothing
    public Angle DesiredAzimuth;
    public Angle DesiredAltitude;
    public Angle SpeedH = 360.Degrees(); // per second
    public Angle SpeedV = 360.Degrees(); // per second

    private delegate void RMICameraDelegate(CameraEx* self, int inputMode, float speedH, float speedV);
    [Signature("40 53 48 83 EC 70 44 0F 29 44 24 ?? 48 8B D9")]
    private Hook<RMICameraDelegate> _rmiCameraHook = null!;

    public OverrideCamera()
    {
        Svc.Hook.InitializeFromAttributes(this);
        Svc.Log.Information($"RMICamera address: 0x{_rmiCameraHook.Address:X}");
    }

    public void Dispose()
    {
        _rmiCameraHook.Dispose();
    }

    private void RMICameraDetour(CameraEx* self, int inputMode, float speedH, float speedV)
    {
        _rmiCameraHook.Original(self, inputMode, speedH, speedV);
        if (IgnoreUserInput || inputMode == 0) // let user override...
        {
            var dt = Framework.Instance()->FrameDeltaTime;
            var deltaH = (DesiredAzimuth - self->DirH.Radians()).Normalized();
            var deltaV = (DesiredAltitude - self->DirV.Radians()).Normalized();
            var maxH = SpeedH.Rad * dt;
            var maxV = SpeedV.Rad * dt;
            //self->InputDeltaH = Math.Clamp(deltaH.Rad, -maxH, maxH);
            //self->InputDeltaV = Math.Clamp(deltaV.Rad, -maxV, maxV);
            self->InputDeltaH = deltaH.Rad;
            self->InputDeltaV = deltaV.Rad;
            Enabled = false;
        }
    }
}

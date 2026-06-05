using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public const byte MOUSEBUTTON0 = 1;
    public const byte MOUSEBUTTON1 = 2;
    public const byte SHIFT = 3;
    public const byte JUMP = 4;
    public const byte LOCKON = 5;
    public const byte LOCKON_CANCEL = 6;

    public NetworkButtons buttons;
    public Vector3 direction;
    public int actionId;
}

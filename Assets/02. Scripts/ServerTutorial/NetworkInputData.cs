using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public const byte MOUSEBUTTON0 = 1;
    public const byte MOUSEBUTTON1 = 2;
    public const byte SHIFT = 3;
    public const byte JUMP = 4;

    public NetworkButtons buttons;
    public Vector3 direction;
}

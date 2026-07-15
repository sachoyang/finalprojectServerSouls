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
    public int actionId;
    // 이 입력을 만든 Fusion PlayerRef.RawEncoded 값.
    // 서버/호스트에서 여러 플레이어 오브젝트가 같은 입력을 잘못 소비하지 않도록 소유자 검증에 사용한다.
    public int inputAuthorityRaw;
}

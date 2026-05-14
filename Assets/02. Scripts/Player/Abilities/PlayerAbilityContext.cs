using Fusion;
using UnityEngine;

// 능력 모듈이 실행될 때 필요한 플레이어 관련 정보를 한 번에 넘기기 위한 묶음이다.
// ScriptableObject인 PlayerAbilityModule은 특정 플레이어 GameObject에 붙어 있지 않기 때문에,
// 실제 효과를 적용하려면 "누가 이 능력을 쓰는지"를 외부에서 전달받아야 한다.
public readonly struct PlayerAbilityContext
{
    public PlayerAbilityContext(GameObject owner, PlayerStats stats, NetworkRunner runner)
    {
        Owner = owner;
        Transform = owner != null ? owner.transform : null;
        Stats = stats;
        Runner = runner;
    }

    // 능력을 소유한 플레이어 오브젝트.
    // 이 오브젝트에서 다른 컴포넌트를 찾거나, 위치 기준으로 이펙트/투사체를 생성할 수 있다.
    public GameObject Owner { get; }

    // Owner의 Transform을 자주 쓰기 때문에 편의용으로 같이 보관한다.
    // 위치, 회전, 바라보는 방향이 필요한 액티브 스킬에서 주로 사용한다.
    public Transform Transform { get; }

    // 체력, 스태미나, 사망 여부 등을 관리하는 기존 PlayerStats 컴포넌트.
    // 회복, 스태미나 소모/회복 같은 기본 능력 효과는 여기로 처리한다.
    public PlayerStats Stats { get; }

    // Fusion 네트워크 시간이나 Spawn 처리가 필요할 때 사용할 수 있는 Runner.
    // 단순 로컬 효과만 만들 때는 사용하지 않아도 된다.
    public NetworkRunner Runner { get; }
}

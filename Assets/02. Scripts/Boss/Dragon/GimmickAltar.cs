using Fusion;
using UnityEngine;

public class GimmickAltar : NetworkBehaviour
{
    [Header("제단 설정")]
    public float maxHP = 300f;
    [Networked] public float CurrentHP { get; set; }

    // 에러를 유발했던 OnChanged 속성을 지우고 심플하게 둡니다.
    [Networked] 
    public NetworkBool IsDestroyed { get; set; }

    // 내 화면(로컬)에서 이 제단이 시각적으로 꺼졌는지 추적하는 변수
    private bool _isVisualDestroyed = false;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            CurrentHP = maxHP;
            IsDestroyed = false;
        }
        _isVisualDestroyed = false; // 스폰 시 초기화
    }

    // 🔥 클라이언트들의 화면을 갱신하는 Render에서 직접 변화를 캐치합니다!
    public override void Render()
    {
        // 서버에서 제단이 파괴(IsDestroyed = true)되었는데, 내 화면에선 아직 안 꺼졌다면?
        if (IsDestroyed && !_isVisualDestroyed)
        {
            _isVisualDestroyed = true; // 중복 실행 방지
            
            // TODO: 여기서 파괴 파티클 이펙트나 소리를 재생하면 됩니다!
            gameObject.SetActive(false); 
        }
    }

    // 플레이어의 무기가 제단을 때렸을 때 호출되는 함수
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(float damage)
    {
        if (IsDestroyed) return;

        CurrentHP -= damage;
        if (CurrentHP <= 0)
        {
            CurrentHP = 0;
            IsDestroyed = true; // 상태를 true로 바꾸면 클라이언트들의 Render()가 알아서 반응함
            
            // 맵 매니저에게 파괴되었다고 알림
            if (DragonArenaGimmick.Instance != null)
            {
                DragonArenaGimmick.Instance.OnAltarDestroyed();
            }
        }
    }
}
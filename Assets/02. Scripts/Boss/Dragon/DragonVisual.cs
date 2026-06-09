using UnityEngine;

// [핵심] IBossVisual 인터페이스를 상속받습니다.
public class DragonVisual : MonoBehaviour, IBossVisual
{
    public Animator anim;

    [Header("이펙트")]
    public ParticleSystem fireBreath;

    [Header("히트박스 연결")]
    public BossHitbox mouthHitbox;
    public BossHitbox leftClawHitbox;
    public BossHitbox rightClawHitbox;
    public BossHitbox bodyHitbox;

    [Header("광역기 이펙트 프리팹")]
    public GameObject jumpSlamEffectPrefab;
    public Transform jumpSlamSpawnPoint;

    [Header("경고 장판 (Telegraph)")]
    public GameObject jumpWarningPrefab;

    public AudioClip[] audioClips;

    private void Awake()
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
        }
    }

    // ==========================================
    // [IBossVisual 구현부] 서버의 명령을 통합해서 처리하는 단일 함수
    // ==========================================
    public void PlayAction(int stateHash, float crossFadeTime = 0.1f)
    {
        anim.CrossFade(stateHash, crossFadeTime, 0, 0f);

        // (참고) 만약 특정 이펙트를 위해 어떤 해시인지 검사해야 한다면?
        // if (stateHash == Animator.StringToHash("Basic Attack")) { fireBreath.Play(); }
    }

    public void SetSpeed(float speedValue) { anim.SetFloat("MoveSpeed", speedValue); }
    public void SetAnimSpeed(float multiplier) { anim.speed = multiplier; }
    public void DoLocomotion() { anim.CrossFade("Locomotion", 0.1f); }

    // ==========================================
    // [애니메이션 이벤트용 함수] 
    // 애니메이션 클립 타임라인에 심어둔 이벤트들이 이 함수들을 호출하므로 그대로 둡니다.
    // ==========================================
    public void EnableMouthHitbox() { if (mouthHitbox != null) mouthHitbox.StartAttack(); }
    public void DisableMouthHitbox() { if (mouthHitbox != null) mouthHitbox.StopAttack(); }

    public void EnableClawHitbox()
    {
        if (leftClawHitbox != null) leftClawHitbox.StartAttack();
        if (rightClawHitbox != null) rightClawHitbox.StartAttack();
        if (audioClips[0] != null)
        {
            SoundManager.Instance.PlaySFX_3D(audioClips[0], transform.position, SoundCategory.CombatHit);
        }
    }
    public void DisableClawHitbox()
    {
        if (leftClawHitbox != null) leftClawHitbox.StopAttack();
        if (rightClawHitbox != null) rightClawHitbox.StopAttack();
    }

    public void EnableBodyHitbox()
    {
        if (bodyHitbox != null) bodyHitbox.StartAttack();
    }
    public void DisableBodyHitbox()
    {
        if (bodyHitbox != null) bodyHitbox.StopAttack();
    }

    public void SpawnJumpWarning()
    {
        if (jumpWarningPrefab != null && jumpSlamSpawnPoint != null)
        {
            Instantiate(jumpWarningPrefab, jumpSlamSpawnPoint.position, jumpWarningPrefab.transform.rotation);
        }
    }

    public void SpawnJumpSlamEffect()
    {
        if (jumpSlamEffectPrefab != null && jumpSlamSpawnPoint != null)
        {
            Instantiate(jumpSlamEffectPrefab, jumpSlamSpawnPoint.position, jumpSlamSpawnPoint.rotation);
        }
    }

    public void EnableHornAttackHitbox()
    {
        if (leftClawHitbox != null) leftClawHitbox.StartAttack();
        if (rightClawHitbox != null) rightClawHitbox.StartAttack();
        if (mouthHitbox != null) mouthHitbox.StartAttack();
        if (bodyHitbox != null) bodyHitbox.StartAttack();
    }

    public void DisableHornAttackHitbox()
    {
        if (leftClawHitbox != null) leftClawHitbox.StopAttack();
        if (rightClawHitbox != null) rightClawHitbox.StopAttack();
        if (mouthHitbox != null) mouthHitbox.StopAttack();
        if (bodyHitbox != null) bodyHitbox.StopAttack();
    }
}
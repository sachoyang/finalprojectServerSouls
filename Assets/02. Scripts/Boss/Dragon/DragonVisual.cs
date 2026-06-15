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

    [Header("공격 패턴 사운드")]
    public AudioClip[] audioClips;

    [Header("상태 변화 사운드")]
    public AudioClip wakeUpAndPhaseSound; // (구 Core.audioClips[0])
    public AudioClip groggySound;         // (구 Core.audioClips[1])
    public AudioClip dieSound;            // (필요시 추가)
    public AudioClip walkSound;

    [Header("2페이즈 전용 테마곡")]
    [Tooltip("변신 연출이 시작될 때 재생되며, 기존 맵 BGM과 크로스페이드 됩니다.")]
    public AudioClip phase2ThemeBGM;

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

    public void PlayWakeUp(int wakeUpHash)
    {
        PlayAction(wakeUpHash);
        if (wakeUpAndPhaseSound != null)
        {
            SoundManager.Instance.PlaySFX_3D(wakeUpAndPhaseSound, transform.position, SoundCategory.BossGimmick, 1f, 1f);
        }
    }

    public void PlayPhaseTransition(int wakeUpHash)
    {
        // 변신 연출도 기상(WakeUp)과 같은 동작/사운드를 사용했었음
        PlayAction(wakeUpHash);
        if (wakeUpAndPhaseSound != null)
        {
            SoundManager.Instance.PlaySFX_3D(wakeUpAndPhaseSound, transform.position, SoundCategory.BossGimmick, 1f, 1f);
        }

        if (phase2ThemeBGM != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(phase2ThemeBGM);
            Debug.Log("[Visual] 2페이즈 돌입! 2페이즈 전용 테마곡으로 브금을 교체합니다.");
        }
    }

    public void PlayGroggy(float speedMultiplier)
    {
        SetAnimSpeed(speedMultiplier);
        PlayAction(Animator.StringToHash("getHit"));

        if (groggySound != null)
        {
            SoundManager.Instance.PlaySFX_3D(groggySound, transform.position, SoundCategory.BossGimmick, 0.5f, 0.1f);
        }
    }

    public void PlayDie()
    {
        PlayAction(Animator.StringToHash("die"));

        if (dieSound != null)
        {
            SoundManager.Instance.PlaySFX_3D(dieSound, transform.position, SoundCategory.BossGimmick);
        }
    }

    public void SetSpeed(float speedValue) { anim.SetFloat("MoveSpeed", speedValue); }
    public void SetAnimSpeed(float multiplier) { anim.speed = multiplier; }
    public void DoLocomotion()
    {
        anim.CrossFade("Locomotion", 0.1f);
    }

    public void SetRootMotionCapture(bool enabled)
    {
    }
 
    public Vector3 ConsumeRootMotionDelta()
    {
        return Vector3.zero;
    }

    // ==========================================
    // [애니메이션 이벤트용 함수] 
    // ==========================================

    public void PlayFootstep()
    {
        if (walkSound != null)
        {
            SoundManager.Instance.PlaySFX_3D(walkSound, transform.position, SoundCategory.Footstep);
        }
    }

    public void EnableMouthHitbox()
    {
        if (mouthHitbox != null) mouthHitbox.StartAttack();
        if (audioClips[2] != null)//bite1
        {
            SoundManager.Instance.PlaySFX_3D(audioClips[2], transform.position, SoundCategory.BossGimmick);
        }
    }
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
        //if (bodyHitbox != null) bodyHitbox.StartAttack();
    }

    public void DisableHornAttackHitbox()
    {
        if (leftClawHitbox != null) leftClawHitbox.StopAttack();
        if (rightClawHitbox != null) rightClawHitbox.StopAttack();
        if (mouthHitbox != null) mouthHitbox.StopAttack();
        //if (bodyHitbox != null) bodyHitbox.StopAttack();
    }
}
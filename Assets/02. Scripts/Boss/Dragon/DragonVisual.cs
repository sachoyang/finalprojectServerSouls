using UnityEngine;

// [핵심] IBossVisual 인터페이스를 상속받습니다.
public class DragonVisual : MonoBehaviour, IBossVisual
{
    public Animator anim;

    private Vector3 _ikLookAtPosition;
    private float _ikWeight = 0f;

    [Header("수동 IK (Generic 몬스터용)")]
    [Tooltip("드래곤의 머리(Head) 또는 목(Neck) 뼈 Transform을 하이라키에서 찾아 드래그하세요!")]
    public Transform headBone;
    public Transform lookAtGuide;

    [Header("이펙트 프리펩")]
    public GameObject fireBreath;
    public Transform fireBreathPoint;

    [Header("근접 공격 판정 (신형 Sweep)")]
    // 기존의 BossHitbox 타입이었던 것을 BossMeleeAttack으로 바꿉니다!
    public BossMeleeAttack leftClawAttack;
    public BossMeleeAttack rightClawAttack;
    public BossMeleeAttack bodyAttack;
    public BossMeleeAttack mouthAttack;

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

    private NetworkBossCore _bossCore;

    private void Awake()
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
        }

        _bossCore = GetComponentInParent<NetworkBossCore>();
    }

    private void LateUpdate()
    {
        // 뼈나 가이드가 없거나, 쳐다볼 필요가 없을 땐 무시
        if (headBone == null || lookAtGuide == null || _ikWeight <= 0.01f) return;

        // 1. 타겟을 향하는 '이상적인 방향' 계산
        Vector3 targetDirection = (_ikLookAtPosition - headBone.position).normalized;

        // 2. 드래곤이 애니메이션에 의해 '현재 실제로 쳐다보고 있는 방향' (가이드의 Z축)
        Vector3 currentLookDirection = lookAtGuide.forward;

        // 3. 현재 쳐다보는 방향에서 타겟 방향으로 '얼마나 회전해야 하는지(Delta)'를 계산합니다.
        // 이 FromToRotation이 뼈가 꼬인 문제를 완벽하게 무시해줍니다.
        Quaternion rotationDelta = Quaternion.FromToRotation(currentLookDirection, targetDirection);

        // 4. 방금 애니메이터가 잡아놓은 원래 뼈 각도에, 계산한 회전 차이값(Delta)을 얹어줍니다.
        Quaternion finalRotation = rotationDelta * headBone.rotation;

        // 5. 부드럽게 블렌딩!
        headBone.rotation = Quaternion.Lerp(headBone.rotation, finalRotation, _ikWeight);
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
        SpawnFireEffect();
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

    public void PlayGroggy(float speedMultiplier, float groggyDuration)
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

    public void SetDirection(float dirX, float dirY)
    {
        // Lerp를 사용하여 방향 전환이나 정지 시 뚝뚝 끊기지 않고 부드럽게 감속/가속되게 합니다.
        float currentX = anim.GetFloat("DirX");
        float currentY = anim.GetFloat("DirY");
        anim.SetFloat("DirX", Mathf.Lerp(currentX, dirX, Time.deltaTime * 5f));
        anim.SetFloat("DirY", Mathf.Lerp(currentY, dirY, Time.deltaTime * 5f));
    }

    // 제자리 회전 블렌딩. DragonVisual은 DirX/DirY(2D) 블렌드를 쓰므로,
    // 제자리 턴 스텝을 쓰려면 Animator에 "Turn" 파라미터 + 턴 블렌드를 추가하면 됨. (없으면 무시)
    public void SetTurn(float turnSign)
    {
        // 필요 시 여기에 anim.SetFloat("Turn", ...) 연결. 현재는 no-op(기존 동작 유지).
    }

    // 2. 시선 처리 목표 위치 갱신
    public void SetLookAtTarget(Vector3 targetPos)
    {
        _ikLookAtPosition = targetPos;
        _ikWeight = Mathf.Lerp(_ikWeight, 1f, Time.deltaTime * 2f); // 서서히 쳐다봄
    }

    // 시선 처리 초기화 (타겟이 없거나 죽었을 때)
    public void ResetLookAt()
    {
        _ikWeight = Mathf.Lerp(_ikWeight, 0f, Time.deltaTime * 3f);
    }

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

    public Quaternion ConsumeRootMotionRotation()
    {
        return Quaternion.identity;
    }
    
    // ==========================================
    // [IBossVisual 인터페이스 구현 - 빈 껍데기]
    // 이 보스는 패턴 중 Y축(공중) 이동 기믹을 사용하지 않으므로 비워둡니다.
    // ==========================================
    public void AddPatternYOffset(float deltaY)
    {
        // 내용 없음
    }

    public void ResetPatternYOffset()
    {
        // 내용 없음
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
        if (mouthAttack != null) mouthAttack.StartAttack();
        if (audioClips[2] != null)//bite1
        {
            SoundManager.Instance.PlaySFX_3D(audioClips[2], transform.position, SoundCategory.BossGimmick);
        }
    }
    public void DisableMouthHitbox() { if (mouthAttack != null) mouthAttack.StopAttack(); }

    // public void EnableClawHitbox()
    // {
    //     if (leftClawHitbox != null) leftClawHitbox.StartAttack();
    //     if (rightClawHitbox != null) rightClawHitbox.StartAttack();
    //     if (audioClips[0] != null)
    //     {
    //         SoundManager.Instance.PlaySFX_3D(audioClips[0], transform.position, SoundCategory.CombatHit);
    //     }
    // }
    // public void DisableClawHitbox()
    // {
    //     if (leftClawHitbox != null) leftClawHitbox.StopAttack();
    //     if (rightClawHitbox != null) rightClawHitbox.StopAttack();
    // }


    public void EnableClawHitbox()
    {
        // 신형 스윕 공격 시작!
        if (leftClawAttack != null) leftClawAttack.StartAttack();
        if (rightClawAttack != null) rightClawAttack.StartAttack();

        if (audioClips[0] != null)
        {
            SoundManager.Instance.PlaySFX_3D(audioClips[0], transform.position, SoundCategory.CombatHit);
        }
    }

    public void DisableClawHitbox()
    {
        // 신형 스윕 공격 종료!
        if (leftClawAttack != null) leftClawAttack.StopAttack();
        if (rightClawAttack != null) rightClawAttack.StopAttack();
    }

    public void EnableBodyHitbox()
    {
        if (bodyAttack != null) bodyAttack.StartAttack();
    }
    public void DisableBodyHitbox()
    {
        if (bodyAttack != null) bodyAttack.StopAttack();
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
            // 부모 없이 월드에 덩그러니 생성합니다. (보스가 도망가도 그 자리에 폭발)
            // 풀에서 꺼내 재사용(파티클 끝나면 자동 회수). 풀 없으면 폴백 생성.
            GameObject effect = EffectPoolManager.SpawnPooled(jumpSlamEffectPrefab, jumpSlamSpawnPoint.position, jumpSlamSpawnPoint.rotation);

            // 방장의 공격력 배율을 이펙트 스크립트에 주입!
            BossAoEAttack aoe = effect.GetComponent<BossAoEAttack>();
            if (aoe != null && _bossCore != null)
            {
                aoe.Initialize(_bossCore.GetOutgoingDamageMultiplier());
            }
        }
    }

    public void SpawnFireEffect()
    {
        if (fireBreath != null && fireBreathPoint != null)
        {
            // 🔥 [핵심 수정] fireBreathPoint(입 뼈)를 부모로 넣어 고개를 돌려도 브레스가 입에 붙어 따라갑니다.
            // 풀에서 꺼내 재사용(파티클 끝나면 자동 회수). 풀 없으면 폴백 생성.
            GameObject effect = EffectPoolManager.SpawnPooled(fireBreath, fireBreathPoint.position, fireBreathPoint.rotation, fireBreathPoint);

            // 🔥 [데미지 연동] 방장의 공격력 배율을 이펙트 스크립트에 주입!
            BossAoEAttack aoe = effect.GetComponent<BossAoEAttack>();
            if (aoe != null && _bossCore != null)
            {
                aoe.Initialize(_bossCore.GetOutgoingDamageMultiplier());
            }
        }
    }

    public void EnableHornAttackHitbox()
    {
        if (leftClawAttack != null) leftClawAttack.StartAttack();
        if (rightClawAttack != null) rightClawAttack.StartAttack();
        if (mouthAttack != null) mouthAttack.StartAttack();
        //if (bodyHitbox != null) bodyHitbox.StartAttack();
    }

    public void DisableHornAttackHitbox()
    {
        if (leftClawAttack != null) leftClawAttack.StopAttack();
        if (rightClawAttack != null) rightClawAttack.StopAttack();
        if (mouthAttack != null) mouthAttack.StopAttack();
        //if (bodyHitbox != null) bodyHitbox.StopAttack();
    }
}
using UnityEngine;

// [핵심] 폴라 드래곤 전용 Visual 클래스
public class PolarDragonVisual : MonoBehaviour, IBossVisual
{
    public Animator anim;

    [Header("수동 IK (고개 돌리기)")]
    public Transform headBone;
    public Transform lookAtGuide;
    private Vector3 _ikLookAtPosition;
    private float _ikWeight = 0f;

    [Header("근접 공격 판정 (신형 Sweep)")]
    [Tooltip("BiteAttack 애니메이션에 사용할 입 부분의 판정")]
    public BossMeleeAttack biteAttack;

    [Header("얼음 마법(AoE) 프리팹")]
    public GameObject spitFrozenBallPrefab; // SpitFrozenBall 애니메이션용 투사체
    public GameObject spreadFrozenBreathPrefab; // SpreadFrozenBreath 애니메이션용 브레스
    public Transform breathSpawnPoint; // 입 뼈(Bone) 위치

    [Header("사운드")]
    public AudioClip wakeUpSound;
    public AudioClip groggySound;
    public AudioClip dieSound;
    public AudioClip walkSound;
    public AudioClip iceMagicSound;

    private NetworkBossCore _bossCore;

    private void Awake()
    {
        if (anim == null) anim = GetComponent<Animator>();
        _bossCore = GetComponentInParent<NetworkBossCore>();
    }

    private void LateUpdate()
    {
        if (headBone == null || lookAtGuide == null || _ikWeight <= 0.01f) return;
        Vector3 targetDirection = (_ikLookAtPosition - headBone.position).normalized;
        Vector3 currentLookDirection = lookAtGuide.forward;
        Quaternion rotationDelta = Quaternion.FromToRotation(currentLookDirection, targetDirection);
        headBone.rotation = Quaternion.Lerp(headBone.rotation, rotationDelta * headBone.rotation, _ikWeight);
    }

    // ==========================================
    // [IBossVisual 구현부]
    // ==========================================
    public void PlayAction(int stateHash, float crossFadeTime = 0.1f) => anim.CrossFade(stateHash, crossFadeTime, 0, 0f);

    public void PlayWakeUp(int wakeUpHash)
    {
        PlayAction(wakeUpHash);
        if (wakeUpSound != null) SoundManager.Instance.PlaySFX_3D(wakeUpSound, transform.position, SoundCategory.BossGimmick);
    }

    public void PlayPhaseTransition(int wakeUpHash)
    {
        // 서버가 "체력 50% 이하! 변신해라!" 라고 명령하면 이륙 애니메이션을 틉니다.
        PlayAction(Animator.StringToHash("TakeOff")); 
        
        if (wakeUpSound != null) 
            SoundManager.Instance.PlaySFX_3D(wakeUpSound, transform.position, SoundCategory.BossGimmick);
    }

    public void PlayGroggy(float speedMultiplier)
    {
        SetAnimSpeed(speedMultiplier);
        // 올려주신 Animator 이미지에 있는 "GetHit1" State 사용
        PlayAction(Animator.StringToHash("GetHit1")); 
        if (groggySound != null) SoundManager.Instance.PlaySFX_3D(groggySound, transform.position, SoundCategory.BossGimmick);
    }

    public void PlayDie()
    {
        // 올려주신 Animator 이미지에 있는 "Death" State 사용
        PlayAction(Animator.StringToHash("Death")); 
        if (dieSound != null) SoundManager.Instance.PlaySFX_3D(dieSound, transform.position, SoundCategory.BossGimmick);
    }

    public void SetDirection(float dirX, float dirY)
    {
        // 서버에서 넘겨주는 방향 벡터의 길이(Magnitude)를 구해서 속도로 변환합니다.
        Vector2 dir = new Vector2(dirX, dirY);
        float targetSpeed = dir.magnitude; 

        float currentSpeed = anim.GetFloat("MoveSpeed");
        // 부드럽게 감속/가속되도록 Lerp 처리
        anim.SetFloat("MoveSpeed", Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 5f));
    }

    public void SetLookAtTarget(Vector3 targetPos)
    {
        _ikLookAtPosition = targetPos;
        _ikWeight = Mathf.Lerp(_ikWeight, 1f, Time.deltaTime * 2f);
    }

    public void ResetLookAt()
    {
        _ikWeight = Mathf.Lerp(_ikWeight, 0f, Time.deltaTime * 3f);
    }

    public void SetAnimSpeed(float multiplier) { anim.speed = multiplier; }

    public void DoLocomotion()
    {
        // 방장(Core)이 알려주는 현재 페이즈를 확인합니다.
        if (_bossCore != null && _bossCore.CurrentPhase == 2)
        {
            // 2페이즈: 체공 상태 (FlyStationary <-> Fly 블렌드 트리)
            anim.CrossFade("FlyLocomotion", 0.2f);
        }
        else
        {
            // 1페이즈: 지상 상태 (IdleBreathe <-> Walk 블렌드 트리)
            anim.CrossFade("GroundLocomotion", 0.2f);
        }
    }

    public void SetRootMotionCapture(bool enabled) {}
    public Vector3 ConsumeRootMotionDelta() { return Vector3.zero; }

    // ==========================================
    // [애니메이션 이벤트용 함수] 애니메이션 클립에서 호출!
    // ==========================================
    public void PlayFootstep()
    {
        if (walkSound != null) SoundManager.Instance.PlaySFX_3D(walkSound, transform.position, SoundCategory.Footstep);
    }

    public void EnableBiteHitbox() { if (biteAttack != null) biteAttack.StartAttack(); }
    public void DisableBiteHitbox() { if (biteAttack != null) biteAttack.StopAttack(); }

    // 애니메이션: SpitFrozenBall 프레임에 맞춤
    public void SpawnFrozenBall()
    {
        if (spitFrozenBallPrefab != null && breathSpawnPoint != null)
        {
            // 구체는 날아가야 하므로 부모 없이 월드 공간에 생성
            GameObject effect = Instantiate(spitFrozenBallPrefab, breathSpawnPoint.position, breathSpawnPoint.rotation);
            BossAoEAttack aoe = effect.GetComponent<BossAoEAttack>();
            if (aoe != null && _bossCore != null) aoe.Initialize(_bossCore.GetOutgoingDamageMultiplier());
            if (iceMagicSound != null) SoundManager.Instance.PlaySFX_3D(iceMagicSound, transform.position, SoundCategory.BossGimmick);
        }
    }

    // 애니메이션: SpreadFrozenBreath 프레임에 맞춤
    public void SpawnFrozenBreath()
    {
        if (spreadFrozenBreathPrefab != null && breathSpawnPoint != null)
        {
            // 브레스는 고개를 따라가야 하므로 breathSpawnPoint를 부모로 지정
            GameObject effect = Instantiate(spreadFrozenBreathPrefab, breathSpawnPoint.position, breathSpawnPoint.rotation, breathSpawnPoint);
            BossAoEAttack aoe = effect.GetComponent<BossAoEAttack>();
            if (aoe != null && _bossCore != null) aoe.Initialize(_bossCore.GetOutgoingDamageMultiplier());
            if (iceMagicSound != null) SoundManager.Instance.PlaySFX_3D(iceMagicSound, transform.position, SoundCategory.BossGimmick);
        }
    }
}
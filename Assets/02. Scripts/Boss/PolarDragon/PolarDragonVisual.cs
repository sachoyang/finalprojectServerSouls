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

    private float _currentHeight = 0f;

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

    [Header("공중 피격(Groggy) 설정")]
    [Tooltip("FlyStationaryGetHit 클립의 원본 길이(초). 지상 GetHit1과 길이가 달라 별도 지정.")]
    public float flyingGetHitLength = 0.833f;

    [Header("루트모션 캡처")]
    [Tooltip("Animator 가 붙은 오브젝트의 BossRootMotionCapture. 비워두면 자동으로 찾거나 부착합니다.")]
    public BossRootMotionCapture rootMotionCapture;

    private NetworkBossCore _bossCore;

    private void Awake()
    {
        if (anim == null) anim = GetComponent<Animator>();
        _bossCore = GetComponentInParent<NetworkBossCore>();

        // 🔥 [버그 픽스] 루트모션 캡처 컴포넌트 확보.
        //    이게 Animator 오브젝트에 있어야 OnAnimatorMove 가 루트모션 자동적용을 가로채서
        //    메쉬가 제멋대로 이동/회전(옆모습 버그)하는 것을 막고, 이동량만 코어에 넘겨준다.
        if (rootMotionCapture == null && anim != null)
        {
            rootMotionCapture = anim.GetComponent<BossRootMotionCapture>();
            if (rootMotionCapture == null)
                rootMotionCapture = anim.gameObject.AddComponent<BossRootMotionCapture>();
        }
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
    // 매 프레임 높이 조절 (부드러운 공중부양)
    // ==========================================
private void Update()
    {
        if (_bossCore == null) return;
        PolarDragonBoss polarBoss = _bossCore as PolarDragonBoss;
        if (polarBoss == null) return;

        // 🔥 [수정됨] HasStatus 대신 안전하게 IsFlightActive 사용
        float targetHeight = polarBoss.IsFlightActive ? polarBoss.flightHeight : 0f;
        _currentHeight = Mathf.Lerp(_currentHeight, targetHeight, Time.deltaTime * 2.5f);
        transform.localPosition = new Vector3(0f, _currentHeight, 0f);
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

    public void PlayGroggy(float speedMultiplier, float groggyDuration)
    {
        PolarDragonBoss polarBoss = _bossCore as PolarDragonBoss;

        // 🔥 [신규] 공중에 있을 땐 공중 전용 피격 모션 재생!
        if (polarBoss != null && polarBoss.IsFlightActive)
        {
            // 공중 피격 클립은 길이가 달라(예: 0.833s) 코어가 준 배속을 쓰면 엇박.
            // groggyDuration 동안 이 클립을 꽉 채우도록 배속을 직접 재계산한다.
            float flySpeed = (groggyDuration > 0f) ? flyingGetHitLength / groggyDuration : 1f;
            SetAnimSpeed(flySpeed);
            PlayAction(Animator.StringToHash("FlyStationaryGetHit"));
        }
        else
        {
            SetAnimSpeed(speedMultiplier);
            PlayAction(Animator.StringToHash("GetHit1"));
        }

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
        PolarDragonBoss polarBoss = _bossCore as PolarDragonBoss;
        // 🔥 [수정] HasStatus(버프 유무)가 아니라 IsFlightActive(안전장치 포함)로 통일.
        //    버프가 막 끝났어도 비행 패턴이 끝나기 전까지는 공중 Locomotion을 유지해야 한다.
        bool isFlying = polarBoss != null && polarBoss.IsFlightActive;

        if (isFlying)
        {
            // 공중: 체공 상태 (FlyStationary <-> Fly 블렌드 트리)
            anim.CrossFade("FlyLocomotion", 0.2f);
        }
        else
        {
            // 지상: (IdleBreathe <-> Walk 블렌드 트리)
            anim.CrossFade("GroundLocomotion", 0.2f);
        }
    }
    

    // 🔥 [버그 픽스] 빈 깡통이었던 부분. 이제 실제 캡처 컴포넌트로 위임한다.
    public void SetRootMotionCapture(bool enabled)
    {
        if (rootMotionCapture != null) rootMotionCapture.SetCapture(enabled);
    }

    public Vector3 ConsumeRootMotionDelta()
    {
        return rootMotionCapture != null ? rootMotionCapture.ConsumeDelta() : Vector3.zero;
    }

    // 🔥 [버그 픽스] 90도 루트모션 턴은 회전이 핵심. deltaRotation 을 코어에 넘겨줘야 실제로 돈다.
    public Quaternion ConsumeRootMotionRotation()
    {
        return rootMotionCapture != null ? rootMotionCapture.ConsumeDeltaRotation() : Quaternion.identity;
    }

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
using UnityEngine;

// [핵심] 폴라 드래곤 전용 Visual 클래스
public class PolarDragonVisual : MonoBehaviour, IBossVisual
{
    public Animator anim;

    [Header("수동 IK (고개 돌리기)")]
    public Transform headBone;
    public Transform lookAtGuide;
    [Tooltip("몸통 정면 기준 고개가 돌아갈 수 있는 최대 각도(도). 이 이상은 클램프해서 목 꺾임 방지.")]
    public float maxLookAngle = 70f;
    private Vector3 _ikLookAtPosition;
    private float _ikWeight = 0f;

    [Header("근접 공격 판정 (신형 Sweep)")]
    [Tooltip("BiteAttack 애니메이션에 사용할 입 부분의 판정")]
    public BossMeleeAttack biteAttack;

    private float _currentHeight = 0f;

    [Header("이착륙 고도 연출 (모션 동기화)")]
    [Tooltip("이륙 모션 길이(초). TakeOff 클립 길이와 맞추세요. 이 시간 동안 고도가 takeoffHeightCurve를 따라 올라갑니다.")]
    public float takeoffDuration = 1.5f;
    [Tooltip("착륙 모션 길이(초). Landing 클립 길이와 맞추세요.")]
    public float landingDuration = 1.2f;
    [Tooltip("이륙 진행(0~1) → 고도 비율(0~1). 앞부분을 평평하게 두면 '웅크렸다 도약'하는 앞동작 동안 안 떠서 땅에서 차고 오르는 느낌이 납니다.")]
    public AnimationCurve takeoffHeightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("착륙 진행(0~1) → 고도 비율(1~0).")]
    public AnimationCurve landingHeightCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    // 이착륙 전환을 각 클라이언트가 IsFlightActive 변화로 감지해 로컬 타이머로 연출 (네트워크 변수 불필요)
    private bool _prevFlight = false;
    private float _transitionRemaining = 0f;
    private bool _transitionIsTakeoff = false;

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
    private bool _hasTurnParam; // Animator에 "Turn" 파라미터가 있는지 (없으면 SetTurn graceful 무시)

    private void Awake()
    {
        if (anim == null) anim = GetComponent<Animator>();
        _bossCore = GetComponentInParent<NetworkBossCore>();

        // "Turn" 파라미터 존재 여부 1회 캐싱 (없을 때 SetFloat 경고 스팸 방지)
        if (anim != null)
            foreach (var p in anim.parameters)
                if (p.type == AnimatorControllerParameterType.Float && p.name == "Turn") { _hasTurnParam = true; break; }

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

        // 🔥 [목 꺾임 방지] 몸통 정면(루트 forward) 기준 maxLookAngle 콘 안으로 시선 방향을 클램프.
        //    타겟이 측후방에 있어도 고개가 콘 가장자리까지만 돌아간다.
        Vector3 bodyForward = (_bossCore != null ? _bossCore.transform.forward : transform.forward);
        Vector3 clampedDirection = Vector3.RotateTowards(bodyForward, targetDirection, maxLookAngle * Mathf.Deg2Rad, 0f);

        Vector3 currentLookDirection = lookAtGuide.forward;
        Quaternion rotationDelta = Quaternion.FromToRotation(currentLookDirection, clampedDirection);
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

        bool flying = polarBoss.IsFlightActive;

        // 🔥 [이착륙 동기화] IsFlightActive가 바뀌는 순간 이륙/착륙 전환 타이머 시작.
        //    고정 속도 lerp 대신 '진행도(0~1)를 커브로 매핑'해서 모션과 같은 타임라인으로 고도를 움직인다.
        if (flying != _prevFlight)
        {
            _transitionIsTakeoff = flying;
            _transitionRemaining = flying ? takeoffDuration : landingDuration;
            _prevFlight = flying;
        }

        float heightFrac;
        if (_transitionRemaining > 0f)
        {
            _transitionRemaining -= Time.deltaTime;
            float dur = _transitionIsTakeoff ? takeoffDuration : landingDuration;
            float p = (dur > 0f) ? Mathf.Clamp01(1f - (_transitionRemaining / dur)) : 1f;
            AnimationCurve curve = _transitionIsTakeoff ? takeoffHeightCurve : landingHeightCurve;
            heightFrac = Mathf.Clamp01(curve.Evaluate(p));
        }
        else
        {
            heightFrac = flying ? 1f : 0f; // 전환이 끝나면 완전 비행고도/지상으로 고정
        }

        float targetHeight = polarBoss.flightHeight * heightFrac;
        // 틱 단위 값의 미세 계단을 부드럽게(약한 스무딩). 커브가 이미 모양을 잡으므로 빠르게 추종.
        _currentHeight = Mathf.Lerp(_currentHeight, targetHeight, Time.deltaTime * 12f);
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

    // 🔥 [요구사항4] 제자리 회전 시 발 스텝 턴 모션을 블렌딩 (Foot Sliding 제거)
    public void SetTurn(float turnSign)
    {
        if (!_hasTurnParam) return; // 파라미터 미설정 시 안전하게 무시 (몸통은 코어가 턴 속도로 회전)
        float cur = anim.GetFloat("Turn");
        anim.SetFloat("Turn", Mathf.Lerp(cur, turnSign, Time.deltaTime * 8f));
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
            anim.CrossFade("FlyLocomotion", 0.05f);
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
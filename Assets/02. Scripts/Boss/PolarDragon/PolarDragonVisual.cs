using UnityEngine;
using UnityEngine.Serialization;

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
    public GameObject spreadFrozenBreathPrefab; // SpreadFrozenBreath 애니메이션용 브레스
    public Transform breathSpawnPoint; // 입 뼈(Bone) 위치

    [Header("얼음창(IceLance) 투사체")]
    [Tooltip("발사할 얼음창 프리팹 (icelance_one). IceLanceProjectile 컴포넌트가 붙어 있어야 함.")]
    public GameObject iceLancePrefab;
    [Tooltip("얼음창이 땅/플레이어에 맞았을 때 생성할 폭발 프리팹 (EnergyExplosion). BossAoEAttack로 데미지 처리.")]
    public GameObject energyExplosionPrefab;
    [Tooltip("조준 높이 보정. 어그로 플레이어의 발 기준 위치에서 이만큼 올려 조준(몸통/가슴을 노림).")]
    public float lanceAimHeightOffset = 1.0f;

    [Header("브레스 스트림 (IceThrower)")]
    [Tooltip("emitBreath 액션에서 입/머리에 붙어 뿜어질 브레스 프리팹(IceThrower). 판정이 필요하면 이 프리팹 루트에 " +
             "BossAoEAttack를 붙여 두세요(IceThrower엔 이미 있음). RetreatBreath/FlyBreath/네이팜 모두 이걸 공유합니다.")]
    [FormerlySerializedAs("napalmBreathPrefab")]
    public GameObject breathPrefab;
    [Tooltip("브레스가 붙을 뼈. 머리 본(headBone)이나 그 자식으로 지정하면 머리 스윙을 따라 브레스가 스윕됩니다.\n" +
             "비워두면 breathSpawnPoint(입 뼈)를 사용합니다.")]
    public Transform breathAttachPoint;

    [Header("뼈대 뒤틀림 보정")]
    [Tooltip("파티클은 무조건 +Z로 나가는데, 보스의 입 뼈대가 틀어져 있을 때 0도를 맞춰주기 위한 각도.\n" +
             "왼쪽 볼로 새어나가면 (0, 90, 0) 또는 (0, -90, 0)을 넣어서 정면을 맞춰주세요.")]
    public Vector3 muzzleRotationOffset = new Vector3(0f, 90f, 0f);


    [Header("네이팜 융단폭격 (비행 전용)")]
    [Tooltip("바닥에 줄줄이 떨어지는 불장판 프리팹(WildFire). 지속 데미지가 필요하면 이 프리팹 루트에 " +
             "BossAoEAttack(isMultiHit=ON, hitInterval, hitZones)를 붙여 두세요. 없으면 시각 연출만 됩니다.")]
    public GameObject napalmPoolPrefab;
    [Tooltip("네이팜이 안착할 지면 레이어. 보스 groundLayerMask와 동일하게 두면 됩니다.")]
    public LayerMask napalmGroundMask;
    [Tooltip("지면 감지 레이의 최대 길이(넉넉히).")]
    public float napalmRayHeight = 40f;
    [Tooltip("WildFire 풀 생성용 레이가 '보스 정면'에서 아래로 숙이는 각도(도). 0=수평 정면, 45=45°아래, 90=수직 아래.\n" +
             "이 각도만 바꾸면 풀이 보스 앞쪽 얼마나 멀리 깔릴지 조절된다(브레스 비주얼과 무관, 독립). 작을수록 더 멀리 앞.")]
    [Range(0f, 90f)] public float napalmRayDownAngle = 45f;
    [Tooltip("네이팜 투하/브레스 사운드(선택).")]
    public AudioClip napalmSound;
    [Tooltip("씬 뷰에서 네이팜 브레스 레이를 그려 디버깅한다(Play 중, Gizmos 켜기).\n" +
             "초록=바닥 명중, 빨강=허공(못 맞힘 → 폴백), 노랑=입→풀 생성지점, 자홍=폴백(바로 아래) 레이.")]
    public bool debugNapalmRay = false;

    // 네이팜은 각 클라이언트의 Update에서 지면에 투하한다(시각은 모두, 판정은 권한자만).
    // 현재 액션(패턴/스텝)이 바뀌면 리셋하고, 경과 시간 기준으로 몇 발까지 떨궜는지 추적한다.
    private int _napalmStepKey = -1;
    private int _napalmDropped = 0;
    private bool _breathSpawnedThisStep = false; // 이 액션에서 브레스 분사를 이미 시작했는지 (재분사 방지)

    // 진행 중인 브레스 스트림(입/머리에 붙은 IceThrower). 브레스 구간이 끝날 때 방출을 멈추고 놓아준다.
    private GameObject _activeBreath;
    private ParticleSystem[] _activeBreathPs;
    private Transform _breathAttach;        // 현재 브레스가 붙어 있는 부착점(조준 원점)
    private bool _breathTrackTarget;        // 매 프레임 플레이어를 향해 조준할지
    private Quaternion _breathAxisFix;      // 파티클 방출축(-X/+X 등)을 +Z로 정렬하는 보정

    [Header("사운드")]
    public AudioClip wakeUpSound;
    public AudioClip groggySound;
    public AudioClip dieSound;
    [Tooltip("걷기 발소리. 클립의 발 닿는 프레임에 PlayFootstep() 애니메이션 이벤트가 걸려 있어야 한다.")]
    public AudioClip walkSound;
    public AudioClip wingSound;
    [Tooltip("2페이즈 이륙(TakeOff). 비워두면 wakeUpSound를 대신 쓴다.")]
    public AudioClip takeOffSound;

    // 🔊 공격/패턴 효과음(물기, 얼음창, 브레스 등)은 여기가 아니라
    //    각 패턴 SO(BossPatternModule)의 액션마다 있는 actionSound 필드에서 지정한다.
    //    NetworkBossCore가 actionSoundPercent 타이밍에 맞춰 재생해 준다.
    //    여기 남은 건 패턴으로 표현할 수 없는 것들뿐이다:
    //      - 상태 사운드(각성/그로기/사망/이륙)
    //      - 발소리(Locomotion은 패턴이 아님)
    //      - 네이팜(액션당 1회가 아니라 장판마다, 보스가 아닌 지면 위치에서 울림)

    // SoundManager가 씬에 없을 때 MonoSingleton이 빈 오브젝트를 자동 생성하지 않도록 HasInstance로 막는다.
    private void Play3D(AudioClip clip, SoundCategory category)
    {
        if (clip == null || !SoundManager.HasInstance) return;
        SoundManager.Instance.PlaySFX_3D(clip, transform.position, category);
    }

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
        if (headBone != null && lookAtGuide != null && _ikWeight > 0.01f)
        {
            Vector3 targetDirection = (_ikLookAtPosition - headBone.position).normalized;

            // 🔥 [목 꺾임 방지] 몸통 정면(루트 forward) 기준 maxLookAngle 콘 안으로 시선 방향을 클램프.
            //    타겟이 측후방에 있어도 고개가 콘 가장자리까지만 돌아간다.
            Vector3 bodyForward = (_bossCore != null ? _bossCore.transform.forward : transform.forward);
            Vector3 clampedDirection = Vector3.RotateTowards(bodyForward, targetDirection, maxLookAngle * Mathf.Deg2Rad, 0f);

            Vector3 currentLookDirection = lookAtGuide.forward;
            Quaternion rotationDelta = Quaternion.FromToRotation(currentLookDirection, clampedDirection);
            headBone.rotation = Quaternion.Lerp(headBone.rotation, rotationDelta * headBone.rotation, _ikWeight);
        }

        // 🔥 [브레스 조준 - 머리 IK 다음에 갱신]
        if (_activeBreath != null && _breathTrackTarget && _breathAttach != null)
        {
            _activeBreath.transform.rotation = BreathAimRotationToTarget(_breathAttach);
        }
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

        // 🔥 [멀티 동기화 버그 픽스] 패턴 Y 누적치는 코어의 [Networked] PatternYOffset에서 읽는다.
        //    (기존 로컬 누적 방식은 호스트에서만 값이 쌓여, 클라이언트에선 네이팜 활공 중 바닥에 붙어 보였음)
        //    틱 스냅샷의 계단 현상은 비행고도와 같은 스무딩으로 완화.
        _patternYOffset = Mathf.Lerp(_patternYOffset, polarBoss.PatternYOffset, Time.deltaTime * 12f);

        // _currentHeight(2페이즈 비행 버프)와 _patternYOffset(현재 패턴 공중부양 누적치)를 합산!
        transform.localPosition = new Vector3(0f, _currentHeight + _patternYOffset, 0f);

        // 🔥 [브레스 + 네이팜] 브레스 스트림 유지 + 글라이드 중 불장판 투하.
        UpdateBreathAndNapalm(polarBoss);
    }

    // ==========================================
    // [브레스 스트림 + 네이팜 융단폭격]
    //  - emitBreath 액션 시작 시 입/머리에서 브레스(IceThrower)를 뿜기 시작(루프)하고,
    //    브레스 구간(emitBreath 또는 dropNapalm이 이어지는 동안)이 끝나면 자동으로 멈춘다.
    //  - dropNapalm 액션 동안엔 napalmInterval마다 보스 바로 아래 지면에 불장판을 떨군다.
    //  Update는 모든 클라이언트에서 돌기 때문에, 시각(파티클)은 전원이 보되 데미지 판정은 권한자만 켠다.
    // ==========================================
    private void UpdateBreathAndNapalm(PolarDragonBoss boss)
    {
        BossActionModule action = boss.CurrentAction;
        if (action == null)
        {
            // 패턴 실행 중이 아니면(종료/그로기/사망 등) 브레스를 끄고 카운터 리셋
            StopBreath();
            _napalmStepKey = -1;
            _napalmDropped = 0;
            return;
        }

        // 🔥 [SO 타이밍] 액션 진행률(0~1) 계산. 애니메이션 이벤트 대신 이 값과 SO의
        //    breathStart/StopPercent, napalmStart/StopPercent를 비교해 이펙트 타이밍을 잡는다.
        //    StateTimer는 [Networked]라 호스트/클라이언트 모두 같은 진행률을 본다.
        float remaining = boss.StateTimer.RemainingTime(boss.Runner) ?? 0f;
        float elapsed = action.duration - remaining;
        float progress = (action.duration > 0f) ? Mathf.Clamp01(elapsed / action.duration) : 1f;

        // 패턴/스텝이 바뀌는 순간을 감지(액션 진입 1회 처리)
        int key = boss.CurrentPatternIndex * 1000 + boss.CurrentStepIndex;
        if (key != _napalmStepKey)
        {
            _napalmStepKey = key;
            _napalmDropped = 0;
            _breathSpawnedThisStep = false;
        }

        // 브레스 시작: emitBreath 액션에서 breathStartPercent 도달 시 1회 분사 시작 (0이면 액션 시작과 동시)
        if (action.emitBreath && !_breathSpawnedThisStep && progress >= action.breathStartPercent)
        {
            _breathSpawnedThisStep = true;
            SpawnBreath(boss, action);
        }

        // 🔥 브레스는 '브레스 구간(emitBreath 또는 dropNapalm 액션)' 동안만 유지.
        //    구간을 벗어나 다음 액션(예: GlideToLanding, 착륙 등)으로 넘어가면 방출을 멈춰
        //    파티클 길이를 손대지 않아도 항상 액션 종료까지 자동으로 이어진다.
        //    추가로 emitBreath 액션에 breathStopPercent(<1)를 설정하면 그 시점에 조기 종료(후딜 연출용).
        bool inBreathPhase = action.emitBreath || action.dropNapalm;
        bool earlyStop = action.emitBreath && action.breathStopPercent < 1f && progress >= action.breathStopPercent;
        if (!inBreathPhase || earlyStop) StopBreath();

        // 🔥 [플레이어 조준 추적] LateUpdate로 이동함(여기서 먼저 조준해도 이후 머리 IK가 부착 본을
        //    또 돌리면 그만큼 브레스가 밀려 어긋나기 때문). 실제 갱신 로직은 LateUpdate() 참고.

        // 불장판 투하(dropNapalm 켠 액션에서만, napalmStart~StopPercent 창 안에서만)
        if (!action.dropNapalm || napalmPoolPrefab == null) return;
        if (progress < action.napalmStartPercent || progress > action.napalmStopPercent) return;

        // 투하 창 안에서 경과 시간(실초) 기준으로 "지금까지 몇 발을 떨궜어야 하는지" 계산 → 프레임률과 무관하게 균일한 카펫.
        float interval = Mathf.Max(0.05f, action.napalmInterval);
        float windowElapsed = elapsed - (action.napalmStartPercent * action.duration);
        int shouldHave = Mathf.FloorToInt(windowElapsed / interval) + 1; // 창 시작 시점에서 첫 발
        // 혹시 모를 폭주 방지(한 프레임에 과다 스폰 차단)
        if (shouldHave - _napalmDropped > 32) _napalmDropped = shouldHave - 32;
        while (_napalmDropped < shouldHave)
        {
            DropNapalm(boss);
            _napalmDropped++;
        }
    }

    // 🔥 WildFire 풀을 '보스 앞쪽 바닥'에 스폰한다.
    //  레이는 브레스 비주얼과 무관하게 독립적으로 정한다:
    //   - 출발점 = 입(마우스 본) 위치
    //   - 방향 = 보스 정면(수평, 플레이어 안 따라감)을 napalmRayDownAngle만큼 아래로 숙인 방향
    //   → 45°면 보스보다 앞쪽 바닥에 닿아, 앞으로 훑는 융단폭격 느낌. 각도만 바꾸면 거리 조절.
    //   (레이가 빗나가면 보스 바로 아래로 폴백)
    private void DropNapalm(PolarDragonBoss boss)
    {
        Vector3 spawnPos;
        bool placed = false;

        // 출발점: 입(마우스 본). 브레스 부착점이 있으면 그걸, 없으면 breathSpawnPoint.
        Transform mouth = (_breathAttach != null) ? _breathAttach
                        : (breathAttachPoint != null) ? breathAttachPoint : breathSpawnPoint;

        if (mouth != null)
        {
            // 방향: 보스 정면(수평) → napalmRayDownAngle만큼 아래로. (heading·cos + down·sin)
            Vector3 heading = boss.transform.forward;
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.0001f) heading = mouth.forward;
            heading.y = 0f;
            heading.Normalize();

            float a = napalmRayDownAngle * Mathf.Deg2Rad;
            Vector3 rayDir = (heading * Mathf.Cos(a) + Vector3.down * Mathf.Sin(a)).normalized;

            Vector3 origin = mouth.position;
            float rayLen = napalmRayHeight * 4f;

            bool hitGround = Physics.Raycast(origin, rayDir, out RaycastHit bh, rayLen, napalmGroundMask);
            if (debugNapalmRay)
                Debug.DrawRay(origin, rayDir * rayLen, hitGround ? Color.green : Color.red, 1.5f);

            if (hitGround)
            {
                spawnPos = bh.point;
                placed = true;
                if (debugNapalmRay) Debug.DrawLine(origin, spawnPos, Color.yellow, 1.5f);
            }
            else spawnPos = origin;
        }
        else spawnPos = boss.transform.position;

        if (!placed)
        {
            // 폴백: 보스 바로 아래 지면
            Vector3 rootPos = boss.transform.position;
            Vector3 start = rootPos + Vector3.up * napalmRayHeight;
            if (debugNapalmRay)
                Debug.DrawRay(start, Vector3.down * (napalmRayHeight * 2f), Color.magenta, 1.5f);
            if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, napalmRayHeight * 2f, napalmGroundMask))
                spawnPos = hit.point;
            else
                spawnPos = new Vector3(rootPos.x, rootPos.y - boss.flightHeight, rootPos.z);
        }

        // 부모 없이 월드에 생성(보스가 지나가도 그 자리에 장판이 남음). 풀에서 꺼내 재사용.
        GameObject fx = EffectPoolManager.SpawnPooled(napalmPoolPrefab, spawnPos, Quaternion.identity);
        InitAoeForRole(fx, boss);

        // 네이팜은 보스가 아니라 불장판이 떨어진 지면에서 소리가 나야 한다.
        if (napalmSound != null && SoundManager.HasInstance)
            SoundManager.Instance.PlaySFX_3D(napalmSound, spawnPos, SoundCategory.BossGimmick);
    }

    private void SpawnBreath(PolarDragonBoss boss, BossActionModule action)
    {
        Transform attach = (breathAttachPoint != null) ? breathAttachPoint : breathSpawnPoint;
        if (breathPrefab == null || attach == null) return;

        StopBreath();

        Quaternion aimRot;

        if (action.breathAimAtTarget)
        {
            // 일반 브레스: 플레이어 높이 조준 + 애니메이션 좌우 스윕
            aimRot = BreathAimRotationToTarget(attach);
        }
        else
        {
            // 네이팜 (고정 발사): 조준 없이 진짜 입 정면에서 설정한 각도만큼 아래로 숙임
            Quaternion trueMouthRot = attach.rotation * Quaternion.Euler(muzzleRotationOffset);
            aimRot = trueMouthRot * Quaternion.Euler(action.breathDownAngle, 0f, 0f);
        }

        // 파티클은 +Z로 방출되므로, aimRot 방향을 그대로 월드 회전값으로 주면 완벽하게 날아감
        GameObject fx = EffectPoolManager.SpawnPooled(breathPrefab, attach.position, aimRot, attach);
        InitAoeForRole(fx, boss);
        if (fx == null) return;

        _breathAttach = attach;
        _breathTrackTarget = action.breathAimAtTarget;

        _activeBreath = fx;
        _activeBreathPs = fx.GetComponentsInChildren<ParticleSystem>(true);
        if (_activeBreathPs != null)
        {
            foreach (var ps in _activeBreathPs)
            {
                if (ps == null) continue;
                var main = ps.main;
                main.loop = true;
                ps.Play(true);
            }
        }
    }

    private Quaternion BreathAimRotationToTarget(Transform attach)
    {
        // 1. 뼈대의 꼬임을 풀어서 입이 바라보는 '진짜 월드 정면' 방향을 구함
        Quaternion trueMouthRot = attach.rotation * Quaternion.Euler(muzzleRotationOffset);
        Vector3 trueMouthForward = trueMouthRot * Vector3.forward;

        // 2. 위아래(Pitch)를 무시하고 순수하게 고개를 좌우로 흔드는 방향(Yaw)만 추출
        Vector3 flatForward = new Vector3(trueMouthForward.x, 0f, trueMouthForward.z).normalized;
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = transform.forward;

        if (_bossCore != null && _bossCore.AggroTarget != null)
        {
            // 3. 타겟(플레이어 가슴팍)까지의 수평 거리와 수직 높이(Y) 차이 계산
            Vector3 targetPos = _bossCore.AggroTarget.transform.position + Vector3.up * 1.0f;
            float distXZ = Vector2.Distance(new Vector2(targetPos.x, targetPos.z), new Vector2(attach.position.x, attach.position.z));
            float dy = targetPos.y - attach.position.y;

            // 4. 좌우는 애니메이션(flatForward)을 따르고, 높이는 플레이어(dy)를 따르도록 벡터 합성!
            Vector3 finalAimDir = (flatForward * distXZ + Vector3.up * dy).normalized;

            // 파티클의 +Z축이 이 합성된 방향을 정확히 쳐다보게 만듦
            return Quaternion.LookRotation(finalAimDir, Vector3.up);
        }

        // 타겟이 없으면 그냥 진짜 입 방향으로 발사
        return Quaternion.LookRotation(trueMouthForward, Vector3.up);
    }

    // 브레스 방출을 멈추고(잔여 파티클은 자연스레 소멸) 참조를 놓는다.
    //  AutoReturnToPool이 파티클이 다 죽은 것을 감지해 풀로 회수한다.
    private void StopBreath()
    {
        if (_activeBreath == null) return;

        if (_activeBreathPs != null)
        {
            foreach (var ps in _activeBreathPs)
            {
                if (ps == null) continue;
                // StopEmitting: 새로 뿜는 것만 멈추고 이미 뿜은 불길은 수명만큼 남아 사라진다(뚝 끊김 방지)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
        _activeBreath = null;
        _activeBreathPs = null;
        _breathAttach = null;
        _breathTrackTarget = false;
    }

    // 스폰한 이펙트의 BossAoEAttack 판정을 권한자만 켠다.
    //  - 권한자: Initialize로 데미지 배율 주입(판정 ON)
    //  - 원격 클라: 컴포넌트 비활성화(시각만) → TakeDamage가 RPC로 권한자에 재전송되어 인원수만큼 중복되는 것을 방지
    private void InitAoeForRole(GameObject fx, PolarDragonBoss boss)
    {
        if (fx == null) return;
        BossAoEAttack aoe = fx.GetComponent<BossAoEAttack>();
        if (aoe == null) return;

        if (boss != null && boss.HasStateAuthority)
            aoe.Initialize(boss.GetOutgoingDamageMultiplier());
        else
            // 시각/사운드는 모든 클라이언트가, 데미지 판정만 권한자가.
            // 컴포넌트를 통째로 끄면 풀 재사용 시 OnEnable이 안 와서 스폰 사운드가 빠진다.
            aoe.SetDamageEnabled(false);
    }

    // ==========================================
    // [IBossVisual 구현부]
    // ==========================================
    public void PlayAction(int stateHash, float crossFadeTime = 0.1f) => anim.CrossFade(stateHash, crossFadeTime, 0, 0f);

    public void PlayWakeUp(int wakeUpHash)
    {
        PlayAction(wakeUpHash);
        Play3D(wakeUpSound, SoundCategory.BossGimmick);
    }

    public void PlayPhaseTransition(int wakeUpHash)
    {
        // 서버가 "체력 50% 이하! 변신해라!" 라고 명령하면 이륙 애니메이션을 틉니다.
        PlayAction(Animator.StringToHash("TakeOff"));

        // 전용 이륙음이 없으면 각성음으로 폴백 (기존 동작 유지)
        Play3D(takeOffSound != null ? takeOffSound : wakeUpSound, SoundCategory.BossGimmick);
    }

    [Header("그로기 모션 방식")]
    // 루프 모드 ON/OFF와 피격 배속은 보스 코어(NetworkBossCore)의
    // groggyDurationAfterHitAnim / groggyHitAnimSpeed에서 관리한다 (설정 창구 일원화).
    // 지상(GetHit1)/공중(FlyStationaryGetHit) 각각 루프 스테이트를 만들어 Exit Time 전이로 연결할 것.
    [Tooltip("그로기(웅크린 루프)에서 일어나 Locomotion으로 돌아올 때의 크로스페이드 시간(초).\n" +
             "웅크린 포즈와 서 있는 포즈의 차이가 커서 짧은 블렌드로는 뚝 끊겨 보인다. 0.4~0.6 권장.")]
    public float groggyRecoverBlendTime = 0.5f;

    private bool _groggyRecoverPending = false;

    public void PlayGroggy(float speedMultiplier, float groggyDuration)
    {
        PolarDragonBoss polarBoss = _bossCore as PolarDragonBoss;

        // 🔥 [신규] 공중에 있을 땐 공중 전용 피격 모션 재생!
        if (polarBoss != null && polarBoss.IsFlightActive)
        {
            // 공중 피격 클립은 길이가 달라(예: 0.833s) 스트레치 모드에선 코어가 준 배속을 쓰면 엇박.
            // groggyDuration 동안 이 클립을 꽉 채우도록 배속을 직접 재계산한다.
            // 루프 모드에선 코어가 넘겨준 배속(groggyHitAnimSpeed)을 그대로 쓴다 (루프가 나머지 시간을 채움).
            float flySpeed = (groggyDuration > 0f) ? flyingGetHitLength / groggyDuration : 1f;
            bool loopMode = _bossCore != null && _bossCore.groggyDurationAfterHitAnim;
            SetAnimSpeed(loopMode ? speedMultiplier : flySpeed);
            PlayAction(Animator.StringToHash("FlyStationaryGetHit"));
        }
        else
        {
            // 루프/스트레치 모드 공통: 코어가 모드에 맞는 배속을 계산해서 넘겨준다.
            SetAnimSpeed(speedMultiplier);
            PlayAction(Animator.StringToHash("GetHit1"));
        }

        _groggyRecoverPending = true; // 다음 Locomotion 복귀는 긴 블렌드로

        Play3D(groggySound, SoundCategory.BossGimmick);
    }

    public void PlayDie()
    {
        // 올려주신 Animator 이미지에 있는 "Death" State 사용
        PlayAction(Animator.StringToHash("Death"));
        Play3D(dieSound, SoundCategory.BossGimmick);
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

        // 그로기(웅크림)에서 일어나는 복귀는 포즈 차이가 커서 긴 블렌드로 부드럽게 잇는다.
        bool recovering = _groggyRecoverPending;
        _groggyRecoverPending = false;

        if (isFlying)
        {
            // 공중: 체공 상태 (FlyStationary <-> Fly 블렌드 트리)
            anim.CrossFade("FlyLocomotion", recovering ? groggyRecoverBlendTime : 0.05f);
        }
        else
        {
            // 지상: (IdleBreathe <-> Walk 블렌드 트리)
            anim.CrossFade("GroundLocomotion", recovering ? groggyRecoverBlendTime : 0.2f);
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
    private float _patternYOffset = 0f; // 코어의 PatternYOffset을 스무딩해서 따라가는 표시용 값

    // ==========================================
    // [애니메이션 이벤트용 함수] 애니메이션 클립에서 호출!
    // ==========================================
    public void PlayFootstep()
    {
        Play3D(walkSound, SoundCategory.Footstep);
    }

    public void PlayWing()
    {
        Play3D(wingSound, SoundCategory.Footstep);
    }

    // 물기 효과음은 BiteAttack 패턴 SO의 actionSound에서 지정한다.
    public void EnableBiteHitbox() { if (biteAttack != null) biteAttack.StartAttack(); }
    public void DisableBiteHitbox() { if (biteAttack != null) biteAttack.StopAttack(); }

    // ==========================================
    // [애니메이션 이벤트] 얼음창 발사 — SpitFrozenBall / FlyStationarySpitFrozenBall 클립의 발사 프레임에 연결.
    //  지상/공중 두 SpitBall 패턴 모두 이 함수 하나로 처리됨.
    //  목적지 = 호출(발사) 시점의 어그로 플레이어 위치(고정, 따라가지 않음).
    // ==========================================
    public void SpawnIceLance()
    {
        if (iceLancePrefab == null || breathSpawnPoint == null) return;

        // 1) 목적지 = 지금 이 순간 어그로 플레이어 위치 (없으면 입 정면으로 폴백)
        Vector3 targetPos;
        if (_bossCore != null && _bossCore.AggroTarget != null)
            targetPos = _bossCore.AggroTarget.transform.position + Vector3.up * lanceAimHeightOffset;
        else
            targetPos = breathSpawnPoint.position + breathSpawnPoint.forward * 20f;

        // 2) 입에서 생성 (월드 공간 — 날아가야 하므로 부모 없음)
        Vector3 dir = (targetPos - breathSpawnPoint.position).normalized;
        Quaternion rot = (dir != Vector3.zero) ? Quaternion.LookRotation(dir) : breathSpawnPoint.rotation;
        GameObject lance = Instantiate(iceLancePrefab, breathSpawnPoint.position, rot);

        // 3) 발사 지시. 데미지는 보스 권한(호스트)에서만 → 멀티 중복 데미지 방지.
        //    스크립트가 루트가 아닌 자식 파티클(TinyShards 등)에 붙어 있어도 찾도록 InChildren 사용.
        IceLanceProjectile proj = lance.GetComponentInChildren<IceLanceProjectile>();
        if (proj != null)
        {
            bool applyDamage = _bossCore != null && _bossCore.HasStateAuthority;
            float mult = _bossCore != null ? _bossCore.GetOutgoingDamageMultiplier() : 1f;
            proj.Launch(targetPos, energyExplosionPrefab, applyDamage, mult);
        }
    }
}
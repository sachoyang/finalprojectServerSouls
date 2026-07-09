using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class OrcAssassinVisual : MonoBehaviour, IBossVisual
{
    public Animator anim;

    private Vector3 _ikLookAtPosition;
    private float _ikWeight = 0f;

    [Header("이펙트 & 투사체")]
    public GameObject poisonDaggerPrefab;
    public Transform daggerSpawnPoint;
    public ParticleSystem smokeBombEffect; // 2페이즈 은신/이동용

    [Header("투명화(다크 템플러) 연출")]
    [Tooltip("보스의 피부, 옷, 무기 등을 렌더링하는 MeshRenderer들을 전부 넣어주세요.")]
    public SkinnedMeshRenderer[] bossRenderers;
    [Tooltip("에셋이나 쉐이더로 만든 다크 템플러(굴절) 머티리얼을 넣습니다.")]
    public Material stealthMaterial;

    [Header("투명화 타이밍 (자연스러운 연막 연출)")]
    [Tooltip("연막을 터트린 뒤 몸을 숨기기(머티리얼 교체) 시작할 때까지의 대기 시간(초).\n" +
             "연막 파티클이 보스 몸을 가릴 만큼 짙어지는 타이밍에 맞춰주세요.")]
    public float stealthSwapDelay = 0.4f;
    [Tooltip("은신 머티리얼로 바꾼 뒤 서서히 스며들게 하는 페이드 시간(초).\n" +
             "은신 쉐이더에 _Color(알파) 프로퍼티가 있어야 동작하며, 없으면 연막에 가려진 채 즉시 전환된다.")]
    public float stealthFadeDuration = 1.0f;

    private Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();

    // 공유 에셋(stealthMaterial)을 직접 페이드하면 에디터에서 에셋 원본이 오염되므로,
    // 런타임 인스턴스 1개를 만들어 모든 렌더러가 공유하게 한다.
    private Material _stealthMatInstance;
    private Coroutine _stealthRoutine;
    private static readonly int ColorProp = Shader.PropertyToID("_Color");

    [Header("근접 공격 판정 (신형 Sweep)")]
    // BossHurtbox -> BossMeleeAttack으로 변경 및 이름 수정
    public BossMeleeAttack leftDaggerAttack;
    public BossMeleeAttack rightDaggerAttack;

    [Header("사운드")]
    [Tooltip("걷기 발소리. Locomotion 클립의 발 닿는 프레임에 PlayFootstep() 애니메이션 이벤트를 찍어야 난다.")]
    public AudioClip walkSound;
    public AudioClip vanishSound;          // 은신 소리
    [Tooltip("각성(전투 진입) 포효.")]
    public AudioClip wakeUpSound;
    [Tooltip("그로기(경직)로 무너질 때.")]
    public AudioClip groggySound;
    [Tooltip("사망.")]
    public AudioClip dieSound;

    // 🔊 공격/패턴 효과음(단검 휘두르기, 독 단검 투척 등)은 여기가 아니라
    //    각 패턴 SO(BossPatternModule)의 액션마다 있는 actionSound 필드에서 지정한다.
    //    NetworkBossCore가 actionSoundPercent 타이밍에 맞춰 재생해 준다.
    //    여기 남은 건 패턴으로 표현할 수 없는 상태 사운드와 발소리뿐이다.

    // SoundManager가 씬에 없을 때 MonoSingleton이 빈 오브젝트를 자동 생성하지 않도록 HasInstance로 막는다.
    private void Play3D(AudioClip clip, SoundCategory category)
    {
        if (clip == null || !SoundManager.HasInstance) return;
        SoundManager.Instance.PlaySFX_3D(clip, transform.position, category);
    }

    [Header("루트모션 캡처")]
    [Tooltip("Animator 가 붙은 오브젝트의 BossRootMotionCapture. 비워두면 자동 검색.")]
    public BossRootMotionCapture rootMotionCapture;

    private void Awake()
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
        }

        foreach (var r in bossRenderers)
        {
            if (r != null)
            {
                _originalMaterials[r] = r.sharedMaterials;
            }
        }
    }

    // ==========================================
    // 은신 켜기 / 끄기 함수
    //  연출 순서: 연막 폭발 → 연막이 몸을 가리는 동안(stealthSwapDelay) 대기 →
    //             은신 머티리얼로 교체 → 알파 페이드로 스르륵 스며듦(stealthFadeDuration)
    //  연막이 걷힐 때쯤엔 이미 투명해져 있어서 '연기 속으로 사라진' 것처럼 보인다.
    // ==========================================
    public void EnableStealth()
    {
        // PlayPhaseTransition이 같은 프레임에 이미 연막을 터트렸을 수 있으므로 재시작하지 않는다.
        if (smokeBombEffect != null && !smokeBombEffect.isPlaying) smokeBombEffect.Play();
        if (vanishSound != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX_3D(vanishSound, transform.position, SoundCategory.BossGimmick);
        }

        if (_stealthRoutine != null) StopCoroutine(_stealthRoutine);
        _stealthRoutine = StartCoroutine(StealthInRoutine());
    }

    private IEnumerator StealthInRoutine()
    {
        // 1. 연막이 보스 몸을 가릴 만큼 짙어질 때까지 잠깐 대기
        if (stealthSwapDelay > 0f)
        {
            yield return new WaitForSeconds(stealthSwapDelay);
        }

        // 2. 연막 속에서 은신 머티리얼로 교체
        if (_stealthMatInstance == null && stealthMaterial != null)
        {
            _stealthMatInstance = new Material(stealthMaterial);
        }

        foreach (var r in bossRenderers)
        {
            if (r == null) continue;

            Material[] stealthMats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < stealthMats.Length; i++)
            {
                stealthMats[i] = _stealthMatInstance != null ? _stealthMatInstance : stealthMaterial;
            }
            r.sharedMaterials = stealthMats;
        }

        // 3. 쉐이더가 _Color(알파)를 지원하면, 반투명 실루엣 → 원래 은신 농도로 서서히 페이드
        if (_stealthMatInstance != null &&
            _stealthMatInstance.HasProperty(ColorProp) &&
            stealthFadeDuration > 0f)
        {
            Color targetColor = stealthMaterial.GetColor(ColorProp); // 에셋에 세팅된 최종 은신 색/농도
            Color startColor = targetColor;
            startColor.a = Mathf.Max(targetColor.a, 0.6f); // 교체 직후엔 실루엣이 살짝 보이는 상태에서 시작

            float elapsed = 0f;
            while (elapsed < stealthFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / stealthFadeDuration);
                _stealthMatInstance.SetColor(ColorProp, Color.Lerp(startColor, targetColor, t));
                yield return null;
            }
            _stealthMatInstance.SetColor(ColorProp, targetColor);
        }

        _stealthRoutine = null;
    }

    public void DisableStealth()
    {
        // 은신 진입 페이드가 진행 중이었다면 중단하고 즉시 복구
        if (_stealthRoutine != null)
        {
            StopCoroutine(_stealthRoutine);
            _stealthRoutine = null;
        }

        // 다시 나타날 때도 연막을 터트려 '연기 속에서 나타나는' 연출로 자연스럽게
        if (smokeBombEffect != null && !smokeBombEffect.isPlaying) smokeBombEffect.Play();

        foreach (var r in bossRenderers)
        {
            if (r == null) continue;

            if (_originalMaterials.TryGetValue(r, out Material[] origMats))
            {
                r.sharedMaterials = origMats;
            }
        }
        Debug.Log("[Visual] 오크 어쌔신 투명화 해제! 원래 모습으로 돌아옵니다.");
    }

    private void OnDestroy()
    {
        // 런타임에 만든 머티리얼 인스턴스 정리 (씬 전환 시 누수 방지)
        if (_stealthMatInstance != null)
        {
            Destroy(_stealthMatInstance);
        }
    }

    // ==========================================
    // [IBossVisual 구현부] 
    // ==========================================
    public void PlayAction(int stateHash, float crossFadeTime = 0.1f) => anim.CrossFade(stateHash, crossFadeTime, 0, 0f);

    public void SetDirection(float dirX, float dirY)
    {
        Vector2 dir = new Vector2(dirX, dirY);
        float targetSpeed = dir.magnitude;
        float currentSpeed = anim.GetFloat("MoveSpeed");
        anim.SetFloat("MoveSpeed", Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 5f));
    }

    public void SetTurn(float turnSign)
    {
        // OrcAssassin은 제자리 턴 블렌드 미사용. 필요 시 anim.SetFloat("Turn", ...) 연결. (현재 no-op)
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

    private void OnAnimatorIK(int layerIndex)
    {
        if (anim == null) return;

        if (_ikWeight > 0.01f)
        {
            anim.SetLookAtWeight(_ikWeight, 0.2f, 0.8f, 0f, 0.5f);
            anim.SetLookAtPosition(_ikLookAtPosition);
        }
        else
        {
            anim.SetLookAtWeight(0);
        }
    }

    public void SetAnimSpeed(float multiplier) => anim.speed = multiplier;
    public void DoLocomotion()
    {
        // 그로기(웅크림)에서 일어나는 복귀는 포즈 차이가 커서 긴 블렌드로 부드럽게 잇는다.
        float fadeTime = _groggyRecoverPending ? groggyRecoverBlendTime : 0.1f;
        _groggyRecoverPending = false;

        anim.CrossFade("Locomotion", fadeTime);
    }

    public void PlayWakeUp(int wakeUpHash)
    {
        PlayAction(wakeUpHash);
        Play3D(wakeUpSound, SoundCategory.BossGimmick);
    }

    public void PlayPhaseTransition(int wakeUpHash)
    {
        PlayAction(wakeUpHash);
        if (smokeBombEffect != null) smokeBombEffect.Play();
    }

    [Header("그로기 모션 방식")]
    // 루프 모드 ON/OFF와 gethit 배속은 보스 코어(NetworkBossCore)의
    // groggyDurationAfterHitAnim / groggyHitAnimSpeed에서 관리한다 (설정 창구 일원화).
    [Tooltip("그로기(웅크린 루프)에서 일어나 Locomotion으로 돌아올 때의 크로스페이드 시간(초).\n" +
             "웅크린 포즈와 서 있는 포즈의 차이가 커서 기본 0.1초로는 뚝 끊겨 보인다. 0.4~0.6 권장.")]
    public float groggyRecoverBlendTime = 0.5f;

    private bool _groggyRecoverPending = false;

    public void PlayGroggy(float speedMultiplier, float groggyDuration)
    {
        // 루프 모드: 코어가 groggyHitAnimSpeed(gethit 재생 배속)를 speedMultiplier로 넘겨준다. 기본 1배속.
        // 스트레치 모드: 클립을 그로기 시간에 맞춰 잡아늘리는 배속이 넘어온다. (기존 동작)
        SetAnimSpeed(speedMultiplier);
        anim.CrossFade("gethit", 0.1f);
        _groggyRecoverPending = true; // 다음 Locomotion 복귀는 긴 블렌드로

        Play3D(groggySound, SoundCategory.BossGimmick);
    }
    public void PlayDie()
    {
        anim.CrossFade("death1", 0.1f);
        Play3D(dieSound, SoundCategory.BossGimmick);
    }

    public void SetRootMotionCapture(bool enabled)
    {
        if (rootMotionCapture != null) rootMotionCapture.SetCapture(enabled);
    }

    public Vector3 ConsumeRootMotionDelta()
    {
        return rootMotionCapture != null ? rootMotionCapture.ConsumeDelta() : Vector3.zero;
    }

    public Quaternion ConsumeRootMotionRotation()
    {
        return rootMotionCapture != null ? rootMotionCapture.ConsumeDeltaRotation() : Quaternion.identity;
    }

    // ==========================================
    // [애니메이션 이벤트용 함수] 
    // ==========================================
    // 휘두르는 효과음은 해당 공격 패턴 SO의 actionSound에서 지정한다.
    public void EnableRightDagger()
    {
        // 🔥 [수정됨] 신형 근접 공격 시스템 호출
        if (rightDaggerAttack != null) rightDaggerAttack.StartAttack();
        if (leftDaggerAttack != null) leftDaggerAttack.StartAttack();
    }
    public void DisableRightDagger()
    {
        if (rightDaggerAttack != null) rightDaggerAttack.StopAttack();
        if (leftDaggerAttack != null) leftDaggerAttack.StopAttack();
    }

    // 걷기 발소리. Locomotion 클립의 발 닿는 프레임에 이 함수를 애니메이션 이벤트로 걸어야 한다.
    public void PlayFootstep()
    {
        Play3D(walkSound, SoundCategory.Footstep);
    }

    // 투척 효과음은 해당 패턴 SO의 actionSound에서 지정한다.
    public void ThrowPoisonDagger()
    {
        if (poisonDaggerPrefab && daggerSpawnPoint)
            Instantiate(poisonDaggerPrefab, daggerSpawnPoint.position, daggerSpawnPoint.rotation);
    }
}
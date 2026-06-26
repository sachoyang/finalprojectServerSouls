using UnityEngine;
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

    private Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();

    [Header("근접 공격 판정 (신형 Sweep)")]
    // BossHurtbox -> BossMeleeAttack으로 변경 및 이름 수정
    public BossMeleeAttack leftDaggerAttack;
    public BossMeleeAttack rightDaggerAttack;

    [Header("사운드")]
    public AudioClip walkSound;
    public AudioClip attackSound;
    public AudioClip vanishSound; // 은신 소리

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
    // ==========================================
    public void EnableStealth()
    {
        if (smokeBombEffect != null) smokeBombEffect.Play();
        if (vanishSound != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX_3D(vanishSound, transform.position, SoundCategory.BossGimmick);
        }

        foreach (var r in bossRenderers)
        {
            if (r == null) continue;

            Material[] stealthMats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < stealthMats.Length; i++)
            {
                stealthMats[i] = stealthMaterial;
            }
            r.sharedMaterials = stealthMats;
        }
    }

    public void DisableStealth()
    {
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
    public void DoLocomotion() => anim.CrossFade("Locomotion", 0.1f);

    public void PlayWakeUp(int wakeUpHash)
    {
        PlayAction(wakeUpHash);
    }

    public void PlayPhaseTransition(int wakeUpHash)
    {
        PlayAction(wakeUpHash);
        if (smokeBombEffect != null) smokeBombEffect.Play();
    }

    public void PlayGroggy(float speedMultiplier, float groggyDuration)
    {
        SetAnimSpeed(speedMultiplier);
        anim.CrossFade("gethit", 0.1f);
    }
    public void PlayDie()
    {
        anim.CrossFade("death1", 0.1f);
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

    public void ThrowPoisonDagger()
    {
        if (poisonDaggerPrefab && daggerSpawnPoint)
            Instantiate(poisonDaggerPrefab, daggerSpawnPoint.position, daggerSpawnPoint.rotation);
    }
}
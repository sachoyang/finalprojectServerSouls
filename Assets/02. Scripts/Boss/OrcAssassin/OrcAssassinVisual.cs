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

    // 원래 옷(머티리얼)을 기억해둘 백업 사전
    private Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();

    [Header("히트박스 연결")]
    public BossHitbox leftDaggerHitbox;
    public BossHitbox rightDaggerHitbox;

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
    // 은신 켜기 / 끄기 함수 (애니메이션 이벤트나 Core에서 호출)
    // ==========================================
    public void EnableStealth()
    {
        // 펑! 하는 연막탄과 쉭~ 하는 소리 재생
        if (smokeBombEffect != null) smokeBombEffect.Play();
        if (vanishSound != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX_3D(vanishSound, transform.position, SoundCategory.BossGimmick);
        }

        // 보스의 모든 부위를 투명화 머티리얼로 강제 스왑
        foreach (var r in bossRenderers)
        {
            if (r == null) continue;

            Material[] stealthMats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < stealthMats.Length; i++)
            {
                stealthMats[i] = stealthMaterial; // 슬롯 개수만큼 투명 머티리얼 채워넣기
            }
            r.sharedMaterials = stealthMats;
        }
    }

    public void DisableStealth()
    {
        foreach (var r in bossRenderers)
        {
            if (r == null) continue;

            // 기억해뒀던 원래 머티리얼을 꺼내서 다시 입혀줍니다.
            if (_originalMaterials.TryGetValue(r, out Material[] origMats))
            {
                r.sharedMaterials = origMats;
            }
        }
        Debug.Log("[Visual] 오크 어쌔신 투명화 해제! 원래 모습으로 돌아옵니다.");
    }

    // ==========================================
    // [IBossVisual 구현부] 부모(Core)가 호출해 줄 함수들
    // ==========================================
    public void PlayAction(int stateHash, float crossFadeTime = 0.1f) => anim.CrossFade(stateHash, crossFadeTime, 0, 0f);

    public void SetDirection(float dirX, float dirY)
    {
        // 전달받은 방향(X, Y)의 크기를 구합니다. 
        // 가만히 있으면 0, 어느 방향이든 움직이려 하면 1이 됩니다.
        Vector2 dir = new Vector2(dirX, dirY);
        float targetSpeed = dir.magnitude; 

        // 기존에 쓰시던 MoveSpeed 파라미터를 그대로 사용합니다!
        float currentSpeed = anim.GetFloat("MoveSpeed");
        anim.SetFloat("MoveSpeed", Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 5f));
    }

    // 타겟 주시 (LookAt)
    public void SetLookAtTarget(Vector3 targetPos)
    {
        _ikLookAtPosition = targetPos;
        _ikWeight = Mathf.Lerp(_ikWeight, 1f, Time.deltaTime * 2f); 
    }

    // 타겟 주시 해제
    public void ResetLookAt()
    {
        _ikWeight = Mathf.Lerp(_ikWeight, 0f, Time.deltaTime * 3f);
    }

    // 애니메이터 IK (고개 돌리기)
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
        // 오크 어쌔신만의 등장 사운드 재생
    }

    public void PlayPhaseTransition(int wakeUpHash)
    {
        PlayAction(wakeUpHash);
        if (smokeBombEffect != null) smokeBombEffect.Play();
        // 연막탄 터트리면서 2페이즈 진입 연출
    }

    public void PlayGroggy(float speedMultiplier)
    {
        SetAnimSpeed(speedMultiplier);
        anim.CrossFade("gethit", 0.1f);
    }
    public void PlayDie()
    {
        anim.CrossFade("death1", 0.1f);
    }

    // 루트모션 위임
    public void SetRootMotionCapture(bool enabled)
    {
        if (rootMotionCapture != null) rootMotionCapture.SetCapture(enabled);
    }

    public Vector3 ConsumeRootMotionDelta()
    {
        return rootMotionCapture != null ? rootMotionCapture.ConsumeDelta() : Vector3.zero;
    }

    // ==========================================
    // [애니메이션 이벤트용 함수] 애니메이션 클립에서 호출
    // ==========================================
    public void EnableRightDagger()
    {
        rightDaggerHitbox.StartAttack();
        leftDaggerHitbox.StartAttack();
    }
    public void DisableRightDagger()
    {
        rightDaggerHitbox.StopAttack();
        leftDaggerHitbox.StopAttack();
    }

    public void ThrowPoisonDagger()
    {
        // 단검 투척 애니메이션 타이밍에 맞춰 단검 프리팹 생성
        if (poisonDaggerPrefab && daggerSpawnPoint)
            Instantiate(poisonDaggerPrefab, daggerSpawnPoint.position, daggerSpawnPoint.rotation);
    }
}
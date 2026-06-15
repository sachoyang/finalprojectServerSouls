using UnityEngine;

public class OrcAssassinVisual : MonoBehaviour, IBossVisual
{
    public Animator anim;

    [Header("이펙트 & 투사체")]
    public GameObject poisonDaggerPrefab;
    public Transform daggerSpawnPoint;
    public ParticleSystem smokeBombEffect; // 2페이즈 은신/이동용

    [Header("히트박스 연결")]
    public BossHitbox leftDaggerHitbox;
    public BossHitbox rightDaggerHitbox;

    [Header("사운드")]
    public AudioClip walkSound;
    public AudioClip attackSound;
    public AudioClip vanishSound; // 은신 소리

    // ==========================================
    // [IBossVisual 구현부] 부모(Core)가 호출해 줄 함수들
    // ==========================================
    public void PlayAction(int stateHash, float crossFadeTime = 0.1f) => anim.CrossFade(stateHash, crossFadeTime, 0, 0f);
    public void SetSpeed(float speedValue) => anim.SetFloat("MoveSpeed", speedValue);
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
    
    public void PlayGroggy(float speedMultiplier) { /* 그로기 연출 */ }
    public void PlayDie() { /* 사망 연출 */ }

    // ==========================================
    // [애니메이션 이벤트용 함수] 애니메이션 클립에서 호출
    // ==========================================
    public void EnableRightDagger() { rightDaggerHitbox.StartAttack(); }
    public void DisableRightDagger() { rightDaggerHitbox.StopAttack(); }
    
    public void ThrowPoisonDagger()
    {
        // 단검 투척 애니메이션 타이밍에 맞춰 단검 프리팹 생성
        if (poisonDaggerPrefab && daggerSpawnPoint)
            Instantiate(poisonDaggerPrefab, daggerSpawnPoint.position, daggerSpawnPoint.rotation);
    }
}
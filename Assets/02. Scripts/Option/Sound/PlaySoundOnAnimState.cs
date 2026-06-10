using UnityEngine;

public class PlaySoundOnAnimState : StateMachineBehaviour
{
    [Header("재생할 사운드")]
    public AudioClip soundToPlay;
    public SoundCategory category = SoundCategory.CombatHit; // 기본값
    
    [Range(0f, 2f)]
    public float volume = 1f;

    [Header("재생 지연 시간 (초)")]
    [Tooltip("애니메이션 시작 후 몇 초 뒤에 소리를 낼지 설정합니다.")]
    public float delay = 0f; // 🔥 딜레이 변수 추가!

    // 🔥 애니메이션 상태(State)로 진입하는 딱 그 1프레임에 호출됩니다!
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (soundToPlay != null && SoundManager.Instance != null)
        {
            // animator.transform.position을 가져오면 그 애니메이션을 재생 중인 보스의 위치가 나옵니다.
            SoundManager.Instance.PlaySFX_3D(soundToPlay, animator.transform.position, category, volume, delay);
        }
    }

    // (참고) 만약 애니메이션이 "끝날 때" 뭔가 하고 싶다면 아래 함수를 쓰면 됩니다.
    // override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) { }
}
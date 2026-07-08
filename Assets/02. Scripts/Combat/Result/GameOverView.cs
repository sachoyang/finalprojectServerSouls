using Fusion;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Text messageText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Optional Button")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Text continueButtonText;

    [Header("Fade")]
    [SerializeField] private float fadeDuration = 2f;
    [SerializeField, Range(0f, 1f)] private float startAlpha = 0f;
    [SerializeField, Range(0f, 1f)] private float targetAlpha = 1f;

    [Header("Input Lock")]
    [SerializeField] private float inputLockDuration = 5f;
    [SerializeField] private bool allowAnyKeyAfterUnlock = true;

    [Header("Message")]
    [SerializeField] private string defeatMessage = "DEFEATED";
    [SerializeField] private string retreatMessage = "RETREAT";
    [SerializeField] private string continueText = "Continue";
    [SerializeField] private string waitingForHostText = "Waiting for Host...";

    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "scLobbyMain";

    private bool isPlaying;
    private bool canLeave;
    private bool isLoadingScene;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnClickContinue);
            continueButton.onClick.AddListener(OnClickContinue);
        }

        if (continueButtonText != null)
            continueButtonText.text = continueText;

        SetAlpha(startAlpha);

        // 🔥 [수정] 예전엔 gameObject.SetActive(false)로 숨겼는데, 프리팹에서 이 오브젝트가
        //    비활성으로 시작하면(항복 UI 커밋이 GameOver의 m_IsActive를 0으로 바꿈) Play()의
        //    SetActive(true) 순간 Awake가 '처음' 실행되며 여기서 다시 스스로를 꺼버려
        //    바로 뒤 StartCoroutine이 "game object is inactive"로 실패했다.
        //    이제 자기 자신을 끄지 않고 CanvasGroup(alpha 0 · 입력 차단)으로만 숨긴다.
        HidePanel();
    }

    private void Update()
    {
        if (!canLeave || isLoadingScene)
            return;

        if (allowAnyKeyAfterUnlock && Input.anyKeyDown)
            LoadLobbyScene();
    }

    public void PlayDefeat()
    {
        Play(defeatMessage);
    }

    public void PlayRetreat()
    {
        Play(retreatMessage);
    }

    public void Play(string message)
    {
        if (isPlaying)
            return;

        // 프리팹에서 비활성으로 시작했더라도 여기서 활성화한다.
        // (Awake는 더 이상 자신을 끄지 않으므로 이 SetActive(true)가 그대로 유지됨)
        gameObject.SetActive(true);
        ShowPanel();

        // 🔥 게임오버 화면부터 마우스 커서를 되살린다 (Continue 버튼 클릭용).
        //    커서 잠금은 ThirdPersonCameraController가 매 프레임 다시 걸기 때문에,
        //    보상 화면(RewardSelectView)과 같은 공식 훅인 ForceCursorVisible로 눌러둔다.
        ThirdPersonCameraController.ForceCursorVisible = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (messageText != null)
            messageText.text = message;

        StartCoroutine(PlayRoutine());
    }

    public void OnClickContinue()
    {
        if (!canLeave || isLoadingScene)
            return;

        LoadLobbyScene();
    }

    private IEnumerator PlayRoutine()
    {
        isPlaying = true;
        canLeave = false;

        SetInputEnabled(false);
        SetAlpha(startAlpha);

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = fadeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeDuration);
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetAlpha(targetAlpha);

        if (inputLockDuration > 0f)
            yield return new WaitForSeconds(inputLockDuration);

        canLeave = true;
        SetInputEnabled(true);
    }

    private void SetAlpha(float alpha)
    {
        if (backgroundImage == null)
            return;

        Color color = backgroundImage.color;
        color.a = alpha;
        backgroundImage.color = color;
    }

    private void SetInputEnabled(bool enabled)
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = enabled;
        }

        if (continueButton != null)
            continueButton.interactable = enabled;
    }

    // 게임오버 화면 표시(오브젝트는 항상 활성, 표시 여부는 CanvasGroup으로만 제어).
    private void ShowPanel()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
    }

    // 대기 중(전투 진행 중)에는 완전히 숨기고 입력도 차단해 HUD 클릭을 막지 않는다.
    private void HidePanel()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    // ==========================================
    // 🔥 [세션 유지 복귀] 로비로 돌아가되 세션(NetworkRunner)은 절대 끊지 않는다.
    //  - 호스트: runner.LoadScene(Single) → 게스트 전원이 Fusion 씬 동기화로 자동으로 따라온다.
    //  - 게스트: 직접 LoadScene/Shutdown 금지! (기존 버그: 각 클라가 제멋대로 셧다운+로컬 로드
    //            → 세션 파괴 + 클라별 씬 불일치로 네트워크 오브젝트 참조가 줄줄이 깨졌음)
    //            버튼을 눌러도 "호스트 대기" 표시만 하고 기다린다.
    //  - 런 상태 청소(레벨/이펙트/플레이어 스냅샷)는 GameProgressionManager.ResetRun()이
    //    로비 씬 로드 시점에 모든 클라이언트에서 각자 수행한다. (여기서 할 일 아님)
    // ==========================================
    private void LoadLobbyScene()
    {
        if (isLoadingScene)
            return;

        NetworkRunner runner = GetRunner();

        if (runner != null && runner.IsRunning)
        {
            if (!runner.IsServer)
            {
                ShowWaitingForHost();
                return;
            }

            isLoadingScene = true;
            runner.LoadScene(lobbySceneName, LoadSceneMode.Single);
            return;
        }

        // 러너가 없거나 이미 종료된 경우(디버그 단독 실행 등)만 로컬 로드 폴백
        isLoadingScene = true;
        SceneManager.LoadScene(lobbySceneName);
    }

    // 게스트가 먼저 Continue를 눌렀을 때: 호스트가 씬을 넘겨줄 때까지 대기 중임을 표시
    private void ShowWaitingForHost()
    {
        if (continueButtonText != null)
            continueButtonText.text = waitingForHostText;

        if (continueButton != null)
            continueButton.interactable = false;
    }

    private NetworkRunner GetRunner()
    {
        if (NetworkManager.HasInstance && NetworkManager.Instance.Runner != null)
            return NetworkManager.Instance.Runner;

        return FindObjectOfType<NetworkRunner>();
    }
}
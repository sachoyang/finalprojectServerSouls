using UnityEngine;
using UnityEngine.SceneManagement;

// 인트로 → 로그인 → 타이틀 씬까지 같은 자리에 계속 떠 있는 게임 이름(SOUL RUSH) UI다.
// 씬마다 프리팹을 배치하지 않고, 게임 시작 시 Resources에서 한 번만 생성해 DontDestroyOnLoad로 유지한다.
// 표시 대상이 아닌 씬(로비, 전투 등)으로 넘어가면 스스로 파괴된다.
[DisallowMultipleComponent]
public class GameTitleLogo : MonoBehaviour
{
    // Assets/Resources/UI/GameTitleLogo.prefab 을 가리킨다.
    public const string ResourcePath = "UI/GameTitleLogo";

    [Header("Fade")]
    // 씬 페이드와 같이 밝아지고 어두워지도록 알파를 직접 조절한다.
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("타이틀을 띄울 씬 이름")]
    // 여기 적힌 씬에서만 로고가 살아있다. 씬 이름을 바꾸면 이 배열도 같이 수정해야 한다.
    [SerializeField]
    private string[] visibleSceneNames =
    {
        "scIntro",
        "scLogin",
        "scTitle uicreate Main"
    };

    private static GameTitleLogo instance;

    // 현재 씬의 페이드 담당자다. 씬이 바뀌면 새 씬 것으로 다시 잡는다.
    private SceneFadeManager fadeManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void SpawnOnStartup()
    {
        // 어느 씬에서 플레이를 시작하든 동일하게 동작하도록 런타임에 한 번 생성한다.
        // 표시 대상 씬이 아니면 아래 Awake에서 곧바로 정리된다.
        if (instance != null)
        {
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"GameTitleLogo: Resources/{ResourcePath} 프리팹을 찾지 못했습니다.");
            return;
        }

        GameObject logoObject = Instantiate(prefab);
        logoObject.name = prefab.name;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        SceneManager.sceneLoaded += OnSceneLoaded;

        // 씬에 직접 배치해두고 그 씬부터 실행한 경우도 있어서 현재 씬을 한 번 검사한다.
        ApplyForScene(SceneManager.GetActiveScene().name);
        AcquireFadeManager();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 추가 로드(Additive)는 메인 씬 흐름이 아니므로 판단 기준에서 제외한다.
        if (mode == LoadSceneMode.Additive)
        {
            return;
        }

        ApplyForScene(scene.name);
        AcquireFadeManager();
    }

    private void AcquireFadeManager()
    {
        fadeManager = FindObjectOfType<SceneFadeManager>();

        if (canvasGroup == null)
            return;

        // 페이드가 있는 씬은 검은 화면에서 시작하므로 로고도 투명하게 두고 페이드인에 맞춰 같이 밝아진다.
        canvasGroup.alpha = fadeManager != null ? 0f : 1f;
    }

    private void LateUpdate()
    {
        if (canvasGroup == null)
            return;

        // 페이드 이미지가 가린 만큼 로고도 같이 어두워진다. 페이드가 없는 씬이면 그냥 보여준다.
        canvasGroup.alpha = fadeManager != null ? 1f - fadeManager.FadeAlpha : 1f;
    }

    private void ApplyForScene(string sceneName)
    {
        if (IsVisibleScene(sceneName))
        {
            return;
        }

        Destroy(gameObject);
    }

    private bool IsVisibleScene(string sceneName)
    {
        if (visibleSceneNames == null)
        {
            return false;
        }

        for (int i = 0; i < visibleSceneNames.Length; i++)
        {
            if (string.Equals(visibleSceneNames[i], sceneName))
            {
                return true;
            }
        }

        return false;
    }
}

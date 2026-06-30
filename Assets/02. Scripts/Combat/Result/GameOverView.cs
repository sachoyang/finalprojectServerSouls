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

    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "scLobbyMain";
    [SerializeField] private bool shutdownRunnerBeforeLoad = true;

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
        SetInputEnabled(false);

        gameObject.SetActive(false);
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

        gameObject.SetActive(true);

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

    private async void LoadLobbyScene()
    {
        if (isLoadingScene)
            return;

        isLoadingScene = true;

        NetworkRunner runner = GetRunner();

        if (runner != null && shutdownRunnerBeforeLoad)
        {
            await runner.Shutdown();
            SceneManager.LoadScene(lobbySceneName);
            return;
        }

        if (runner != null)
        {
            if (runner.IsServer)
                await runner.LoadScene(lobbySceneName, LoadSceneMode.Single);

            return;
        }

        SceneManager.LoadScene(lobbySceneName);
    }

    private NetworkRunner GetRunner()
    {
        if (NetworkManager.HasInstance && NetworkManager.Instance.Runner != null)
            return NetworkManager.Instance.Runner;

        return FindObjectOfType<NetworkRunner>();
    }
}
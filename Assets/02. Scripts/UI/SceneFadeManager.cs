using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneFadeManager : MonoBehaviour
{
    [SerializeField] private Image fadeImage;

    [Header("Fade Time")]
    [SerializeField] private float fadeInDuration = 5f;
    [SerializeField] private float fadeOutDuration = 1f;

    public bool IsFading { get; private set; }

    // 페이드 이미지 위에 그려지는 UI(타이틀 로고 등)가 같은 속도로 같이 사라지도록 현재 가림 정도를 알려준다.
    // 0이면 화면이 완전히 보이는 상태, 1이면 완전히 검은 상태다.
    public float FadeAlpha
    {
        get
        {
            if (fadeImage == null || !fadeImage.gameObject.activeInHierarchy)
                return 0f;

            return fadeImage.color.a;
        }
    }

    private void Start()
    {
        StartCoroutine(FadeIn());
    }

    public void ChangeScene(string sceneName)
    {
        if (IsFading)
            return;

        StartCoroutine(FadeOutAndLoad(sceneName));
    }

    private IEnumerator FadeIn()
    {
        yield return Fade(1f, 0f, fadeInDuration);
    }

    private IEnumerator FadeOutAndLoad(string sceneName)
    {
        yield return Fade(0f, 1f, fadeOutDuration);
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator Fade(float startAlpha, float endAlpha, float duration)
    {
        IsFading = true;

        fadeImage.gameObject.SetActive(true);
        fadeImage.raycastTarget = true;

        float time = 0f;

        Color color = fadeImage.color;
        color.a = startAlpha;
        fadeImage.color = color;

        while (time < duration)
        {
            time += Time.deltaTime;

            float normalizedTime = time / duration;
            float easedTime = Mathf.SmoothStep(0f, 1f, normalizedTime);
            float alpha = Mathf.Lerp(startAlpha, endAlpha, easedTime);

            color.a = alpha;
            fadeImage.color = color;

            yield return null;
        }

        color.a = endAlpha;
        fadeImage.color = color;

        if (endAlpha <= 0f)
        {
            fadeImage.raycastTarget = false;
            fadeImage.gameObject.SetActive(false);
        }
        else
        {
            fadeImage.raycastTarget = true;
        }

        IsFading = false;
    }
}

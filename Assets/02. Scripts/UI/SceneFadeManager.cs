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

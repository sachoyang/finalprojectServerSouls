using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class IntroSceneController : MonoBehaviour
{
    [SerializeField] private SceneFadeManager fadeManager;
    [SerializeField] private Text pressAnyKeyText;
    [SerializeField] private string nextSceneName = "scTitle";

    private bool canInput;
    private bool isChangingScene;

    private IEnumerator Start()
    {
        pressAnyKeyText.gameObject.SetActive(false);
        canInput = false;

        while (fadeManager.IsFading)
        {
            yield return null;
        }

        pressAnyKeyText.gameObject.SetActive(true);
        canInput = true;
    }

    private void Update()
    {
        if (!canInput || isChangingScene)
            return;

        if (Input.anyKeyDown)
        {
            isChangingScene = true;
            canInput = false;
            pressAnyKeyText.gameObject.SetActive(false);

            fadeManager.ChangeScene(nextSceneName);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour
{
    [SerializeField] private AudioClip clickSound;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (button == null)
            button = GetComponent<Button>();

        button.onClick.RemoveListener(PlayClickSound);
        button.onClick.AddListener(PlayClickSound);
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(PlayClickSound);
    }

    private void PlayClickSound()
    {
        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("[UIButtonSound] AudioManager가 없습니다.");
            return;
        }

        if (clickSound != null)
            AudioManager.Instance.PlaySFX(clickSound);
        else
            AudioManager.Instance.PlayButtonClick();
    }
}
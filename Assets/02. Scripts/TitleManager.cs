using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    public GameObject tutorialPopupPanel;
    public GameObject mainTitlePanel;
    public GameObject matchPanel; // 새로 만든 매칭 패널
    public GameObject CreatePanel;
    void Start()
    {
        CheckTutorialStatus();
    }

    private void CheckTutorialStatus()
    {
        int isTutorialDone = PlayerPrefs.GetInt("TutorialDone", 0);

        if (isTutorialDone == 0)
        {
            tutorialPopupPanel.SetActive(true);
            mainTitlePanel.SetActive(false);
        }
        else
        {
            tutorialPopupPanel.SetActive(false);
            mainTitlePanel.SetActive(true);
        }
    }

    // [인스펙터의 TitleManager 컴포넌트를 우클릭하면 나타나는 메뉴]
    [ContextMenu("Debug/Reset Tutorial Status")]
    public void ResetTutorialStatus()
    {
        PlayerPrefs.DeleteKey("TutorialDone"); // 데이터 삭제
        PlayerPrefs.Save();
        Debug.Log("Tutorial Status has been reset!");

        // 초기화 후 즉시 화면 반영 (에디터 재생 중일 때)
        CheckTutorialStatus();
    }

    public void OnClickTutorialYes()
    {
        PlayerPrefs.SetInt("TutorialDone", 1);
        PlayerPrefs.Save();
        SceneManager.LoadScene("TutorialScene");
    }

    public void OnClickTutorialNo()
    {
        PlayerPrefs.SetInt("TutorialDone", 1);
        PlayerPrefs.Save();
        tutorialPopupPanel.SetActive(false);
        mainTitlePanel.SetActive(true);
    }

    // 시작(Play) 버튼을 눌렀을 때 실행될 함수
    public void OnClickPlayButton()
    {
        mainTitlePanel.SetActive(true); // 메인 타이틀 화면 끄기
        matchPanel.SetActive(true);      // 매칭 패널 켜기
    }

    public void OnClickEnterButton()
    {
        matchPanel.SetActive(false); // 매칭 패널 끄기
        CreatePanel.SetActive(true);      // 생성 패널 켜기
    }

    public void OnClickCloseButton()
    {
        matchPanel.SetActive(false); // 매칭 패널 끄기
        CreatePanel.SetActive(false);      // 생성 패널 켜기
    }

}
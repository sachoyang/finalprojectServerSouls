using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    public GameObject tutorialPopupPanel;
    public GameObject mainTitlePanel;
    public GameObject matchPanel; // 새로 만든 매칭 패널

    [Header("캐릭터 선택 기능")]
    public GameObject[] titleModels; // 타이틀 씬에 배치된 3D 모델들 (전사, 마법사 등)
    private int _currentCharacterIndex = 0;

    void Start()
    {
        CheckTutorialStatus();
        // 이전에 저장된 캐릭터 번호를 불러옴 (없으면 0번)
        _currentCharacterIndex = PlayerPrefs.GetInt("SelectedCharacter", 0);
        UpdateTitleModelDisplay();
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
        mainTitlePanel.SetActive(false); // 메인 타이틀 화면 끄기
        matchPanel.SetActive(true);      // 매칭 패널 켜기
    }

    // UI에서 [<] [>] 같은 Select 버튼을 눌렀을 때 실행될 함수
    public void OnClickChangeCharacter(int direction)
    {
        _currentCharacterIndex += direction;
        
        // 인덱스가 범위를 벗어나지 않게 순환
        if (_currentCharacterIndex < 0) _currentCharacterIndex = titleModels.Length - 1;
        if (_currentCharacterIndex >= titleModels.Length) _currentCharacterIndex = 0;

        // 선택한 번호를 로컬(컴퓨터)에 저장! (나중에 대기실에서 꺼내 씁니다)
        PlayerPrefs.SetInt("SelectedCharacter", _currentCharacterIndex);
        UpdateTitleModelDisplay();
    }

    private void UpdateTitleModelDisplay()
    {
        // 모든 모델을 끄고 선택된 것만 켭니다.
        for (int i = 0; i < titleModels.Length; i++)
        {
            titleModels[i].SetActive(i == _currentCharacterIndex);
        }
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ==========================================
// 🏁 EndingSceneController — 게임 클리어(scEnding) 화면 + 랭킹 표
//  흐름:
//   1) "GAME CLEAR" 배너와 이번 판 소요 시간(GameProgressionManager 기준)을 보여준다.
//   2) 방장(HasStateAuthority/솔로)이고 로그인 상태면 이번 기록을 서버에 등록한다.
//   3) 서버에서 상위 랭킹을 받아와 표(순위 / 이름 / 소요 시간 / 딜량)로 그린다.
//   4) [타이틀로] 버튼으로 세션을 끊고 타이틀 씬으로 복귀.
//
//  UI는 코드로 자동 생성한다(별도 프리팹/캔버스 배선 불필요). 디자인을 바꾸고 싶으면
//  이 스크립트의 Build* 메서드를 수정하거나, 나중에 손으로 만든 캔버스로 교체하면 된다.
//
//  ⚠️ "지금은 소요 시간만" 표시한다. 딜량/파티원 이름 컬럼은 이미 자리를 잡아두었고,
//     RankingEntry.total_damage / players_json 값이 채워지면 자동으로 표시된다.
// ==========================================
public class EndingSceneController : MonoBehaviour
{
    [Header("씬 이름")]
    [SerializeField] private string titleSceneName = "scTitle uicreate Main";

    [Header("랭킹 설정")]
    [Tooltip("표에 보여줄 상위 기록 개수")]
    [SerializeField] private int rankingLimit = 10;

    [Header("색상 테마")]
    [SerializeField] private Color backgroundColor = new Color(0.03f, 0.03f, 0.05f, 1f);
    [SerializeField] private Color clearTitleColor = new Color(1f, 0.85f, 0.35f, 1f);
    [SerializeField] private Color panelColor = new Color(0.10f, 0.10f, 0.14f, 0.92f);
    [SerializeField] private Color headerColor = new Color(0.8f, 0.8f, 0.9f, 1f);
    [SerializeField] private Color rowColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    [SerializeField] private Color myRowColor = new Color(1f, 0.85f, 0.35f, 1f);

    private Font _font;
    private Transform _rowsParent;   // 랭킹 줄들이 들어갈 컨테이너
    private Text _statusText;        // "불러오는 중..." 같은 안내 문구
    private Text _myRecordText;      // 이번 판 내 기록

    // 이번 판이 몇 등인지(서버가 알려주면 하이라이트에 사용)
    private int _myRank = -1;

    // 컬럼 폭(픽셀) — 헤더/각 줄이 같은 폭을 써야 정렬이 맞는다.
    private static readonly float[] ColumnWidths = { 90f, 320f, 220f, 180f };
    private static readonly string[] ColumnHeaders = { "순위", "이름", "소요 시간", "딜량" };

    private void Start()
    {
        _font = ResolveFont();
        BuildUI();
        ShowMyRecord();
        BeginRankingFlow();
    }

    // ==========================================
    // 랭킹 등록 → 조회 흐름
    // ==========================================
    private void BeginRankingFlow()
    {
        SetStatus("랭킹을 불러오는 중...");

        if (ShouldSubmitRecord(out RunRecordPayload payload))
        {
            RankingManager.Instance.SubmitRun(payload, (ok, res) =>
            {
                if (ok && res != null)
                {
                    _myRank = res.rank;
                }
                // 등록 성공/실패와 무관하게 목록은 항상 불러온다.
                RefreshRankingList();
            });
        }
        else
        {
            RefreshRankingList();
        }
    }

    private void RefreshRankingList()
    {
        RankingManager.Instance.FetchRankings(rankingLimit, (ok, entries) =>
        {
            if (!ok || entries == null)
            {
                SetStatus("랭킹을 불러오지 못했습니다. (서버 미연결/미구현)");
                return;
            }

            PopulateTable(entries);
            SetStatus(entries.Count > 0 ? "" : "아직 등록된 기록이 없습니다.");
        });
    }

    // 이 클라이언트가 기록을 등록해야 하는지 판단한다.
    //  - 방장(또는 솔로/네트워크 없음)만 등록 → 파티에서 중복 등록 방지
    //  - 로그인한 계정만 등록 → 게스트 모드는 랭킹 오염 방지(조회는 가능)
    private bool ShouldSubmitRecord(out RunRecordPayload payload)
    {
        payload = null;

        var runner = NetworkManager.HasInstance ? NetworkManager.Instance.Runner : null;
        bool isHost = runner == null || runner.IsServer; // 러너가 없으면 솔로/디버그 → 등록 허용
        if (!isHost)
        {
            return false;
        }

        if (!BackendManager.HasInstance || string.IsNullOrEmpty(BackendManager.Instance.CurrentLoginID))
        {
            // 로그인 안 된 상태(게스트)면 등록하지 않는다.
            return false;
        }

        BackendManager backend = BackendManager.Instance;
        GameProgressionManager gpm = GameProgressionManager.Instance;

        payload = new RunRecordPayload
        {
            nickname = string.IsNullOrEmpty(backend.CurrentNickname) ? backend.CurrentLoginID : backend.CurrentNickname,
            clear_time_seconds = GetRunSeconds(),
            cleared_level = gpm != null ? gpm.maxLevel : 0,
            // ▼ 확장 필드: 딜량/파티 집계가 붙기 전까지는 기본값으로 보낸다 ▼
            total_damage = 0,
            party_size = runner != null ? CountActivePlayers(runner) : 1,
            players_json = ""
        };
        return true;
    }

    private static int CountActivePlayers(Fusion.NetworkRunner runner)
    {
        int count = 0;
        foreach (var _ in runner.ActivePlayers) count++;
        return Mathf.Max(1, count);
    }

    private int GetRunSeconds()
    {
        GameProgressionManager gpm = GameProgressionManager.Instance;
        return gpm != null ? Mathf.FloorToInt(gpm.RunCombatSeconds) : 0;
    }

    private void ShowMyRecord()
    {
        GameProgressionManager gpm = GameProgressionManager.Instance;
        string timeText = gpm != null ? gpm.GetRunCombatTimeText() : "--:--";
        if (_myRecordText != null)
        {
            _myRecordText.text = $"이번 판 소요 시간   <b>{timeText}</b>";
        }
    }

    // ==========================================
    // 표 채우기
    // ==========================================
    private void PopulateTable(List<RankingEntry> entries)
    {
        // 기존 줄 제거
        for (int i = _rowsParent.childCount - 1; i >= 0; i--)
        {
            Destroy(_rowsParent.GetChild(i).gameObject);
        }

        string myNickname = BackendManager.HasInstance ? BackendManager.Instance.CurrentNickname : null;

        foreach (RankingEntry entry in entries)
        {
            if (entry == null) continue;

            bool isMine = _myRank > 0
                ? entry.rank == _myRank
                : (!string.IsNullOrEmpty(myNickname) && entry.nickname == myNickname);

            string[] cells =
            {
                entry.rank > 0 ? entry.rank.ToString() : "-",
                string.IsNullOrEmpty(entry.nickname) ? "-" : entry.nickname,
                FormatTime(entry.clear_time_seconds),
                entry.total_damage > 0 ? entry.total_damage.ToString("N0") : "-", // 딜량 붙으면 자동 표시
            };

            BuildRow(_rowsParent, cells, isMine ? myRowColor : rowColor, false);
        }
    }

    // ==========================================
    // UI 자동 생성
    // ==========================================
    private void BuildUI()
    {
        // --- Canvas ---
        GameObject canvasObj = new GameObject("EndingCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // EventSystem (버튼 클릭 처리에 필요) — 없으면 만든다.
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        // --- 배경 ---
        Image bg = CreateChildImage(canvasObj.transform, "Background", backgroundColor);
        StretchFull(bg.rectTransform);

        // --- "GAME CLEAR" 배너 ---
        Text title = CreateText(canvasObj.transform, "ClearTitle", "GAME CLEAR", 96, clearTitleColor, FontStyle.Bold, TextAnchor.MiddleCenter);
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0, -60);
        titleRt.sizeDelta = new Vector2(1200, 140);

        // --- 이번 판 내 기록 ---
        _myRecordText = CreateText(canvasObj.transform, "MyRecord", "", 44, clearTitleColor, FontStyle.Normal, TextAnchor.MiddleCenter);
        _myRecordText.supportRichText = true;
        RectTransform recRt = _myRecordText.rectTransform;
        recRt.anchorMin = new Vector2(0.5f, 1f);
        recRt.anchorMax = new Vector2(0.5f, 1f);
        recRt.pivot = new Vector2(0.5f, 1f);
        recRt.anchoredPosition = new Vector2(0, -210);
        recRt.sizeDelta = new Vector2(1200, 60);

        // --- 랭킹 패널 ---
        Image panel = CreateChildImage(canvasObj.transform, "RankingPanel", panelColor);
        RectTransform panelRt = panel.rectTransform;
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = new Vector2(0, -60);
        panelRt.sizeDelta = new Vector2(900, 620);

        // 패널 세로 배치(헤더 + 줄 목록)
        VerticalLayoutGroup panelLayout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(20, 20, 20, 20);
        panelLayout.spacing = 6;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = false;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        // "RANKING" 소제목
        Text rankTitle = CreateText(panel.transform, "PanelTitle", "RANKING", 40, headerColor, FontStyle.Bold, TextAnchor.MiddleCenter);
        AddFixedHeight(rankTitle.gameObject, 56);

        // 헤더 줄
        BuildRow(panel.transform, ColumnHeaders, headerColor, true);

        // 줄 목록 컨테이너
        GameObject rows = new GameObject("Rows", typeof(RectTransform));
        rows.transform.SetParent(panel.transform, false);
        VerticalLayoutGroup rowsLayout = rows.AddComponent<VerticalLayoutGroup>();
        rowsLayout.spacing = 4;
        rowsLayout.childControlWidth = true;
        rowsLayout.childControlHeight = false;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;
        LayoutElement rowsLe = rows.AddComponent<LayoutElement>();
        rowsLe.flexibleHeight = 1;
        _rowsParent = rows.transform;

        // 상태 문구
        _statusText = CreateText(panel.transform, "Status", "", 26, headerColor, FontStyle.Italic, TextAnchor.MiddleCenter);
        AddFixedHeight(_statusText.gameObject, 40);

        // --- [타이틀로] 버튼 ---
        BuildReturnButton(canvasObj.transform);
    }

    private void BuildReturnButton(Transform parent)
    {
        Image btnImg = CreateChildImage(parent, "ReturnButton", new Color(0.2f, 0.2f, 0.26f, 1f));
        RectTransform rt = btnImg.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0, 50);
        rt.sizeDelta = new Vector2(340, 80);

        Button btn = btnImg.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(OnClickReturnToTitle);

        Text label = CreateText(btnImg.transform, "Label", "타이틀로", 34, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        StretchFull(label.rectTransform);
    }

    private void OnClickReturnToTitle()
    {
        if (NetworkManager.HasInstance)
        {
            NetworkManager.Instance.ShutdownAndLoad(titleSceneName);
        }
        else
        {
            SceneManager.LoadScene(titleSceneName);
        }
    }

    // 한 줄(행)을 만들어 parent 밑에 붙인다. isHeader면 살짝 다른 스타일.
    private void BuildRow(Transform parent, IReadOnlyList<string> cells, Color textColor, bool isHeader)
    {
        GameObject row = new GameObject(isHeader ? "HeaderRow" : "Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);

        HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        AddFixedHeight(row, isHeader ? 48 : 44);

        for (int i = 0; i < cells.Count; i++)
        {
            TextAnchor anchor = i == 1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter; // 이름만 좌측 정렬
            Text cell = CreateText(row.transform, "Cell" + i, cells[i],
                isHeader ? 28 : 30, textColor,
                isHeader ? FontStyle.Bold : FontStyle.Normal, anchor);
            LayoutElement le = cell.gameObject.AddComponent<LayoutElement>();
            float w = i < ColumnWidths.Length ? ColumnWidths[i] : 150f;
            le.preferredWidth = w;
            le.flexibleWidth = 0;
        }
    }

    // ==========================================
    // 유틸
    // ==========================================
    private void SetStatus(string message)
    {
        if (_statusText != null) _statusText.text = message;
    }

    // 초 → mm:ss 또는 h:mm:ss (GameProgressionManager.GetRunCombatTimeText 와 동일 규칙)
    private static string FormatTime(int totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;
        return hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{minutes:00}:{seconds:00}";
    }

    private Font ResolveFont()
    {
        // Unity 2022 내장 폰트. 버전에 따라 이름이 달라 순차 시도한다.
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    private Text CreateText(Transform parent, string name, string content, int fontSize, Color color, FontStyle style, TextAnchor anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.font = _font;
        t.text = content;
        t.fontSize = fontSize;
        t.color = color;
        t.fontStyle = style;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private Image CreateChildImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void AddFixedHeight(GameObject go, float height)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
    }
}

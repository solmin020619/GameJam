using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// AScene_FightUI 를 Additive 로 KScene 위에 로드 + 양측 챔프 카드에 데이터 자동 표시.
/// 카드 안 자식 이름:
///   HP / Attack / AttackSpeed / Armor / Intersection(사거리) / Speed(이속)
///   + 카드 자체에 portrait Image, name TMP 등 (있다면)
/// </summary>
public class FightUIController : MonoBehaviour
{
    public string uiSceneName = "AScene_FightUI";
    public Sprite swordIcon;   // (선택) 가운데 검 아이콘

    [Header("Camera Zoom (맵+캐릭터를 화면 안에 작게 보이게)")]
    [Tooltip("1.0 = 원본. 1.4~1.6 = TFM 스타일 (좌우 UI 카드와 안 겹침)")]
    [Range(0.8f, 2.5f)] public float cameraZoomMultiplier = 1.5f;

    [Header("Score Text (상단 점수 폰트 크기)")]
    public int scoreFontSize = 64;

    [Header("Kill Log (타이머 아래로 내리기)")]
    [Tooltip("KillLogRoot Y 위치. 음수 = 위에서부터 내려간 거리. -200 정도가 타이머 아래")]
    public float killLogYOffset = -220f;

    Transform[] _blueCards = new Transform[3];
    Transform[] _redCards = new Transform[3];
    TextMeshProUGUI _team0ScoreText;
    TextMeshProUGUI _team1ScoreText;
    Text _team0ScoreLegacy;        // ashyun1 씬의 점수는 Legacy UI.Text 인 경우 대비
    Text _team1ScoreLegacy;
    TextMeshProUGUI _timerText;
    bool _wired = false;
    float _originalOrthoSize = -1f;

    void Start()
    {
        ApplyCameraZoom();

        var scene = SceneManager.GetSceneByName(uiSceneName);
        if (!scene.isLoaded)
        {
            var op = SceneManager.LoadSceneAsync(uiSceneName, LoadSceneMode.Additive);
            op.completed += _ => { CleanupAndWire(); };
        }
        else
        {
            CleanupAndWire();
        }
    }

    void ApplyCameraZoom()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;
        if (_originalOrthoSize < 0f) _originalOrthoSize = cam.orthographicSize;
        cam.orthographicSize = _originalOrthoSize * cameraZoomMultiplier;
        Debug.Log($"[FightUI] 카메라 줌 — {_originalOrthoSize:0.00} → {cam.orthographicSize:0.00} (x{cameraZoomMultiplier})");
    }

    void ConfigureScoreLegacy(Text t)
    {
        t.text = "0";
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        // BestFit 켜서 컨테이너 안에서 최대로 크게
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 20;
        t.resizeTextMaxSize = scoreFontSize;
    }

    void ConfigureScoreTMP(TextMeshProUGUI t)
    {
        t.text = "0";
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = true;
        t.fontSizeMin = 20;
        t.fontSizeMax = scoreFontSize;
    }

    static string StripIndex(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        int sp = s.LastIndexOf(' ');
        if (sp <= 0) return s;
        // 마지막 공백 뒤가 숫자면 떼기
        string tail = s.Substring(sp + 1);
        return int.TryParse(tail, out _) ? s.Substring(0, sp) : s;
    }

    // ============ Role Icon 매핑 (Resources/RoleIcons/*.png) ============
    static readonly System.Collections.Generic.Dictionary<ChampionRole, Sprite> _roleIconCache = new();
    static bool _roleIconLoaded = false;

    static void LoadRoleIconsOnce()
    {
        if (_roleIconLoaded) return;
        _roleIconLoaded = true;
        void Bind(ChampionRole r, string fname)
        {
            var s = Resources.Load<Sprite>($"RoleIcons/{fname}");
            if (s != null) _roleIconCache[r] = s;
            else Debug.LogWarning($"[FightUI] RoleIcons/{fname} 로드 실패 — TFM > Setup Role Icons (Sprite Import) 메뉴 실행 필요");
        }
        Bind(ChampionRole.Tank,        "shield");
        Bind(ChampionRole.Marksman,    "bow");
        Bind(ChampionRole.Mage,        "staff");
        Bind(ChampionRole.Healer,      "cross");
        Bind(ChampionRole.Disruptor,   "hammer");
        Bind(ChampionRole.Skirmisher,  "axe");
        Bind(ChampionRole.Duelist,     "spear");
        Bind(ChampionRole.Assassin,    "dagger");
        // Fighter — 검 (기존 sword 그대로 유지: Icon_Weapon 의 원래 sprite 안 건드림)
    }

    static void ApplyRoleIcon(Transform card, ChampionRole role)
    {
        LoadRoleIconsOnce();
        if (!_roleIconCache.TryGetValue(role, out var sprite)) return; // Fighter 등 매핑 없음 → 원본 검 유지

        // 카드 안 'Icon_Weapon' 모두 찾아서 교체 (BlueCard 안 여러 군데에 있을 수도)
        foreach (var t in card.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Icon_Weapon")
            {
                var img = t.GetComponent<Image>();
                if (img != null) { img.sprite = sprite; img.color = Color.white; img.preserveAspect = true; }
            }
        }
    }

    // Name 텍스트 전용 — 좁은 컨테이너에서도 안 잘리게 overflow / autosize 적용
    static void SetNameText(Transform card, string childName, string value)
    {
        Transform child = null;
        foreach (var t in card.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName) { child = t; break; }
        }
        if (child == null) return;

        var tmp = child.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = value;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return;
        }
        var legacy = child.GetComponentInChildren<Text>(true);
        if (legacy != null)
        {
            legacy.text = value;
            legacy.horizontalOverflow = HorizontalWrapMode.Overflow;
            legacy.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }

    void CleanupAndWire()
    {
        var scene = SceneManager.GetSceneByName(uiSceneName);
        if (!scene.IsValid()) { Debug.LogWarning($"[FightUI] {uiSceneName} 로드 실패"); return; }

        // KScene 과 충돌하는 AScene_FightUI 안 컴포넌트들 비활성
        // (Destroy 보다 SetActive(false) 가 같은 프레임 안전)
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == null) continue;

            // Camera 중복 — Main Camera 충돌 / AudioListener 중복 방지
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                cam.gameObject.SetActive(false);

            // BattleManager 중복 — 챔프 2배 스폰 방지 (싱글톤 Awake 도 처리하지만 이중 안전)
            foreach (var bm in root.GetComponentsInChildren<BattleManager>(true))
            {
                Debug.Log($"[FightUI] AScene_FightUI 안 중복 BattleManager '{bm.gameObject.name}' 비활성");
                bm.gameObject.SetActive(false);
            }

            // EventSystem 중복 — "only one Event System" 경고 방지
            foreach (var es in root.GetComponentsInChildren<EventSystem>(true))
                es.gameObject.SetActive(false);

            // Light2D 중복 — "More than one global light" 경고 방지 (타입명 매칭)
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Name == "Light2D")
                    mb.gameObject.SetActive(false);
            }
        }

        // 1) KScene 쪽 옛 타이머/스코어 UI 강제 비활성 (AScene_FightUI 가 새로 표시할 거라)
        var myScene = gameObject.scene;
        foreach (var root in myScene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name;
                if (n == "TimerText" || n == "Team0Score" || n == "Team1Score")
                {
                    if (t.gameObject.scene == scene) continue;
                    t.gameObject.SetActive(false);
                }
            }
        }

        // 2) AScene_FightUI 안 ResultPanel 강제 비활성 — 화면 전체 덮는 흰색 반투명 = 시작 시 안개 효과
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "ResultPanel" && t.gameObject.activeSelf)
                {
                    Debug.Log("[FightUI] AScene_FightUI 안 ResultPanel 강제 비활성 (시작 시 안개 제거)");
                    t.gameObject.SetActive(false);
                }
            }
        }

        // 3) KScene 의 KillLogRoot 를 타이머 아래로 이동 (AScene_FightUI 타이머랑 안 겹치게)
        foreach (var root in myScene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "KillLogRoot")
                {
                    var rt = t as RectTransform;
                    if (rt != null)
                    {
                        var p = rt.anchoredPosition;
                        p.y = killLogYOffset;
                        rt.anchoredPosition = p;
                        Debug.Log($"[FightUI] KillLogRoot Y 위치 → {killLogYOffset}");
                    }
                }
            }
        }

        // Canvas 안 카드 / 텍스트 찾기
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<Canvas>() == null) continue;
            // 자식 전체 순회
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "BlueCard_1": _blueCards[0] = t; break;
                    case "BlueCard_2": _blueCards[1] = t; break;
                    case "BlueCard_3": _blueCards[2] = t; break;
                    case "RedCard_1":  _redCards[0]  = t; break;
                    case "RedCard_2":  _redCards[1]  = t; break;
                    case "RedCard_3":  _redCards[2]  = t; break;
                    case "TimerText":  _timerText    = t.GetComponent<TextMeshProUGUI>(); break;
                    case "Team0Score": _team0ScoreText = t.GetComponent<TextMeshProUGUI>(); break;
                    case "Team1Score": _team1ScoreText = t.GetComponent<TextMeshProUGUI>(); break;
                }
            }

            // Fallback — TMP / Legacy UI.Text 중 텍스트가 "New Text" 인 거 두 개 = 좌/우 점수
            if (_team0ScoreText == null && _team0ScoreLegacy == null)
            {
                var newLegacy = new List<Text>();
                foreach (var t in root.GetComponentsInChildren<Text>(true))
                {
                    if (t.text == "New Text") newLegacy.Add(t);
                }
                if (newLegacy.Count >= 2)
                {
                    newLegacy.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
                    _team0ScoreLegacy = newLegacy[0];
                    _team1ScoreLegacy = newLegacy[1];
                    // 시작 시 0 + BestFit 으로 컨테이너에 맞춰 자동 크게
                    ConfigureScoreLegacy(_team0ScoreLegacy);
                    ConfigureScoreLegacy(_team1ScoreLegacy);
                }
                else
                {
                    var newTmp = new List<TextMeshProUGUI>();
                    foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        if (t.text == "New Text") newTmp.Add(t);
                    }
                    if (newTmp.Count >= 2)
                    {
                        newTmp.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
                        _team0ScoreText = newTmp[0];
                        _team1ScoreText = newTmp[1];
                        ConfigureScoreTMP(_team0ScoreText);
                        ConfigureScoreTMP(_team1ScoreText);
                    }
                }
            }
        }

        // TimerText 와이어링 + KScene 옛 TimerText 비활성
        if (_timerText != null && BattleManager.Instance != null)
        {
            var oldTimer = BattleManager.Instance.TimerText;
            if (oldTimer != null && oldTimer != _timerText)
                oldTimer.gameObject.SetActive(false);
            BattleManager.Instance.TimerText = _timerText;
        }

        // 진단 — 카드 누락이면 로그
        int blueFound = 0, redFound = 0;
        for (int i = 0; i < 3; i++) { if (_blueCards[i] != null) blueFound++; if (_redCards[i] != null) redFound++; }
        Debug.Log($"[FightUI] 카드 매칭 — Blue {blueFound}/3, Red {redFound}/3, Timer:{(_timerText != null)}, Team0Score:{(_team0ScoreText != null)}, Team1Score:{(_team1ScoreText != null)}");
        if (blueFound < 3 || redFound < 3)
            Debug.LogWarning("[FightUI] AScene_FightUI 안 카드 자식 이름이 BlueCard_1~3 / RedCard_1~3 가 아닐 수 있음 — Hierarchy 확인 필요");

        // 카드 첫 번째 자식 구조 덤프 (Image 컴포넌트 이름들 → portrait 매핑 디버그용)
        if (_blueCards[0] != null)
        {
            var sb = new System.Text.StringBuilder("[FightUI] BlueCard_1 의 Image 자식들: ");
            foreach (var img in _blueCards[0].GetComponentsInChildren<Image>(true))
                sb.Append($"'{img.gameObject.name}'(spr:{(img.sprite != null ? img.sprite.name : "null")}) ");
            Debug.Log(sb.ToString());
        }

        // RedCard 의 자식을 통째로 BlueCard 자식 복제로 교체 + 색깔만 RGB 채널 swap (파랑→빨강)
        // → 카드 위치(화면 좌/우)만 Red 유지, 안 layout/sprite 다 Blue 와 동일
        if (_blueCards[0] != null)
        {
            for (int i = 0; i < 3; i++)
            {
                if (_redCards[i] != null) CloneBlueCardToRed(_blueCards[0], _redCards[i]);
            }
            Debug.Log("[FightUI] RedCard 의 자식을 BlueCard 복제 + RGB swap (파랑→빨강)");
        }

        _wired = true;
    }

    // RedCard 의 자식 구조를 BlueCard 복제로 교체하면서, sprite/color 는 path 기반으로 정확히 복원
    static void CloneBlueCardToRed(Transform blueCard, Transform redCard)
    {
        // 0) Red 의 원본 sprite/color 를 path(자식 경로) 별로 매핑
        // 동일 path 가 여러 개면 첫 번째 것만 (보통 unique)
        var pathToImageSprite = new Dictionary<string, Sprite>();
        var pathToImageColor  = new Dictionary<string, Color>();
        var pathToTextColor   = new Dictionary<string, Color>();
        var pathToTmpColor    = new Dictionary<string, Color>();

        foreach (var img in redCard.GetComponentsInChildren<Image>(true))
        {
            string p = GetPath(img.transform, redCard);
            if (!pathToImageSprite.ContainsKey(p))
            {
                pathToImageSprite[p] = img.sprite;
                pathToImageColor[p]  = img.color;
            }
        }
        foreach (var t in redCard.GetComponentsInChildren<Text>(true))
        {
            string p = GetPath(t.transform, redCard);
            if (!pathToTextColor.ContainsKey(p)) pathToTextColor[p] = t.color;
        }
        foreach (var t in redCard.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string p = GetPath(t.transform, redCard);
            if (!pathToTmpColor.ContainsKey(p)) pathToTmpColor[p] = t.color;
        }

        // 1) Red 의 기존 자식 즉시 제거
        var existing = new List<GameObject>();
        foreach (Transform c in redCard) existing.Add(c.gameObject);
        foreach (var go in existing)
            if (go != null) Object.DestroyImmediate(go);

        // 2) Blue 의 자식들을 Instantiate (sibling 순서 유지)
        for (int i = 0; i < blueCard.childCount; i++)
        {
            var src = blueCard.GetChild(i).gameObject;
            var clone = Object.Instantiate(src, redCard);
            clone.name = src.name;
        }

        // 3) 복제된 자식들에 Red 원본 sprite/color 를 path 매칭으로 복원
        // path 가 매핑에 없으면 (= Red 에 그 자식이 없었음) BlueCard sprite 그대로 둠
        int imgFixed = 0, textFixed = 0, tmpFixed = 0;
        foreach (var img in redCard.GetComponentsInChildren<Image>(true))
        {
            string p = GetPath(img.transform, redCard);
            if (pathToImageSprite.TryGetValue(p, out var sp) && sp != null) { img.sprite = sp; imgFixed++; }
            if (pathToImageColor.TryGetValue(p, out var c)) img.color = c;
        }
        foreach (var t in redCard.GetComponentsInChildren<Text>(true))
        {
            string p = GetPath(t.transform, redCard);
            if (pathToTextColor.TryGetValue(p, out var c)) { t.color = c; textFixed++; }
        }
        foreach (var t in redCard.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string p = GetPath(t.transform, redCard);
            if (pathToTmpColor.TryGetValue(p, out var c)) { t.color = c; tmpFixed++; }
        }

        Debug.Log($"[FightUI] '{redCard.name}' sprite/color 복원 — Image:{imgFixed}, Text:{textFixed}, TMP:{tmpFixed}");
    }

    static string GetPath(Transform t, Transform root)
    {
        var sb = new System.Text.StringBuilder();
        while (t != null && t != root)
        {
            if (sb.Length > 0) sb.Insert(0, '/');
            sb.Insert(0, t.name);
            t = t.parent;
        }
        return sb.ToString();
    }

    void Update()
    {
        if (!_wired) return;
        if (BattleManager.Instance == null) return;

        // 양 팀 카드 데이터 갱신
        var team0 = BattleManager.Instance.GetAllies(0);
        var team1 = BattleManager.Instance.GetAllies(1);
        for (int i = 0; i < 3; i++)
        {
            UpdateCard(_blueCards[i], team0 != null && i < team0.Count ? team0[i] : null);
            UpdateCard(_redCards[i],  team1 != null && i < team1.Count ? team1[i] : null);
        }

        // 스코어 표시 (TMP 또는 Legacy 둘 중 와이어링된 거)
        int k0 = BattleManager.Instance.Team0Kills;
        int k1 = BattleManager.Instance.Team1Kills;
        if (_team0ScoreText != null) _team0ScoreText.text = k0.ToString();
        if (_team1ScoreText != null) _team1ScoreText.text = k1.ToString();
        if (_team0ScoreLegacy != null) _team0ScoreLegacy.text = k0.ToString();
        if (_team1ScoreLegacy != null) _team1ScoreLegacy.text = k1.ToString();
    }

    /// <summary>한 카드에 해당 챔프 데이터 표시</summary>
    void UpdateCard(Transform card, ChampionUnit unit)
    {
        if (card == null) return;

        // 챔프 없으면 카드 숨김 (옵션). 일단 그대로 두기.
        if (unit == null || unit.Data == null) return;

        // 자식 이름으로 매핑
        SetStatText(card, "HP", unit.IsDead ? "0" : Mathf.RoundToInt(unit.CurrentHp).ToString());
        SetStatText(card, "Attack", Mathf.RoundToInt(unit.Data.AttackDamage).ToString());
        SetStatText(card, "AttackSpeed", unit.Data.AttackSpeed.ToString("0.0"));
        SetStatText(card, "Armor", Mathf.RoundToInt(unit.Data.Defense).ToString());
        SetStatText(card, "Intersection", unit.Data.AttackRange.ToString("0.0"));   // 사거리
        SetStatText(card, "Speed", unit.Data.MoveSpeed.ToString("0.0"));            // 이속

        // 이름 — 끝에 붙은 인덱스 " 1", " 2" 떼고 역할 이름만 ("수호기사 1" → "수호기사")
        // 컨테이너가 좁으면 잘리니까 overflow 도 같이 켜기
        string roleName = StripIndex(unit.Data.ChampionName);
        SetNameText(card, "Name", roleName);
        SetNameText(card, "ChampionName", roleName);

        // 역할 아이콘 — 카드 상단의 Icon_Weapon 을 role 별 sprite 로 교체 (검 → 방패/활/지팡이/...)
        ApplyRoleIcon(card, unit.Data.Role);

        // 초상화 — 'Image' 이름의 SPUM placeholder 자식이 정답 (AScene_FightUI 기준 100x100)
        // 그 외 카드 배경/헤더 바(Blue, Card_BG, BlueBG, Red, RedBG 등)는 절대 건드리면 안 됨
        var portrait = FindPortraitImage(card);
        if (portrait != null && unit.Data.Icon != null)
        {
            portrait.sprite = unit.Data.Icon;
            portrait.color = Color.white;
            portrait.preserveAspect = true;
        }
    }

    static readonly System.Collections.Generic.HashSet<string> _excludeImageNames = new()
    {
        "Card_BG", "Blue", "BlueBG", "Red", "RedBG", "BG",
        "Icon_Weapon", "Ball_1", "Ball_2",
    };

    static Image FindPortraitImage(Transform card)
    {
        // 1순위 — 명시적 portrait 이름들
        var named = FindImageByNames(card,
            "Portrait", "Icon", "Char", "CharIcon", "Char_Icon",
            "ChampIcon", "ChampionIcon", "캐릭터", "ChampImage", "ChampPortrait");
        if (named != null) return named;

        // 2순위 — 'Image' (AScene_FightUI placeholder 의 기본 이름).
        // 단, BG/header 류는 제외. 한 카드에 'Image' 가 여러 개면 가장 정사각형에 가까운 거.
        Image best = null; float bestScore = float.MaxValue;
        foreach (var img in card.GetComponentsInChildren<Image>(true))
        {
            if (img.transform == card) continue;
            if (img.gameObject.name != "Image") continue;
            if (_excludeImageNames.Contains(img.gameObject.name)) continue;
            var rt = img.GetComponent<RectTransform>();
            if (rt == null) continue;
            float w = Mathf.Abs(rt.rect.width), h = Mathf.Abs(rt.rect.height);
            float aspectScore = Mathf.Abs(w - h); // 정사각형일수록 낮음
            if (aspectScore < bestScore) { bestScore = aspectScore; best = img; }
        }
        return best;
    }

    static Image FindImageByNames(Transform parent, params string[] names)
    {
        var set = new System.Collections.Generic.HashSet<string>(names);
        foreach (var t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (set.Contains(t.name))
            {
                var img = t.GetComponent<Image>();
                if (img != null) return img;
            }
        }
        return null;
    }

    static void SetStatText(Transform card, string childName, string value)
    {
        var child = card.Find(childName);
        if (child == null)
        {
            // 깊은 검색
            foreach (var t in card.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == childName) { child = t; break; }
            }
            if (child == null) return;
        }
        // 자식 안 어떤 텍스트 컴포넌트에라도 표시
        var tmp = child.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null) { tmp.text = value; return; }
        var legacy = child.GetComponentInChildren<Text>(true);
        if (legacy != null) legacy.text = value;
    }
}

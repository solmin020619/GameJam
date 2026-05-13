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

    Transform[] _blueCards = new Transform[3];
    Transform[] _redCards = new Transform[3];
    TextMeshProUGUI _team0ScoreText;
    TextMeshProUGUI _team1ScoreText;
    TextMeshProUGUI _timerText;
    bool _wired = false;

    void Start()
    {
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

        // KScene 쪽 옛 타이머/스코어 UI 강제로 모두 비활성 (이름으로 검색)
        // → 인스펙터 와이어링 누락으로 BM.TimerText 가 null 이어도 옛 UI 안 보이게
        var myScene = gameObject.scene;
        foreach (var root in myScene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name;
                if (n == "TimerText" || n == "Team0Score" || n == "Team1Score" ||
                    n == "ResultPanel" /* 결과는 새 UI 가 처리할 수도 */ )
                {
                    // ResultPanel 은 BattleManager 가 SetActive(false) 하니까 살려두고
                    if (n == "ResultPanel") continue;
                    // 자기 자신(새 UI scene)에 있는 거면 skip
                    if (t.gameObject.scene == scene) continue;
                    t.gameObject.SetActive(false);
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

            // Fallback — "New Text" 이름의 TMP 가 두 개라면 좌/우로 추정 (점수)
            if (_team0ScoreText == null || _team1ScoreText == null)
            {
                var newTexts = new List<TextMeshProUGUI>();
                foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (t.gameObject.name == "New Text") newTexts.Add(t);
                }
                if (newTexts.Count >= 2)
                {
                    newTexts.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
                    if (_team0ScoreText == null) _team0ScoreText = newTexts[0];
                    if (_team1ScoreText == null) _team1ScoreText = newTexts[1];
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

        _wired = true;
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

        // 스코어 표시
        if (_team0ScoreText != null) _team0ScoreText.text = BattleManager.Instance.Team0Kills.ToString();
        if (_team1ScoreText != null) _team1ScoreText.text = BattleManager.Instance.Team1Kills.ToString();
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

        // 이름: 카드 자체에 TMP 있으면 (Card 의 자식 중 어떤 텍스트가 이름인지는 씬마다 다름)
        SetStatText(card, "Name", unit.Data.ChampionName);
        SetStatText(card, "ChampionName", unit.Data.ChampionName);

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

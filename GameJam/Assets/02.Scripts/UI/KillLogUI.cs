using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 킬로그 UI — 맵 가운데 상단에 [killer][검][victim] 가로 박스 스택.
/// 새 엔트리는 위에 추가 → 기존이 아래로 밀려남.
/// 2.5초 후 페이드아웃 + Destroy. 최대 5개 유지.
/// 검 sprite / 캐릭터 portrait 는 추후 채움 (현재 null).
/// </summary>
public class KillLogUI : MonoBehaviour
{
    public static KillLogUI Instance;

    [Header("Refs (Installer가 자동 세팅)")]
    public Transform entryContainer;
    public Sprite swordSprite;          // 사용자가 나중에 할당

    [Header("Settings")]
    public int maxEntries = 5;
    public float fadeOutAfter = 2.5f;
    public float fadeDuration = 0.4f;

    [Header("Colors")]
    public Color allyTeamColor = new Color(0.3f, 0.5f, 0.95f);   // 파랑 (Team 0)
    public Color enemyTeamColor = new Color(0.95f, 0.3f, 0.3f);  // 빨강 (Team 1)

    [Header("Entry Size")]
    public float boxSize = 52f;                     // 세 박스 다 같은 크기
    public Color swordBgColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    public int borderWidth = 0;                     // 외곽 테두리 (0 = 없음)
    public int boxSpacing = 0;                      // 박스 사이 간격 (0 = 박스끼리 붙음)

    static Sprite _whiteSprite;
    static Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite == null)
                _whiteSprite = Sprite.Create(Texture2D.whiteTexture,
                    new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            return _whiteSprite;
        }
    }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public void AddEntry(ChampionUnit killer, ChampionUnit victim)
    {
        if (entryContainer == null) return;
        if (victim == null) return;

        var entry = CreateEntry(killer, victim);
        entry.transform.SetAsFirstSibling();  // 위로 → 기존이 아래로 밀려남

        // 최대 개수 초과 시 가장 오래된 거 (맨 아래) 즉시 제거
        while (entryContainer.childCount > maxEntries)
        {
            var last = entryContainer.GetChild(entryContainer.childCount - 1).gameObject;
            Destroy(last);
        }

        StartCoroutine(FadeAndDestroy(entry));
    }

    GameObject CreateEntry(ChampionUnit killer, ChampionUnit victim)
    {
        var go = new GameObject("KillEntry");
        go.transform.SetParent(entryContainer, false);

        var rt = go.AddComponent<RectTransform>();

        // (테두리 제거됨 — 박스만 표시)

        var hg = go.AddComponent<HorizontalLayoutGroup>();
        hg.childForceExpandWidth = false;
        hg.childForceExpandHeight = false;
        hg.childAlignment = TextAnchor.MiddleCenter;
        hg.spacing = boxSpacing;  // 박스 사이 흰 줄
        hg.padding = new RectOffset(borderWidth, borderWidth, borderWidth, borderWidth);

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        go.AddComponent<CanvasGroup>();

        // 좌: killer / 중: 검 / 우: victim — 세 박스 모두 같은 크기
        CreatePortraitBox(go.transform, killer, "KillerBox");
        CreateSwordBox(go.transform);
        CreatePortraitBox(go.transform, victim, "VictimBox");

        return go;
    }

    void CreatePortraitBox(Transform parent, ChampionUnit unit, string name)
    {
        var box = new GameObject(name);
        box.transform.SetParent(parent, false);
        var rt = box.AddComponent<RectTransform>();
        var le = box.AddComponent<LayoutElement>();
        le.preferredWidth = boxSize;
        le.preferredHeight = boxSize;

        // 배경 (팀 색) — 박스 자체가 mask 역할
        var bg = box.AddComponent<Image>();
        bg.sprite = WhiteSprite;
        bg.color = unit != null ? (unit.TeamId == 0 ? allyTeamColor : enemyTeamColor) : Color.gray;
        bg.raycastTarget = false;
        // RectMask2D 추가 — 자식 portrait 가 박스 밖으로 튀어나오면 자름
        box.AddComponent<RectMask2D>();

        // 머리 아이콘 — 박스보다 살짝 크게 (sprite 안 여백 보정하여 머리가 더 크게 보임)
        var portraitGo = new GameObject("Portrait");
        portraitGo.transform.SetParent(box.transform, false);
        var prt = portraitGo.AddComponent<RectTransform>();
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        // 박스 안 zoom — ChampionData 의 KillIconZoom 으로 챔프별 조절 (기본 1.0)
        // RectMask2D 가 박스 밖 영역 자르므로 안전
        const float baseExpand = 14f;
        float zoom = (unit != null && unit.Data != null) ? Mathf.Max(0.5f, unit.Data.KillIconZoom) : 1f;
        Vector2 off = (unit != null && unit.Data != null) ? unit.Data.KillIconOffset : Vector2.zero;
        float ex = baseExpand * zoom;
        // offset 큰 값도 받게 — offset 크기만큼 portrait sizeDelta 추가 확장 (머리 영역이 항상 box 안에 잡힘)
        ex += Mathf.Max(Mathf.Abs(off.x), Mathf.Abs(off.y));
        prt.offsetMin = new Vector2(-ex + off.x, -ex + off.y);
        prt.offsetMax = new Vector2( ex + off.x,  ex + off.y);
        var portrait = portraitGo.AddComponent<Image>();
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        // 우선순위: KillIcon (머리 컷) → Icon (전신) → 투명
        Sprite face = null;
        if (unit != null && unit.Data != null)
            face = unit.Data.KillIcon != null ? unit.Data.KillIcon : unit.Data.Icon;
        if (face != null)
        {
            portrait.sprite = face;
            portrait.color = Color.white;
        }
        else
        {
            portrait.color = new Color(1, 1, 1, 0f);
        }
    }

    void CreateSwordBox(Transform parent)
    {
        var box = new GameObject("SwordBox");
        box.transform.SetParent(parent, false);
        var rt = box.AddComponent<RectTransform>();
        var le = box.AddComponent<LayoutElement>();
        le.preferredWidth = boxSize;
        le.preferredHeight = boxSize;

        // 검정 배경
        var bg = box.AddComponent<Image>();
        bg.sprite = WhiteSprite;
        bg.color = swordBgColor;
        bg.raycastTarget = false;

        // 검 sprite (박스 안쪽 패딩 살짝)
        var swordGo = new GameObject("Sword");
        swordGo.transform.SetParent(box.transform, false);
        var prt = swordGo.AddComponent<RectTransform>();
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(4, 4); prt.offsetMax = new Vector2(-4, -4);
        var sword = swordGo.AddComponent<Image>();
        sword.preserveAspect = true;
        sword.raycastTarget = false;
        if (swordSprite != null)
        {
            sword.sprite = swordSprite;
            sword.color = Color.white;
        }
        else
        {
            sword.color = new Color(1, 1, 1, 0f);  // sprite 없으면 투명 (검정 배경만 보임)
        }
    }

    IEnumerator FadeAndDestroy(GameObject entry)
    {
        yield return new WaitForSeconds(fadeOutAfter);
        if (entry == null) yield break;

        var cg = entry.GetComponent<CanvasGroup>();
        if (cg == null) { Destroy(entry); yield break; }

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            cg.alpha = 1f - (t / fadeDuration);
            yield return null;
        }
        Destroy(entry);
    }
}

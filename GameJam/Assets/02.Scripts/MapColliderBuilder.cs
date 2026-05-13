// MapColliderBuilder.cs
// Attach to the GameObject with your map SpriteRenderer.
// Generates a PolygonCollider2D so the stone walls block the player.
//
// Setup (one time):
//   1) Put walkable_polygon.json inside  Assets/Resources/walkable_polygon.json
//   2) Drop map_transparent.png in Assets. In the import inspector:
//        - Texture Type        : Sprite (2D and UI)
//        - Sprite Mode         : Single
//        - Pixels Per Unit     : 100   (must match the JSON)
//        - Mesh Type           : Full Rect
//        - Filter Mode         : Bilinear  (or Point for crisp pixel-art)
//        - Compression         : None      (preserves quality)
//   3) Create an empty GameObject, add SpriteRenderer, drag the sprite in.
//   4) Add this script. PolygonCollider2D will be built automatically on Awake.
//   5) If your player is a Rigidbody2D + Collider2D, walls will now stop them.

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class MapColliderBuilder : MonoBehaviour
{
    [Tooltip("Name of the JSON file under Resources/ (without extension).")]
    public string jsonResourceName = "walkable_polygon";

    [Tooltip("true  = build wall collider (everything OUTSIDE the floor blocks the player).\n" +
             "false = build the walkable polygon directly (useful as trigger / nav bounds).")]
    public bool buildWallCollider = true;

    [Tooltip("Shrink the walkable polygon inward by this many world units (extra safety margin).")]
    public float inwardPadding = 0f;

    [Tooltip("Rebuild every time the GameObject is enabled (handy while iterating).")]
    public bool rebuildOnEnable = true;

    void OnEnable()
    {
        if (rebuildOnEnable) Build();
    }

    [ContextMenu("Rebuild Collider")]
    public void Build()
    {
        var ta = Resources.Load<TextAsset>(jsonResourceName);
        if (ta == null)
        {
            Debug.LogError($"[MapColliderBuilder] Resources/{jsonResourceName}.json not found.");
            return;
        }

        var data = JsonUtility.FromJson<PolyJson>(ta.text);
        if (data == null || data.xs == null || data.ys == null || data.xs.Length < 3)
        {
            Debug.LogError("[MapColliderBuilder] Invalid polygon JSON.");
            return;
        }

        var walkable = new Vector2[data.xs.Length];
        for (int i = 0; i < walkable.Length; i++)
            walkable[i] = new Vector2(data.xs[i], data.ys[i]);

        if (inwardPadding > 0f)
        {
            Vector2 c = Vector2.zero;
            for (int i = 0; i < walkable.Length; i++) c += walkable[i];
            c /= walkable.Length;
            for (int i = 0; i < walkable.Length; i++)
            {
                var dir = (walkable[i] - c).normalized;
                walkable[i] -= dir * inwardPadding;
            }
        }

        // Remove old colliders so re-runs don't pile up.
        foreach (var col in GetComponents<PolygonCollider2D>())
        {
            if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
        }
        var poly = gameObject.AddComponent<PolygonCollider2D>();

        if (!buildWallCollider)
        {
            poly.pathCount = 1;
            poly.SetPath(0, walkable);
            return;
        }

        // Wall collider = outer rectangle minus walkable hole.
        // PolygonCollider2D supports multiple paths; opposite winding = hole.
        float halfW = data.imageWidth  / (2f * data.pixelsPerUnit);
        float halfH = data.imageHeight / (2f * data.pixelsPerUnit);
        var outer = new Vector2[]
        {
            new Vector2(-halfW, -halfH),
            new Vector2( halfW, -halfH),
            new Vector2( halfW,  halfH),
            new Vector2(-halfW,  halfH),
        };

        poly.pathCount = 2;
        poly.SetPath(0, outer);
        poly.SetPath(1, walkable);
    }

    [System.Serializable]
    class PolyJson
    {
        public int imageWidth;
        public int imageHeight;
        public float pixelsPerUnit;
        public float[] xs;
        public float[] ys;
    }
}

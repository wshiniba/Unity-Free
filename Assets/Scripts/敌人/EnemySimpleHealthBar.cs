using UnityEngine;

/// <summary>
/// 使用 SpriteRenderer 绘制的简易世界空间血条，适合作为小怪模板的临时血条表现。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Enemy))]
public class EnemySimpleHealthBar : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("要读取生命值的敌人组件。未指定时自动使用当前物体上的 Enemy。")]
    public Enemy enemy;

    [Tooltip("血条背景 SpriteRenderer。为空时会自动创建或查找名为 HealthBar_Back 的子物体。")]
    public SpriteRenderer backgroundRenderer;

    [Tooltip("缓冲血量 SpriteRenderer。为空时会自动创建或查找名为 HealthBar_Buffer 的子物体。")]
    public SpriteRenderer bufferRenderer;

    [Tooltip("真实血量 SpriteRenderer。为空时会自动创建或查找名为 HealthBar_Fill 的子物体。")]
    public SpriteRenderer fillRenderer;

    [Tooltip("敌人可处决时显示的 E 键提示。为空时会自动创建或查找名为 HealthBar_ExecutePrompt 的子物体。")]
    public TextMesh executePromptText;

    [Header("布局")]
    [Tooltip("血条相对敌人中心的世界空间偏移。")]
    public Vector3 localOffset = new Vector3(0f, 1.25f, 0f);

    [Tooltip("满血时血条的宽度。")]
    public float width = 1.1f;

    [Tooltip("血条的高度。")]
    public float height = 0.1f;

    [Tooltip("血条各层之间的 Z 轴偏移，避免重叠闪烁。")]
    public float layerDepthStep = -0.01f;

    [Tooltip("生命值为满时是否隐藏血条。")]
    public bool hideWhenFull = false;

    [Header("处决提示")]
    [Tooltip("敌人可被处决时，是否在血条上方显示 E 提示。")]
    public bool showExecutePrompt = true;

    [Tooltip("处决提示相对血条中心的本地偏移。")]
    public Vector3 executePromptOffset = new Vector3(0f, 0.28f, 0f);

    [Tooltip("处决提示文字。")]
    public string executePrompt = "E";

    [Tooltip("处决提示字体大小。")]
    public int executePromptFontSize = 48;

    [Tooltip("处决提示在世界空间中的整体缩放。")]
    public float executePromptCharacterSize = 0.055f;

    [Header("颜色")]
    [Tooltip("血条背景颜色。")]
    public Color backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.85f);

    [Tooltip("缓冲血量颜色。")]
    public Color bufferColor = new Color(1f, 0.7f, 0.15f, 0.9f);

    [Tooltip("真实血量颜色。")]
    public Color fillColor = new Color(0.2f, 1f, 0.25f, 0.95f);

    [Tooltip("处决提示颜色。")]
    public Color executePromptColor = new Color(1f, 0.92f, 0.2f, 1f);

    [Header("动态")]
    [Tooltip("缓冲血量追向真实血量的速度。")]
    public float bufferLerpSpeed = 5f;

    [Tooltip("血条 SpriteRenderer 的排序层级。")]
    public string sortingLayerName = "Default";

    [Tooltip("血条 SpriteRenderer 的排序序号。数值越大越靠前。")]
    public int sortingOrder = 20;

    private const string BackName = "HealthBar_Back";
    private const string BufferName = "HealthBar_Buffer";
    private const string FillName = "HealthBar_Fill";
    private const string ExecutePromptName = "HealthBar_ExecutePrompt";
    private static Sprite sharedBarSprite;
    private float bufferRatio = 1f;

    void Reset()
    {
        enemy = GetComponent<Enemy>();
        EnsureRenderers();
        RefreshImmediate();
    }

    void Awake()
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
        EnsureRenderers();
        RefreshImmediate();
    }

    void OnValidate()
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
        FindExistingRenderers();
        RefreshImmediate();
    }

    void LateUpdate()
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
        if (enemy == null) return;

        EnsureRenderers();

        float ratio = enemy.GetHealthRatio();
        bufferRatio = Mathf.MoveTowards(bufferRatio, ratio, Mathf.Max(0.01f, bufferLerpSpeed) * Time.deltaTime);

        ApplyRendererState(backgroundRenderer, 1f, backgroundColor, 0);
        ApplyRendererState(bufferRenderer, bufferRatio, bufferColor, 1);
        ApplyRendererState(fillRenderer, ratio, fillColor, 2);

        bool visible = !hideWhenFull || ratio < 0.999f;
        SetVisible(visible);
        UpdateExecutePrompt(visible);
    }

    public void RefreshImmediate()
    {
        if (enemy == null) return;

        float ratio = enemy.GetHealthRatio();
        bufferRatio = ratio;

        ApplyRendererState(backgroundRenderer, 1f, backgroundColor, 0);
        ApplyRendererState(bufferRenderer, ratio, bufferColor, 1);
        ApplyRendererState(fillRenderer, ratio, fillColor, 2);
    }

    public Vector3 GetWorldCenter()
    {
        if (backgroundRenderer != null)
            return backgroundRenderer.bounds.center;

        return transform.position + localOffset;
    }

    private void EnsureRenderers()
    {
        backgroundRenderer = EnsureRenderer(backgroundRenderer, BackName);
        bufferRenderer = EnsureRenderer(bufferRenderer, BufferName);
        fillRenderer = EnsureRenderer(fillRenderer, FillName);
        executePromptText = EnsureExecutePrompt(executePromptText);
    }

    private void FindExistingRenderers()
    {
        if (backgroundRenderer == null)
            backgroundRenderer = FindChildRenderer(BackName);

        if (bufferRenderer == null)
            bufferRenderer = FindChildRenderer(BufferName);

        if (fillRenderer == null)
            fillRenderer = FindChildRenderer(FillName);

        if (executePromptText == null)
            executePromptText = FindChildText(ExecutePromptName);
    }

    private SpriteRenderer FindChildRenderer(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<SpriteRenderer>() : null;
    }

    private TextMesh FindChildText(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<TextMesh>() : null;
    }

    private SpriteRenderer EnsureRenderer(SpriteRenderer renderer, string childName)
    {
        if (renderer != null)
        {
            PrepareRenderer(renderer);
            return renderer;
        }

        Transform child = transform.Find(childName);
        if (child == null)
        {
            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(transform, false);
            child = childObject.transform;
        }

        renderer = child.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = child.gameObject.AddComponent<SpriteRenderer>();

        PrepareRenderer(renderer);
        return renderer;
    }

    private void PrepareRenderer(SpriteRenderer renderer)
    {
        renderer.sprite = GetBarSprite();
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = sortingOrder;
    }

    private TextMesh EnsureExecutePrompt(TextMesh prompt)
    {
        if (prompt == null)
        {
            Transform child = transform.Find(ExecutePromptName);
            if (child == null)
            {
                GameObject childObject = new GameObject(ExecutePromptName);
                childObject.transform.SetParent(transform, false);
                child = childObject.transform;
            }

            prompt = child.GetComponent<TextMesh>();
            if (prompt == null)
                prompt = child.gameObject.AddComponent<TextMesh>();
        }

        prompt.anchor = TextAnchor.MiddleCenter;
        prompt.alignment = TextAlignment.Center;
        prompt.text = executePrompt;
        prompt.fontSize = Mathf.Max(1, executePromptFontSize);
        prompt.characterSize = Mathf.Max(0.001f, executePromptCharacterSize);
        prompt.color = executePromptColor;

        MeshRenderer meshRenderer = prompt.GetComponent<MeshRenderer>();
        meshRenderer.sortingLayerName = sortingLayerName;
        meshRenderer.sortingOrder = sortingOrder + 10;

        return prompt;
    }

    private void ApplyRendererState(SpriteRenderer renderer, float ratio, Color color, int layerIndex)
    {
        if (renderer == null) return;

        float safeWidth = Mathf.Max(0.01f, width);
        float safeHeight = Mathf.Max(0.01f, height);
        float clampedRatio = Mathf.Clamp01(ratio);
        float currentWidth = safeWidth * clampedRatio;

        Transform target = renderer.transform;
        target.localPosition = localOffset + new Vector3((currentWidth - safeWidth) * 0.5f, 0f, layerDepthStep * layerIndex);
        target.localRotation = Quaternion.identity;
        target.localScale = new Vector3(currentWidth, safeHeight, 1f);

        renderer.color = color;
    }

    private void SetVisible(bool visible)
    {
        if (backgroundRenderer != null) backgroundRenderer.enabled = visible;
        if (bufferRenderer != null) bufferRenderer.enabled = visible;
        if (fillRenderer != null) fillRenderer.enabled = visible;
    }

    private void UpdateExecutePrompt(bool healthBarVisible)
    {
        if (executePromptText == null)
            return;

        bool visible = showExecutePrompt && healthBarVisible && enemy != null && enemy.CanBeExecuted();
        executePromptText.gameObject.SetActive(visible);
        if (!visible)
            return;

        Transform promptTransform = executePromptText.transform;
        promptTransform.localPosition = localOffset + executePromptOffset + new Vector3(0f, 0f, layerDepthStep * 3f);
        promptTransform.localRotation = Quaternion.identity;
        promptTransform.localScale = Vector3.one;
        executePromptText.text = executePrompt;
        executePromptText.color = executePromptColor;
    }

    private static Sprite GetBarSprite()
    {
        if (sharedBarSprite != null)
            return sharedBarSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        sharedBarSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        sharedBarSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedBarSprite;
    }
}

using UnityEngine;

/// <summary>
/// 监听 Enemy 受伤事件，在敌人上方生成向上弹起再落下的伤害数字。
/// </summary>
[RequireComponent(typeof(Enemy))]
public class EnemyDamagePopup : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("要监听受伤事件的敌人组件。未指定时自动使用当前物体上的 Enemy。")]
    public Enemy enemy;

    [Header("生成位置")]
    [Tooltip("伤害数字相对敌人中心的生成偏移，通常放在血条上方。")]
    public Vector3 spawnOffset = new Vector3(0f, 1.55f, 0f);

    [Tooltip("每个数字生成时的随机水平偏移范围。")]
    public float horizontalSpawnJitter = 0.12f;

    [Header("运动")]
    [Tooltip("数字生成时的初始向上速度。")]
    public float jumpVelocity = 2.2f;

    [Tooltip("数字生成时的随机水平速度范围。")]
    public float horizontalVelocity = 0.45f;

    [Tooltip("数字落下时使用的重力加速度。")]
    public float gravity = 7f;

    [Tooltip("数字存在的总时长。")]
    public float lifetime = 0.85f;

    [Tooltip("生命周期达到该比例后开始淡出。")]
    [Range(0f, 1f)]
    public float fadeStartRatio = 0.45f;

    [Header("文字")]
    [Tooltip("伤害数字颜色。")]
    public Color textColor = new Color(1f, 0.18f, 0.08f, 1f);

    [Tooltip("文字字体大小。")]
    public int fontSize = 42;

    [Tooltip("文字在世界空间中的整体缩放。")]
    public float characterSize = 0.06f;

    [Tooltip("伤害数字的排序层级。")]
    public string sortingLayerName = "Default";

    [Tooltip("伤害数字的排序序号。数值越大越靠前。")]
    public int sortingOrder = 40;

    void Awake()
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
    }

    void OnEnable()
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
        if (enemy != null)
            enemy.DamageTaken += SpawnDamageNumber;
    }

    void OnDisable()
    {
        if (enemy != null)
            enemy.DamageTaken -= SpawnDamageNumber;
    }

    void OnValidate()
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
    }

    private void SpawnDamageNumber(int damage)
    {
        GameObject popupObject = new GameObject("DamagePopup");
        popupObject.transform.position = transform.position + spawnOffset + new Vector3(
            Random.Range(-horizontalSpawnJitter, horizontalSpawnJitter),
            0f,
            0f);

        TextMesh textMesh = popupObject.AddComponent<TextMesh>();
        textMesh.text = damage.ToString();
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = Mathf.Max(1, fontSize);
        textMesh.characterSize = Mathf.Max(0.001f, characterSize);
        textMesh.color = textColor;

        MeshRenderer meshRenderer = popupObject.GetComponent<MeshRenderer>();
        meshRenderer.sortingLayerName = sortingLayerName;
        meshRenderer.sortingOrder = sortingOrder;

        DamagePopupNumber number = popupObject.AddComponent<DamagePopupNumber>();
        number.Initialize(
            new Vector3(Random.Range(-horizontalVelocity, horizontalVelocity), jumpVelocity, 0f),
            gravity,
            lifetime,
            fadeStartRatio,
            textColor);
    }
}

/// <summary>
/// 单个伤害数字的运动和淡出控制。
/// </summary>
public class DamagePopupNumber : MonoBehaviour
{
    private TextMesh textMesh;
    private Vector3 velocity;
    private Color baseColor;
    private float gravity;
    private float lifetime;
    private float fadeStartRatio;
    private float elapsed;

    public void Initialize(Vector3 initialVelocity, float gravityValue, float lifetimeValue, float fadeStart, Color color)
    {
        textMesh = GetComponent<TextMesh>();
        velocity = initialVelocity;
        gravity = Mathf.Max(0f, gravityValue);
        lifetime = Mathf.Max(0.01f, lifetimeValue);
        fadeStartRatio = Mathf.Clamp01(fadeStart);
        baseColor = color;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
        velocity.y -= gravity * Time.deltaTime;

        if (textMesh != null)
        {
            float fadeStartTime = lifetime * fadeStartRatio;
            float fadeDuration = Mathf.Max(0.01f, lifetime - fadeStartTime);
            float fadeRatio = elapsed <= fadeStartTime ? 1f : 1f - Mathf.Clamp01((elapsed - fadeStartTime) / fadeDuration);
            Color color = baseColor;
            color.a *= fadeRatio;
            textMesh.color = color;
        }

        if (elapsed >= lifetime)
            Destroy(gameObject);
    }
    
}

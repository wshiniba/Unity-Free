using UnityEngine;

/// <summary>
/// 临时掉落物拾取表现。
/// 后续接正式背包系统时，可以在 TryPickup 中把 itemId 和 amount 交给背包管理器。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class LootPickup : MonoBehaviour
{
    [Header("物品数据")]
    [Tooltip("物品配置 ID。后续接 Excel/CSV 物品表或背包系统时，用这个 ID 查找正式物品数据。")]
    public string itemId = "special_material";

    [Tooltip("该掉落物代表的物品数量。")]
    public int amount = 1;

    [Header("拾取判定")]
    [Tooltip("开启后，玩家碰到掉落物时会自动拾取。")]
    public bool canAutoPickup = true;

    [Tooltip("掉落物可存在的最长时间。小于等于 0 时不会自动消失。")]
    public float lifetime = 20f;

    [Tooltip("没有正式背包系统时，拾取后是否在 Console 输出调试日志。")]
    public bool logPickup = true;

    [Header("临时显示")]
    [Tooltip("临时掉落物的颜色。接入正式物品素材后可以不再使用这个表现。")]
    public Color pickupColor = new Color(1f, 0.82f, 0.2f, 1f);

    [Tooltip("临时掉落物上下浮动的幅度。")]
    public float bobAmplitude = 0.08f;

    [Tooltip("临时掉落物上下浮动的速度。")]
    public float bobSpeed = 3f;

    private SpriteRenderer spriteRenderer;
    private Vector3 basePosition;
    private float spawnTime;

    private static Sprite defaultSprite;

    public void Initialize(string newItemId, int newAmount)
    {
        itemId = string.IsNullOrWhiteSpace(newItemId) ? itemId : newItemId;
        amount = Mathf.Max(1, newAmount);
    }

    public static LootPickup CreateDefault(string itemId, int amount, Vector3 position)
    {
        GameObject pickupObject = new GameObject($"LootPickup_{itemId}_x{Mathf.Max(1, amount)}");
        pickupObject.transform.position = position;
        pickupObject.transform.localScale = new Vector3(0.28f, 0.28f, 1f);

        SpriteRenderer renderer = pickupObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetDefaultSprite();
        renderer.color = new Color(1f, 0.82f, 0.2f, 1f);
        renderer.sortingOrder = 20;

        CircleCollider2D collider = pickupObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.6f;

        Rigidbody2D rigidbody = pickupObject.AddComponent<Rigidbody2D>();
        rigidbody.bodyType = RigidbodyType2D.Kinematic;
        rigidbody.gravityScale = 0f;
        rigidbody.freezeRotation = true;

        LootPickup pickup = pickupObject.AddComponent<LootPickup>();
        pickup.Initialize(itemId, amount);
        pickup.spriteRenderer = renderer;
        return pickup;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        Collider2D pickupCollider = GetComponent<Collider2D>();
        if (pickupCollider != null)
            pickupCollider.isTrigger = true;
    }

    private void Start()
    {
        spawnTime = Time.time;
        basePosition = transform.position;

        if (spriteRenderer != null)
            spriteRenderer.color = pickupColor;
    }

    private void Update()
    {
        if (bobAmplitude > 0f && bobSpeed > 0f)
        {
            Vector3 position = basePosition;
            position.y += Mathf.Sin((Time.time - spawnTime) * bobSpeed) * bobAmplitude;
            transform.position = position;
        }

        if (lifetime > 0f && Time.time - spawnTime >= lifetime)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!canAutoPickup || other == null)
            return;

        if (other.GetComponentInParent<OdmController>() == null && other.GetComponentInParent<PlayerHealth>() == null)
            return;

        TryPickup(other.transform.root.gameObject);
    }

    private void TryPickup(GameObject picker)
    {
        PlayerInventory inventory = picker != null ? picker.GetComponentInParent<PlayerInventory>() : null;
        if (inventory == null && picker != null)
            inventory = picker.GetComponentInChildren<PlayerInventory>();

        bool addedToInventory = inventory != null && inventory.AddItem(itemId, amount);

        if (logPickup)
        {
            string inventoryState = addedToInventory ? "已写入背包" : "未找到背包，仅完成临时拾取";
            Debug.Log($"拾取物品：{itemId} x{amount}，{inventoryState}", picker != null ? picker : gameObject);
        }

        Destroy(gameObject);
    }

    private static Sprite GetDefaultSprite()
    {
        if (defaultSprite != null)
            return defaultSprite;

        Texture2D texture = Texture2D.whiteTexture;
        defaultSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            1f);
        return defaultSprite;
    }

    private void OnValidate()
    {
        amount = Mathf.Max(1, amount);
        lifetime = Mathf.Max(0f, lifetime);
        bobAmplitude = Mathf.Max(0f, bobAmplitude);
        bobSpeed = Mathf.Max(0f, bobSpeed);
    }
}

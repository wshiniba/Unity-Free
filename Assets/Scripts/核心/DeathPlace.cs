using UnityEngine;

/// <summary>
/// 死亡区域触发器。玩家进入后会被传送到 checkpoint。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DeathPlace : MonoBehaviour
{
    [Header("传送目标")]
    [Tooltip("玩家触碰死亡区域后要传送到的位置。为空时会按 checkpointObjectName 在场景中自动查找。")]
    public Transform checkpoint;

    [Tooltip("自动查找 checkpoint 时使用的物体名称。场景中的检查点物体建议命名为 checkpoint。")]
    public string checkpointObjectName = "checkpoint";

    [Tooltip("传送到 checkpoint 后额外叠加的位置偏移。通常保持为 0。")]
    public Vector3 respawnOffset = Vector3.zero;

    [Header("玩家判定")]
    [Tooltip("开启后，只传送带有 OdmController 或 PlayerHealth 的玩家对象，避免敌人或投掷物触发传送。")]
    public bool onlyTeleportPlayer = true;

    [Header("传送行为")]
    [Tooltip("传送前是否清除玩家左右触手和悬停状态。建议开启，避免旧锚点把玩家拉回死亡区域。")]
    public bool clearPlayerTentacles = true;

    [Tooltip("传送后是否清空玩家 Rigidbody2D 的线速度和角速度。建议开启，避免玩家带着坠落速度继续飞出检查点。")]
    public bool resetPlayerVelocity = true;

    [Tooltip("开启后，如果没有手动绑定 checkpoint，Start 时会按名称自动查找。")]
    public bool autoFindCheckpointOnStart = true;

    [Tooltip("开启后，缺少 Trigger 或 checkpoint 时会在 Console 输出提醒。")]
    public bool logWarnings = true;

    private Collider2D triggerCollider;

    private void Reset()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
    }

    private void Start()
    {
        if (triggerCollider != null && !triggerCollider.isTrigger && logWarnings)
            Debug.LogWarning("DeathPlace 所在物体的 Collider2D 没有勾选 Is Trigger。", this);

        if (autoFindCheckpointOnStart)
            FindCheckpointIfNeeded();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryTeleportPlayer(other);
    }

    private void TryTeleportPlayer(Collider2D other)
    {
        if (other == null)
            return;

        OdmController controller = other.GetComponentInParent<OdmController>();
        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (onlyTeleportPlayer && controller == null && health == null)
            return;

        Transform playerTransform = controller != null
            ? controller.transform
            : health != null ? health.transform : other.transform;

        FindCheckpointIfNeeded();
        if (checkpoint == null)
        {
            if (logWarnings)
                Debug.LogWarning($"DeathPlace 找不到传送目标：{checkpointObjectName}", this);
            return;
        }

        if (clearPlayerTentacles && controller != null)
            controller.ResetCablesForTeleport();

        Vector3 targetPosition = checkpoint.position + respawnOffset;
        Rigidbody2D playerBody = playerTransform.GetComponent<Rigidbody2D>();
        if (playerBody != null)
        {
            playerBody.position = targetPosition;

            if (resetPlayerVelocity)
            {
                playerBody.linearVelocity = Vector2.zero;
                playerBody.angularVelocity = 0f;
            }
        }
        else
        {
            playerTransform.position = targetPosition;
        }

        Physics2D.SyncTransforms();
    }

    private void FindCheckpointIfNeeded()
    {
        if (checkpoint != null || string.IsNullOrWhiteSpace(checkpointObjectName))
            return;

        GameObject checkpointObject = GameObject.Find(checkpointObjectName);
        if (checkpointObject != null)
            checkpoint = checkpointObject.transform;
    }
}

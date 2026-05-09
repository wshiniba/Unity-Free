using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在屏幕上显示玩家当前 Rigidbody2D 速度。
/// </summary>
public class PlayerSpeedDisplay : MonoBehaviour
{
    [Header("玩家引用")]
    [Tooltip("要读取速度的玩家刚体。未指定时会自动查找场景中的 OdmController。")]
    public Rigidbody2D playerRigidbody;

    [Header("文本")]
    [Tooltip("用于显示速度的 UI Text。需要在场景中手动创建并拖入。")]
    public Text speedText;

    [Tooltip("速度显示格式。{0} 会被替换为当前速度数值。")]
    public string displayFormat = "速度: {0:0.0}";

    [Tooltip("速度数值刷新间隔。数值越小刷新越及时。")]
    public float refreshInterval = 0.05f;

    private float nextRefreshTime;

    /// <summary>
    /// 自动补齐玩家刚体引用。
    /// </summary>
    void Awake()
    {
        if (playerRigidbody == null)
        {
            OdmController controller = FindAnyObjectByType<OdmController>();
            if (controller != null)
                playerRigidbody = controller.GetComponent<Rigidbody2D>();
        }
    }

    /// <summary>
    /// 按固定间隔刷新速度文本。
    /// </summary>
    void Update()
    {
        if (speedText == null || playerRigidbody == null)
            return;

        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.01f, refreshInterval);
        speedText.text = string.Format(displayFormat, playerRigidbody.linearVelocity.magnitude);
    }
}

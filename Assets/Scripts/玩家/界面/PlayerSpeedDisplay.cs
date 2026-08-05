using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在屏幕上显示玩家当前 Rigidbody2D 速度，方便调试不同 ODM 状态下的速度变化。
/// </summary>
[DisallowMultipleComponent]
public class PlayerSpeedDisplay : MonoBehaviour
{
    [Header("绑定")]
    [Tooltip("要读取速度的玩家控制器。为空时会自动优先查找场景中的 Player 2。")]
    public OdmController controller;

    [Tooltip("要读取速度的玩家刚体。为空时会从 controller 或场景中的 OdmController 自动补齐。")]
    public Rigidbody2D playerRigidbody;

    [Tooltip("用于显示速度的 UI Text。为空时会自动使用当前物体上的 Text。")]
    public Text speedText;

    [Header("显示")]
    [Tooltip("刷新间隔，单位秒。数值越小刷新越及时。")]
    public float refreshInterval = 0.05f;

    [Tooltip("开启后显示 X/Y 速度分量。")]
    public bool showVelocityComponents = true;

    [Tooltip("开启后显示当前 OdmController 状态。")]
    public bool showOdmState = true;

    [Tooltip("开启后显示当前速度占 OdmController.maxSpeed 的比例。")]
    public bool showMaxSpeedRatio = true;

    [Tooltip("没有找到玩家刚体时是否隐藏文本。")]
    public bool hideWhenNoTarget = false;

    private float nextRefreshTime;

    private void Awake()
    {
        BindMissingReferences();
        RefreshText();
    }

    private void OnEnable()
    {
        BindMissingReferences();
        RefreshText();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.01f, refreshInterval);
        RefreshText();
    }

    [ContextMenu("立即刷新速度显示")]
    public void RefreshText()
    {
        BindMissingReferences();

        bool hasTarget = playerRigidbody != null;
        if (speedText != null && hideWhenNoTarget)
            speedText.enabled = hasTarget;

        if (speedText == null || !hasTarget)
            return;

        Vector2 velocity = playerRigidbody.linearVelocity;
        float speed = velocity.magnitude;
        string text = $"速度 {speed:0.0}";

        if (showVelocityComponents)
            text += $"\nX {velocity.x:0.0} / Y {velocity.y:0.0}";

        if (showOdmState && controller != null)
            text += $"\n状态 {controller.currentState}";

        if (showMaxSpeedRatio && controller != null && controller.maxSpeed > 0f)
            text += $"\n上限 {speed / controller.maxSpeed:0%}";

        speedText.text = text;
    }

    private void BindMissingReferences()
    {
        if (speedText == null)
            speedText = GetComponent<Text>();

        if (controller == null)
            controller = FindBestController();

        if (playerRigidbody == null && controller != null)
            playerRigidbody = controller.GetComponent<Rigidbody2D>();
    }

    private OdmController FindBestController()
    {
        OdmController[] controllers = FindObjectsByType<OdmController>(FindObjectsSortMode.None);
        OdmController best = null;

        for (int i = 0; i < controllers.Length; i++)
        {
            OdmController candidate = controllers[i];
            if (candidate == null)
                continue;

            if (best == null || IsPreferredController(candidate, best))
                best = candidate;
        }

        return best;
    }

    private bool IsPreferredController(OdmController candidate, OdmController current)
    {
        bool candidateIsPlayer2 = candidate.name.Contains("Player 2");
        bool currentIsPlayer2 = current != null && current.name.Contains("Player 2");
        if (candidateIsPlayer2 != currentIsPlayer2)
            return candidateIsPlayer2;

        return current == null;
    }
}

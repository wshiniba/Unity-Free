using UnityEngine;

/// <summary>
/// 控制绳索追踪摄像机和对应 UI 小窗口，只负责显示远距离绳索目标，不创建任何场景对象。
/// </summary>
public class RopeCameraViewController : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("玩家身上的 OdmController，用于读取绳索飞行点和连接点。")]
    public OdmController odmController;

    [Tooltip("专门渲染绳索目标的小窗口摄像机。请手动创建 Camera 并把 Target Texture 指向你的 Render Texture。")]
    public Camera ropeCamera;

    [Tooltip("小窗口 UI 根物体。可以拖 Raw Image 自身，也可以拖包含边框和 Raw Image 的父物体。")]
    public GameObject viewRoot;

    [Header("显示条件")]
    [Tooltip("绳索目标距离玩家超过该距离时，才显示小窗口。")]
    public float showDistance = 12f;

    [Tooltip("左右绳索同时存在时，优先显示距离玩家更远的绳索目标。关闭后优先显示正在飞行的绳索，其次左绳，再其次右绳。")]
    public bool preferFartherRope = true;

    [Tooltip("开启后，绳索已连接到远距离目标时也会显示小窗口；关闭后只在绳索飞行过程中显示。")]
    public bool showAnchoredRope = true;

    [Header("摄像机")]
    [Tooltip("小窗口摄像机的正交视野大小。数值越大，小窗口能看到的范围越大。")]
    public float cameraOrthographicSize = 5f;

    [Tooltip("小窗口摄像机的 Z 轴位置。2D 项目通常使用 -10。")]
    public float cameraZ = -10f;

    [Tooltip("摄像机跟随绳索目标的平滑时间。设为 0 时立即跟随。")]
    public float followSmoothTime = 0.08f;

    [Tooltip("沿绳索发射方向额外前看一点距离，让小窗口更容易看到即将连接的对象。")]
    public float lookAheadDistance = 2f;

    private Vector3 followVelocity;

    /// <summary>
    /// 初始化时先隐藏窗口，避免未绑定绳索目标时显示空画面。
    /// </summary>
    private void Awake()
    {
        SetViewVisible(false);
    }

    /// <summary>
    /// 每帧根据当前绳索目标刷新摄像机位置和 UI 显示状态。
    /// </summary>
    private void LateUpdate()
    {
        if (odmController == null || ropeCamera == null || viewRoot == null)
        {
            SetViewVisible(false);
            return;
        }

        if (!TryGetViewTarget(out Vector2 target, out Vector2 aimDirection, out float distance))
        {
            SetViewVisible(false);
            return;
        }

        bool shouldShow = distance >= showDistance;
        SetViewVisible(shouldShow);
        ropeCamera.enabled = shouldShow;

        if (!shouldShow)
            return;

        ropeCamera.orthographic = true;
        ropeCamera.orthographicSize = Mathf.Max(0.1f, cameraOrthographicSize);

        Vector2 cameraTarget = target + aimDirection * lookAheadDistance;
        Vector3 desiredPosition = new Vector3(cameraTarget.x, cameraTarget.y, cameraZ);

        if (followSmoothTime <= 0f)
        {
            ropeCamera.transform.position = desiredPosition;
        }
        else
        {
            ropeCamera.transform.position = Vector3.SmoothDamp(
                ropeCamera.transform.position,
                desiredPosition,
                ref followVelocity,
                followSmoothTime);
        }
    }

    /// <summary>
    /// 从左右绳索中选择当前最需要被小窗口显示的目标。
    /// </summary>
    private bool TryGetViewTarget(out Vector2 target, out Vector2 aimDirection, out float distance)
    {
        RopeTarget left = GetRopeTarget(true);
        RopeTarget right = GetRopeTarget(false);

        bool hasLeft = left.isValid;
        bool hasRight = right.isValid;

        if (!hasLeft && !hasRight)
        {
            target = Vector2.zero;
            aimDirection = Vector2.right;
            distance = 0f;
            return false;
        }

        RopeTarget selected;
        if (hasLeft && hasRight)
            selected = SelectTarget(left, right);
        else
            selected = hasLeft ? left : right;

        target = selected.position;
        aimDirection = selected.direction;
        distance = selected.distance;
        return true;
    }

    /// <summary>
    /// 根据设置在左右绳索目标之间做优先级选择。
    /// </summary>
    private RopeTarget SelectTarget(RopeTarget left, RopeTarget right)
    {
        if (preferFartherRope)
            return left.distance >= right.distance ? left : right;

        if (left.isFlying != right.isFlying)
            return left.isFlying ? left : right;

        return left;
    }

    /// <summary>
    /// 读取单侧绳索当前的飞行点或连接点。
    /// </summary>
    private RopeTarget GetRopeTarget(bool isLeft)
    {
        bool isFlying = isLeft ? odmController.IsLeftFlying : odmController.IsRightFlying;
        bool isAnchored = isLeft ? odmController.IsLeftAnchored : odmController.IsRightAnchored;

        if (!isFlying && (!showAnchoredRope || !isAnchored))
            return RopeTarget.Invalid;

        Vector2 playerPosition = odmController.Rb != null
            ? odmController.Rb.position
            : (Vector2)odmController.transform.position;

        Vector2 position = isFlying
            ? isLeft ? odmController.LeftFlyTip : odmController.RightFlyTip
            : isLeft ? odmController.LeftAnchorPos : odmController.RightAnchorPos;

        Vector2 toTarget = position - playerPosition;
        Vector2 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.right;

        return new RopeTarget(true, isFlying, position, direction, toTarget.magnitude);
    }

    /// <summary>
    /// 统一控制 UI 和摄像机开关，避免窗口隐藏时摄像机还在渲染。
    /// </summary>
    private void SetViewVisible(bool visible)
    {
        if (viewRoot != null && viewRoot.activeSelf != visible)
            viewRoot.SetActive(visible);

        if (ropeCamera != null && ropeCamera.enabled != visible)
            ropeCamera.enabled = visible;
    }

    private readonly struct RopeTarget
    {
        public static readonly RopeTarget Invalid = new RopeTarget(false, false, Vector2.zero, Vector2.right, 0f);

        public readonly bool isValid;
        public readonly bool isFlying;
        public readonly Vector2 position;
        public readonly Vector2 direction;
        public readonly float distance;

        public RopeTarget(bool isValid, bool isFlying, Vector2 position, Vector2 direction, float distance)
        {
            this.isValid = isValid;
            this.isFlying = isFlying;
            this.position = position;
            this.direction = direction;
            this.distance = distance;
        }
    }
}

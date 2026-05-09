using Cinemachine;
using UnityEngine;

/// <summary>
/// 更新 ODM 绳索 LineRenderer 和基于速度的视觉效果。
/// </summary>
[RequireComponent(typeof(OdmController))]
public class OdmVisuals : MonoBehaviour
{
    [Header("绳索渲染器")]
    [Tooltip("用于绘制左侧绳索路径的 LineRenderer。")]
    public LineRenderer leftCableRenderer;

    [Tooltip("用于绘制右侧绳索路径的 LineRenderer。")]
    public LineRenderer rightCableRenderer;

    [Header("相机")]
    [Tooltip("视野会随玩家速度变化的 Cinemachine 虚拟相机。未指定时会自动查找场景中的虚拟相机。")]
    public CinemachineVirtualCamera virtualCamera;

    [Tooltip("未使用 Cinemachine 时的后备正交相机。未指定时使用 Camera.main。")]
    public Camera mainCamera;

    [Tooltip("玩家低速移动时的正交相机尺寸。数值越大，画面看起来越远。")]
    public float baseOrthographicSize = 5f;

    [Tooltip("玩家接近最大速度时的正交相机尺寸。")]
    public float maxOrthographicSize = 8f;

    [Tooltip("相机尺寸向目标尺寸过渡的速度。")]
    public float cameraSizeChangeSpeed = 5f;

    [Header("鼠标预瞄")]
    [Tooltip("启用后，鼠标靠近屏幕边缘时会让 Cinemachine 相机按鼠标方向预瞄。")]
    public bool enableMouseLookAhead = true;

    [Tooltip("鼠标到达屏幕最左或最右时，相机水平偏移的最大世界单位距离。")]
    public float horizontalLookAheadDistance = 3f;

    [Tooltip("鼠标到达屏幕最上或最下时，相机垂直偏移的最大世界单位距离。")]
    public float verticalLookAheadDistance = 2f;

    [Tooltip("鼠标靠近屏幕中心时不触发预瞄的范围，0 表示无死区，0.5 表示半屏死区。")]
    [Range(0f, 0.49f)]
    public float lookAheadDeadZone = 0.15f;

    [Tooltip("相机预瞄过渡到目标偏移所需的平滑时间。")]
    public float lookAheadSmoothTime = 0.18f;

    [Header("速度效果")]
    [Tooltip("可选速度线粒子效果，玩家速度接近 maxSpeed 时启用。")]
    public ParticleSystem speedLinesEffect;

    // 缓存玩家刚体，用于读取移动速度。
    private Rigidbody2D rb;

    // 缓存 ODM 控制器，用于获取绳索路径和 maxSpeed。
    private OdmController controller;

    private CinemachineFramingTransposer framingTransposer;
    private Vector3 baseTrackedObjectOffset;
    private Vector2 currentLookAheadOffset;
    private Vector2 lookAheadVelocity;

    /// <summary>
    /// 缓存引用，并在未指定相机时使用主相机。
    /// </summary>
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        controller = GetComponent<OdmController>();
        if (virtualCamera == null) virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        if (mainCamera == null) mainCamera = Camera.main;

        CacheCameraBody();
    }

    /// <summary>
    /// 每帧刷新绳索线条和基于速度的视觉效果。
    /// </summary>
    void Update()
    {
        DrawCable(leftCableRenderer, controller.GetCablePath(true));
        DrawCable(rightCableRenderer, controller.GetCablePath(false));
        UpdateCameraSize();
        UpdateMouseLookAhead();
        UpdateSpeedLines();
    }

    /// <summary>
    /// 根据传入路径绘制或隐藏绳索 LineRenderer。
    /// </summary>
    /// <param name="line">需要更新的 LineRenderer。</param>
    /// <param name="path">世界坐标下的绳索路径。为 null 时隐藏渲染器。</param>
    private void DrawCable(LineRenderer line, Vector3[] path)
    {
        if (line == null) return;

        line.enabled = path != null && path.Length >= 2;
        if (!line.enabled) return;

        line.positionCount = path.Length;
        line.SetPositions(path);
    }

    /// <summary>
    /// 根据当前玩家速度平滑调整正交相机尺寸。
    /// </summary>
    private void UpdateCameraSize()
    {
        if (rb == null || controller == null) return;

        float speedRatio = Mathf.Clamp01(rb.linearVelocity.magnitude / Mathf.Max(controller.maxSpeed, 0.001f));
        float targetSize = Mathf.Lerp(baseOrthographicSize, maxOrthographicSize, speedRatio);

        if (virtualCamera != null)
        {
            LensSettings lens = virtualCamera.m_Lens;
            lens.OrthographicSize = Mathf.Lerp(lens.OrthographicSize, targetSize, Time.deltaTime * cameraSizeChangeSpeed);
            virtualCamera.m_Lens = lens;
            return;
        }

        if (mainCamera != null)
            mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, targetSize, Time.deltaTime * cameraSizeChangeSpeed);
    }

    /// <summary>
    /// 根据鼠标在屏幕中的位置平滑调整 Cinemachine 跟随偏移。
    /// </summary>
    private void UpdateMouseLookAhead()
    {
        if (framingTransposer == null) return;

        Vector2 targetOffset = enableMouseLookAhead ? GetTargetLookAheadOffset() : Vector2.zero;
        float smoothTime = Mathf.Max(0.001f, lookAheadSmoothTime);
        currentLookAheadOffset.x = Mathf.SmoothDamp(currentLookAheadOffset.x, targetOffset.x, ref lookAheadVelocity.x, smoothTime);
        currentLookAheadOffset.y = Mathf.SmoothDamp(currentLookAheadOffset.y, targetOffset.y, ref lookAheadVelocity.y, smoothTime);

        Vector3 offset = baseTrackedObjectOffset;
        offset.x += currentLookAheadOffset.x;
        offset.y += currentLookAheadOffset.y;
        framingTransposer.m_TrackedObjectOffset = offset;
    }

    private Vector2 GetTargetLookAheadOffset()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return Vector2.zero;

        float centeredMouseX = Mathf.Clamp01(Input.mousePosition.x / Screen.width) * 2f - 1f;
        float centeredMouseY = Mathf.Clamp01(Input.mousePosition.y / Screen.height) * 2f - 1f;
        return new Vector2(
            GetLookAheadAxisOffset(centeredMouseX, horizontalLookAheadDistance),
            GetLookAheadAxisOffset(centeredMouseY, verticalLookAheadDistance));
    }

    private float GetLookAheadAxisOffset(float centeredMouseAxis, float maxDistance)
    {
        float absAxis = Mathf.Abs(centeredMouseAxis);
        if (absAxis <= lookAheadDeadZone) return 0f;

        float activeRange = 1f - lookAheadDeadZone;
        float strength = Mathf.Clamp01((absAxis - lookAheadDeadZone) / activeRange);
        return Mathf.Sign(centeredMouseAxis) * maxDistance * strength;
    }

    private void CacheCameraBody()
    {
        framingTransposer = null;

        if (virtualCamera == null) return;

        framingTransposer = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (framingTransposer == null) return;

        baseTrackedObjectOffset = framingTransposer.m_TrackedObjectOffset;
        currentLookAheadOffset = Vector2.zero;
        lookAheadVelocity = Vector2.zero;
    }

    /// <summary>
    /// 玩家接近最大速度时启用速度线粒子发射。
    /// </summary>
    private void UpdateSpeedLines()
    {
        if (speedLinesEffect == null || rb == null || controller == null) return;

        var emission = speedLinesEffect.emission;
        emission.enabled = rb.linearVelocity.magnitude > controller.maxSpeed * 0.8f;
    }
}

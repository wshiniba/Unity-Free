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

    [Header("绳索发射波浪")]
    [Tooltip("开启后，绳索飞行途中会用多段波浪线显示，模拟锚头带出绳索的甩动感。只影响视觉，不影响钩锁判定。")]
    public bool enableShootWave = true;

    [Tooltip("发射波浪线使用的分段数。数值越高越平滑，但 LineRenderer 点数也越多。")]
    [Range(4, 32)]
    public int shootWaveSegments = 16;

    [Tooltip("绳索刚发射时的主波浪最大幅度。")]
    public float shootWaveAmplitude = 0.9f;

    [Tooltip("主波浪在整条绳索上的重复次数。")]
    public float shootWaveFrequency = 2.2f;

    [Tooltip("波浪沿绳索滚动的速度。")]
    public float shootWaveSpeed = 16f;

    [Tooltip("绳头飞出多少世界单位后，波浪逐渐衰减为直线。")]
    public float shootWaveFadeDistance = 20f;

    [Tooltip("叠加在主波浪上的小幅细波，用于模拟 2D 螺旋感。")]
    public float shootWaveSecondaryAmplitude = 0.25f;

    [Tooltip("细波在整条绳索上的重复次数。")]
    public float shootWaveSecondaryFrequency = 6f;

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
    private Vector3[] leftShootWavePoints;
    private Vector3[] rightShootWavePoints;

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
        DrawCable(leftCableRenderer, controller.GetCablePath(true), true);
        DrawCable(rightCableRenderer, controller.GetCablePath(false), false);
        UpdateCameraSize();
        UpdateMouseLookAhead();
        UpdateSpeedLines();
    }

    /// <summary>
    /// 根据传入路径绘制或隐藏绳索 LineRenderer。
    /// </summary>
    /// <param name="line">需要更新的 LineRenderer。</param>
    /// <param name="path">世界坐标下的绳索路径。为 null 时隐藏渲染器。</param>
    /// <param name="isLeft">是否为左侧绳索。</param>
    private void DrawCable(LineRenderer line, Vector3[] path, bool isLeft)
    {
        if (line == null) return;

        line.enabled = path != null && path.Length >= 2;
        if (!line.enabled) return;

        if (ShouldDrawShootWave(isLeft, path))
        {
            Vector3[] wavePath = BuildShootWavePath(path[0], path[path.Length - 1], isLeft);
            line.positionCount = wavePath.Length;
            line.SetPositions(wavePath);
            return;
        }

        line.positionCount = path.Length;
        line.SetPositions(path);
    }

    /// <summary>
    /// 判断当前绳索是否处于需要波浪显示的飞行阶段。
    /// </summary>
    private bool ShouldDrawShootWave(bool isLeft, Vector3[] path)
    {
        if (!enableShootWave || controller == null || path == null || path.Length < 2)
            return false;

        return isLeft ? controller.IsLeftFlying : controller.IsRightFlying;
    }

    /// <summary>
    /// 根据起点和绳头位置生成 2D 波浪路径，表现绳索刚发射时的甩动和螺旋感。
    /// </summary>
    private Vector3[] BuildShootWavePath(Vector3 start, Vector3 tip, bool isLeft)
    {
        int pointCount = Mathf.Max(2, shootWaveSegments + 1);
        Vector3[] points = GetShootWaveBuffer(isLeft, pointCount);

        Vector2 start2D = start;
        Vector2 tip2D = tip;
        Vector2 direction = tip2D - start2D;
        float distance = direction.magnitude;

        if (distance < 0.001f)
        {
            points[0] = start;
            points[pointCount - 1] = tip;
            return points;
        }

        Vector2 dir = direction / distance;
        Vector2 normal = new Vector2(-dir.y, dir.x);
        float fadeDistance = Mathf.Max(0.001f, shootWaveFadeDistance);
        float fade = 1f - Mathf.Clamp01(distance / fadeDistance);
        float timePhase = Time.time * shootWaveSpeed;

        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            Vector2 straightPoint = Vector2.Lerp(start2D, tip2D, t);
            float envelope = Mathf.Sin(t * Mathf.PI);
            float mainWave = Mathf.Sin(t * Mathf.PI * 2f * shootWaveFrequency + timePhase) * shootWaveAmplitude;
            float detailWave = Mathf.Sin(t * Mathf.PI * 2f * shootWaveSecondaryFrequency - timePhase * 1.35f) * shootWaveSecondaryAmplitude;
            Vector2 wavePoint = straightPoint + normal * ((mainWave + detailWave) * envelope * fade);

            points[i] = new Vector3(wavePoint.x, wavePoint.y, Mathf.Lerp(start.z, tip.z, t));
        }

        points[0] = start;
        points[pointCount - 1] = tip;
        return points;
    }

    private Vector3[] GetShootWaveBuffer(bool isLeft, int pointCount)
    {
        Vector3[] buffer = isLeft ? leftShootWavePoints : rightShootWavePoints;
        if (buffer == null || buffer.Length != pointCount)
            buffer = new Vector3[pointCount];

        if (isLeft) leftShootWavePoints = buffer;
        else rightShootWavePoints = buffer;

        return buffer;
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

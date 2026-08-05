using Cinemachine;
using UnityEngine;

/// <summary>
/// 更新 ODM 绳索 LineRenderer 和基于速度的视觉效果。
/// </summary>
[RequireComponent(typeof(OdmController))]
public class OdmVisuals : MonoBehaviour
{
    private const bool EnableElasticCable = true;
    private const int ElasticCableSegments = 18;
    private const bool ElasticAffectsTerrainAnchor = false;
    private const float VisualCollisionSkin = 0.03f;

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

    [Header("触手弹性视觉")]
    [Tooltip("触手中段弹性弯曲允许偏离直线的最大距离。")]
    public float elasticMaxLagDistance = 2.5f;

    [Tooltip("触手末端横向速度转换为中段弯曲幅度的倍率。")]
    public float elasticBendVelocityScale = 0.04f;

    [Tooltip("触手中段弯曲追随目标弯曲量的平滑时间。")]
    public float elasticBendSmoothTime = 0.08f;

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
    private Vector3[] leftElasticPoints;
    private Vector3[] rightElasticPoints;
    private Vector2 leftElasticBend;
    private Vector2 rightElasticBend;
    private Vector2 leftElasticBendVelocity;
    private Vector2 rightElasticBendVelocity;
    private Vector3 leftPreviousTip;
    private Vector3 rightPreviousTip;
    private bool leftElasticInitialized;
    private bool rightElasticInitialized;

    /// <summary>
    /// 缓存引用，并在未指定相机时使用主相机。
    /// </summary>
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        controller = GetComponent<OdmController>();
        if (virtualCamera == null) virtualCamera = FindAnyObjectByType<CinemachineVirtualCamera>();
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
        if (!line.enabled)
        {
            ResetElasticState(isLeft);
            return;
        }

        Vector3[] drawPath = ShouldDrawElasticCable(isLeft, path)
            ? BuildElasticCablePath(path, isLeft)
            : path;

        if (ShouldDrawShootWave(isLeft, drawPath))
        {
            Vector3[] wavePath = BuildShootWavePath(drawPath, isLeft);
            line.positionCount = wavePath.Length;
            line.SetPositions(wavePath);
            return;
        }

        line.positionCount = drawPath.Length;
        line.SetPositions(drawPath);
    }

    private bool ShouldDrawElasticCable(bool isLeft, Vector3[] path)
    {
        if (!EnableElasticCable || controller == null || path == null || path.Length != 2)
            return false;

        bool isFlying = isLeft ? controller.IsLeftFlying : controller.IsRightFlying;
        bool isAnchored = isLeft ? controller.IsLeftAnchored : controller.IsRightAnchored;
        bool isEnemyAnchor = isLeft ? controller.IsLeftEnemyAnchored : controller.IsRightEnemyAnchored;
        return isFlying || isEnemyAnchor || (ElasticAffectsTerrainAnchor && isAnchored);
    }

    private Vector3[] BuildElasticCablePath(Vector3[] path, bool isLeft)
    {
        int pointCount = Mathf.Max(2, ElasticCableSegments + 1);
        Vector3[] points = GetElasticPointBuffer(isLeft, pointCount);
        bool initialized = isLeft ? leftElasticInitialized : rightElasticInitialized;
        Vector3 start = path[0];
        Vector3 tip = path[path.Length - 1];
        Vector2 cable = tip - start;
        float cableLength = cable.magnitude;

        if (!initialized || cableLength < 0.001f)
        {
            ResetElasticState(isLeft);
            SetPreviousTip(isLeft, tip);
            for (int i = 0; i < pointCount; i++)
                points[i] = SamplePath(path, i / (float)(pointCount - 1));

            if (isLeft) leftElasticInitialized = true;
            else rightElasticInitialized = true;

            return points;
        }

        Vector2 previousTip = GetPreviousTip(isLeft);
        Vector2 tipVelocity = ((Vector2)tip - previousTip) / Mathf.Max(Time.deltaTime, 0.0001f);
        Vector2 direction = cable / cableLength;
        Vector2 lateralVelocity = tipVelocity - direction * Vector2.Dot(tipVelocity, direction);
        Vector2 targetBend = -lateralVelocity * Mathf.Max(0f, elasticBendVelocityScale);
        float maxLag = Mathf.Max(0f, elasticMaxLagDistance);
        if (maxLag > 0f && targetBend.magnitude > maxLag)
            targetBend = targetBend.normalized * maxLag;

        Vector2 bend = GetElasticBend(isLeft);
        Vector2 bendVelocity = GetElasticBendVelocity(isLeft);
        bend = Vector2.SmoothDamp(
            bend,
            targetBend,
            ref bendVelocity,
            Mathf.Max(0.001f, elasticBendSmoothTime));

        SetElasticBend(isLeft, bend);
        SetElasticBendVelocity(isLeft, bendVelocity);
        SetPreviousTip(isLeft, tip);

        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            Vector3 basePoint = SamplePath(path, t);
            float envelope = Mathf.Sin(t * Mathf.PI);
            Vector3 candidate = basePoint + (Vector3)(bend * envelope);
            points[i] = ConstrainVisualPoint(basePoint, candidate);
        }

        points[0] = path[0];
        points[pointCount - 1] = path[path.Length - 1];
        ConstrainVisualSegments(points);

        return points;
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
    private Vector3[] BuildShootWavePath(Vector3[] path, bool isLeft)
    {
        int pointCount = Mathf.Max(2, shootWaveSegments + 1);
        Vector3[] points = GetShootWaveBuffer(isLeft, pointCount);

        float distance = GetPathLength(path);

        if (distance < 0.001f)
        {
            points[0] = path[0];
            points[pointCount - 1] = path[path.Length - 1];
            return points;
        }

        float fadeDistance = Mathf.Max(0.001f, shootWaveFadeDistance);
        float fade = 1f - Mathf.Clamp01(distance / fadeDistance);
        float timePhase = Time.time * shootWaveSpeed;

        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            Vector3 basePoint = SamplePath(path, t);
            Vector3 before = SamplePath(path, Mathf.Clamp01(t - 0.01f));
            Vector3 after = SamplePath(path, Mathf.Clamp01(t + 0.01f));
            Vector2 tangent = after - before;
            Vector2 dir = tangent.sqrMagnitude > 0.000001f ? tangent.normalized : Vector2.right;
            Vector2 normal = new Vector2(-dir.y, dir.x);
            float envelope = Mathf.Sin(t * Mathf.PI);
            float mainWave = Mathf.Sin(t * Mathf.PI * 2f * shootWaveFrequency + timePhase) * shootWaveAmplitude;
            float detailWave = Mathf.Sin(t * Mathf.PI * 2f * shootWaveSecondaryFrequency - timePhase * 1.35f) * shootWaveSecondaryAmplitude;
            Vector2 wavePoint = (Vector2)basePoint + normal * ((mainWave + detailWave) * envelope * fade);
            Vector3 candidate = new Vector3(wavePoint.x, wavePoint.y, basePoint.z);

            points[i] = ConstrainVisualPoint(basePoint, candidate);
        }

        points[0] = path[0];
        points[pointCount - 1] = path[path.Length - 1];
        ConstrainVisualSegments(points);
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

    private Vector3 ConstrainVisualPoint(Vector3 basePoint, Vector3 candidate)
    {
        if (controller == null)
            return candidate;

        Vector2 from = basePoint;
        Vector2 to = candidate;
        if ((to - from).sqrMagnitude < 0.000001f)
            return candidate;

        RaycastHit2D hit = Physics2D.Linecast(from, to, controller.CableSolidMask);
        if (hit.collider == null)
            return candidate;

        Vector2 safePoint = hit.point + hit.normal * VisualCollisionSkin;
        return new Vector3(safePoint.x, safePoint.y, candidate.z);
    }

    private void ConstrainVisualSegments(Vector3[] points)
    {
        if (controller == null || points == null || points.Length < 2)
            return;

        LayerMask mask = controller.CableSolidMask;
        for (int i = 1; i < points.Length; i++)
        {
            RaycastHit2D hit = Physics2D.Linecast(points[i - 1], points[i], mask);
            if (hit.collider == null)
                continue;

            Vector2 safePoint = hit.point + hit.normal * VisualCollisionSkin;
            Vector3 constrained = new Vector3(safePoint.x, safePoint.y, points[i].z);
            if (i < points.Length - 1)
                points[i] = constrained;
            else
                points[i - 1] = constrained;
        }
    }

    private Vector3[] GetElasticPointBuffer(bool isLeft, int pointCount)
    {
        Vector3[] buffer = isLeft ? leftElasticPoints : rightElasticPoints;
        if (buffer == null || buffer.Length != pointCount)
        {
            buffer = new Vector3[pointCount];
            if (isLeft) leftElasticInitialized = false;
            else rightElasticInitialized = false;
        }

        if (isLeft) leftElasticPoints = buffer;
        else rightElasticPoints = buffer;

        return buffer;
    }

    private void ResetElasticState(bool isLeft)
    {
        if (isLeft)
        {
            leftElasticInitialized = false;
            leftElasticBend = Vector2.zero;
            leftElasticBendVelocity = Vector2.zero;
        }
        else
        {
            rightElasticInitialized = false;
            rightElasticBend = Vector2.zero;
            rightElasticBendVelocity = Vector2.zero;
        }
    }

    private Vector2 GetElasticBend(bool isLeft)
    {
        return isLeft ? leftElasticBend : rightElasticBend;
    }

    private void SetElasticBend(bool isLeft, Vector2 value)
    {
        if (isLeft) leftElasticBend = value;
        else rightElasticBend = value;
    }

    private Vector2 GetElasticBendVelocity(bool isLeft)
    {
        return isLeft ? leftElasticBendVelocity : rightElasticBendVelocity;
    }

    private void SetElasticBendVelocity(bool isLeft, Vector2 value)
    {
        if (isLeft) leftElasticBendVelocity = value;
        else rightElasticBendVelocity = value;
    }

    private Vector3 GetPreviousTip(bool isLeft)
    {
        return isLeft ? leftPreviousTip : rightPreviousTip;
    }

    private void SetPreviousTip(bool isLeft, Vector3 value)
    {
        if (isLeft) leftPreviousTip = value;
        else rightPreviousTip = value;
    }

    private Vector3 SamplePath(Vector3[] path, float t)
    {
        if (path == null || path.Length == 0) return Vector3.zero;
        if (path.Length == 1) return path[0];

        float totalLength = GetPathLength(path);
        if (totalLength < 0.001f) return path[path.Length - 1];

        float targetDistance = Mathf.Clamp01(t) * totalLength;
        float walked = 0f;

        for (int i = 1; i < path.Length; i++)
        {
            Vector3 previous = path[i - 1];
            Vector3 current = path[i];
            float segmentLength = Vector3.Distance(previous, current);
            if (segmentLength < 0.001f) continue;

            if (walked + segmentLength >= targetDistance)
            {
                float segmentT = (targetDistance - walked) / segmentLength;
                return Vector3.Lerp(previous, current, segmentT);
            }

            walked += segmentLength;
        }

        return path[path.Length - 1];
    }

    private float GetPathLength(Vector3[] path)
    {
        if (path == null || path.Length < 2) return 0f;

        float length = 0f;
        for (int i = 1; i < path.Length; i++)
            length += Vector3.Distance(path[i - 1], path[i]);

        return length;
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

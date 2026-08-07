using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class JumpReachabilityWindow : EditorWindow
{
    private const float DefaultPreviewSeconds = 4f;
    private const float MinimumDeltaTime = 0.001f;
    private const float HandlePickSize = 0.12f;

    [SerializeField] private OdmController controller;
    [SerializeField] private bool autoUseSelection = true;
    [SerializeField] private bool drawInScene = true;
    [SerializeField] private bool pointsAreGapEdges = true;
    [SerializeField] private bool snapPointsToPlatformEdges = true;
    [SerializeField] private bool showEdgeSnapSearchRadius = true;
    [SerializeField] private bool showComfortLine = true;
    [SerializeField] private bool showMaxLine = true;
    [SerializeField] private bool useFallSpeedClamp = true;
    [SerializeField] private bool includeCoyoteTime = true;
    [SerializeField] private float edgeSnapSearchRadius = 2f;
    [SerializeField] private float edgeContactRatio = 1f;
    [SerializeField] private float coyoteTimeRatio = 1f;
    [SerializeField] private float comfortHorizontalRatio = 0.85f;
    [SerializeField] private float previewSeconds = DefaultPreviewSeconds;
    [SerializeField] private Vector2 startPoint;
    [SerializeField] private Vector2 targetPoint = new Vector2(10f, 0f);
    [SerializeField] private bool hasStartPoint;
    [SerializeField] private bool hasTargetPoint = true;

    private readonly List<TrajectorySample> maxTrajectory = new List<TrajectorySample>(256);
    private readonly List<TrajectorySample> comfortTrajectory = new List<TrajectorySample>(256);
    private readonly List<TrajectorySample> coyoteProbeTrajectory = new List<TrajectorySample>(256);
    private JumpMetrics metrics;
    private ReachResult targetReach;
    private ReachResult comfortReach;

    [MenuItem("Window/Free Tools/Jump Reachability")]
    public static void Open()
    {
        GetWindow<JumpReachabilityWindow>("Jump Reachability");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        TryUseSelectedController();
        EnsureStartFromController();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnSelectionChange()
    {
        if (autoUseSelection && TryUseSelectedController())
            RepaintSceneViews();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("跳跃可达性预览", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        controller = (OdmController)EditorGUILayout.ObjectField("玩家控制器", controller, typeof(OdmController), true);
        autoUseSelection = EditorGUILayout.Toggle("自动使用选中玩家", autoUseSelection);
        drawInScene = EditorGUILayout.Toggle("Scene 视图绘制", drawInScene);
        pointsAreGapEdges = EditorGUILayout.Toggle("起终点是坑边", pointsAreGapEdges);
        using (new EditorGUI.DisabledScope(!pointsAreGapEdges))
        {
            snapPointsToPlatformEdges = EditorGUILayout.Toggle("自动贴合平台边缘", snapPointsToPlatformEdges);
            using (new EditorGUI.DisabledScope(!snapPointsToPlatformEdges))
            {
                edgeSnapSearchRadius = EditorGUILayout.Slider("贴合搜索半径", edgeSnapSearchRadius, 0.1f, 10f);
                showEdgeSnapSearchRadius = EditorGUILayout.Toggle("显示贴合搜索范围", showEdgeSnapSearchRadius);
            }
        }
        showMaxLine = EditorGUILayout.Toggle("显示极限轨迹", showMaxLine);
        showComfortLine = EditorGUILayout.Toggle("显示舒适轨迹", showComfortLine);
        useFallSpeedClamp = EditorGUILayout.Toggle("计算最大下落限速", useFallSpeedClamp);
        edgeContactRatio = EditorGUILayout.Slider("坑边碰撞体余量", edgeContactRatio, 0f, 1f);
        includeCoyoteTime = EditorGUILayout.Toggle("计入土狼时间极限", includeCoyoteTime);
        using (new EditorGUI.DisabledScope(!includeCoyoteTime))
            coyoteTimeRatio = EditorGUILayout.Slider("土狼时间使用比例", coyoteTimeRatio, 0f, 1f);
        comfortHorizontalRatio = EditorGUILayout.Slider("舒适水平比例", comfortHorizontalRatio, 0.5f, 1f);
        previewSeconds = EditorGUILayout.Slider("预览秒数", previewSeconds, 0.5f, 8f);
        startPoint = EditorGUILayout.Vector2Field("起点", startPoint);
        targetPoint = EditorGUILayout.Vector2Field("终点", targetPoint);

        if (EditorGUI.EndChangeCheck())
        {
            NormalizeToolSettings();
            SnapBothPointsToPlatformEdges();
            RepaintSceneViews();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("起点取玩家脚底"))
            {
                SetStartFromController();
                RepaintSceneViews();
            }

            if (GUILayout.Button("终点取玩家脚底"))
            {
                SetTargetFromController();
                RepaintSceneViews();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("交换起终点"))
            {
                (startPoint, targetPoint) = (targetPoint, startPoint);
                hasStartPoint = true;
                hasTargetPoint = true;
                RepaintSceneViews();
            }

            if (GUILayout.Button("重新贴合边缘"))
            {
                SnapBothPointsToPlatformEdges();
                Repaint();
                RepaintSceneViews();
            }

            if (GUILayout.Button("刷新 Scene"))
                RepaintSceneViews();
        }

        EditorGUILayout.Space(8f);
        DrawMetricsPanel();

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "Scene 操作：拖动绿色起点和蓝色终点手柄；Shift+左键设置起点；Ctrl+Shift+左键设置终点。\n" +
            "“自动贴合平台边缘”开启时，设置或拖动点会吸附到附近平台上边缘，并按跳跃方向选择面对坑的一侧边缘。\n" +
            "“起终点是坑边”开启时，把两点放在左右平台边缘，工具会计入玩家碰撞体边缘余量来判断可跨坑宽；土狼时间可作为极限余量单独开关。",
            MessageType.Info);
    }

    private void DrawMetricsPanel()
    {
        if (controller == null)
        {
            EditorGUILayout.HelpBox("选择场景中的 Player 2 或带 OdmController 的物体后开始预览。", MessageType.Warning);
            return;
        }

        metrics = ReadMetrics();
        UpdateTrajectories();

        EditorGUILayout.LabelField("当前参数", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("跳跃速度", metrics.jumpSpeed.ToString("0.###"));
        EditorGUILayout.LabelField("满按跳跃时间", metrics.fullJumpHoldTime.ToString("0.###"));
        EditorGUILayout.LabelField("水平速度", metrics.horizontalSpeed.ToString("0.###"));
        EditorGUILayout.LabelField("空中重力", metrics.airGravity.ToString("0.###"));
        EditorGUILayout.LabelField("玩家碰撞体宽度", metrics.colliderWidth.ToString("0.###"));
        EditorGUILayout.LabelField("坑边额外可跨距离", GetEdgeAllowance(metrics).ToString("0.###"));
        EditorGUILayout.LabelField("极限土狼额外距离", targetReach.coyoteDistanceGain.ToString("0.###"));

        float maxJumpHeight = GetMaxHeight(maxTrajectory);
        float maxVerticalFromMaxSpeed = EstimateVerticalApex(metrics.maxSpeed, metrics.ropeGravity, metrics.ropeDamping);
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("普通满按跳跃最大高度", maxJumpHeight.ToString("0.###"));
        EditorGUILayout.LabelField("最大速度垂直向上距离", maxVerticalFromMaxSpeed.ToString("0.###"));

        if (hasStartPoint && hasTargetPoint)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("当前起终点判断", EditorStyles.boldLabel);
            DrawReachLine("极限", targetReach);
            DrawReachLine("舒适", comfortReach);
        }
    }

    private static void DrawReachLine(string label, ReachResult result)
    {
        string state = result.isReachable
            ? result.difficultyRatio <= 0.85f ? "可达，舒适" : result.difficultyRatio <= 0.95f ? "可达，偏难" : "可达，极限"
            : "不可达";
        string allowance = result.edgeAllowance > 0f || result.coyoteDistanceGain > 0f
            ? $" / 中心 {result.centerReachDistance:0.###}"
            : string.Empty;
        EditorGUILayout.LabelField(label, $"{state} / 需要 {result.requiredDistance:0.###} / 可达 {result.reachableDistance:0.###}{allowance}");
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!drawInScene)
            return;

        if (autoUseSelection)
            TryUseSelectedController();

        if (controller == null)
            return;

        EnsureStartFromController();
        metrics = ReadMetrics();
        UpdateTrajectories();
        HandleSceneHotkeys();
        DrawSceneHandles();
        DrawSceneOverlay();
    }

    private bool TryUseSelectedController()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
            return false;

        OdmController selectedController = selected.GetComponent<OdmController>();
        if (selectedController == null)
            selectedController = selected.GetComponentInParent<OdmController>();

        if (selectedController == null || selectedController == controller)
            return false;

        controller = selectedController;
        SetStartFromController();
        return true;
    }

    private void EnsureStartFromController()
    {
        if (!hasStartPoint && controller != null)
            SetStartFromController();
    }

    private void SetStartFromController()
    {
        if (controller == null)
            return;

        startPoint = SnapPointToPlatformEdge(GetControllerFootPoint(controller), true);
        hasStartPoint = true;

        if (!hasTargetPoint)
        {
            targetPoint = startPoint + Vector2.right * 10f;
            hasTargetPoint = true;
        }
    }

    private void SetTargetFromController()
    {
        if (controller == null)
            return;

        targetPoint = SnapPointToPlatformEdge(GetControllerFootPoint(controller), false);
        hasTargetPoint = true;
    }

    private Vector2 GetControllerFootPoint(OdmController targetController)
    {
        Collider2D bodyCollider = targetController.GetComponent<Collider2D>();
        if (bodyCollider != null)
            return new Vector2(targetController.transform.position.x, bodyCollider.bounds.min.y);

        return targetController.transform.position;
    }

    private JumpMetrics ReadMetrics()
    {
        Collider2D bodyCollider = controller.GetComponent<Collider2D>();
        float colliderWidth = 1f;
        if (bodyCollider is CapsuleCollider2D capsule)
            colliderWidth = Mathf.Abs(capsule.size.x * controller.transform.lossyScale.x);
        else if (bodyCollider is BoxCollider2D box)
            colliderWidth = Mathf.Abs(box.size.x * controller.transform.lossyScale.x);
        else if (bodyCollider != null && bodyCollider.bounds.size.x > 0f)
            colliderWidth = bodyCollider.bounds.size.x;

        float fixedDeltaTime = Time.fixedDeltaTime > MinimumDeltaTime ? Time.fixedDeltaTime : 0.02f;
        float gravityMagnitude = Mathf.Abs(Physics2D.gravity.y);

        return new JumpMetrics
        {
            fixedDeltaTime = fixedDeltaTime,
            jumpSpeed = Mathf.Max(0f, controller.jumpSpeed),
            fullJumpHoldTime = Mathf.Max(0f, controller.fullJumpHoldTime),
            horizontalSpeed = Mathf.Max(0f, controller.groundMoveSpeed),
            maxSpeed = Mathf.Max(0f, controller.maxSpeed),
            airGravity = gravityMagnitude * Mathf.Max(0f, controller.dailyAirGravityScale),
            airDamping = Mathf.Max(0f, controller.dailyAirDamping),
            ropeGravity = gravityMagnitude * Mathf.Max(0f, controller.ropeMobilityGravityScale),
            ropeDamping = Mathf.Max(0f, controller.ropeMobilityDamping),
            maxFallSpeed = useFallSpeedClamp && controller.limitFallSpeed ? Mathf.Max(0f, controller.jumpSpeed) : 0f,
            colliderWidth = Mathf.Max(0f, colliderWidth),
            coyoteTime = Mathf.Max(0f, controller.coyoteTime),
        };
    }

    private void UpdateTrajectories()
    {
        if (!hasStartPoint || !hasTargetPoint)
            return;

        float direction = targetPoint.x >= startPoint.x ? 1f : -1f;
        SimulateJump(metrics, startPoint, direction, 1f, maxTrajectory);
        SimulateJump(metrics, startPoint, direction, comfortHorizontalRatio, comfortTrajectory);

        targetReach = EvaluateReach(maxTrajectory, 1f);
        comfortReach = EvaluateReach(comfortTrajectory, comfortHorizontalRatio);
    }

    private void SimulateJump(JumpMetrics currentMetrics, Vector2 origin, float direction, float horizontalRatio, List<TrajectorySample> output)
    {
        output.Clear();

        float dt = currentMetrics.fixedDeltaTime;
        int maxSteps = Mathf.CeilToInt(Mathf.Max(dt, previewSeconds) / dt);
        Vector2 position = origin;
        Vector2 velocity = new Vector2(0f, currentMetrics.jumpSpeed);
        float time = 0f;
        float holdTimer = 0f;
        bool sustainJump = currentMetrics.fullJumpHoldTime > 0f;

        output.Add(new TrajectorySample(time, position, velocity));

        for (int i = 0; i < maxSteps; i++)
        {
            if (sustainJump)
            {
                holdTimer += dt;
                if (holdTimer >= currentMetrics.fullJumpHoldTime)
                    sustainJump = false;
                else
                    velocity.y = currentMetrics.jumpSpeed;
            }

            velocity.x = direction * currentMetrics.horizontalSpeed * Mathf.Clamp01(horizontalRatio);
            velocity = ClampMagnitude(velocity, currentMetrics.maxSpeed);
            if (currentMetrics.maxFallSpeed > 0f && velocity.y < -currentMetrics.maxFallSpeed)
                velocity.y = -currentMetrics.maxFallSpeed;

            velocity.y -= currentMetrics.airGravity * dt;
            if (currentMetrics.airDamping > 0f)
                velocity /= 1f + currentMetrics.airDamping * dt;

            position += velocity * dt;
            time += dt;
            output.Add(new TrajectorySample(time, position, velocity));

            if (position.y < origin.y - 80f)
                break;
        }
    }

    private ReachResult EvaluateReach(List<TrajectorySample> trajectory, float horizontalRatio)
    {
        if (trajectory.Count < 2)
            return ReachResult.Unreachable;

        float requiredDistance = Mathf.Abs(targetPoint.x - startPoint.x);
        float landingDistance = GetReachableDistanceAtTargetHeight(trajectory, targetPoint.y, startPoint.x, out bool hasLanding);
        float centerReachDistance = landingDistance;
        float coyoteDistanceGain = 0f;

        if (pointsAreGapEdges && includeCoyoteTime)
        {
            float coyoteCenterDistance = GetBestCoyoteCenterDistance(horizontalRatio, landingDistance, hasLanding, out bool hasCoyoteLanding);
            centerReachDistance = Mathf.Max(centerReachDistance, coyoteCenterDistance);
            coyoteDistanceGain = Mathf.Max(0f, centerReachDistance - landingDistance);
            hasLanding |= hasCoyoteLanding;
        }

        float edgeAllowance = GetEdgeAllowance(metrics);
        float reachableDistance = pointsAreGapEdges ? centerReachDistance + edgeAllowance : centerReachDistance;
        float ratio = reachableDistance > 0f ? requiredDistance / reachableDistance : float.PositiveInfinity;

        return new ReachResult
        {
            hasLandingHeight = hasLanding,
            isReachable = hasLanding && requiredDistance <= reachableDistance,
            requiredDistance = requiredDistance,
            reachableDistance = reachableDistance,
            centerReachDistance = centerReachDistance,
            edgeAllowance = edgeAllowance,
            coyoteDistanceGain = coyoteDistanceGain,
            difficultyRatio = ratio,
        };
    }

    private float GetEdgeAllowance(JumpMetrics currentMetrics)
    {
        return pointsAreGapEdges ? currentMetrics.colliderWidth * Mathf.Clamp01(edgeContactRatio) : 0f;
    }

    private float GetBestCoyoteCenterDistance(float horizontalRatio, float baseCenterDistance, bool baseHasLanding, out bool hasLanding)
    {
        hasLanding = baseHasLanding;

        float coyoteDuration = metrics.coyoteTime * Mathf.Clamp01(coyoteTimeRatio);
        if (coyoteDuration <= 0f)
            return baseHasLanding ? baseCenterDistance : 0f;

        float bestDistance = baseHasLanding ? baseCenterDistance : 0f;
        float dt = metrics.fixedDeltaTime;
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(coyoteDuration / dt));
        float direction = targetPoint.x >= startPoint.x ? 1f : -1f;

        for (int i = 1; i <= sampleCount; i++)
        {
            float delay = Mathf.Min(coyoteDuration, i * dt);
            Vector2 coyoteOrigin = GetCoyoteJumpOrigin(metrics, direction, delay);
            SimulateJump(metrics, coyoteOrigin, direction, horizontalRatio, coyoteProbeTrajectory);

            float coyoteDistance = GetReachableDistanceAtTargetHeight(coyoteProbeTrajectory, targetPoint.y, startPoint.x, out bool coyoteHasLanding);
            if (!coyoteHasLanding)
                continue;

            hasLanding = true;
            bestDistance = Mathf.Max(bestDistance, coyoteDistance);
        }

        return bestDistance;
    }

    private Vector2 GetCoyoteJumpOrigin(JumpMetrics currentMetrics, float direction, float delay)
    {
        Vector2 position = startPoint;
        Vector2 velocity = new Vector2(direction * currentMetrics.horizontalSpeed, 0f);
        float elapsed = 0f;

        while (elapsed < delay)
        {
            float dt = Mathf.Min(currentMetrics.fixedDeltaTime, delay - elapsed);
            velocity.x = direction * currentMetrics.horizontalSpeed;
            velocity = ClampMagnitude(velocity, currentMetrics.maxSpeed);
            if (currentMetrics.maxFallSpeed > 0f && velocity.y < -currentMetrics.maxFallSpeed)
                velocity.y = -currentMetrics.maxFallSpeed;

            velocity.y -= currentMetrics.airGravity * dt;
            if (currentMetrics.airDamping > 0f)
                velocity /= 1f + currentMetrics.airDamping * dt;

            position += velocity * dt;
            elapsed += dt;
        }

        return position;
    }

    private float GetReachableDistanceAtTargetHeight(List<TrajectorySample> trajectory, float targetY, float referenceX, out bool hasLanding)
    {
        hasLanding = false;

        for (int i = 1; i < trajectory.Count; i++)
        {
            TrajectorySample previous = trajectory[i - 1];
            TrajectorySample current = trajectory[i];
            bool isDescendingSegment = previous.velocity.y <= 0f || current.velocity.y <= 0f;
            bool crossesTargetHeight = previous.position.y >= targetY && current.position.y <= targetY;
            if (!isDescendingSegment || !crossesTargetHeight)
                continue;

            float heightDelta = previous.position.y - current.position.y;
            float t = heightDelta > 0.00001f ? Mathf.Clamp01((previous.position.y - targetY) / heightDelta) : 0f;
            Vector2 landingPoint = Vector2.Lerp(previous.position, current.position, t);
            hasLanding = true;
            return Mathf.Abs(landingPoint.x - referenceX);
        }

        return 0f;
    }

    private float GetMaxHeight(List<TrajectorySample> trajectory)
    {
        float maxHeight = 0f;
        for (int i = 0; i < trajectory.Count; i++)
            maxHeight = Mathf.Max(maxHeight, trajectory[i].position.y - startPoint.y);

        return maxHeight;
    }

    private float EstimateVerticalApex(float startSpeed, float gravity, float damping)
    {
        float dt = metrics.fixedDeltaTime;
        float y = 0f;
        float velocity = Mathf.Max(0f, startSpeed);
        float maxY = 0f;
        int maxSteps = Mathf.CeilToInt(previewSeconds / dt);

        for (int i = 0; i < maxSteps; i++)
        {
            velocity -= gravity * dt;
            if (damping > 0f)
                velocity /= 1f + damping * dt;

            y += velocity * dt;
            maxY = Mathf.Max(maxY, y);
            if (velocity <= 0f)
                break;
        }

        return maxY;
    }

    private void HandleSceneHotkeys()
    {
        Event evt = Event.current;
        if (evt == null || evt.type != EventType.MouseDown || evt.button != 0 || !evt.shift)
            return;

        Vector2 point = GetMouseWorldPoint(evt.mousePosition);
        if (evt.control || evt.command)
        {
            targetPoint = SnapPointToPlatformEdge(point, false);
            hasTargetPoint = true;
        }
        else
        {
            startPoint = SnapPointToPlatformEdge(point, true);
            hasStartPoint = true;
        }

        evt.Use();
        Repaint();
        RepaintSceneViews();
    }

    private Vector2 GetMouseWorldPoint(Vector2 mousePosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        float planeZ = controller != null ? controller.transform.position.z : 0f;
        if (Mathf.Abs(ray.direction.z) > 0.0001f)
        {
            float distance = (planeZ - ray.origin.z) / ray.direction.z;
            return ray.origin + ray.direction * distance;
        }

        return ray.origin;
    }

    private void NormalizeToolSettings()
    {
        edgeSnapSearchRadius = Mathf.Max(0.1f, edgeSnapSearchRadius);
        edgeContactRatio = Mathf.Clamp01(edgeContactRatio);
        coyoteTimeRatio = Mathf.Clamp01(coyoteTimeRatio);
        comfortHorizontalRatio = Mathf.Clamp(comfortHorizontalRatio, 0.5f, 1f);
        previewSeconds = Mathf.Clamp(previewSeconds, 0.5f, 8f);
    }

    private bool ShouldSnapToPlatformEdges()
    {
        return pointsAreGapEdges && snapPointsToPlatformEdges;
    }

    private void SnapBothPointsToPlatformEdges()
    {
        if (!ShouldSnapToPlatformEdges())
            return;

        if (hasStartPoint)
            startPoint = SnapPointToPlatformEdge(startPoint, true);

        if (hasTargetPoint)
            targetPoint = SnapPointToPlatformEdge(targetPoint, false);
    }

    private Vector2 SnapPointToPlatformEdge(Vector2 point, bool isStartPoint)
    {
        if (!ShouldSnapToPlatformEdges())
            return point;

        int desiredSide = GetDesiredSnapSide(point, isStartPoint);
        return TryFindNearestPlatformEdge(point, desiredSide, out Vector2 snappedPoint) ? snappedPoint : point;
    }

    private int GetDesiredSnapSide(Vector2 point, bool isStartPoint)
    {
        bool hasReferencePoint = isStartPoint ? hasTargetPoint : hasStartPoint;
        if (!hasReferencePoint)
            return 0;

        float referenceX = isStartPoint ? targetPoint.x : startPoint.x;
        float delta = referenceX - point.x;
        if (Mathf.Abs(delta) <= 0.001f)
            return 0;

        return delta > 0f ? 1 : -1;
    }

    private bool TryFindNearestPlatformEdge(Vector2 point, int desiredSide, out Vector2 snappedPoint)
    {
        snappedPoint = point;

        float radius = Mathf.Max(0.1f, edgeSnapSearchRadius);
        int layerMask = GetPlatformSnapLayerMask();
        Collider2D[] candidates = Physics2D.OverlapCircleAll(point, radius, layerMask);
        if (candidates == null || candidates.Length == 0)
            return false;

        bool found = false;
        float bestSqrDistance = radius * radius;
        for (int i = 0; i < candidates.Length; i++)
        {
            Collider2D candidate = candidates[i];
            if (ShouldIgnoreSnapCollider(candidate))
                continue;

            AddColliderSnapCandidates(candidate, point, desiredSide, ref snappedPoint, ref bestSqrDistance, ref found);
        }

        return found;
    }

    private int GetPlatformSnapLayerMask()
    {
        if (controller != null)
        {
            int groundMask = controller.groundLayer.value;
            if (groundMask != 0)
                return groundMask;

            int solidMask = controller.solidLayer.value;
            if (solidMask != 0)
                return solidMask;

            int anchorableMask = controller.anchorableLayer.value;
            if (anchorableMask != 0)
                return anchorableMask;
        }

        return Physics2D.DefaultRaycastLayers;
    }

    private bool ShouldIgnoreSnapCollider(Collider2D candidate)
    {
        if (candidate == null || !candidate.enabled)
            return true;

        return controller != null && candidate.transform.IsChildOf(controller.transform);
    }

    private int AddColliderSnapCandidates(
        Collider2D candidate,
        Vector2 point,
        int desiredSide,
        ref Vector2 bestPoint,
        ref float bestSqrDistance,
        ref bool found)
    {
        if (candidate is BoxCollider2D box)
            return AddBoxSnapCandidates(box, point, desiredSide, ref bestPoint, ref bestSqrDistance, ref found);

        int count = 0;
        if (candidate is PolygonCollider2D polygon)
            count = AddPolygonSnapCandidates(polygon, point, desiredSide, ref bestPoint, ref bestSqrDistance, ref found);
        else if (candidate is EdgeCollider2D edge)
            count = AddPathSnapCandidates(edge, edge.points, false, point, desiredSide, ref bestPoint, ref bestSqrDistance, ref found);
        else if (candidate is CompositeCollider2D composite)
            count = AddCompositeSnapCandidates(composite, point, desiredSide, ref bestPoint, ref bestSqrDistance, ref found);

        if (count > 0)
            return count;

        return AddBoundsSnapCandidates(candidate, point, desiredSide, ref bestPoint, ref bestSqrDistance, ref found);
    }

    private int AddBoxSnapCandidates(
        BoxCollider2D box,
        Vector2 point,
        int desiredSide,
        ref Vector2 bestPoint,
        ref float bestSqrDistance,
        ref bool found)
    {
        Vector2 halfSize = box.size * 0.5f;
        Vector2 leftTop = box.transform.TransformPoint(box.offset + new Vector2(-halfSize.x, halfSize.y));
        Vector2 rightTop = box.transform.TransformPoint(box.offset + new Vector2(halfSize.x, halfSize.y));
        return AddSideSnapCandidates(leftTop, rightTop, point, desiredSide, ref bestPoint, ref bestSqrDistance, ref found);
    }

    private int AddPolygonSnapCandidates(
        PolygonCollider2D polygon,
        Vector2 point,
        int desiredSide,
        ref Vector2 bestPoint,
        ref float bestSqrDistance,
        ref bool found)
    {
        int count = 0;
        for (int i = 0; i < polygon.pathCount; i++)
            count += AddPathSnapCandidates(polygon, polygon.GetPath(i), true, point, desiredSide, ref bestPoint, ref bestSqrDistance, ref found);

        return count;
    }

    private int AddCompositeSnapCandidates(
        CompositeCollider2D composite,
        Vector2 point,
        int desiredSide,
        ref Vector2 bestPoint,
        ref float bestSqrDistance,
        ref bool found)
    {
        int count = 0;
        for (int i = 0; i < composite.pathCount; i++)
        {
            int pointCount = composite.GetPathPointCount(i);
            if (pointCount < 2)
                continue;

            Vector2[] path = new Vector2[pointCount];
            composite.GetPath(i, path);
            count += AddPathSnapCandidates(composite, path, true, point, desiredSide, ref bestPoint, ref bestSqrDistance, ref found);
        }

        return count;
    }

    private int AddPathSnapCandidates(
        Collider2D candidate,
        Vector2[] path,
        bool closed,
        Vector2 point,
        int desiredSide,
        ref Vector2 bestPoint,
        ref float bestSqrDistance,
        ref bool found)
    {
        if (path == null || path.Length < 2)
            return 0;

        int count = 0;
        int segmentCount = closed ? path.Length : path.Length - 1;
        for (int i = 0; i < segmentCount; i++)
        {
            Vector2 worldA = TransformColliderPoint(candidate, path[i]);
            Vector2 worldB = TransformColliderPoint(candidate, path[(i + 1) % path.Length]);
            Vector2 segment = worldB - worldA;
            if (Mathf.Abs(segment.x) < 0.05f)
                continue;

            if (Mathf.Abs(segment.y) > Mathf.Abs(segment.x) * 0.25f)
                continue;

            Vector2 midpoint = (worldA + worldB) * 0.5f;
            if (!IsLikelyTopSurface(candidate, midpoint))
                continue;

            Vector2 left = worldA.x <= worldB.x ? worldA : worldB;
            Vector2 right = worldA.x <= worldB.x ? worldB : worldA;
            count += AddSideSnapCandidates(left, right, point, desiredSide, ref bestPoint, ref bestSqrDistance, ref found);
        }

        return count;
    }

    private int AddBoundsSnapCandidates(
        Collider2D candidate,
        Vector2 point,
        int desiredSide,
        ref Vector2 bestPoint,
        ref float bestSqrDistance,
        ref bool found)
    {
        Bounds bounds = candidate.bounds;
        if (bounds.size.x <= 0.001f || bounds.size.y <= 0.001f)
            return 0;

        Vector2 leftTop = new Vector2(bounds.min.x, bounds.max.y);
        Vector2 rightTop = new Vector2(bounds.max.x, bounds.max.y);
        return AddSideSnapCandidates(leftTop, rightTop, point, desiredSide, ref bestPoint, ref bestSqrDistance, ref found);
    }

    private int AddSideSnapCandidates(
        Vector2 left,
        Vector2 right,
        Vector2 point,
        int desiredSide,
        ref Vector2 bestPoint,
        ref float bestSqrDistance,
        ref bool found)
    {
        int count = 0;
        if (desiredSide <= 0)
        {
            ConsiderSnapCandidate(left, point, ref bestPoint, ref bestSqrDistance, ref found);
            count++;
        }

        if (desiredSide >= 0)
        {
            ConsiderSnapCandidate(right, point, ref bestPoint, ref bestSqrDistance, ref found);
            count++;
        }

        return count;
    }

    private void ConsiderSnapCandidate(
        Vector2 candidate,
        Vector2 point,
        ref Vector2 bestPoint,
        ref float bestSqrDistance,
        ref bool found)
    {
        float sqrDistance = (candidate - point).sqrMagnitude;
        if (sqrDistance > bestSqrDistance)
            return;

        bestPoint = candidate;
        bestSqrDistance = sqrDistance;
        found = true;
    }

    private Vector2 TransformColliderPoint(Collider2D candidate, Vector2 localPoint)
    {
        return candidate.transform.TransformPoint(localPoint + candidate.offset);
    }

    private bool IsLikelyTopSurface(Collider2D candidate, Vector2 midpoint)
    {
        if (candidate is EdgeCollider2D)
            return true;

        float probeDistance = Mathf.Clamp(edgeSnapSearchRadius * 0.02f, 0.02f, 0.12f);
        bool hasSolidBelow = candidate.OverlapPoint(midpoint + Vector2.down * probeDistance);
        bool hasSolidAbove = candidate.OverlapPoint(midpoint + Vector2.up * probeDistance);
        if (hasSolidBelow && !hasSolidAbove)
            return true;

        return midpoint.y >= candidate.bounds.center.y && !hasSolidAbove;
    }

    private void DrawSnapSearchRadius()
    {
        if (!ShouldSnapToPlatformEdges() || !showEdgeSnapSearchRadius)
            return;

        Handles.color = new Color(1f, 0.72f, 0.12f, 0.55f);
        if (hasStartPoint)
            Handles.DrawWireDisc(startPoint, Vector3.forward, edgeSnapSearchRadius);

        if (hasTargetPoint)
            Handles.DrawWireDisc(targetPoint, Vector3.forward, edgeSnapSearchRadius);
    }

    private void DrawSceneHandles()
    {
        if (!hasStartPoint || !hasTargetPoint)
            return;

        Vector3 start = startPoint;
        Vector3 target = targetPoint;
        float handleSize = HandleUtility.GetHandleSize(start) * HandlePickSize;

        EditorGUI.BeginChangeCheck();
        Handles.color = new Color(0.15f, 1f, 0.35f, 1f);
        start = Handles.FreeMoveHandle(start, handleSize, Vector3.zero, Handles.SphereHandleCap);
        Handles.color = new Color(0.25f, 0.55f, 1f, 1f);
        target = Handles.FreeMoveHandle(target, handleSize, Vector3.zero, Handles.SphereHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            startPoint = SnapPointToPlatformEdge(start, true);
            targetPoint = SnapPointToPlatformEdge(target, false);
            Repaint();
        }

        DrawSnapSearchRadius();
        DrawTrajectory(comfortTrajectory, new Color(0.2f, 0.75f, 1f, 0.5f), showComfortLine, 3f);
        DrawTrajectory(maxTrajectory, targetReach.isReachable ? new Color(0.1f, 0.95f, 0.35f, 1f) : new Color(1f, 0.18f, 0.12f, 1f), showMaxLine, 4f);

        DrawReachMarkers();
        Handles.color = Color.white;
        Handles.DrawLine(startPoint, targetPoint);

        float direction = targetPoint.x >= startPoint.x ? 1f : -1f;
        Handles.Label(startPoint + Vector2.up * 0.3f, "起点");
        Handles.Label(targetPoint + Vector2.up * 0.3f, "终点");
        Handles.Label((startPoint + targetPoint) * 0.5f + Vector2.up * 0.35f, BuildSceneLabel(direction));
    }

    private void DrawTrajectory(List<TrajectorySample> trajectory, Color color, bool shouldDraw, float width)
    {
        if (!shouldDraw || trajectory.Count < 2)
            return;

        Vector3[] points = new Vector3[trajectory.Count];
        for (int i = 0; i < trajectory.Count; i++)
            points[i] = trajectory[i].position;

        Handles.color = color;
        Handles.DrawAAPolyLine(width, points);
    }

    private void DrawReachMarkers()
    {
        float direction = targetPoint.x >= startPoint.x ? 1f : -1f;
        DrawSingleReachMarker(targetReach, direction, new Color(0.1f, 0.95f, 0.35f, 1f), pointsAreGapEdges ? "极限坑宽" : "极限落点");
        DrawSingleReachMarker(comfortReach, direction, new Color(0.2f, 0.75f, 1f, 1f), pointsAreGapEdges ? "舒适坑宽" : "舒适落点");
    }

    private void DrawSingleReachMarker(ReachResult result, float direction, Color color, string label)
    {
        if (!result.hasLandingHeight)
            return;

        Vector2 point = new Vector2(startPoint.x + direction * result.reachableDistance, targetPoint.y);
        Handles.color = color;
        Handles.DrawWireDisc(point, Vector3.forward, HandleUtility.GetHandleSize(point) * 0.08f);
        Handles.Label(point + Vector2.up * 0.35f, $"{label} {result.reachableDistance:0.##}");
    }

    private string BuildSceneLabel(float direction)
    {
        string state = targetReach.isReachable
            ? targetReach.difficultyRatio <= comfortHorizontalRatio ? "可达：舒适" : targetReach.difficultyRatio <= 0.95f ? "可达：偏难" : "可达：极限"
            : "不可达";
        float dx = Mathf.Abs(targetPoint.x - startPoint.x);
        float dy = targetPoint.y - startPoint.y;
        string distanceName = pointsAreGapEdges ? "坑宽" : "中心距";
        string arrow = direction > 0f ? "→" : "←";
        string allowance = pointsAreGapEdges
            ? $"\n边缘余量 {targetReach.edgeAllowance:0.##} / 土狼 {targetReach.coyoteDistanceGain:0.##}"
            : string.Empty;
        return $"{state} {arrow}\n{distanceName} {dx:0.##} / 高差 {dy:0.##}\n极限 {targetReach.reachableDistance:0.##} / 舒适 {comfortReach.reachableDistance:0.##}{allowance}";
    }

    private void DrawSceneOverlay()
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(12f, 12f, 380f, 148f), "Jump Reachability", GUI.skin.window);
        GUILayout.Label("Shift+左键：设置起点    Ctrl+Shift+左键：设置终点");
        GUILayout.Label(pointsAreGapEdges ? "坑边模式：两点放在平台边缘，已计入玩家边缘余量" : "中心模式：两点代表玩家脚底中心");
        if (pointsAreGapEdges)
            GUILayout.Label(snapPointsToPlatformEdges ? $"自动贴边：开 / 半径 {edgeSnapSearchRadius:0.##}" : "自动贴边：关");
        if (pointsAreGapEdges)
            GUILayout.Label($"边缘余量 {targetReach.edgeAllowance:0.##} / 土狼余量 {targetReach.coyoteDistanceGain:0.##}");
        GUILayout.Label($"跳高 {GetMaxHeight(maxTrajectory):0.##} / 竖直极速上升 {EstimateVerticalApex(metrics.maxSpeed, metrics.ropeGravity, metrics.ropeDamping):0.##}");
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private static Vector2 ClampMagnitude(Vector2 velocity, float maxSpeed)
    {
        if (maxSpeed <= 0f)
            return Vector2.zero;

        float sqrMagnitude = velocity.sqrMagnitude;
        return sqrMagnitude > maxSpeed * maxSpeed ? velocity.normalized * maxSpeed : velocity;
    }

    private static void RepaintSceneViews()
    {
        SceneView.RepaintAll();
    }

    private struct JumpMetrics
    {
        public float fixedDeltaTime;
        public float jumpSpeed;
        public float fullJumpHoldTime;
        public float horizontalSpeed;
        public float maxSpeed;
        public float airGravity;
        public float airDamping;
        public float ropeGravity;
        public float ropeDamping;
        public float maxFallSpeed;
        public float colliderWidth;
        public float coyoteTime;
    }

    private struct TrajectorySample
    {
        public readonly float time;
        public readonly Vector2 position;
        public readonly Vector2 velocity;

        public TrajectorySample(float time, Vector2 position, Vector2 velocity)
        {
            this.time = time;
            this.position = position;
            this.velocity = velocity;
        }
    }

    private struct ReachResult
    {
        public static readonly ReachResult Unreachable = new ReachResult
        {
            hasLandingHeight = false,
            isReachable = false,
            requiredDistance = 0f,
            reachableDistance = 0f,
            centerReachDistance = 0f,
            edgeAllowance = 0f,
            coyoteDistanceGain = 0f,
            difficultyRatio = float.PositiveInfinity,
        };

        public bool hasLandingHeight;
        public bool isReachable;
        public float requiredDistance;
        public float reachableDistance;
        public float centerReachDistance;
        public float edgeAllowance;
        public float coyoteDistanceGain;
        public float difficultyRatio;
    }

}

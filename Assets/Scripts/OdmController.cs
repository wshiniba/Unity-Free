using System.Collections.Generic;
using UnityEngine;

public enum OdmState
{
    Grounded,

    Airborne,

    Shooting,

    Pulling,

    Swinging,

    Hovering,

    BurstPulling
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(OdmGasSystem))]
public class OdmController : MonoBehaviour
{
    private struct RopeBend
    {
        public Vector2 position;
        public Collider2D collider;
        public Vector2 normal;
        public float wrapSide;

        public RopeBend(Vector2 position, Collider2D collider, Vector2 normal, float wrapSide)
        {
            this.position = position;
            this.collider = collider;
            this.normal = normal;
            this.wrapSide = wrapSide;
        }
    }

    [Header("状态")]
    [Tooltip("当前 ODM 运行状态，仅用于调试观察，不建议手动修改。")]
    public OdmState currentState = OdmState.Grounded;

    [Header("绳索锚点")]
    [Tooltip("绳索允许连接的目标层级。")]
    public LayerMask anchorableLayer;

    [Tooltip("会被视为实体障碍的层级，用于身体防穿模和绳索弯折；未设置时使用可连接目标层级。")]
    public LayerMask solidLayer;

    [Tooltip("左侧绳索发射点；未指定时使用玩家 Transform。")]
    public Transform leftAnchorPoint;

    [Tooltip("右侧绳索发射点；未指定时使用玩家 Transform。")]
    public Transform rightAnchorPoint;

    [Tooltip("搜索可连接目标时的最大射线距离。")]
    public float anchorRaycastDistance = 50f;

    [Tooltip("绳索最大可用长度；超出该距离的命中会被忽略，飞行绳索会收回。")]
    public float maxCableLength = 30f;

    [Tooltip("绳索头发射和收回时的可视移动速度。")]
    public float cableShootSpeed = 50f;

    [Header("空发回收")]
    [Tooltip("绳索没有命中可连接目标时，绳头最多飞出多远后开始自动收回。该值会被 maxCableLength 限制。")]
    public float missedShotTravelDistance = 12f;

    [Tooltip("连接点向碰撞体表面外侧偏移的距离，避免锚点卡进碰撞体内部。")]
    public float anchorSurfaceOffset = 0.15f;


    [Header("移动参数")]
    [Tooltip("玩家站在地面上时，由 A/D 或 Horizontal 输入控制的水平移动速度。")]
    public float groundMoveSpeed = 8f;

    [Tooltip("地面左右移动追接目标速度的加速度，数值越大响应越快。")]
    public float groundAcceleration = 60f;

    [Header("跳跃")]
    [Tooltip("按下跳跃键时写入 Rigidbody2D 的向上速度。数值越大，起跳越高。")]
    public float jumpSpeed = 18f;

    [Tooltip("摆荡时由 A/D 或 Horizontal 输入施加的水平力。")]
    public float swingForce = 10f;

    [Tooltip("按住 Q 或使用 E 自动牵引时，朝当前绳索锚点施加的牵引力。")]
    public float pullForce = 20f;

    [Header("发射移动")]
    [Tooltip("开启后，绳索飞行但还没有连接到目标时，角色在地面上仍然可以继续用 A/D 移动。")]
    public bool allowGroundMoveWhileShooting = true;


    [Tooltip("玩家按 Space 冲刺时施加的瞬时冲量。")]
    public float airDashForce = 15f;

    [Header("刚体参数")]
    [Tooltip("启用后，Awake 会使用下方参数覆盖 Rigidbody2D 面板中的质量、线性阻尼和重力倍率。关闭后可直接在 Rigidbody2D 组件上调整这些手感参数。")]
    public bool overrideRigidbodySettings = true;

    [Tooltip("角色刚体质量。只在 overrideRigidbodySettings 启用时应用。")]
    public float bodyMass = 1f;

    [Tooltip("角色刚体线性阻尼。数值越低速度保留越久，惯性越强；数值越高越容易停下。只在 overrideRigidbodySettings 启用时应用。")]
    public float bodyLinearDamping = 0.5f;

    [Tooltip("角色刚体重力倍率。只在 overrideRigidbodySettings 启用时应用。")]
    public float bodyGravityScale = 2f;


    [Header("状态物理")]
    [Tooltip("启用后，根据 ODM 状态动态切换重力和线性阻尼。关闭后回到 Rigidbody2D 面板或刚体参数中的固定重力/阻尼。")]
    public bool enableStatePhysics = true;

    [Tooltip("站在地面时的重力倍率。")]
    public float groundedGravityScale = 2f;

    [Tooltip("普通空中状态的重力倍率。数值越高，下坠越有重量。")]
    public float airborneGravityScale = 2.8f;

    [Tooltip("持续牵引状态的重力倍率。数值越低，拉绳时越不容易被下坠拖住。")]
    public float pullingGravityScale = 1.2f;

    [Tooltip("摆荡状态的重力倍率。用于保留弧线感，同时避免过重。")]
    public float swingingGravityScale = 1.5f;

    [Tooltip("爆发牵引状态的重力倍率。通常应较低，让角色更像被快速拽出。")]
    public float burstPullGravityScale = 0.3f;

    [Tooltip("悬停状态的重力倍率。默认保持 0，延续原先 Hover 由切线重力单独处理的行为。")]
    public float hoveringGravityScale = 0f;

    [Tooltip("站在地面时的线性阻尼。")]
    public float groundedDamping = 0.5f;

    [Tooltip("普通空中状态的线性阻尼。数值越低，空中惯性保留越强。")]
    public float airborneDamping = 0.05f;

    [Tooltip("持续牵引状态的线性阻尼。")]
    public float pullingDamping = 0f;

    [Tooltip("摆荡状态的线性阻尼。")]
    public float swingingDamping = 0.02f;

    [Tooltip("爆发牵引状态的线性阻尼。")]
    public float burstPullDamping = 0f;

    [Tooltip("悬停状态的线性阻尼。")]
    public float hoveringDamping = 0.1f;

    [Tooltip("重力倍率切换速度。数值越大，状态切换时重力变化越快。")]
    public float gravityChangeSpeed = 20f;

    [Tooltip("线性阻尼切换速度。数值越大，状态切换时阻尼变化越快。")]
    public float dampingChangeSpeed = 40f;

    [Header("下落控制")]
    [Tooltip("是否限制最大下落速度。")]
    public bool limitFallSpeed = true;

    [Tooltip("最大下落速度。允许重力更有重量，但避免无限加速下坠导致失控。")]
    public float maxFallSpeed = 28f;

    [Tooltip("是否在高速移动时降低重力影响。")]
    public bool reduceGravityAtHighSpeed = true;

    [Tooltip("高速时最低重力倍率。0.55 表示高速时最终重力最低降到当前状态重力的 55%。")]
    [Range(0f, 1f)]
    public float highSpeedGravityMinMultiplier = 0.55f;

    [Tooltip("用于计算高速弱重力的参考速度。小于等于 0 时使用 maxSpeed。")]
    public float highSpeedGravityReference = 30f;


    [Header("自动牵引")]
    [Tooltip("按 E 自动牵引时，距离目标点多近会自动断开绳索；单绳目标是锚点，双绳目标是两个锚点的中点。")]
    public float burstReleaseDistance = 1.2f;

    [Tooltip("按 E 后至少牵引这么久，才允许自动断绳；避免近距离连接时看起来没有反应。")]
    public float burstMinDuration = 0.12f;

    [Tooltip("按 E 后至少移动这么远，才允许自动断绳；数值过大可能导致近距离锚点绕过头。")]
    public float burstMinTravelDistance = 0.25f;

    [Tooltip("所有 ODM 移动结算后的 Rigidbody2D 最大线速度。")]
    public float maxSpeed = 40f;

    [Tooltip("紧急停止键使用的速度倍率；数值越低，停止越快。")]
    public float stopDamping = 0.5f;

    [Header("角色朝向")]
    [Tooltip("用于根据水平输入翻转的角色 SpriteRenderer。未指定时自动使用当前物体上的 SpriteRenderer。默认 Sprite 朝向应为右。")]
    public SpriteRenderer characterRenderer;

    [Tooltip("水平输入绝对值大于该阈值时才会更新角色朝向，避免摇杆或轴输入轻微抖动导致频繁翻转。")]
    public float facingInputThreshold = 0.1f;

    [Header("角色动画")]
    [Tooltip("玩家本体 Animator。未指定时自动使用当前物体上的 Animator。")]
    public Animator characterAnimator;

    [Tooltip("行走动画使用的 Float 参数名。")]
    public string walkParameterName = "walk";

    [Tooltip("空中动画使用的 Bool 参数名。离地时为 true，落地时为 false，可用于切换到 Roll Fly。")]
    public string airborneParameterName = "isAirborne";


    [Header("碰撞保护")]
    [Tooltip("碰撞体 Cast 的额外检测距离，用于在角色重叠实体前提前停止速度。")]
    public float groundCheckDistance = 0.08f;

    [Header("地面检测")]
    [Tooltip("从玩家碰撞体向下 Cast 的距离，用于判断脚下是否仍然有实体。")]
    public float castSkin = 0.05f;

    [Header("绳索弯折")]
    [Tooltip("绳索绕过实体拐角时，弯折点向外偏移的距离。")]
    public float ropeBendOffset = 0.08f;

    [Tooltip("避免在同一个拐角重复添加弯折点的距离阈值。")]
    public float ropeCornerReleaseDistance = 0.2f;

    [Tooltip("单根绳索绕过障碍时最多保留的弯折点数量。")]
    public int maxRopeBends = 4;

    public Rigidbody2D Rb { get; private set; }

    public bool IsPullKeyHeld { get; set; }

    public bool IsHovering { get; private set; }

    public bool IsGrounded { get; private set; }

    public Vector2 LeftAnchorPos { get; private set; }

    public Vector2 RightAnchorPos { get; private set; }

    public bool IsLeftAnchored { get; private set; }

    public bool IsRightAnchored { get; private set; }

    public bool IsLeftFlying { get; private set; }

    public bool IsRightFlying { get; private set; }

    public Vector2 LeftFlyTip { get; private set; }

    public Vector2 RightFlyTip { get; private set; }

    private Collider2D bodyCollider;

    private OdmGasSystem gasSystem;

    private float horizontalInput;


    private float leftHoverRopeLength;
    private float rightHoverRopeLength;

    private float defaultGravityScale;

    private bool burstPullActive;
    private bool burstPullLeft;
    private float burstPullStartTime;
    private Vector2 burstPullStartPosition;

    private float leftRopeLength;
    private float rightRopeLength;

    private Vector2 leftFlyTarget;
    private Vector2 rightFlyTarget;

    private bool leftFlyCanAnchor;
    private bool rightFlyCanAnchor;

    private bool leftFlyRetracting;
    private bool rightFlyRetracting;

    private readonly List<RopeBend> leftBends = new List<RopeBend>();
    private readonly List<RopeBend> rightBends = new List<RopeBend>();

    private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];

    void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        gasSystem = GetComponent<OdmGasSystem>();
        if (characterRenderer == null) characterRenderer = GetComponent<SpriteRenderer>();
        if (characterAnimator == null) characterAnimator = GetComponent<Animator>();

        if (overrideRigidbodySettings)
        {
            Rb.mass = bodyMass;
            Rb.linearDamping = bodyLinearDamping;
            Rb.gravityScale = bodyGravityScale;
        }

        Rb.freezeRotation = true;
        Rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        Rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        defaultGravityScale = Rb.gravityScale;
    }

    void Update()
    {
        UpdateCableFlight();
    }

    void FixedUpdate()
    {
        IsGrounded = CheckGrounded();
        UpdateMovementState();
        UpdateStatePhysics();
        UpdateCableBends();
        ApplyMovement();
        ApplyRopeLimits();
        StopIntoSolids();
        ClampMaxSpeed();
        ClampFallSpeed();
        UpdateWalkAnimation();
        UpdateAirborneAnimation();
    }

    public void SetHorizontalInput(float input)
    {
        horizontalInput = input;
        UpdateFacingDirection(input);
    }

    /// <summary>
    /// 尝试执行地面跳跃。返回 false 表示当前不满足跳跃条件。
    /// </summary>
    public bool TryJump()
    {
        if (!IsGrounded && !CheckGrounded())
            return false;

        SetHovering(false);
        burstPullActive = false;

        Vector2 velocity = Rb.linearVelocity;
        velocity.y = Mathf.Max(velocity.y, jumpSpeed);
        Rb.linearVelocity = velocity;

        currentState = OdmState.Airborne;
        IsGrounded = false;
        return true;
    }

    private void UpdateFacingDirection(float input)
    {
        if (characterRenderer == null || Mathf.Abs(input) <= facingInputThreshold) return;

        characterRenderer.flipX = input < 0f;
    }

    private void UpdateWalkAnimation()
    {
        if (!HasAnimatorParameter(walkParameterName, AnimatorControllerParameterType.Float))
            return;

        float walkValue = IsGrounded ? Mathf.Abs(horizontalInput) : 0f;
        characterAnimator.SetFloat(walkParameterName, walkValue);
    }

    private void UpdateAirborneAnimation()
    {
        if (!HasAnimatorParameter(airborneParameterName, AnimatorControllerParameterType.Bool))
            return;

        characterAnimator.SetBool(airborneParameterName, !IsGrounded);
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (characterAnimator == null || string.IsNullOrEmpty(parameterName))
            return false;

        AnimatorControllerParameter[] parameters = characterAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == parameterType && parameters[i].name == parameterName)
                return true;
        }

        return false;
    }

    public void BeginPullKey()
    {
        IsPullKeyHeld = true;
    }

    public void EndPullKey()
    {
        IsPullKeyHeld = false;
    }

    public void ToggleHoverByInput()
    {
        ToggleHovering();
    }

    public void TriggerBurstPull()
    {
        if (!HasAnchoredCable() || gasSystem.IsGasEmpty) return;

        SetHovering(false);

        burstPullActive = true;
        burstPullLeft = IsLeftAnchored && !IsRightAnchored;
        burstPullStartTime = Time.time;
        burstPullStartPosition = Rb.position;
        currentState = OdmState.BurstPulling;
    }

    public Vector3[] GetCablePath(bool isLeft)
    {
        Transform start = GetShootPoint(isLeft);

        if (!HasCablePath(isLeft))
            return null;

        List<RopeBend> bends = isLeft ? leftBends : rightBends;
        Vector2 terminal = GetCableTerminal(isLeft);
        Vector3[] path = new Vector3[bends.Count + 2];
        path[0] = start.position;

        for (int i = 0; i < bends.Count; i++)
            path[i + 1] = bends[i].position;

        path[path.Length - 1] = terminal;
        return path;
    }

    public void ShootCable(Vector2 direction, bool isLeft)
    {
        if (gasSystem.IsGasEmpty || direction.sqrMagnitude < 0.001f) return;
        if (isLeft ? IsLeftFlying || IsLeftAnchored : IsRightFlying || IsRightAnchored) return;

        Transform start = GetShootPoint(isLeft);
        Vector2 dir = direction.normalized;
        RaycastHit2D hit = Physics2D.Raycast(start.position, dir, anchorRaycastDistance, anchorableLayer);

        bool canAnchor = hit.collider != null && hit.distance <= maxCableLength;
        float missedTravelDistance = Mathf.Min(Mathf.Max(0.1f, missedShotTravelDistance), maxCableLength);
        Vector2 target = canAnchor
            ? GetSafeAnchorPoint(hit)
            : (Vector2)start.position + dir * missedTravelDistance;

        if (isLeft)
        {
            IsLeftFlying = true;
            LeftFlyTip = start.position;
            leftFlyTarget = target;
            leftFlyCanAnchor = canAnchor;
            leftFlyRetracting = false;
            leftBends.Clear();
        }
        else
        {
            IsRightFlying = true;
            RightFlyTip = start.position;
            rightFlyTarget = target;
            rightFlyCanAnchor = canAnchor;
            rightFlyRetracting = false;
            rightBends.Clear();
        }

        currentState = OdmState.Shooting;
    }

    public void ReleaseCable(bool isLeft)
    {
        if (isLeft)
        {
            if (IsLeftFlying) leftFlyRetracting = true;
            else ClearLeftCable();
        }
        else
        {
            if (IsRightFlying) rightFlyRetracting = true;
            else ClearRightCable();
        }

        if (!HasAnchoredCable())
        {
            SetHovering(false);
            burstPullActive = false;
        }

        if (!HasActiveCable())
            currentState = IsGrounded ? OdmState.Grounded : OdmState.Airborne;
    }

    public void AirDash(Vector2 direction)
    {
        if (gasSystem.IsGasEmpty || direction.sqrMagnitude < 0.001f) return;

        SetHovering(false);
        burstPullActive = false;

        Rb.linearVelocity += direction.normalized * airDashForce;
        gasSystem.ConsumeGas(10f);
    }

    public void EmergencyStop()
    {
        Rb.linearVelocity *= stopDamping;
    }

    private void UpdateMovementState()
    {
        if (ShouldPullCable())
        {
            SetHovering(false);
            burstPullActive = false;
            currentState = OdmState.Pulling;
            return;
        }

        if (burstPullActive && HasAnchoredCable() && !gasSystem.IsGasEmpty)
        {
            currentState = OdmState.BurstPulling;
            return;
        }

        burstPullActive = false;

        if (IsGrounded)
        {
            currentState = HasFlyingCable() ? OdmState.Shooting : OdmState.Grounded;
            return;
        }

        if (HasFlyingCable())
        {
            currentState = OdmState.Shooting;
            return;
        }

        if (!HasAnchoredCable())
        {
            SetHovering(false);
            burstPullActive = false;
            currentState = OdmState.Airborne;
            return;
        }

        currentState = IsHovering ? OdmState.Hovering : OdmState.Swinging;
    }

    private void ApplyMovement()
    {
        if (currentState == OdmState.BurstPulling)
        {
            ApplyBurstPull();
            return;
        }

        if (currentState == OdmState.Pulling)
        {
            PullTowardCable(true);
            PullTowardCable(false);
            gasSystem.HandleContinuousConsumption(Time.fixedDeltaTime);

            if (IsGrounded)
                ApplyGroundMovement();

            return;
        }

        if (currentState == OdmState.Shooting)
        {
            if (IsGrounded && allowGroundMoveWhileShooting)
                ApplyGroundMovement();

            return;
        }

        if (currentState == OdmState.Grounded)
        {
            ApplyGroundMovement();
            return;
        }

        if ((currentState == OdmState.Swinging || currentState == OdmState.Hovering) && Mathf.Abs(horizontalInput) > 0.1f)
            Rb.AddForce(Vector2.right * horizontalInput * swingForce, ForceMode2D.Force);

        if (currentState == OdmState.Hovering)
            ApplyHoverTangentGravity();
    }

    private void ApplyGroundMovement()
    {
        float targetX = horizontalInput * groundMoveSpeed;

        if (Mathf.Abs(Rb.linearVelocity.x) > groundMoveSpeed && Mathf.Sign(Rb.linearVelocity.x) == Mathf.Sign(targetX))
            return;

        float nextX = Mathf.MoveTowards(Rb.linearVelocity.x, targetX, groundAcceleration * Time.fixedDeltaTime);
        Rb.linearVelocity = new Vector2(nextX, Rb.linearVelocity.y);
    }

    private void PullTowardCable(bool isLeft)
    {
        if (!(isLeft ? IsLeftAnchored : IsRightAnchored)) return;

        Vector2 target = GetEffectiveAnchor(isLeft);
        if (IsBlockedToward(target)) return;

        Vector2 dir = (target - (Vector2)transform.position).normalized;
        Rb.AddForce(dir * pullForce, ForceMode2D.Force);
    }

    private void UpdateCableFlight()
    {
        if (IsLeftFlying) AdvanceFlight(true);
        if (IsRightFlying) AdvanceFlight(false);
    }

    private void ApplyBurstPull()
    {
        if (!HasAnchoredCable() || gasSystem.IsGasEmpty)
        {
            burstPullActive = false;
            return;
        }

        if (IsLeftAnchored && IsRightAnchored)
        {
            ApplyBurstPullToCenter();
            return;
        }

        burstPullLeft = IsLeftAnchored;
        Vector2 target = GetEffectiveAnchor(burstPullLeft);
        Vector2 toTarget = target - Rb.position;
        if (ShouldReleaseBurstAtTarget(toTarget))
        {
            ReleaseBurstCable();
            return;
        }

        PullTowardCable(burstPullLeft);
        gasSystem.HandleContinuousConsumption(Time.fixedDeltaTime);
    }

    private void ApplyBurstPullToCenter()
    {
        Vector2 center = (GetEffectiveAnchor(true) + GetEffectiveAnchor(false)) * 0.5f;
        Vector2 toCenter = center - Rb.position;
        if (ShouldReleaseBurstAtTarget(toCenter))
        {
            ClearLeftCable();
            ClearRightCable();
            burstPullActive = false;
            currentState = IsGrounded ? OdmState.Grounded : OdmState.Airborne;
            return;
        }

        PullTowardCable(true);
        PullTowardCable(false);
        gasSystem.HandleContinuousConsumption(Time.fixedDeltaTime);
    }

    private bool ShouldReleaseBurstAtTarget(Vector2 toTarget)
    {
        bool hasPulledLongEnough = Time.time - burstPullStartTime >= burstMinDuration;
        bool hasMovedEnough = Vector2.Distance(Rb.position, burstPullStartPosition) >= burstMinTravelDistance;
        if (!hasPulledLongEnough && !hasMovedEnough)
            return false;

        float distance = toTarget.magnitude;
        if (distance <= burstReleaseDistance) return true;
        if (distance > burstReleaseDistance * 2f || distance < 0.000001f) return false;

        return Vector2.Dot(Rb.linearVelocity, toTarget.normalized) <= 0f;
    }

    private void ReleaseBurstCable()
    {
        if (burstPullLeft) ClearLeftCable();
        else ClearRightCable();

        burstPullActive = false;
        currentState = HasAnchoredCable() ? OdmState.Swinging : OdmState.Airborne;
    }

    private void AdvanceFlight(bool isLeft)
    {
        Transform start = GetShootPoint(isLeft);
        Vector2 tip = isLeft ? LeftFlyTip : RightFlyTip;
        Vector2 target = isLeft ? leftFlyTarget : rightFlyTarget;
        bool retracting = isLeft ? leftFlyRetracting : rightFlyRetracting;
        bool canAnchor = isLeft ? leftFlyCanAnchor : rightFlyCanAnchor;
        Vector2 destination = retracting ? (Vector2)start.position : target;
        Vector2 nextTip = Vector2.MoveTowards(tip, destination, cableShootSpeed * Time.deltaTime);

        if (!retracting)
        {
            RaycastHit2D hit = Physics2D.Linecast(tip, nextTip, anchorableLayer);
            if (hit.collider != null)
            {
                AnchorCable(isLeft, GetSafeAnchorPoint(hit));
                return;
            }
        }

        if (isLeft) LeftFlyTip = nextTip;
        else RightFlyTip = nextTip;

        if (!retracting && canAnchor && nextTip == target)
        {
            AnchorCable(isLeft, target);
        }
        else if (!retracting && !canAnchor && nextTip == target)
        {
            SetRetracting(isLeft, true);
        }
        else if (!retracting && GetRopePathLength(isLeft, nextTip) >= maxCableLength)
        {
            SetRetracting(isLeft, true);
        }
        else if (retracting && nextTip == (Vector2)start.position)
        {
            CancelFlight(isLeft);
        }
    }

    private void AnchorCable(bool isLeft, Vector2 anchor)
    {
        float ropeLength = maxCableLength;

        if (isLeft)
        {
            IsLeftFlying = false;
            leftFlyRetracting = false;
            IsLeftAnchored = true;
            LeftAnchorPos = anchor;
            leftRopeLength = ropeLength;
        }
        else
        {
            IsRightFlying = false;
            rightFlyRetracting = false;
            IsRightAnchored = true;
            RightAnchorPos = anchor;
            rightRopeLength = ropeLength;
        }

        currentState = IsGrounded ? OdmState.Grounded : OdmState.Swinging;
    }

    private void ApplyRopeLimits()
    {
        LimitRope(true);
        LimitRope(false);

        if (IsHovering)
            ApplyHoverRopeLocks();
    }

    private bool ShouldPullCable()
    {
        return HasAnchoredCable() && IsPullKeyHeld && !gasSystem.IsGasEmpty;
    }

    private void ToggleHovering()
    {
        if (IsHovering)
        {
            SetHovering(false);
            return;
        }

        if (!HasAnchoredCable() || IsGrounded)
            return;

        SetHovering(true);
    }

    private void SetHovering(bool value)
    {
        if (IsHovering == value) return;

        IsHovering = value;

        if (IsHovering)
        {
            leftHoverRopeLength = IsLeftAnchored ? GetRopePathLength(true) : 0f;
            rightHoverRopeLength = IsRightAnchored ? GetRopePathLength(false) : 0f;
            if (!enableStatePhysics)
                Rb.gravityScale = 0f;
            RemoveHoverOutwardVelocity(true);
            RemoveHoverOutwardVelocity(false);
        }
        else
        {
            if (!enableStatePhysics)
                Rb.gravityScale = defaultGravityScale;
        }
    }

    private void ApplyHoverTangentGravity()
    {
        Vector2 anchor = GetClosestEffectiveAnchor();
        Vector2 fromAnchor = Rb.position - anchor;
        if (fromAnchor.sqrMagnitude < 0.000001f) return;

        Vector2 ropeDir = fromAnchor.normalized;
        Vector2 gravity = Physics2D.gravity * defaultGravityScale;
        Vector2 tangentGravity = gravity - ropeDir * Vector2.Dot(gravity, ropeDir);
        Rb.AddForce(tangentGravity, ForceMode2D.Force);
    }

    private void ApplyHoverRopeLocks()
    {
        if (IsGrounded || !HasAnchoredCable())
        {
            SetHovering(false);
            return;
        }

        LimitHoverRope(true);
        LimitHoverRope(false);
        RemoveHoverOutwardVelocity(true);
        RemoveHoverOutwardVelocity(false);
    }

    private void LimitHoverRope(bool isLeft)
    {
        if (!(isLeft ? IsLeftAnchored : IsRightAnchored)) return;

        float maxLength = isLeft ? leftHoverRopeLength : rightHoverRopeLength;
        if (maxLength <= 0f) return;

        float distance = GetRopePathLength(isLeft);
        if (distance <= maxLength) return;

        Vector2 limitPoint = GetEffectiveAnchor(isLeft);
        Vector2 fromLimitPoint = Rb.position - limitPoint;
        if (fromLimitPoint.sqrMagnitude < 0.000001f) return;

        Vector2 away = fromLimitPoint.normalized;
        Rb.position -= away * (distance - maxLength);
    }

    private void RemoveHoverOutwardVelocity(bool isLeft)
    {
        if (!(isLeft ? IsLeftAnchored : IsRightAnchored)) return;

        Vector2 anchor = GetEffectiveAnchor(isLeft);
        Vector2 fromAnchor = Rb.position - anchor;
        if (fromAnchor.sqrMagnitude < 0.000001f) return;

        Vector2 away = fromAnchor.normalized;
        float outwardSpeed = Vector2.Dot(Rb.linearVelocity, away);
        if (outwardSpeed > 0f)
            Rb.linearVelocity -= away * outwardSpeed;
    }

    private Vector2 GetClosestEffectiveAnchor()
    {
        if (IsLeftAnchored && IsRightAnchored)
        {
            Vector2 left = GetEffectiveAnchor(true);
            Vector2 right = GetEffectiveAnchor(false);
            float leftDistance = ((Vector2)transform.position - left).sqrMagnitude;
            float rightDistance = ((Vector2)transform.position - right).sqrMagnitude;
            return leftDistance <= rightDistance ? left : right;
        }

        return IsLeftAnchored ? GetEffectiveAnchor(true) : GetEffectiveAnchor(false);
    }

    private void LimitRope(bool isLeft)
    {
        if (!(isLeft ? IsLeftAnchored : IsRightAnchored)) return;

        float maxLength = isLeft ? leftRopeLength : rightRopeLength;
        float distance = GetRopePathLength(isLeft);
        if (distance <= maxLength) return;

        Vector2 limitPoint = GetEffectiveAnchor(isLeft);
        Vector2 fromLimitPoint = Rb.position - limitPoint;
        if (fromLimitPoint.sqrMagnitude < 0.000001f) return;

        Vector2 away = fromLimitPoint.normalized;
        // 超出普通摆荡绳长时，把刚体拉回边界，并阻止继续远离锚点的速度。
        Rb.position -= away * (distance - maxLength);

        float outwardSpeed = Vector2.Dot(Rb.linearVelocity, away);
        if (outwardSpeed > 0f)
            Rb.linearVelocity -= away * outwardSpeed;
    }

    private void UpdateCableBends()
    {
        UpdateCableBend(true);
        UpdateCableBend(false);
    }

    private void UpdateCableBend(bool isLeft)
    {
        if (!HasCablePath(isLeft)) return;

        Transform start = GetShootPoint(isLeft);
        List<RopeBend> bends = isLeft ? leftBends : rightBends;
        Vector2 terminal = GetCableTerminal(isLeft);
        Vector2 target = bends.Count > 0 ? bends[0].position : terminal;
        LayerMask mask = GetSolidMask();

        RaycastHit2D hit = Physics2D.Linecast(start.position, target, mask);
        if (hit.collider != null && bends.Count < maxRopeBends)
        {
            Vector2 bend = hit.point + hit.normal * ropeBendOffset;
            if (bends.Count == 0 || Vector2.Distance(bend, bends[0].position) > ropeCornerReleaseDistance)
            {
                float wrapSide = GetSide(target - bend, (Vector2)start.position - bend);
                if (Mathf.Abs(wrapSide) < 0.001f)
                    wrapSide = 1f;

                bends.Insert(0, new RopeBend(bend, hit.collider, hit.normal, Mathf.Sign(wrapSide)));
            }
        }

        if (bends.Count == 0) return;

        Vector2 next = bends.Count > 1 ? bends[1].position : terminal;
        if (ShouldReleaseBend(start.position, bends[0], next, mask))
            bends.RemoveAt(0);
    }

    private void StopIntoSolids()
    {
        if (bodyCollider == null) return;

        Vector2 velocity = Rb.linearVelocity;
        float speed = velocity.magnitude;
        if (speed < 0.001f) return;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(GetSolidMask());

        int count = bodyCollider.Cast(velocity / speed, filter, castHits, speed * Time.fixedDeltaTime + castSkin);
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = castHits[i];
            if (hit.collider == null || hit.collider == bodyCollider) continue;

            float intoSurfaceSpeed = Vector2.Dot(velocity, hit.normal);
            if (intoSurfaceSpeed < 0f)
                velocity -= hit.normal * intoSurfaceSpeed;
        }

        Rb.linearVelocity = velocity;
    }

    private bool IsBlockedToward(Vector2 target)
    {
        Vector2 direction = target - (Vector2)transform.position;
        float distance = direction.magnitude;
        if (distance < 0.001f) return false;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(GetSolidMask());

        int count = bodyCollider.Cast(direction / distance, filter, castHits, castSkin + anchorSurfaceOffset);
        for (int i = 0; i < count; i++)
        {
            if (castHits[i].collider != null && castHits[i].collider != bodyCollider)
                return true;
        }

        return false;
    }

    private Vector2 GetEffectiveAnchor(bool isLeft)
    {
        List<RopeBend> bends = isLeft ? leftBends : rightBends;
        return bends.Count > 0 ? bends[0].position : isLeft ? LeftAnchorPos : RightAnchorPos;
    }

    private bool HasCablePath(bool isLeft)
    {
        return isLeft
            ? IsLeftFlying || IsLeftAnchored
            : IsRightFlying || IsRightAnchored;
    }

    private Vector2 GetCableTerminal(bool isLeft)
    {
        if (isLeft)
            return IsLeftFlying ? LeftFlyTip : LeftAnchorPos;

        return IsRightFlying ? RightFlyTip : RightAnchorPos;
    }

    private float GetRopePathLength(bool isLeft)
    {
        if (!(isLeft ? IsLeftAnchored : IsRightAnchored)) return 0f;

        Vector2 anchor = isLeft ? LeftAnchorPos : RightAnchorPos;
        return GetRopePathLength(isLeft, anchor);
    }

    private float GetRopePathLength(bool isLeft, Vector2 terminal)
    {
        List<RopeBend> bends = isLeft ? leftBends : rightBends;
        Vector2 previous = Rb != null ? Rb.position : (Vector2)transform.position;
        float length = 0f;

        for (int i = 0; i < bends.Count; i++)
        {
            length += Vector2.Distance(previous, bends[i].position);
            previous = bends[i].position;
        }

        length += Vector2.Distance(previous, terminal);
        return length;
    }

    private bool ShouldReleaseBend(Vector2 start, RopeBend bend, Vector2 next, LayerMask mask)
    {
        if (Physics2D.Linecast(start, next, mask))
            return false;

        float currentSide = GetSide(next - bend.position, start - bend.position);
        return Mathf.Abs(currentSide) > 0.001f && Mathf.Sign(currentSide) != Mathf.Sign(bend.wrapSide);
    }

    private float GetSide(Vector2 axis, Vector2 point)
    {
        return axis.x * point.y - axis.y * point.x;
    }

    private Vector2 GetSafeAnchorPoint(RaycastHit2D hit)
    {
        return hit.point + hit.normal * anchorSurfaceOffset;
    }

    private Transform GetShootPoint(bool isLeft)
    {
        Transform point = isLeft ? leftAnchorPoint : rightAnchorPoint;
        return point != null ? point : transform;
    }

    private LayerMask GetSolidMask()
    {
        return solidLayer.value != 0 ? solidLayer : anchorableLayer;
    }

    private void SetRetracting(bool isLeft, bool value)
    {
        if (isLeft) leftFlyRetracting = value;
        else rightFlyRetracting = value;
    }

    private void CancelFlight(bool isLeft)
    {
        if (isLeft) IsLeftFlying = leftFlyRetracting = false;
        else IsRightFlying = rightFlyRetracting = false;

        if (!HasActiveCable())
            currentState = IsGrounded ? OdmState.Grounded : OdmState.Airborne;
    }

    private void ClearLeftCable()
    {
        IsLeftAnchored = false;
        IsLeftFlying = false;
        leftFlyRetracting = false;
        leftBends.Clear();
    }

    private void ClearRightCable()
    {
        IsRightAnchored = false;
        IsRightFlying = false;
        rightFlyRetracting = false;
        rightBends.Clear();
    }

    private bool HasActiveCable()
    {
        return IsLeftAnchored || IsRightAnchored || IsLeftFlying || IsRightFlying;
    }

    private bool HasFlyingCable()
    {
        return IsLeftFlying || IsRightFlying;
    }

    private bool HasAnchoredCable()
    {
        return IsLeftAnchored || IsRightAnchored;
    }

    private bool CheckGrounded()
    {
        if (bodyCollider == null) return false;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(GetSolidMask());

        int count = bodyCollider.Cast(Vector2.down, filter, castHits, groundCheckDistance);
        for (int i = 0; i < count; i++)
        {
            if (castHits[i].collider != null && castHits[i].collider != bodyCollider)
                return true;
        }

        return false;
    }

    private void UpdateStatePhysics()
    {
        if (!enableStatePhysics)
            return;

        float targetGravity = GetStateGravityScale();
        if (reduceGravityAtHighSpeed && !IsGrounded && targetGravity > 0f)
            targetGravity *= GetHighSpeedGravityMultiplier();

        float targetDamping = GetStateDamping();

        Rb.gravityScale = Mathf.MoveTowards(
            Rb.gravityScale,
            targetGravity,
            Mathf.Max(0f, gravityChangeSpeed) * Time.fixedDeltaTime);

        Rb.linearDamping = Mathf.MoveTowards(
            Rb.linearDamping,
            targetDamping,
            Mathf.Max(0f, dampingChangeSpeed) * Time.fixedDeltaTime);
    }

    private float GetStateGravityScale()
    {
        switch (currentState)
        {
            case OdmState.Grounded:
                return groundedGravityScale;
            case OdmState.Pulling:
                return IsGrounded ? groundedGravityScale : pullingGravityScale;
            case OdmState.Swinging:
                return swingingGravityScale;
            case OdmState.Hovering:
                return hoveringGravityScale;
            case OdmState.BurstPulling:
                return burstPullGravityScale;
            case OdmState.Shooting:
            case OdmState.Airborne:
            default:
                return airborneGravityScale;
        }
    }

    private float GetStateDamping()
    {
        switch (currentState)
        {
            case OdmState.Grounded:
                return groundedDamping;
            case OdmState.Pulling:
                return IsGrounded ? groundedDamping : pullingDamping;
            case OdmState.Swinging:
                return swingingDamping;
            case OdmState.Hovering:
                return hoveringDamping;
            case OdmState.BurstPulling:
                return burstPullDamping;
            case OdmState.Shooting:
            case OdmState.Airborne:
            default:
                return airborneDamping;
        }
    }

    private float GetHighSpeedGravityMultiplier()
    {
        float referenceSpeed = highSpeedGravityReference > 0f ? highSpeedGravityReference : maxSpeed;
        float speedRatio = Mathf.Clamp01(Rb.linearVelocity.magnitude / Mathf.Max(referenceSpeed, 0.001f));
        return Mathf.Lerp(1f, highSpeedGravityMinMultiplier, speedRatio);
    }

    private void ClampFallSpeed()
    {
        if (!limitFallSpeed || maxFallSpeed <= 0f || Rb.linearVelocity.y >= -maxFallSpeed)
            return;

        Rb.linearVelocity = new Vector2(Rb.linearVelocity.x, -maxFallSpeed);
    }

    private void ClampMaxSpeed()
    {
        if (Rb.linearVelocity.magnitude > maxSpeed)
            Rb.linearVelocity = Rb.linearVelocity.normalized * maxSpeed;
    }
}

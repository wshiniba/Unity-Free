using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum OdmState
{
    Grounded,

    Airborne,

    Shooting,

    Pulling,

    Swinging,

    Hovering
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

    private struct ColliderIgnorePair
    {
        public Collider2D targetCollider;
        public Collider2D playerCollider;

        public ColliderIgnorePair(Collider2D targetCollider, Collider2D playerCollider)
        {
            this.targetCollider = targetCollider;
            this.playerCollider = playerCollider;
        }

        public bool Matches(Collider2D target, Collider2D player)
        {
            return targetCollider == target && playerCollider == player;
        }
    }

    [Header("状态")]
    [Tooltip("当前 ODM 运行状态，仅用于调试观察，不建议手动修改。")]
    public OdmState currentState = OdmState.Grounded;

    private struct TentacleHitRecord
    {
        public Object target;
        public float lastHitTime;

        public TentacleHitRecord(Object target, float lastHitTime)
        {
            this.target = target;
            this.lastHitTime = lastHitTime;
        }
    }

    [Header("绳索锚点")]
    [Tooltip("绳索允许连接的目标层级。")]
    public LayerMask anchorableLayer;

    [Tooltip("会被视为实体障碍的层级，用于身体防穿模和绳索弯折；未设置时使用可连接目标层级。")]
    public LayerMask solidLayer;

    [Tooltip("角色判定站在地面时使用的层级。为空时沿用实体障碍层级。适合加入只能踩、但不影响触手弯折的层。")]
    public LayerMask groundLayer;

    [Tooltip("触手中段发生弯折和弹性视觉防穿模时使用的层级。为空时沿用实体障碍层级。不要把可抓取物或只可站立平台放进这里。")]
    public LayerMask cableBendLayer;

    [Tooltip("绳索弯折总控。开启后，cableBendLayer 中的层级可以让绳索中段弯折；关闭后任何层级都不会让绳索弯折，已有弯折点会被清空。触手末端撞墙和锚定逻辑不受影响。")]
    public bool enableCableBending = true;

    [Tooltip("左侧绳索发射点；未指定时使用玩家 Transform。")]
    public Transform leftAnchorPoint;

    [Tooltip("右侧绳索发射点；未指定时使用玩家 Transform。")]
    public Transform rightAnchorPoint;

    [Tooltip("绳索最大可用长度；超出该距离的命中会被忽略，飞行绳索会收回。")]
    public float maxCableLength = 30f;

    [Tooltip("绳索头发射和收回时的可视移动速度。")]
    public float cableShootSpeed = 50f;

    [Tooltip("连接点向碰撞体表面外侧偏移的距离，避免锚点卡进碰撞体内部。")]
    public float anchorSurfaceOffset = 0.15f;

    [Header("触手命中")]
    [Tooltip("触手飞行/甩动时可命中的层级。默认检测全部层级，实际只会伤害 Enemy 或 BossController。")]
    public LayerMask tentacleHitLayer = ~0;

    [Tooltip("触手线段扫过目标时使用的命中半径。")]
    public float tentacleHitRadius = 0.18f;

    [Tooltip("触手抽击的基础伤害。")]
    public int tentacleBaseDamage = 1;

    [Tooltip("触手末端速度转换为额外伤害的倍率。")]
    public float tentacleSpeedDamageScale = 0.08f;

    [Tooltip("触手速度低于该值时不造成抽击伤害，避免轻微接触反复掉血。")]
    public float tentacleMinHitSpeed = 4f;

    [Tooltip("同一条触手对同一个目标重复造成伤害的最短间隔。")]
    public float tentacleHitCooldown = 0.25f;

    [Tooltip("开启后，触手扫到 Enemy 时可按 Q 连接敌人。后续拉拽、举起和处决逻辑会基于这个连接关系扩展。")]
    public bool allowTentacleAnchorEnemies = true;

    [Header("触手受击")]
    [Tooltip("开启后，敌方攻击命中已经存在的触手线段时，会算作命中玩家并扣除玩家生命值。")]
    public bool allowCableDamagePlayer = true;

    [Tooltip("敌方攻击距离触手线段小于该半径时，视为命中触手。")]
    public float cableHurtRadius = 0.18f;

    [Tooltip("触手被敌方攻击命中后，短时间内不再重复受到触手命中伤害。")]
    public float cableDamageCooldown = 0.25f;

    [Header("触手弹反")]
    [Tooltip("触手韧性。敌方攻击力小于等于该值，且攻击允许弹反时，可以被触手弹反。")]
    public float cableToughness = 2f;

    [Tooltip("按下弹反键后，触手弹反判定持续的时间。")]
    public float cableParryWindow = 0.12f;

    [Tooltip("弹反成功后，玩家下一次可触发弹反前的冷却时间。")]
    public float cableParryCooldown = 0.25f;

    [Tooltip("弹反成功后，玩家获得的轻微后坐力。")]
    public float cableParrySelfKnockback = 2f;

    [Tooltip("开启后，按下弹反键会在角色前方生成一个短时间弹反判定区。敌方攻击进入该区域也可以被弹反。")]
    public bool allowCableParryArea = true;

    [Tooltip("弹反判定区的世界空间大小。X 控制前后宽度，Y 控制上下高度。")]
    public Vector2 cableParryAreaSize = new Vector2(1.6f, 2.0f);

    [Tooltip("弹反判定区相对玩家碰撞体中心的偏移。X 会跟随角色朝向自动镜像，正值代表面朝右时在角色前方。")]
    public Vector2 cableParryAreaOffset = new Vector2(0.8f, 0f);

    [Tooltip("选中玩家时，在 Scene 视图显示弹反判定区线框，方便调试判定范围。")]
    public bool showCableParryAreaGizmo = true;

    [Tooltip("弹反判定窗口激活时，角色 Shader 使用的白色闪光颜色。")]
    public Color cableParryFlashColor = Color.white;

    [Tooltip("弹反闪光开始时写入 Shader 的最大闪白强度。0 为无闪光，1 为完全混合到闪光颜色。")]
    [Range(0f, 1f)]
    public float cableParryFlashMaxAlpha = 0.9f;

    [Tooltip("弹反闪光使用的 Shader 名称。默认使用项目内的 Free/Sprite Parry Flash。")]
    public string cableParryFlashShaderName = "Free/Sprite Parry Flash";

    [Tooltip("弹反成功后，角色保持强闪白的持续时间。")]
    public float cableParrySuccessFlashDuration = 0.08f;

    [Tooltip("弹反成功瞬间写入 Shader 的最大闪白强度。0 为无闪光，1 为完全混合到闪光颜色。")]
    [Range(0f, 1f)]
    public float cableParrySuccessFlashMaxAmount = 1f;

    [Tooltip("开启后，弹反成功时会短暂降低全局时间倍率，制造命中顿帧。")]
    public bool enableCableParryHitStop = true;

    [Tooltip("弹反成功顿帧期间的全局时间倍率。数值越接近 0，顿帧越明显。")]
    [Range(0.001f, 1f)]
    public float cableParryHitStopTimeScale = 0.04f;

    [Tooltip("弹反成功顿帧持续时间，使用真实时间，不受 Time.timeScale 影响。")]
    public float cableParryHitStopDuration = 0.04f;

    [Header("触手投掷通用")]
    [Tooltip("抓取目标速度至少达到这个值时，才优先用甩动方向决定投掷方向。低于该值会改用合成速度或兜底方向，避免松手瞬间抖动导致反向甩出。")]
    public float throwAimDirectionMinSpeed = 1.2f;

    [Header("触手抓取敌人")]
    [Tooltip("触手当前力量。只和 Enemy 的质量比较来决定能否抓取；韧性不参与抓取资格。")]
    public float tentaclePower = 2f;

    [Tooltip("开启后，触手连接 Enemy 且力量大于等于敌人质量时，敌人会吸附到触手末端并随鼠标控制。")]
    public bool allowEnemyGrab = true;

    [Tooltip("抓取敌人后，触手末端拖动敌人的基础力度。数值越高，轻敌人越贴近鼠标控制。")]
    public float enemyGrabFollowForce = 55f;

    [Tooltip("抓取敌人后，触手对敌人速度施加的阻尼。数值越高越稳，但会降低甩动感。")]
    public float enemyGrabFollowDamping = 7f;

    [Tooltip("抓取敌人后，质量和韧性对触手控制灵敏度的影响倍率。数值越大，重敌人和高韧性敌人越拖拽。")]
    public float enemyGrabResistanceScale = 1f;

    [Tooltip("被抓取敌人的最大移动速度，避免轻敌人被触手瞬间甩出到不可控。")]
    public float enemyGrabMaxSpeed = 24f;

    [Tooltip("被抓取敌人吸附到触手末端时的局部偏移。一般保持为 0，让敌人中心贴近触手末端。")]
    public Vector2 enemyGrabLocalOffset = Vector2.zero;

    [Tooltip("松开对应鼠标键释放抓取敌人时，触手末端当前速度转换为投掷速度的倍率。")]
    public float enemyThrowVelocityScale = 1f;

    [Tooltip("抓取敌人时记录鼠标牵引速度的平滑强度。0 表示完全不平滑，数值越大越能过滤最后一帧抖动。")]
    public float enemyThrowVelocitySmoothing = 18f;

    [Tooltip("松开鼠标投掷敌人时的最低速度。低于该值会沿投掷方向补足速度，让轻甩也有基础力度。")]
    public float enemyThrowMinSpeed = 4f;

    [Tooltip("松开鼠标投掷敌人时的最高速度。用于避免鼠标瞬间抖动把敌人甩出过快。小于等于 0 时不限制。")]
    public float enemyThrowMaxSpeed = 32f;

    [Tooltip("投掷敌人时混入敌人自身当前速度的比例。适当提高可以让投掷更贴近敌人实际运动惯性。")]
    [Range(0f, 1f)]
    public float enemyThrowCurrentVelocityInfluence = 0.35f;

    [Tooltip("甩出敌人时继承玩家当前速度的比例。0 表示完全不继承，1 表示完整叠加玩家速度。")]
    [Range(0f, 1f)]
    public float enemyThrowPlayerVelocityInheritance = 0.35f;

    [Header("触手抓取场景物品")]
    [Tooltip("开启后，触手扫到 TentacleInteractableObject 时可按 Q 连接并抓取场景物品。")]
    public bool allowObjectGrab = true;

    [Tooltip("抓取场景物品后，触手末端拖动物品的基础力度。")]
    public float objectGrabFollowForce = 55f;

    [Tooltip("抓取场景物品后，对物品速度施加的阻尼。")]
    public float objectGrabFollowDamping = 7f;

    [Tooltip("抓取场景物品后，质量和抗拒程度对触手控制灵敏度的影响倍率。")]
    public float objectGrabResistanceScale = 1f;

    [Tooltip("被抓取场景物品的最大移动速度，避免轻物体被瞬间甩出到不可控。")]
    public float objectGrabMaxSpeed = 24f;

    [Tooltip("松开对应鼠标键释放抓取场景物品时，触手末端当前速度转换为投掷速度的倍率。")]
    public float objectThrowVelocityScale = 1f;

    [Tooltip("抓取场景物品时记录鼠标牵引速度的平滑强度。0 表示完全不平滑，数值越大越能过滤最后一帧抖动。")]
    public float objectThrowVelocitySmoothing = 18f;

    [Tooltip("松开鼠标投掷场景物品时的最低速度。低于该值会沿投掷方向补足速度。")]
    public float objectThrowMinSpeed = 4f;

    [Tooltip("松开鼠标投掷场景物品时的最高速度。小于等于 0 时不限制。")]
    public float objectThrowMaxSpeed = 32f;

    [Tooltip("投掷场景物品时混入物品自身当前速度的比例。适当提高可以让投掷更贴近物体实际运动惯性。")]
    [Range(0f, 1f)]
    public float objectThrowCurrentVelocityInfluence = 0.35f;

    [Tooltip("甩出场景物品时继承玩家当前速度的比例。0 表示完全不继承，1 表示完整叠加玩家速度。")]
    [Range(0f, 1f)]
    public float objectThrowPlayerVelocityInheritance = 0.35f;

    [Header("触手处决")]
    [Tooltip("按 E 查找可被单触手穿刺处决的敌人时使用的层级。")]
    public LayerMask executionTargetLayer = ~0;

    [Tooltip("按 E 自动穿刺处决时，鼠标离敌人多近会被视为优先目标。即使超过该半径，也会在最大距离内自动选择可处决敌人。")]
    public float executionSearchRadius = 1.2f;

    [Tooltip("开启后，左右触手都连接同一个可处决敌人时，按 E 会优先触发双触手撕裂处决。")]
    public bool allowTearExecution = true;

    [Tooltip("开启后，按 E 可对鼠标附近的低血量敌人触发单触手穿刺处决。")]
    public bool allowPierceExecution = true;

    [Tooltip("双触手撕裂处决的表现时长。结束后才会真正结算敌人死亡。")]
    public float tearExecutionDuration = 0.55f;

    [Tooltip("双触手撕裂处决时，左右触手末端向两边拉开的距离。")]
    public float tearExecutionPullDistance = 2.2f;

    [Tooltip("双触手撕裂处决时，触手专用曲线的分段数。数值越高越平滑。")]
    [Range(4, 32)]
    public int tearExecutionCableSegments = 14;

    [Tooltip("双触手撕裂处决时，触手中段向对应外侧偏移的距离。用于避免左右触手在敌人中心交叉。")]
    public float tearExecutionCableOutwardBend = 0.9f;

    [Tooltip("双触手撕裂处决时，靠近玩家一侧的触手中段下压距离。用于保留触手受力弧线。")]
    public float tearExecutionCableLowerBend = 0.45f;

    [Tooltip("双触手撕裂处决时，触手曲线离敌人中心线的最小水平距离。")]
    public float tearExecutionCenterClearance = 0.08f;

    [Tooltip("单触手穿刺处决的最低表现时长。实际时长还会根据距离和穿刺速度计算。")]
    public float pierceExecutionDuration = 0.06f;

    [Tooltip("单触手穿刺处决的触手飞出速度。数值越高，穿刺越快。")]
    public float pierceExecutionSpeed = 85f;

    [Tooltip("单触手穿刺处决时，触手穿过敌人后继续伸出的距离。")]
    public float pierceExecutionOverrunDistance = 1.1f;


    [Header("移动参数")]
    [Tooltip("玩家站在地面上时，由 A/D 或 Horizontal 输入控制的水平移动速度。")]
    public float groundMoveSpeed = 8f;

    [Tooltip("地面左右移动追接目标速度的加速度，数值越大响应越快。")]
    public float groundAcceleration = 60f;

    [Header("跳跃")]
    [Tooltip("按下跳跃键时写入 Rigidbody2D 的向上速度。数值越大，起跳越高。")]
    public float jumpSpeed = 18f;

    [Tooltip("土狼时间。角色刚离开地面后的这段时间内，仍然允许按跳跃键起跳，用于减少从平台边缘落下时跳跃失误。")]
    public float coyoteTime = 0.1f;

    [Tooltip("摆荡时由 A/D 或 Horizontal 输入施加的水平力。")]
    public float swingForce = 10f;

    [Tooltip("按住 Q 时，朝当前绳索锚点施加给玩家的牵引力。")]
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


    [Header("速度限制")]
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

    [Header("受伤击退")]
    [Tooltip("玩家受到伤害并被击退后，暂时不让地面移动覆盖击退速度的时间。数值越大，敌人接触造成的击退越明显。")]
    public float playerDamageKnockbackControlLock = 0.18f;

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

    public bool IsLeftEnemyAnchored => leftEnemyAnchorActive;

    public bool IsRightEnemyAnchored => rightEnemyAnchorActive;

    public bool IsExecutionActive => executionActive;

    public LayerMask CableSolidMask => enableCableBending ? GetCableBendMask() : EmptyLayerMask();

    public Vector2 LeftFlyTip { get; private set; }

    public Vector2 RightFlyTip { get; private set; }

    private Collider2D bodyCollider;
    private Vector2 bodyColliderOriginalOffset;

    private OdmGasSystem gasSystem;
    private PlayerHealth playerHealth;

    private float horizontalInput;
    private float lastGroundedTime = float.NegativeInfinity;
    private bool jumpConsumedSinceGrounded;


    private float leftHoverRopeLength;
    private float rightHoverRopeLength;

    private float defaultGravityScale;
    private float nextCableDamageTime;
    private float damageKnockbackControlLockUntil;
    private float parryActiveUntil;
    private float parrySuccessFlashUntil;
    private float nextParryTime;
    private Material parryRuntimeMaterial;
    private Material originalCharacterMaterial;
    private bool isParryHighlighted;
    private Coroutine parryHitStopRoutine;
    private int parryHitStopToken;

    private float leftRopeLength;
    private float rightRopeLength;

    private Vector2 leftFlyTarget;
    private Vector2 rightFlyTarget;
    private Vector2 leftPreviousTentacleHitTip;
    private Vector2 rightPreviousTentacleHitTip;
    private Transform leftTouchedEnemyTarget;
    private Transform rightTouchedEnemyTarget;
    private Vector2 leftTouchedEnemyLocalPoint;
    private Vector2 rightTouchedEnemyLocalPoint;
    private Transform leftTouchedObjectTarget;
    private Transform rightTouchedObjectTarget;
    private Vector2 leftTouchedObjectLocalPoint;
    private Vector2 rightTouchedObjectLocalPoint;
    private Transform leftEnemyAnchorTarget;
    private Transform rightEnemyAnchorTarget;
    private Vector2 leftEnemyAnchorLocalPoint;
    private Vector2 rightEnemyAnchorLocalPoint;
    private bool leftEnemyAnchorActive;
    private bool rightEnemyAnchorActive;
    private Transform leftObjectAnchorTarget;
    private Transform rightObjectAnchorTarget;
    private Vector2 leftObjectAnchorLocalPoint;
    private Vector2 rightObjectAnchorLocalPoint;
    private bool leftObjectAnchorActive;
    private bool rightObjectAnchorActive;
    private Vector2 leftEnemyGrabTarget;
    private Vector2 rightEnemyGrabTarget;
    private Vector2 leftGrabVelocity;
    private Vector2 rightGrabVelocity;
    private bool executionActive;
    private bool executionUsesLeftCable;
    private Enemy executionEnemy;
    private Rigidbody2D executionEnemyBody;
    private EnemyExecutionType executionType;
    private float executionTimer;
    private float executionDuration;
    private Vector2 executionEnemyCenter;
    private Vector2 executionLeftStart;
    private Vector2 executionRightStart;
    private Vector2 executionPierceStart;
    private Vector2 executionPierceEnd;
    private bool leftFlyRetracting;
    private bool rightFlyRetracting;

    private bool leftTipTouchingSurface;
    private bool rightTipTouchingSurface;
    private Vector2 leftTipSurfaceNormal;
    private Vector2 rightTipSurfaceNormal;
    private Collider2D leftTipSurfaceCollider;
    private Collider2D rightTipSurfaceCollider;

    private readonly List<RopeBend> leftBends = new List<RopeBend>();
    private readonly List<RopeBend> rightBends = new List<RopeBend>();
    private readonly List<ColliderIgnorePair> currentHeldTargetColliderPairs = new List<ColliderIgnorePair>();
    private readonly List<ColliderIgnorePair> ignoredHeldTargetColliderPairs = new List<ColliderIgnorePair>();

    private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
    private readonly RaycastHit2D[] tentacleHitResults = new RaycastHit2D[16];
    private readonly Collider2D[] executionResults = new Collider2D[16];
    private readonly Collider2D[] enemyContactResults = new Collider2D[16];
    private ContactFilter2D tentacleHitFilter;
    private readonly List<TentacleHitRecord> leftTentacleHitRecords = new List<TentacleHitRecord>();
    private readonly List<TentacleHitRecord> rightTentacleHitRecords = new List<TentacleHitRecord>();

    private void OnValidate()
    {
        coyoteTime = Mathf.Max(0f, coyoteTime);
        playerDamageKnockbackControlLock = Mathf.Max(0f, playerDamageKnockbackControlLock);
        throwAimDirectionMinSpeed = Mathf.Max(0f, throwAimDirectionMinSpeed);

        enemyThrowVelocityScale = Mathf.Max(0f, enemyThrowVelocityScale);
        enemyThrowVelocitySmoothing = Mathf.Max(0f, enemyThrowVelocitySmoothing);
        enemyThrowMinSpeed = Mathf.Max(0f, enemyThrowMinSpeed);
        enemyThrowMaxSpeed = Mathf.Max(0f, enemyThrowMaxSpeed);
        enemyThrowPlayerVelocityInheritance = Mathf.Clamp01(enemyThrowPlayerVelocityInheritance);
        enemyThrowCurrentVelocityInfluence = Mathf.Clamp01(enemyThrowCurrentVelocityInfluence);

        objectThrowVelocityScale = Mathf.Max(0f, objectThrowVelocityScale);
        objectThrowVelocitySmoothing = Mathf.Max(0f, objectThrowVelocitySmoothing);
        objectThrowMinSpeed = Mathf.Max(0f, objectThrowMinSpeed);
        objectThrowMaxSpeed = Mathf.Max(0f, objectThrowMaxSpeed);
        objectThrowPlayerVelocityInheritance = Mathf.Clamp01(objectThrowPlayerVelocityInheritance);
        objectThrowCurrentVelocityInfluence = Mathf.Clamp01(objectThrowCurrentVelocityInfluence);
    }

    void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        if (bodyCollider != null)
            bodyColliderOriginalOffset = bodyCollider.offset;
        gasSystem = GetComponent<OdmGasSystem>();
        playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = gameObject.AddComponent<PlayerHealth>();
        if (characterRenderer == null) characterRenderer = GetComponent<SpriteRenderer>();
        if (characterAnimator == null) characterAnimator = GetComponent<Animator>();
        EnsureParryFlashMaterial();

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
        ConfigureTentacleHitFilter();
    }

    private void OnDestroy()
    {
        RestoreAllHeldTargetPlayerCollisions();

        if (characterRenderer != null && originalCharacterMaterial != null)
            characterRenderer.sharedMaterial = originalCharacterMaterial;

        if (parryRuntimeMaterial != null)
            Destroy(parryRuntimeMaterial);

        if (parryHitStopRoutine != null)
        {
            StopCoroutine(parryHitStopRoutine);
            TimeScaleHitStop.End(parryHitStopToken);
            parryHitStopToken = 0;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showCableParryAreaGizmo)
            return;

        Collider2D gizmoCollider = bodyCollider != null ? bodyCollider : GetComponent<Collider2D>();
        SpriteRenderer gizmoRenderer = characterRenderer != null ? characterRenderer : GetComponent<SpriteRenderer>();
        bool facingLeft = gizmoRenderer != null && gizmoRenderer.flipX;
        Vector2 baseCenter = gizmoCollider != null ? (Vector2)gizmoCollider.bounds.center : (Vector2)transform.position;
        Vector2 size = new Vector2(
            Mathf.Max(0.01f, cableParryAreaSize.x),
            Mathf.Max(0.01f, cableParryAreaSize.y));
        float offsetX = Mathf.Abs(cableParryAreaOffset.x) * (facingLeft ? -1f : 1f);
        Vector2 center = baseCenter + new Vector2(offsetX, cableParryAreaOffset.y);

        Gizmos.color = Time.time <= parryActiveUntil
            ? new Color(1f, 1f, 1f, 0.9f)
            : new Color(0.35f, 0.9f, 1f, 0.65f);
        Gizmos.DrawWireCube(center, size);
    }

    void Update()
    {
        UpdateCableParryHighlight();
        UpdateExecution();
        UpdateCableFlight();
        UpdateEnemyAnchorPositions();
        UpdateCableBends();
        DetectTentacleHits();
    }

    void FixedUpdate()
    {
        IsGrounded = CheckGrounded();
        UpdateCoyoteTimeState();
        UpdateEnemyAnchorPositions();
        UpdateMovementState();
        UpdateStatePhysics();
        UpdateCableBends();
        ApplyMovement();
        ApplyEnemyGrabControl();
        ApplyRopeLimits();
        StopIntoSolids();
        GetDamage();
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
        bool groundedNow = IsGrounded || CheckGrounded();
        bool canUseCoyoteTime = !jumpConsumedSinceGrounded && Time.time - lastGroundedTime <= coyoteTime;
        if (!groundedNow && !canUseCoyoteTime)
            return false;

        SetHovering(false);

        Vector2 velocity = Rb.linearVelocity;
        velocity.y = Mathf.Max(velocity.y, jumpSpeed);
        Rb.linearVelocity = velocity;

        currentState = OdmState.Airborne;
        IsGrounded = false;
        jumpConsumedSinceGrounded = true;
        lastGroundedTime = float.NegativeInfinity;
        return true;
    }

    private void UpdateCoyoteTimeState()
    {
        if (!IsGrounded)
            return;

        lastGroundedTime = Time.time;

        if (Rb == null || Rb.linearVelocity.y <= 0.1f)
            jumpConsumedSinceGrounded = false;
    }

    private void UpdateFacingDirection(float input)
    {
        if (characterRenderer == null || Mathf.Abs(input) <= facingInputThreshold) return;

        bool facingLeft = input < 0f;
        characterRenderer.flipX = facingLeft;
        UpdateBodyColliderFacing(facingLeft);
    }

    private void UpdateBodyColliderFacing(bool facingLeft)
    {
        if (bodyCollider == null)
            return;

        bodyCollider.offset = new Vector2(
            facingLeft ? -bodyColliderOriginalOffset.x : bodyColliderOriginalOffset.x,
            bodyColliderOriginalOffset.y);
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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        GetDamage(collision != null ? collision.collider : null);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        GetDamage(collision != null ? collision.collider : null);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        GetDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        GetDamage(other);
    }

    public bool GetDamage(Collider2D enemyCollider)
    {
        if (enemyCollider == null)
            return false;

        Enemy enemy = enemyCollider.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.currentHealth <= 0)
            return false;

        if (!enemyCollider.CompareTag("Enemy") && !enemy.CompareTag("Enemy"))
            return false;

        EnemySimpleAI enemyAI = enemy.GetComponent<EnemySimpleAI>();
        int damage = Mathf.Max(0, enemy.hitBoxDamage);
        float knockback = enemyAI != null ? Mathf.Max(0f, enemyAI.attackKnockback) : damage;
        return GetDamage(damage, enemy.transform.position, knockback);
    }

    public bool GetDamage()
    {
        if (bodyCollider == null)
            return false;

        Bounds bounds = bodyCollider.bounds;
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.y) + 0.35f;
        int count = Physics2D.OverlapCircleNonAlloc(bounds.center, radius, enemyContactResults);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = enemyContactResults[i];
            enemyContactResults[i] = null;
            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null || enemy.currentHealth <= 0 || !enemy.CompareTag("Enemy"))
                continue;

            if (!IsTouchingEnemy(enemy))
                continue;

            EnemySimpleAI enemyAI = enemy.GetComponent<EnemySimpleAI>();
            int damage = Mathf.Max(0, enemy.hitBoxDamage);
            float knockback = enemyAI != null ? Mathf.Max(0f, enemyAI.attackKnockback) : damage;
            return GetDamage(damage, enemy.transform.position, knockback);
        }

        return false;
    }

    private bool IsTouchingEnemy(Enemy enemy)
    {
        if (bodyCollider == null || enemy == null)
            return false;

        Collider2D[] enemyColliders = enemy.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < enemyColliders.Length; i++)
        {
            Collider2D enemyCollider = enemyColliders[i];
            if (enemyCollider == null || !enemyCollider.enabled)
                continue;

            ColliderDistance2D distance = bodyCollider.Distance(enemyCollider);
            if (distance.isOverlapped || distance.distance <= Physics2D.defaultContactOffset + 0.03f)
                return true;
        }

        return false;
    }

    public bool GetDamage(int damage, Vector2 hazardPosition, float knockbackForce)
    {
        if (damage <= 0)
            return false;

        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (playerHealth != null && !playerHealth.TakeDamage(damage))
            return false;

        ApplyHazardKnockback(hazardPosition, knockbackForce);
        return true;
    }

    public bool TryDamageCableFromHazard(Collider2D hazardCollider, Vector2 hazardPosition, float knockbackForce, int damage)
    {
        if (!allowCableDamagePlayer || hazardCollider == null || Time.time < nextCableDamageTime)
            return false;

        if (!IsHazardTouchingCable(hazardCollider, true) && !IsHazardTouchingCable(hazardCollider, false))
            return false;

        if (!GetDamage(damage, hazardPosition, knockbackForce))
            return false;

        nextCableDamageTime = Time.time + Mathf.Max(0f, cableDamageCooldown);
        return true;
    }

    public void BeginCableParry()
    {
        if (Time.time < nextParryTime)
            return;

        parrySuccessFlashUntil = 0f;
        parryActiveUntil = Time.time + Mathf.Max(0.01f, cableParryWindow);
        nextParryTime = Time.time + Mathf.Max(0f, cableParryCooldown);
        SetCableParryHighlight(true);
    }

    public bool TryParryHazard(Collider2D hazardCollider, Vector2 hazardPosition, int attackPower, bool canBeParried)
    {
        if (!canBeParried || hazardCollider == null || Time.time > parryActiveUntil)
            return false;

        if (attackPower > cableToughness)
            return false;

        bool touchesBody = bodyCollider != null && hazardCollider.IsTouching(bodyCollider);
        bool touchesCable = IsHazardTouchingCable(hazardCollider, true) || IsHazardTouchingCable(hazardCollider, false);
        bool touchesParryArea = IsHazardInsideParryArea(hazardCollider);
        if (!touchesBody && !touchesCable && !touchesParryArea)
            return false;

        ApplyHazardKnockback(hazardPosition, cableParrySelfKnockback);
        parryActiveUntil = 0f;
        PlayCableParrySuccessFeedback();
        return true;
    }

    private bool IsHazardInsideParryArea(Collider2D hazardCollider)
    {
        if (!allowCableParryArea || hazardCollider == null)
            return false;

        Rect parryRect = GetCableParryAreaRect();
        Vector2 hazardCenter = hazardCollider.bounds.center;
        if (parryRect.Contains(hazardCenter))
            return true;

        Vector2 closestPoint = hazardCollider.ClosestPoint(hazardCenter);
        if (parryRect.Contains(closestPoint))
            return true;

        Vector2 closestInRect = new Vector2(
            Mathf.Clamp(hazardCenter.x, parryRect.xMin, parryRect.xMax),
            Mathf.Clamp(hazardCenter.y, parryRect.yMin, parryRect.yMax));
        Vector2 closestOnHazard = hazardCollider.ClosestPoint(closestInRect);
        return parryRect.Contains(closestOnHazard);
    }

    private Vector2 GetCableParryAreaCenter()
    {
        Vector2 baseCenter = bodyCollider != null ? (Vector2)bodyCollider.bounds.center : (Vector2)transform.position;
        float offsetX = Mathf.Abs(cableParryAreaOffset.x) * (IsFacingLeft() ? -1f : 1f);
        return baseCenter + new Vector2(offsetX, cableParryAreaOffset.y);
    }

    private Rect GetCableParryAreaRect()
    {
        Vector2 size = new Vector2(
            Mathf.Max(0.01f, cableParryAreaSize.x),
            Mathf.Max(0.01f, cableParryAreaSize.y));
        Vector2 center = GetCableParryAreaCenter();
        return new Rect(center - size * 0.5f, size);
    }

    private void UpdateCableParryHighlight()
    {
        if (Time.unscaledTime <= parrySuccessFlashUntil)
        {
            EnsureParryFlashMaterial();
            if (parryRuntimeMaterial == null)
                return;

            float successFlashDuration = Mathf.Max(0.01f, cableParrySuccessFlashDuration);
            float successFlashRemaining = Mathf.Clamp01((parrySuccessFlashUntil - Time.unscaledTime) / successFlashDuration);
            parryRuntimeMaterial.SetColor("_FlashColor", cableParryFlashColor);
            parryRuntimeMaterial.SetFloat("_FlashAmount", Mathf.Clamp01(cableParrySuccessFlashMaxAmount) * successFlashRemaining);
            isParryHighlighted = true;
            return;
        }

        if (parrySuccessFlashUntil > 0f)
        {
            parrySuccessFlashUntil = 0f;
            SetCableParryHighlight(false);
            return;
        }

        if (isParryHighlighted && Time.time > parryActiveUntil)
        {
            SetCableParryHighlight(false);
            return;
        }

        if (!isParryHighlighted || parryRuntimeMaterial == null)
            return;

        float activeFlashDuration = Mathf.Max(0.01f, cableParryWindow);
        float activeFlashRemaining = Mathf.Clamp01((parryActiveUntil - Time.time) / activeFlashDuration);
        parryRuntimeMaterial.SetFloat("_FlashAmount", Mathf.Clamp01(cableParryFlashMaxAlpha) * activeFlashRemaining);
    }

    private void SetCableParryHighlight(bool highlighted)
    {
        EnsureParryFlashMaterial();
        if (parryRuntimeMaterial == null)
            return;

        if (highlighted)
        {
            parryRuntimeMaterial.SetColor("_FlashColor", cableParryFlashColor);
            parryRuntimeMaterial.SetFloat("_FlashAmount", Mathf.Clamp01(cableParryFlashMaxAlpha));
            isParryHighlighted = true;
            return;
        }

        if (!isParryHighlighted)
            return;

        parryRuntimeMaterial.SetFloat("_FlashAmount", 0f);
        isParryHighlighted = false;
    }

    private void EnsureParryFlashMaterial()
    {
        if (parryRuntimeMaterial != null || characterRenderer == null)
            return;

        Shader flashShader = Shader.Find(cableParryFlashShaderName);
        if (flashShader == null)
        {
            Debug.LogWarning($"找不到弹反闪光 Shader：{cableParryFlashShaderName}", this);
            return;
        }

        originalCharacterMaterial = characterRenderer.sharedMaterial;
        parryRuntimeMaterial = new Material(flashShader);
        parryRuntimeMaterial.SetColor("_FlashColor", cableParryFlashColor);
        parryRuntimeMaterial.SetFloat("_FlashAmount", 0f);
        characterRenderer.material = parryRuntimeMaterial;
    }

    private void PlayCableParrySuccessFeedback()
    {
        parrySuccessFlashUntil = Time.unscaledTime + Mathf.Max(0f, cableParrySuccessFlashDuration);

        EnsureParryFlashMaterial();
        if (parryRuntimeMaterial != null)
        {
            parryRuntimeMaterial.SetColor("_FlashColor", cableParryFlashColor);
            parryRuntimeMaterial.SetFloat("_FlashAmount", Mathf.Clamp01(cableParrySuccessFlashMaxAmount));
            isParryHighlighted = true;
        }

        if (enableCableParryHitStop)
            StartCableParryHitStop();
    }

    private void StartCableParryHitStop()
    {
        if (parryHitStopRoutine != null)
        {
            StopCoroutine(parryHitStopRoutine);
            TimeScaleHitStop.End(parryHitStopToken);
            parryHitStopToken = 0;
        }

        parryHitStopRoutine = StartCoroutine(CableParryHitStopRoutine());
    }

    private IEnumerator CableParryHitStopRoutine()
    {
        parryHitStopToken = TimeScaleHitStop.Begin(cableParryHitStopTimeScale);

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, cableParryHitStopDuration));

        TimeScaleHitStop.End(parryHitStopToken);
        parryHitStopToken = 0;
        parryHitStopRoutine = null;
    }

    public Vector3[] GetCablePath(bool isLeft)
    {
        Vector3[] executionPath = GetExecutionCablePath(isLeft);
        if (executionPath != null)
            return executionPath;

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

    private void ApplyHazardKnockback(Vector2 hazardPosition, float knockbackForce)
    {
        if (Rb == null || knockbackForce <= 0f)
            return;

        Vector2 direction = ((Vector2)transform.position - hazardPosition).normalized;
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.up;

        Rb.linearVelocity = Vector2.zero;
        Rb.AddForce(direction * knockbackForce, ForceMode2D.Impulse);
        damageKnockbackControlLockUntil = Time.time + Mathf.Max(0f, playerDamageKnockbackControlLock);
    }

    private bool IsHazardTouchingCable(Collider2D hazardCollider, bool isLeft)
    {
        Vector3[] path = GetCablePath(isLeft);
        if (path == null || path.Length < 2)
            return false;

        for (int i = 1; i < path.Length; i++)
        {
            if (IsHazardTouchingCableSegment(hazardCollider, path[i - 1], path[i]))
                return true;
        }

        return false;
    }

    private bool IsHazardTouchingCableSegment(Collider2D hazardCollider, Vector2 from, Vector2 to)
    {
        Vector2 segment = to - from;
        float length = segment.magnitude;
        if (length < 0.001f)
            return false;

        int sampleCount = Mathf.Clamp(Mathf.CeilToInt(length / Mathf.Max(0.05f, cableHurtRadius)), 2, 24);
        float hurtRadius = Mathf.Max(0.01f, cableHurtRadius);

        for (int i = 0; i <= sampleCount; i++)
        {
            Vector2 point = Vector2.Lerp(from, to, i / (float)sampleCount);
            Vector2 closest = hazardCollider.ClosestPoint(point);
            if ((closest - point).sqrMagnitude <= hurtRadius * hurtRadius)
                return true;
        }

        return false;
    }

    public void ShootCable(Vector2 direction, bool isLeft)
    {
        if (executionActive)
            return;

        if (gasSystem.IsGasEmpty || direction.sqrMagnitude < 0.001f) return;
        if (isLeft ? IsLeftFlying || IsLeftAnchored : IsRightFlying || IsRightAnchored) return;

        Transform start = GetShootPoint(isLeft);
        Vector2 dir = direction.normalized;
        Vector2 target = (Vector2)start.position + dir * Mathf.Max(0.1f, maxCableLength);

        if (isLeft)
        {
            IsLeftFlying = true;
            LeftFlyTip = start.position;
            leftFlyTarget = target;
            leftPreviousTentacleHitTip = start.position;
            leftFlyRetracting = false;
            SetTipSurfaceContact(true, false, Vector2.zero, null);
            leftBends.Clear();
            leftTentacleHitRecords.Clear();
        }
        else
        {
            IsRightFlying = true;
            RightFlyTip = start.position;
            rightFlyTarget = target;
            rightPreviousTentacleHitTip = start.position;
            rightFlyRetracting = false;
            SetTipSurfaceContact(false, false, Vector2.zero, null);
            rightBends.Clear();
            rightTentacleHitRecords.Clear();
        }

        currentState = OdmState.Shooting;
    }

    public void UpdateCableTarget(Vector2 worldTarget, bool isLeft)
    {
        bool isFlying = isLeft ? IsLeftFlying : IsRightFlying;
        bool isRetracting = isLeft ? leftFlyRetracting : rightFlyRetracting;
        bool isGrabbingEnemy = IsGrabbingEnemy(isLeft);
        bool isGrabbingObject = IsGrabbingObject(isLeft);
        if ((!isFlying && !isGrabbingEnemy && !isGrabbingObject) || isRetracting)
            return;

        Transform start = GetShootPoint(isLeft);
        Vector2 fromStart = worldTarget - (Vector2)start.position;
        float maxLength = Mathf.Max(0.1f, maxCableLength);
        Vector2 target = fromStart.magnitude > maxLength
            ? (Vector2)start.position + fromStart.normalized * maxLength
            : worldTarget;

        if (isGrabbingEnemy || isGrabbingObject)
            SetEnemyGrabTarget(isLeft, target);
        else if (isLeft)
            leftFlyTarget = target;
        else
            rightFlyTarget = target;
    }

    public bool TryAnchorTouchedCable()
    {
        bool anchored = false;

        if (TryAnchorTouchedEnemy(true))
            anchored = true;

        if (TryAnchorTouchedEnemy(false))
            anchored = true;

        if (TryAnchorTouchedObject(true))
            anchored = true;

        if (TryAnchorTouchedObject(false))
            anchored = true;

        if (IsLeftFlying && leftTipTouchingSurface && CanAnchorTouchedSurface(true))
        {
            AnchorCable(true, LeftFlyTip);
            anchored = true;
        }

        if (IsRightFlying && rightTipTouchingSurface && CanAnchorTouchedSurface(false))
        {
            AnchorCable(false, RightFlyTip);
            anchored = true;
        }

        return anchored;
    }

    private bool TryAnchorTouchedEnemy(bool isLeft)
    {
        if (!allowTentacleAnchorEnemies)
            return false;

        bool isFlying = isLeft ? IsLeftFlying : IsRightFlying;
        Transform target = isLeft ? leftTouchedEnemyTarget : rightTouchedEnemyTarget;
        if (!isFlying || target == null)
            return false;

        Vector2 localPoint = isLeft ? leftTouchedEnemyLocalPoint : rightTouchedEnemyLocalPoint;
        AnchorCableToEnemy(isLeft, target, localPoint);
        return true;
    }

    private bool TryAnchorTouchedObject(bool isLeft)
    {
        if (!allowObjectGrab)
            return false;

        bool isFlying = isLeft ? IsLeftFlying : IsRightFlying;
        Transform target = isLeft ? leftTouchedObjectTarget : rightTouchedObjectTarget;
        if (!isFlying || target == null)
            return false;

        if (!IsLayerInMask(target.gameObject.layer, anchorableLayer))
            return false;

        Vector2 localPoint = isLeft ? leftTouchedObjectLocalPoint : rightTouchedObjectLocalPoint;
        AnchorCableToObject(isLeft, target, localPoint);
        return true;
    }

    public bool TryExecuteEnemy(Vector2 targetPosition)
    {
        if (executionActive)
            return false;

        if (TryExecuteTearEnemy())
            return true;

        return TryExecutePierceEnemy(targetPosition);
    }

    private bool TryExecuteTearEnemy()
    {
        if (!allowTearExecution || !leftEnemyAnchorActive || !rightEnemyAnchorActive)
            return false;

        Transform target = leftEnemyAnchorTarget;
        if (target == null || target != rightEnemyAnchorTarget)
            return false;

        Enemy enemy = target.GetComponent<Enemy>();
        if (enemy == null || !enemy.CanBeExecuted() || tentaclePower < enemy.enemyMass)
            return false;

        if (!enemy.BeginTentacleExecution(EnemyExecutionType.Tear))
            return false;

        StartTearExecution(enemy);
        return true;
    }

    private bool TryExecutePierceEnemy(Vector2 targetPosition)
    {
        if (!allowPierceExecution)
            return false;

        Enemy enemy = FindPierceExecutionTarget(targetPosition);
        if (enemy == null)
            return false;

        if (!enemy.BeginTentacleExecution(EnemyExecutionType.Pierce))
            return false;

        StartPierceExecution(enemy);
        return true;
    }

    private Enemy FindPierceExecutionTarget(Vector2 targetPosition)
    {
        float maxDistance = Mathf.Max(0.01f, maxCableLength);
        Vector2 playerPosition = transform.position;
        int hitCount = Physics2D.OverlapCircleNonAlloc(playerPosition, maxDistance, executionResults, executionTargetLayer);
        Enemy bestEnemy = null;
        float bestScore = float.MaxValue;
        float cursorPriorityRadius = Mathf.Max(0.01f, executionSearchRadius);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = executionResults[i];
            executionResults[i] = null;
            if (hit == null)
                continue;

            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null || !enemy.CanBeExecuted())
                continue;

            Vector2 enemyPosition = enemy.transform.position;
            if (Vector2.Distance(playerPosition, enemyPosition) > maxDistance)
                continue;

            float distanceToCursor = Vector2.Distance(targetPosition, enemyPosition);
            float distanceToPlayer = Vector2.Distance(playerPosition, enemyPosition);
            float cursorPenalty = distanceToCursor <= cursorPriorityRadius ? 0f : cursorPriorityRadius;
            float score = distanceToCursor + cursorPenalty + distanceToPlayer * 0.15f;
            if (score >= bestScore)
                continue;

            bestEnemy = enemy;
            bestScore = score;
        }

        return bestEnemy;
    }

    private void StartTearExecution(Enemy enemy)
    {
        executionActive = true;
        executionType = EnemyExecutionType.Tear;
        executionEnemy = enemy;
        executionEnemyBody = enemy.GetComponent<Rigidbody2D>();
        executionTimer = 0f;
        executionDuration = Mathf.Max(0.01f, tearExecutionDuration);
        executionEnemyCenter = enemy.transform.position;
        executionLeftStart = GetEnemyExecutionSidePoint(enemy, -1f);
        executionRightStart = GetEnemyExecutionSidePoint(enemy, 1f);
        LeftAnchorPos = executionLeftStart;
        RightAnchorPos = executionRightStart;
        enemy.BeginTearExecutionVisual(LeftAnchorPos, RightAnchorPos);

        IsLeftAnchored = true;
        IsRightAnchored = true;
        IsLeftFlying = false;
        IsRightFlying = false;
        leftFlyRetracting = false;
        rightFlyRetracting = false;
        leftBends.Clear();
        rightBends.Clear();
        currentState = IsGrounded ? OdmState.Grounded : OdmState.Airborne;
    }

    private void StartPierceExecution(Enemy enemy)
    {
        executionActive = true;
        executionType = EnemyExecutionType.Pierce;
        executionEnemy = enemy;
        executionEnemyBody = enemy.GetComponent<Rigidbody2D>();
        executionTimer = 0f;
        Vector2 enemyCenter = enemy.transform.position;

        Vector2 leftStart = GetShootPoint(true).position;
        Vector2 rightStart = GetShootPoint(false).position;
        executionUsesLeftCable = Vector2.Distance(leftStart, enemyCenter) <= Vector2.Distance(rightStart, enemyCenter);
        executionPierceStart = executionUsesLeftCable ? leftStart : rightStart;
        executionPierceEnd = GetPierceExecutionEndPoint(enemy);
        float pierceDistance = Vector2.Distance(executionPierceStart, executionPierceEnd);
        executionDuration = Mathf.Max(Mathf.Max(0.01f, pierceExecutionDuration), pierceDistance / Mathf.Max(0.01f, pierceExecutionSpeed));

        if (executionUsesLeftCable)
        {
            ClearLeftCable();
            IsLeftFlying = true;
            LeftFlyTip = executionPierceStart;
        }
        else
        {
            ClearRightCable();
            IsRightFlying = true;
            RightFlyTip = executionPierceStart;
        }
    }

    private Vector2 GetEnemyExecutionSidePoint(Enemy enemy, float side)
    {
        if (enemy == null)
            return executionEnemyCenter;

        SpriteRenderer renderer = FindEnemyMainRenderer(enemy);
        if (renderer != null)
        {
            Bounds bounds = renderer.bounds;
            float x = side < 0f ? bounds.min.x : bounds.max.x;
            return new Vector2(x, bounds.center.y);
        }

        Collider2D enemyCollider = enemy.GetComponentInChildren<Collider2D>();
        if (enemyCollider != null)
        {
            Bounds bounds = enemyCollider.bounds;
            float x = side < 0f ? bounds.min.x : bounds.max.x;
            return new Vector2(x, bounds.center.y);
        }

        return (Vector2)enemy.transform.position + (side < 0f ? Vector2.left : Vector2.right) * 0.35f;
    }

    private SpriteRenderer FindEnemyMainRenderer(Enemy enemy)
    {
        if (enemy == null)
            return null;

        SpriteRenderer[] renderers = enemy.GetComponentsInChildren<SpriteRenderer>();
        SpriteRenderer best = null;
        float bestArea = 0f;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null || renderer.name.StartsWith("HealthBar"))
                continue;

            Vector3 size = renderer.bounds.size;
            float area = Mathf.Abs(size.x * size.y);
            if (best != null && area <= bestArea)
                continue;

            best = renderer;
            bestArea = area;
        }

        return best;
    }

    private Vector2 GetPierceExecutionEndPoint(Enemy enemy)
    {
        if (enemy == null)
            return executionPierceEnd;

        Vector2 enemyCenter = enemy.transform.position;
        Vector2 direction = enemyCenter - executionPierceStart;
        if (direction.sqrMagnitude < 0.0001f)
            direction = IsFacingLeft() ? Vector2.left : Vector2.right;

        return enemyCenter + direction.normalized * Mathf.Max(0f, pierceExecutionOverrunDistance);
    }

    private bool IsFacingLeft()
    {
        return characterRenderer != null && characterRenderer.flipX;
    }

    private void UpdateExecution()
    {
        if (!executionActive)
            return;

        if (executionEnemy == null)
        {
            FinishExecution(false);
            return;
        }

        executionTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(executionTimer / Mathf.Max(0.01f, executionDuration));

        if (executionType == EnemyExecutionType.Tear)
        {
            if (executionEnemyBody != null)
            {
                executionEnemyBody.linearVelocity = Vector2.zero;
                executionEnemyBody.MovePosition(executionEnemyCenter);
            }

            float pull = Mathf.SmoothStep(0f, Mathf.Max(0f, tearExecutionPullDistance), progress);
            LeftAnchorPos = executionLeftStart + Vector2.left * pull;
            RightAnchorPos = executionRightStart + Vector2.right * pull;
            executionEnemy.UpdateTearExecutionVisual(LeftAnchorPos, RightAnchorPos);
        }
        else
        {
            executionPierceEnd = GetPierceExecutionEndPoint(executionEnemy);
            Vector2 tip = Vector2.Lerp(executionPierceStart, executionPierceEnd, Mathf.SmoothStep(0f, 1f, progress));
            if (executionUsesLeftCable) LeftFlyTip = tip;
            else RightFlyTip = tip;
        }

        if (progress >= 1f)
            FinishExecution(true);
    }

    private void FinishExecution(bool completeDamage)
    {
        Enemy finishedEnemy = executionEnemy;
        EnemyExecutionType finishedType = executionType;

        executionActive = false;
        executionEnemy = null;
        executionEnemyBody = null;
        executionTimer = 0f;

        if (finishedType == EnemyExecutionType.Tear)
        {
            ClearLeftCable();
            ClearRightCable();
        }
        else if (executionUsesLeftCable)
        {
            ClearLeftCable();
        }
        else
        {
            ClearRightCable();
        }

        if (completeDamage && finishedEnemy != null)
        {
            finishedEnemy.CompleteTentacleExecution();
        }
        else if (finishedEnemy != null)
        {
            finishedEnemy.CancelTentacleExecution();
        }
    }

    private Vector3[] GetExecutionCablePath(bool isLeft)
    {
        if (!executionActive)
            return null;

        if (executionType == EnemyExecutionType.Tear)
            return BuildTearExecutionCablePath(isLeft);

        if (isLeft != executionUsesLeftCable)
            return null;

        return new[] { GetShootPoint(isLeft).position, (Vector3)(isLeft ? LeftFlyTip : RightFlyTip) };
    }

    private Vector3[] BuildTearExecutionCablePath(bool isLeft)
    {
        int segmentCount = Mathf.Clamp(tearExecutionCableSegments, 4, 32);
        Vector3[] path = new Vector3[segmentCount + 1];
        Vector2 start = GetShootPoint(isLeft).position;
        Vector2 end = isLeft ? LeftAnchorPos : RightAnchorPos;
        float side = isLeft ? -1f : 1f;
        float outwardBend = Mathf.Max(0f, tearExecutionCableOutwardBend);
        float lowerBend = Mathf.Max(0f, tearExecutionCableLowerBend);

        Vector2 controlA = Vector2.Lerp(start, end, 0.35f) + new Vector2(side * outwardBend, -lowerBend);
        Vector2 controlB = Vector2.Lerp(start, end, 0.78f) + new Vector2(side * outwardBend, lowerBend * 0.35f);

        controlA = ClampTearCablePointToSide(controlA, side);
        controlB = ClampTearCablePointToSide(controlB, side);
        end = ClampTearCablePointToSide(end, side);

        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            path[i] = SampleCubicBezier(start, controlA, controlB, end, t);
        }

        return path;
    }

    private Vector2 ClampTearCablePointToSide(Vector2 point, float side)
    {
        float clearance = Mathf.Max(0f, tearExecutionCenterClearance);
        float limitX = executionEnemyCenter.x + side * clearance;

        if (side < 0f && point.x > limitX)
            point.x = limitX;
        else if (side > 0f && point.x < limitX)
            point.x = limitX;

        return point;
    }

    private Vector2 SampleCubicBezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        float oneMinusT = 1f - t;
        return oneMinusT * oneMinusT * oneMinusT * a
            + 3f * oneMinusT * oneMinusT * t * b
            + 3f * oneMinusT * t * t * c
            + t * t * t * d;
    }

    public void ReleaseCable(bool isLeft)
    {
        if (executionActive)
            return;

        if (TryThrowGrabbedObject(isLeft) || TryThrowGrabbedEnemy(isLeft))
        {
            if (!HasAnchoredCable())
                SetHovering(false);

            if (!HasActiveCable())
                currentState = IsGrounded ? OdmState.Grounded : OdmState.Airborne;

            return;
        }

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
        }

        if (!HasActiveCable())
            currentState = IsGrounded ? OdmState.Grounded : OdmState.Airborne;
    }

    public void AirDash(Vector2 direction)
    {
        if (gasSystem.IsGasEmpty || direction.sqrMagnitude < 0.001f) return;

        SetHovering(false);

        Rb.linearVelocity += direction.normalized * airDashForce;
        gasSystem.ConsumeGas(10f);
    }

    public void EmergencyStop()
    {
        Rb.linearVelocity *= stopDamping;
    }

    /// <summary>
    /// 传送、重生等强制重置位置时调用。会清除左右触手和悬停状态，但不会触发投掷逻辑。
    /// </summary>
    public void ResetCablesForTeleport()
    {
        ClearLeftCable();
        ClearRightCable();
        SetHovering(false);
        IsPullKeyHeld = false;
        currentState = IsGrounded ? OdmState.Grounded : OdmState.Airborne;
    }

    private void UpdateMovementState()
    {
        if (ShouldPullCable())
        {
            SetHovering(false);
            currentState = OdmState.Pulling;
            return;
        }

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
            currentState = OdmState.Airborne;
            return;
        }

        currentState = IsHovering ? OdmState.Hovering : OdmState.Swinging;
    }

    private void ApplyMovement()
    {
        if (Time.time < damageKnockbackControlLockUntil)
            return;

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
        if (!CanUseAnchorForPlayerPull(isLeft)) return;

        Vector2 target = GetEffectiveAnchor(isLeft);
        if (IsBlockedToward(target)) return;

        Vector2 dir = (target - (Vector2)transform.position).normalized;
        Rb.AddForce(dir * pullForce, ForceMode2D.Force);
    }

    private void ApplyEnemyGrabControl()
    {
        if (executionActive)
        {
            RestoreAllHeldTargetPlayerCollisions();
            return;
        }

        currentHeldTargetColliderPairs.Clear();
        ApplyEnemyGrabControl(true);
        ApplyEnemyGrabControl(false);
        ApplyObjectGrabControl(true);
        ApplyObjectGrabControl(false);
        RestoreReleasedHeldTargetPlayerCollisions();
    }

    private void ApplyEnemyGrabControl(bool isLeft)
    {
        if (!TryGetGrabbableEnemyAnchor(isLeft, out Enemy enemy, out Rigidbody2D enemyRb))
            return;

        Vector2 target = GetEnemyGrabTarget(isLeft) + enemyGrabLocalOffset;
        Vector2 toTarget = target - enemyRb.position;
        float responsiveness = GetEnemyGrabResponsiveness(enemy);
        Vector2 springForce = toTarget * Mathf.Max(0f, enemyGrabFollowForce) * responsiveness;
        Vector2 dampingForce = -enemyRb.linearVelocity * Mathf.Max(0f, enemyGrabFollowDamping) * responsiveness;
        enemyRb.AddForce(springForce + dampingForce, ForceMode2D.Force);
        ClampEnemyGrabSpeed(enemyRb);

        Vector2 velocity = GetEnemyGrabVelocity(isLeft);
        Vector2 anchor = enemyRb.position - enemyGrabLocalOffset;
        SetEnemyAnchorPosition(isLeft, anchor);
        SetEnemyGrabTarget(isLeft, anchor, velocity);
        enemy.MarkTentacleHeld(tentaclePower, enemyRb.linearVelocity);
    }

    private bool TryThrowGrabbedEnemy(bool isLeft)
    {
        if (!TryGetGrabbableEnemyAnchor(isLeft, out Enemy enemy, out Rigidbody2D enemyRb))
            return false;

        Vector2 throwVelocity = BuildThrowVelocity(
            GetEnemyGrabVelocity(isLeft),
            enemyRb.linearVelocity,
            GetThrowFallbackDirection(isLeft, enemyRb.position),
            enemyThrowVelocityScale,
            enemyThrowCurrentVelocityInfluence,
            enemyThrowPlayerVelocityInheritance,
            enemyThrowMinSpeed,
            enemyThrowMaxSpeed);
        if (throwVelocity.sqrMagnitude <= 0.000001f)
            return false;

        enemyRb.linearVelocity = throwVelocity;
        enemy.MarkTentacleThrown(throwVelocity);

        if (isLeft) ClearLeftCable();
        else ClearRightCable();

        return enemy != null;
    }

    private void ApplyObjectGrabControl(bool isLeft)
    {
        if (!TryGetGrabbableObjectAnchor(isLeft, out TentacleInteractableObject interactable, out Rigidbody2D objectRb))
            return;

        Vector2 target = GetEnemyGrabTarget(isLeft);
        Vector2 toTarget = target - objectRb.position;
        float responsiveness = GetObjectGrabResponsiveness(interactable);
        Vector2 springForce = toTarget * Mathf.Max(0f, objectGrabFollowForce) * responsiveness;
        Vector2 dampingForce = -objectRb.linearVelocity * Mathf.Max(0f, objectGrabFollowDamping) * responsiveness;
        objectRb.AddForce(springForce + dampingForce, ForceMode2D.Force);
        ClampGrabbedObjectSpeed(objectRb);

        Vector2 velocity = GetEnemyGrabVelocity(isLeft);
        Vector2 anchor = objectRb.position;
        SetObjectAnchorPosition(isLeft, anchor);
        SetEnemyGrabTarget(isLeft, anchor, velocity);
        IgnoreHeldTargetPlayerCollision(objectRb);
    }

    private void IgnoreHeldTargetPlayerCollision(Rigidbody2D targetBody)
    {
        if (targetBody == null)
            return;

        Collider2D[] targetColliders = targetBody.GetComponentsInChildren<Collider2D>();
        Collider2D[] playerColliders = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < targetColliders.Length; i++)
        {
            Collider2D targetCollider = targetColliders[i];
            if (targetCollider == null || !targetCollider.enabled || targetCollider.isTrigger)
                continue;

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider2D playerCollider = playerColliders[j];
                if (!IsPlayerSolidCollider(playerCollider) || targetCollider == playerCollider)
                    continue;

                ColliderIgnorePair pair = new ColliderIgnorePair(targetCollider, playerCollider);
                if (!ContainsColliderIgnorePair(currentHeldTargetColliderPairs, targetCollider, playerCollider))
                    currentHeldTargetColliderPairs.Add(pair);

                if (ContainsColliderIgnorePair(ignoredHeldTargetColliderPairs, targetCollider, playerCollider))
                    continue;

                Physics2D.IgnoreCollision(targetCollider, playerCollider, true);
                ignoredHeldTargetColliderPairs.Add(pair);
            }
        }
    }

    private void RestoreReleasedHeldTargetPlayerCollisions()
    {
        for (int i = ignoredHeldTargetColliderPairs.Count - 1; i >= 0; i--)
        {
            ColliderIgnorePair pair = ignoredHeldTargetColliderPairs[i];
            bool pairStillHeld = ContainsColliderIgnorePair(
                currentHeldTargetColliderPairs,
                pair.targetCollider,
                pair.playerCollider);

            if (pair.targetCollider != null && pair.playerCollider != null && !pairStillHeld)
                Physics2D.IgnoreCollision(pair.targetCollider, pair.playerCollider, false);

            if (pair.targetCollider == null || pair.playerCollider == null || !pairStillHeld)
                ignoredHeldTargetColliderPairs.RemoveAt(i);
        }
    }

    private void RestoreAllHeldTargetPlayerCollisions()
    {
        for (int i = 0; i < ignoredHeldTargetColliderPairs.Count; i++)
        {
            ColliderIgnorePair pair = ignoredHeldTargetColliderPairs[i];
            if (pair.targetCollider != null && pair.playerCollider != null)
                Physics2D.IgnoreCollision(pair.targetCollider, pair.playerCollider, false);
        }

        currentHeldTargetColliderPairs.Clear();
        ignoredHeldTargetColliderPairs.Clear();
    }

    private bool IsPlayerSolidCollider(Collider2D candidate)
    {
        return candidate != null && candidate.enabled && !candidate.isTrigger;
    }

    private bool ContainsColliderIgnorePair(List<ColliderIgnorePair> pairs, Collider2D target, Collider2D player)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            if (pairs[i].Matches(target, player))
                return true;
        }

        return false;
    }

    private bool TryThrowGrabbedObject(bool isLeft)
    {
        if (!TryGetGrabbableObjectAnchor(isLeft, out TentacleInteractableObject interactable, out Rigidbody2D objectRb))
            return false;

        Vector2 throwVelocity = BuildThrowVelocity(
            GetEnemyGrabVelocity(isLeft),
            objectRb.linearVelocity,
            GetThrowFallbackDirection(isLeft, objectRb.position),
            objectThrowVelocityScale,
            objectThrowCurrentVelocityInfluence,
            objectThrowPlayerVelocityInheritance,
            objectThrowMinSpeed,
            objectThrowMaxSpeed);
        if (throwVelocity.sqrMagnitude <= 0.000001f)
            return false;

        objectRb.linearVelocity = throwVelocity;
        interactable.MarkTentacleThrown(throwVelocity);

        if (isLeft) ClearLeftCable();
        else ClearRightCable();

        return interactable != null;
    }

    private bool TryGetGrabbableEnemyAnchor(bool isLeft, out Enemy enemy, out Rigidbody2D enemyRb)
    {
        enemy = null;
        enemyRb = null;

        if (!allowEnemyGrab)
            return false;

        if (!(isLeft ? leftEnemyAnchorActive : rightEnemyAnchorActive))
            return false;

        Transform target = isLeft ? leftEnemyAnchorTarget : rightEnemyAnchorTarget;
        if (target == null)
            return false;

        enemy = target.GetComponent<Enemy>();
        enemyRb = target.GetComponent<Rigidbody2D>();
        if (enemy == null || enemyRb == null)
            return false;

        return tentaclePower >= enemy.enemyMass;
    }

    private bool TryGetGrabbableObjectAnchor(bool isLeft, out TentacleInteractableObject interactable, out Rigidbody2D objectRb)
    {
        interactable = null;
        objectRb = null;

        if (!allowObjectGrab)
            return false;

        if (!(isLeft ? leftObjectAnchorActive : rightObjectAnchorActive))
            return false;

        Transform target = isLeft ? leftObjectAnchorTarget : rightObjectAnchorTarget;
        if (target == null)
            return false;

        interactable = target.GetComponent<TentacleInteractableObject>();
        objectRb = target.GetComponent<Rigidbody2D>();
        if (interactable == null || objectRb == null)
            return false;

        return tentaclePower >= interactable.EffectiveMass;
    }

    private float GetEnemyGrabResponsiveness(Enemy enemy)
    {
        if (enemy == null)
            return 1f;

        float load = Mathf.Max(0.01f, enemy.enemyMass) + Mathf.Max(0f, enemy.grabTenacity);
        float resistance = Mathf.Max(0f, enemyGrabResistanceScale);
        float advantage = Mathf.Max(0.01f, tentaclePower) / Mathf.Max(0.01f, load * Mathf.Max(0.01f, resistance));
        return Mathf.Clamp01(advantage);
    }

    private float GetObjectGrabResponsiveness(TentacleInteractableObject interactable)
    {
        if (interactable == null)
            return 1f;

        float load = Mathf.Max(0.01f, interactable.EffectiveMass) + Mathf.Max(0f, interactable.grabTenacity);
        float resistance = Mathf.Max(0f, objectGrabResistanceScale);
        float advantage = Mathf.Max(0.01f, tentaclePower) / Mathf.Max(0.01f, load * Mathf.Max(0.01f, resistance));
        return Mathf.Clamp01(advantage);
    }

    private void ClampEnemyGrabSpeed(Rigidbody2D enemyRb)
    {
        if (enemyRb == null)
            return;

        float maxEnemySpeed = Mathf.Max(0f, enemyGrabMaxSpeed);
        if (maxEnemySpeed <= 0f || enemyRb.linearVelocity.sqrMagnitude <= maxEnemySpeed * maxEnemySpeed)
            return;

        enemyRb.linearVelocity = enemyRb.linearVelocity.normalized * maxEnemySpeed;
    }

    private void ClampGrabbedObjectSpeed(Rigidbody2D objectRb)
    {
        if (objectRb == null)
            return;

        float maxObjectSpeed = Mathf.Max(0f, objectGrabMaxSpeed);
        if (maxObjectSpeed <= 0f || objectRb.linearVelocity.sqrMagnitude <= maxObjectSpeed * maxObjectSpeed)
            return;

        objectRb.linearVelocity = objectRb.linearVelocity.normalized * maxObjectSpeed;
    }

    private void UpdateCableFlight()
    {
        if (executionActive)
            return;

        if (IsLeftFlying) AdvanceFlight(true);
        if (IsRightFlying) AdvanceFlight(false);
    }

    private void AdvanceFlight(bool isLeft)
    {
        Transform start = GetShootPoint(isLeft);
        Vector2 tip = isLeft ? LeftFlyTip : RightFlyTip;
        Vector2 target = isLeft ? leftFlyTarget : rightFlyTarget;
        bool retracting = isLeft ? leftFlyRetracting : rightFlyRetracting;

        if (retracting)
        {
            Vector2 nextTip = Vector2.MoveTowards(tip, start.position, cableShootSpeed * Time.deltaTime);
            SetFlyTip(isLeft, nextTip);

            if (nextTip == (Vector2)start.position)
                CancelFlight(isLeft);

            return;
        }

        Vector2 clampedTarget = ClampCableTargetToLength(start.position, target);
        if (isLeft) leftFlyTarget = clampedTarget;
        else rightFlyTarget = clampedTarget;

        Vector2 next = IsTipTouchingSurface(isLeft)
            ? MoveTipAlongSurface(isLeft, tip, clampedTarget)
            : Vector2.MoveTowards(tip, clampedTarget, cableShootSpeed * Time.deltaTime);

        RaycastHit2D hit = Physics2D.Linecast(tip, next, GetSolidMask());
        if (hit.collider != null)
        {
            SetFlyTip(isLeft, GetSafeAnchorPoint(hit));
            SetTipSurfaceContact(isLeft, true, hit.normal, hit.collider);
            return;
        }

        SetFlyTip(isLeft, next);

        if (GetRopePathLength(isLeft, next) > maxCableLength)
            SetFlyTip(isLeft, ClampCableTargetToLength(start.position, next));
    }

    private Vector2 MoveTipAlongSurface(bool isLeft, Vector2 tip, Vector2 target)
    {
        Vector2 normal = isLeft ? leftTipSurfaceNormal : rightTipSurfaceNormal;
        Vector2 toTarget = target - tip;

        if (toTarget.sqrMagnitude < 0.000001f)
            return tip;

        if (Vector2.Dot(toTarget.normalized, normal) > 0.25f)
        {
            SetTipSurfaceContact(isLeft, false, Vector2.zero, null);
            return Vector2.MoveTowards(tip, target, cableShootSpeed * Time.deltaTime);
        }

        Vector2 slide = toTarget - normal * Vector2.Dot(toTarget, normal);
        if (slide.sqrMagnitude < 0.000001f)
            return tip;

        Vector2 next = tip + slide.normalized * Mathf.Min(slide.magnitude, cableShootSpeed * Time.deltaTime);
        RaycastHit2D slideHit = Physics2D.Linecast(tip, next, GetSolidMask());
        if (slideHit.collider != null)
        {
            SetTipSurfaceContact(isLeft, true, slideHit.normal, slideHit.collider);
            return GetSafeAnchorPoint(slideHit);
        }

        return next;
    }

    private Vector2 ClampCableTargetToLength(Vector2 start, Vector2 target)
    {
        float maxLength = Mathf.Max(0.1f, maxCableLength);
        Vector2 fromStart = target - start;
        if (fromStart.magnitude <= maxLength)
            return target;

        return start + fromStart.normalized * maxLength;
    }

    private bool IsTipTouchingSurface(bool isLeft)
    {
        return isLeft ? leftTipTouchingSurface : rightTipTouchingSurface;
    }

    private void SetTipSurfaceContact(bool isLeft, bool value, Vector2 normal, Collider2D surfaceCollider)
    {
        if (isLeft)
        {
            leftTipTouchingSurface = value;
            leftTipSurfaceNormal = normal;
            leftTipSurfaceCollider = value ? surfaceCollider : null;
        }
        else
        {
            rightTipTouchingSurface = value;
            rightTipSurfaceNormal = normal;
            rightTipSurfaceCollider = value ? surfaceCollider : null;
        }
    }

    private bool CanAnchorTouchedSurface(bool isLeft)
    {
        Collider2D surfaceCollider = isLeft ? leftTipSurfaceCollider : rightTipSurfaceCollider;
        return surfaceCollider != null && IsLayerInMask(surfaceCollider.gameObject.layer, anchorableLayer);
    }

    private void SetFlyTip(bool isLeft, Vector2 position)
    {
        if (isLeft) LeftFlyTip = position;
        else RightFlyTip = position;
    }

    private void AnchorCable(bool isLeft, Vector2 anchor)
    {
        float ropeLength = maxCableLength;

        if (isLeft)
        {
            IsLeftFlying = false;
            leftFlyRetracting = false;
            leftTipTouchingSurface = false;
            ClearEnemyAnchor(true);
            ClearObjectAnchor(true);
            IsLeftAnchored = true;
            LeftAnchorPos = anchor;
            leftRopeLength = ropeLength;
        }
        else
        {
            IsRightFlying = false;
            rightFlyRetracting = false;
            rightTipTouchingSurface = false;
            ClearEnemyAnchor(false);
            ClearObjectAnchor(false);
            IsRightAnchored = true;
            RightAnchorPos = anchor;
            rightRopeLength = ropeLength;
        }

        currentState = IsGrounded ? OdmState.Grounded : OdmState.Swinging;
    }

    private void AnchorCableToEnemy(bool isLeft, Transform target, Vector2 localPoint)
    {
        if (target == null)
            return;

        Vector2 anchor = target.TransformPoint(localPoint);
        float ropeLength = maxCableLength;

        if (isLeft)
        {
            IsLeftFlying = false;
            leftFlyRetracting = false;
            leftTipTouchingSurface = false;
            IsLeftAnchored = true;
            LeftAnchorPos = anchor;
            leftRopeLength = ropeLength;
            leftEnemyAnchorTarget = target;
            leftEnemyAnchorLocalPoint = localPoint;
            leftEnemyAnchorActive = true;
            ClearObjectAnchor(true);
            SetEnemyGrabTarget(true, anchor, Vector2.zero);
            leftTouchedEnemyTarget = null;
        }
        else
        {
            IsRightFlying = false;
            rightFlyRetracting = false;
            rightTipTouchingSurface = false;
            IsRightAnchored = true;
            RightAnchorPos = anchor;
            rightRopeLength = ropeLength;
            rightEnemyAnchorTarget = target;
            rightEnemyAnchorLocalPoint = localPoint;
            rightEnemyAnchorActive = true;
            ClearObjectAnchor(false);
            SetEnemyGrabTarget(false, anchor, Vector2.zero);
            rightTouchedEnemyTarget = null;
        }

        currentState = IsGrounded ? OdmState.Grounded : OdmState.Swinging;
    }

    private void AnchorCableToObject(bool isLeft, Transform target, Vector2 localPoint)
    {
        if (target == null)
            return;

        Vector2 anchor = target.TransformPoint(localPoint);
        float ropeLength = maxCableLength;

        if (isLeft)
        {
            IsLeftFlying = false;
            leftFlyRetracting = false;
            leftTipTouchingSurface = false;
            IsLeftAnchored = true;
            LeftAnchorPos = anchor;
            leftRopeLength = ropeLength;
            ClearEnemyAnchor(true);
            leftObjectAnchorTarget = target;
            leftObjectAnchorLocalPoint = localPoint;
            leftObjectAnchorActive = true;
            SetEnemyGrabTarget(true, anchor, Vector2.zero);
            leftTouchedObjectTarget = null;
        }
        else
        {
            IsRightFlying = false;
            rightFlyRetracting = false;
            rightTipTouchingSurface = false;
            IsRightAnchored = true;
            RightAnchorPos = anchor;
            rightRopeLength = ropeLength;
            ClearEnemyAnchor(false);
            rightObjectAnchorTarget = target;
            rightObjectAnchorLocalPoint = localPoint;
            rightObjectAnchorActive = true;
            SetEnemyGrabTarget(false, anchor, Vector2.zero);
            rightTouchedObjectTarget = null;
        }

        currentState = IsGrounded ? OdmState.Grounded : OdmState.Swinging;
    }

    private void UpdateEnemyAnchorPositions()
    {
        if (executionActive && executionType == EnemyExecutionType.Tear)
            return;

        UpdateEnemyAnchorPosition(true);
        UpdateEnemyAnchorPosition(false);
        UpdateObjectAnchorPosition(true);
        UpdateObjectAnchorPosition(false);
    }

    private void UpdateEnemyAnchorPosition(bool isLeft)
    {
        bool isEnemyAnchorActive = isLeft ? leftEnemyAnchorActive : rightEnemyAnchorActive;
        Transform target = isLeft ? leftEnemyAnchorTarget : rightEnemyAnchorTarget;
        if (isEnemyAnchorActive && target == null)
        {
            if (isLeft) ClearLeftCable();
            else ClearRightCable();
            return;
        }

        if (target == null)
            return;

        Vector2 anchor = target.TransformPoint(isLeft ? leftEnemyAnchorLocalPoint : rightEnemyAnchorLocalPoint);
        if (isLeft) LeftAnchorPos = anchor;
        else RightAnchorPos = anchor;
    }

    private void UpdateObjectAnchorPosition(bool isLeft)
    {
        bool isObjectAnchorActive = isLeft ? leftObjectAnchorActive : rightObjectAnchorActive;
        Transform target = isLeft ? leftObjectAnchorTarget : rightObjectAnchorTarget;
        if (isObjectAnchorActive && target == null)
        {
            if (isLeft) ClearLeftCable();
            else ClearRightCable();
            return;
        }

        if (target == null)
            return;

        Vector2 anchor = target.TransformPoint(isLeft ? leftObjectAnchorLocalPoint : rightObjectAnchorLocalPoint);
        if (isLeft) LeftAnchorPos = anchor;
        else RightAnchorPos = anchor;
    }

    private void ClearEnemyAnchor(bool isLeft)
    {
        if (isLeft)
        {
            leftEnemyAnchorTarget = null;
            leftEnemyAnchorLocalPoint = Vector2.zero;
            leftEnemyAnchorActive = false;
            leftEnemyGrabTarget = Vector2.zero;
            leftGrabVelocity = Vector2.zero;
            leftTouchedEnemyTarget = null;
            leftTouchedEnemyLocalPoint = Vector2.zero;
        }
        else
        {
            rightEnemyAnchorTarget = null;
            rightEnemyAnchorLocalPoint = Vector2.zero;
            rightEnemyAnchorActive = false;
            rightEnemyGrabTarget = Vector2.zero;
            rightGrabVelocity = Vector2.zero;
            rightTouchedEnemyTarget = null;
            rightTouchedEnemyLocalPoint = Vector2.zero;
        }
    }

    private void ClearObjectAnchor(bool isLeft)
    {
        if (isLeft)
        {
            leftObjectAnchorTarget = null;
            leftObjectAnchorLocalPoint = Vector2.zero;
            leftObjectAnchorActive = false;
            leftTouchedObjectTarget = null;
            leftTouchedObjectLocalPoint = Vector2.zero;
            leftEnemyGrabTarget = Vector2.zero;
            leftGrabVelocity = Vector2.zero;
        }
        else
        {
            rightObjectAnchorTarget = null;
            rightObjectAnchorLocalPoint = Vector2.zero;
            rightObjectAnchorActive = false;
            rightTouchedObjectTarget = null;
            rightTouchedObjectLocalPoint = Vector2.zero;
            rightEnemyGrabTarget = Vector2.zero;
            rightGrabVelocity = Vector2.zero;
        }
    }

    private bool IsGrabbingEnemy(bool isLeft)
    {
        return TryGetGrabbableEnemyAnchor(isLeft, out _, out _);
    }

    private bool IsGrabbingObject(bool isLeft)
    {
        return TryGetGrabbableObjectAnchor(isLeft, out _, out _);
    }

    private void SetEnemyGrabTarget(bool isLeft, Vector2 target)
    {
        Vector2 previous = isLeft ? leftEnemyGrabTarget : rightEnemyGrabTarget;
        Vector2 rawVelocity = (target - previous) / Mathf.Max(Time.deltaTime, 0.0001f);
        Vector2 previousVelocity = GetEnemyGrabVelocity(isLeft);
        float smoothing = GetGrabVelocitySmoothing(isLeft);
        float blend = smoothing <= 0f ? 1f : 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        Vector2 velocity = Vector2.Lerp(previousVelocity, rawVelocity, Mathf.Clamp01(blend));
        SetEnemyGrabTarget(isLeft, target, velocity);
    }

    private void SetEnemyGrabTarget(bool isLeft, Vector2 target, Vector2 velocity)
    {
        if (isLeft)
        {
            leftEnemyGrabTarget = target;
            leftGrabVelocity = velocity;
        }
        else
        {
            rightEnemyGrabTarget = target;
            rightGrabVelocity = velocity;
        }
    }

    private Vector2 GetEnemyGrabTarget(bool isLeft)
    {
        return isLeft ? leftEnemyGrabTarget : rightEnemyGrabTarget;
    }

    private Vector2 GetEnemyGrabVelocity(bool isLeft)
    {
        return isLeft ? leftGrabVelocity : rightGrabVelocity;
    }

    private float GetGrabVelocitySmoothing(bool isLeft)
    {
        if (IsGrabbingObject(isLeft))
            return Mathf.Max(0f, objectThrowVelocitySmoothing);

        return Mathf.Max(0f, enemyThrowVelocitySmoothing);
    }

    private Vector2 BuildThrowVelocity(
        Vector2 grabVelocity,
        Vector2 grabbedBodyVelocity,
        Vector2 fallbackDirection,
        float grabVelocityScale,
        float grabbedBodyVelocityInfluence,
        float playerVelocityInheritance,
        float minSpeed,
        float maxSpeed)
    {
        Vector2 grabComponent = grabVelocity * Mathf.Max(0f, grabVelocityScale);
        Vector2 velocity = grabComponent;
        velocity += grabbedBodyVelocity * Mathf.Clamp01(grabbedBodyVelocityInfluence);
        if (Rb != null)
            velocity += Rb.linearVelocity * Mathf.Clamp01(playerVelocityInheritance);

        Vector2 direction = GetStableThrowDirection(grabComponent, velocity, grabbedBodyVelocity, fallbackDirection);
        if (direction.sqrMagnitude <= 0.000001f)
            return Vector2.zero;

        float speed = velocity.magnitude;
        float shapedSpeed = Mathf.Max(speed, Mathf.Max(0f, minSpeed));
        if (maxSpeed > 0f)
            shapedSpeed = Mathf.Min(shapedSpeed, maxSpeed);

        return direction * shapedSpeed;
    }

    private Vector2 GetStableThrowDirection(
        Vector2 grabComponent,
        Vector2 combinedVelocity,
        Vector2 grabbedBodyVelocity,
        Vector2 fallbackDirection)
    {
        float aimMinSpeed = Mathf.Max(0f, throwAimDirectionMinSpeed);
        if (grabComponent.sqrMagnitude >= aimMinSpeed * aimMinSpeed)
            return grabComponent.normalized;

        if (combinedVelocity.sqrMagnitude > 0.000001f)
            return combinedVelocity.normalized;

        if (grabbedBodyVelocity.sqrMagnitude > 0.000001f)
            return grabbedBodyVelocity.normalized;

        if (fallbackDirection.sqrMagnitude > 0.000001f)
            return fallbackDirection.normalized;

        return IsFacingLeft() ? Vector2.left : Vector2.right;
    }

    private Vector2 GetThrowFallbackDirection(bool isLeft, Vector2 grabbedPosition)
    {
        Vector2 playerPosition = Rb != null ? Rb.position : (Vector2)transform.position;
        Vector2 fromPlayerToTarget = grabbedPosition - playerPosition;
        if (fromPlayerToTarget.sqrMagnitude > 0.000001f)
            return fromPlayerToTarget;

        Vector2 fromPlayerToAnchor = GetAnchorPosition(isLeft) - playerPosition;
        if (fromPlayerToAnchor.sqrMagnitude > 0.000001f)
            return fromPlayerToAnchor;

        return IsFacingLeft() ? Vector2.left : Vector2.right;
    }

    private void SetEnemyAnchorPosition(bool isLeft, Vector2 anchor)
    {
        Transform target = isLeft ? leftEnemyAnchorTarget : rightEnemyAnchorTarget;
        if (target == null)
            return;

        Vector2 localPoint = target.InverseTransformPoint(anchor);
        if (isLeft)
        {
            leftEnemyAnchorLocalPoint = localPoint;
            LeftAnchorPos = anchor;
        }
        else
        {
            rightEnemyAnchorLocalPoint = localPoint;
            RightAnchorPos = anchor;
        }
    }

    private void SetObjectAnchorPosition(bool isLeft, Vector2 anchor)
    {
        Transform target = isLeft ? leftObjectAnchorTarget : rightObjectAnchorTarget;
        if (target == null)
            return;

        Vector2 localPoint = target.InverseTransformPoint(anchor);
        if (isLeft)
        {
            leftObjectAnchorLocalPoint = localPoint;
            LeftAnchorPos = anchor;
        }
        else
        {
            rightObjectAnchorLocalPoint = localPoint;
            RightAnchorPos = anchor;
        }
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
        return HasPullableAnchoredCable() && IsPullKeyHeld && !gasSystem.IsGasEmpty;
    }

    private bool HasPullableAnchoredCable()
    {
        return CanUseAnchorForPlayerPull(true) || CanUseAnchorForPlayerPull(false);
    }

    private bool CanUseAnchorForPlayerPull(bool isLeft)
    {
        if (!(isLeft ? IsLeftAnchored : IsRightAnchored))
            return false;

        if (TryGetGrabbableEnemyAnchor(isLeft, out _, out _))
            return false;

        if (TryGetGrabbableObjectAnchor(isLeft, out _, out _))
            return false;

        return true;
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
        List<RopeBend> bends = isLeft ? leftBends : rightBends;
        if (!enableCableBending)
        {
            bends.Clear();
            return;
        }

        if (!HasCablePath(isLeft)) return;

        Transform start = GetShootPoint(isLeft);
        Vector2 terminal = GetCableTerminal(isLeft);
        LayerMask mask = GetCableBendMask();

        TryAddBlockedCableBend(start.position, bends, terminal, mask);

        if (bends.Count == 0) return;

        Vector2 next = bends.Count > 1 ? bends[1].position : terminal;
        if (ShouldReleaseBend(start.position, bends[0], next, mask))
            bends.RemoveAt(0);
    }

    private void TryAddBlockedCableBend(Vector2 start, List<RopeBend> bends, Vector2 terminal, LayerMask mask)
    {
        if (bends.Count >= maxRopeBends)
            return;

        Vector2 previous = start;
        for (int i = 0; i <= bends.Count; i++)
        {
            Vector2 target = i < bends.Count ? bends[i].position : terminal;
            RaycastHit2D hit = Physics2D.Linecast(previous, target, mask);
            if (hit.collider == null)
            {
                previous = target;
                continue;
            }

            Vector2 bend = hit.point + hit.normal * ropeBendOffset;
            int insertIndex = Mathf.Clamp(i, 0, bends.Count);
            if (IsDuplicateBend(bends, bend))
                return;

            float wrapSide = GetSide(target - bend, previous - bend);
            if (Mathf.Abs(wrapSide) < 0.001f)
                wrapSide = 1f;

            bends.Insert(insertIndex, new RopeBend(bend, hit.collider, hit.normal, Mathf.Sign(wrapSide)));
            return;
        }
    }

    private bool IsDuplicateBend(List<RopeBend> bends, Vector2 bend)
    {
        for (int i = 0; i < bends.Count; i++)
        {
            if (Vector2.Distance(bend, bends[i].position) <= ropeCornerReleaseDistance)
                return true;
        }

        return false;
    }

    private void DetectTentacleHits()
    {
        if (executionActive)
            return;

        DetectTentacleHits(true);
        DetectTentacleHits(false);
    }

    private void DetectTentacleHits(bool isLeft)
    {
        bool isFlying = isLeft ? IsLeftFlying : IsRightFlying;
        bool isRetracting = isLeft ? leftFlyRetracting : rightFlyRetracting;
        if (!isFlying || isRetracting)
            return;

        Vector2 tipVelocity = GetFlyingTipVelocity(isLeft);
        float tipSpeed = tipVelocity.magnitude;
        SetPreviousTentacleHitTip(isLeft, isLeft ? LeftFlyTip : RightFlyTip);

        Vector3[] path = GetCablePath(isLeft);
        if (path == null || path.Length < 2)
            return;

        int damage = CalculateTentacleDamage(tipSpeed);
        bool canDealDamage = tipSpeed >= Mathf.Max(0f, tentacleMinHitSpeed);
        Vector2 hitDirection = tipVelocity.sqrMagnitude > 0.000001f ? tipVelocity.normalized : Vector2.right;
        float speedRatio = Mathf.Clamp01(tipSpeed / Mathf.Max(maxSpeed, 0.001f));
        List<TentacleHitRecord> records = isLeft ? leftTentacleHitRecords : rightTentacleHitRecords;
        SetTouchedEnemyTarget(isLeft, null, Vector2.zero);
        SetTouchedObjectTarget(isLeft, null, Vector2.zero);

        for (int i = 1; i < path.Length; i++)
        {
            Vector2 from = path[i - 1];
            Vector2 to = path[i];
            Vector2 segment = to - from;
            float distance = segment.magnitude;
            if (distance < 0.001f)
                continue;

            int hitCount = Physics2D.CircleCast(
                from,
                Mathf.Max(0.01f, tentacleHitRadius),
                segment / distance,
                tentacleHitFilter,
                tentacleHitResults,
                distance);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider2D hitCollider = tentacleHitResults[hitIndex].collider;
                if (hitCollider == null || hitCollider == bodyCollider)
                    continue;

                TryRecordTouchedEnemyTarget(isLeft, hitCollider, tentacleHitResults[hitIndex].point);
                TryRecordTouchedObjectTarget(isLeft, hitCollider, tentacleHitResults[hitIndex].point);

                if (canDealDamage)
                    TryDamageTentacleTarget(hitCollider, damage, hitDirection, speedRatio, records);
            }
        }
    }

    private void ConfigureTentacleHitFilter()
    {
        tentacleHitFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = Physics2D.queriesHitTriggers
        };
        tentacleHitFilter.SetLayerMask(tentacleHitLayer);
    }

    private Vector2 GetFlyingTipVelocity(bool isLeft)
    {
        Vector2 tip = isLeft ? LeftFlyTip : RightFlyTip;
        Vector2 previousTip = isLeft ? leftPreviousTentacleHitTip : rightPreviousTentacleHitTip;
        return (tip - previousTip) / Mathf.Max(Time.deltaTime, 0.0001f);
    }

    private void SetPreviousTentacleHitTip(bool isLeft, Vector2 tip)
    {
        if (isLeft) leftPreviousTentacleHitTip = tip;
        else rightPreviousTentacleHitTip = tip;
    }

    private void TryRecordTouchedEnemyTarget(bool isLeft, Collider2D hitCollider, Vector2 hitPoint)
    {
        if (!allowTentacleAnchorEnemies || hitCollider == null)
            return;

        Enemy enemy = hitCollider.GetComponentInParent<Enemy>();
        if (enemy == null)
            return;

        Transform enemyTransform = enemy.transform;
        Vector2 worldPoint = hitPoint;
        if (worldPoint == Vector2.zero)
            worldPoint = hitCollider.ClosestPoint(isLeft ? LeftFlyTip : RightFlyTip);

        Vector2 localPoint = enemyTransform.InverseTransformPoint(worldPoint);
        SetTouchedEnemyTarget(isLeft, enemyTransform, localPoint);
    }

    private void SetTouchedEnemyTarget(bool isLeft, Transform target, Vector2 localPoint)
    {
        if (isLeft)
        {
            leftTouchedEnemyTarget = target;
            leftTouchedEnemyLocalPoint = localPoint;
        }
        else
        {
            rightTouchedEnemyTarget = target;
            rightTouchedEnemyLocalPoint = localPoint;
        }
    }

    private void TryRecordTouchedObjectTarget(bool isLeft, Collider2D hitCollider, Vector2 hitPoint)
    {
        if (!allowObjectGrab || hitCollider == null)
            return;

        TentacleInteractableObject interactable = hitCollider.GetComponentInParent<TentacleInteractableObject>();
        if (interactable == null)
            return;

        if (!IsLayerInMask(hitCollider.gameObject.layer, anchorableLayer))
            return;

        Transform objectTransform = interactable.transform;
        Vector2 worldPoint = hitPoint;
        if (worldPoint == Vector2.zero)
            worldPoint = hitCollider.ClosestPoint(isLeft ? LeftFlyTip : RightFlyTip);

        Vector2 localPoint = objectTransform.InverseTransformPoint(worldPoint);
        SetTouchedObjectTarget(isLeft, objectTransform, localPoint);
    }

    private void SetTouchedObjectTarget(bool isLeft, Transform target, Vector2 localPoint)
    {
        if (isLeft)
        {
            leftTouchedObjectTarget = target;
            leftTouchedObjectLocalPoint = localPoint;
        }
        else
        {
            rightTouchedObjectTarget = target;
            rightTouchedObjectLocalPoint = localPoint;
        }
    }

    private int CalculateTentacleDamage(float tipSpeed)
    {
        int speedBonus = Mathf.FloorToInt(Mathf.Max(0f, tipSpeed - tentacleMinHitSpeed) * Mathf.Max(0f, tentacleSpeedDamageScale));
        return Mathf.Max(1, tentacleBaseDamage + speedBonus);
    }

    private bool TryDamageTentacleTarget(
        Collider2D hitCollider,
        int damage,
        Vector2 hitDirection,
        float speedRatio,
        List<TentacleHitRecord> records)
    {
        Enemy enemy = hitCollider.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            if (!CanDamageTentacleTarget(enemy, records))
                return false;

            enemy.TakeDamage(damage, hitDirection, speedRatio);
            MarkTentacleTargetDamaged(enemy, records);
            return true;
        }

        BossController boss = hitCollider.GetComponentInParent<BossController>();
        if (boss != null)
        {
            if (!CanDamageTentacleTarget(boss, records))
                return false;

            boss.TakeDamage(damage, hitDirection, speedRatio);
            MarkTentacleTargetDamaged(boss, records);
            return true;
        }

        return false;
    }

    private bool CanDamageTentacleTarget(Object target, List<TentacleHitRecord> records)
    {
        float cooldown = Mathf.Max(0f, tentacleHitCooldown);
        for (int i = records.Count - 1; i >= 0; i--)
        {
            if (records[i].target == null)
            {
                records.RemoveAt(i);
                continue;
            }

            if (records[i].target == target)
                return Time.time - records[i].lastHitTime >= cooldown;
        }

        return true;
    }

    private void MarkTentacleTargetDamaged(Object target, List<TentacleHitRecord> records)
    {
        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].target == target)
            {
                records[i] = new TentacleHitRecord(target, Time.time);
                return;
            }
        }

        records.Add(new TentacleHitRecord(target, Time.time));
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
        return bends.Count > 0 ? bends[0].position : GetAnchorPosition(isLeft);
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
            return IsLeftFlying ? LeftFlyTip : GetAnchorPosition(true);

        return IsRightFlying ? RightFlyTip : GetAnchorPosition(false);
    }

    private float GetRopePathLength(bool isLeft)
    {
        if (!(isLeft ? IsLeftAnchored : IsRightAnchored)) return 0f;

        Vector2 anchor = GetAnchorPosition(isLeft);
        return GetRopePathLength(isLeft, anchor);
    }

    private Vector2 GetAnchorPosition(bool isLeft)
    {
        Transform target = isLeft ? leftEnemyAnchorTarget : rightEnemyAnchorTarget;
        if (target != null)
            return target.TransformPoint(isLeft ? leftEnemyAnchorLocalPoint : rightEnemyAnchorLocalPoint);

        target = isLeft ? leftObjectAnchorTarget : rightObjectAnchorTarget;
        if (target != null)
            return target.TransformPoint(isLeft ? leftObjectAnchorLocalPoint : rightObjectAnchorLocalPoint);

        return isLeft ? LeftAnchorPos : RightAnchorPos;
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

    private LayerMask GetGroundMask()
    {
        return groundLayer.value != 0 ? groundLayer : GetSolidMask();
    }

    private LayerMask GetCableBendMask()
    {
        return cableBendLayer.value != 0 ? cableBendLayer : GetSolidMask();
    }

    private LayerMask EmptyLayerMask()
    {
        LayerMask mask = 0;
        return mask;
    }

    private bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void SetRetracting(bool isLeft, bool value)
    {
        if (isLeft) leftFlyRetracting = value;
        else rightFlyRetracting = value;
    }

    private void CancelFlight(bool isLeft)
    {
        if (isLeft)
        {
            IsLeftFlying = leftFlyRetracting = false;
            leftTipTouchingSurface = false;
            leftTentacleHitRecords.Clear();
            ClearEnemyAnchor(true);
            ClearObjectAnchor(true);
        }
        else
        {
            IsRightFlying = rightFlyRetracting = false;
            rightTipTouchingSurface = false;
            rightTentacleHitRecords.Clear();
            ClearEnemyAnchor(false);
            ClearObjectAnchor(false);
        }

        if (!HasActiveCable())
            currentState = IsGrounded ? OdmState.Grounded : OdmState.Airborne;
    }

    private void ClearLeftCable()
    {
        IsLeftAnchored = false;
        IsLeftFlying = false;
        leftFlyRetracting = false;
        leftTipTouchingSurface = false;
        leftBends.Clear();
        leftTentacleHitRecords.Clear();
        ClearEnemyAnchor(true);
        ClearObjectAnchor(true);
    }

    private void ClearRightCable()
    {
        IsRightAnchored = false;
        IsRightFlying = false;
        rightFlyRetracting = false;
        rightTipTouchingSurface = false;
        rightBends.Clear();
        rightTentacleHitRecords.Clear();
        ClearEnemyAnchor(false);
        ClearObjectAnchor(false);
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
        filter.SetLayerMask(GetGroundMask());

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

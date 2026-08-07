import fs from "node:fs";
import path from "node:path";
import zlib from "node:zlib";

const projectRoot = path.resolve("D:/user/Free");
const scriptsRoot = path.join(projectRoot, "Assets", "Scripts");
const outputPath = path.join(projectRoot, "功能参数模块整理.xlsx");

const moduleNames = new Map([
  ["触手", "触手/绳索系统"],
  ["玩家", "玩家系统"],
  ["敌人", "敌人系统"],
  ["首领", "Boss/首领系统"],
  ["成长", "成长/升级系统"],
  ["物品", "物品/掉落系统"],
  ["核心", "核心通用系统"],
  ["反馈", "反馈系统"],
  ["存档", "存档系统"],
]);

const componentNames = new Map([
  ["OdmController", "玩家移动与触手绳索控制"],
  ["OdmInput", "触手输入控制"],
  ["OdmGasSystem", "触手气体资源"],
  ["OdmVisuals", "绳索视觉与摄像机表现"],
  ["OdmCableFeedback", "绳索音效、钩中子弹时间与命中反馈"],
  ["RopeCameraViewController", "远距离绳索目标小窗"],
  ["TentacleInteractableObject", "可被触手抓取的场景物品"],
  ["PlayerAttack", "玩家近战攻击"],
  ["PlayerHealth", "玩家生命"],
  ["PlayerHealthBarUI", "玩家生命条 UI"],
  ["PlayerSpeedDisplay", "玩家速度调试 UI"],
  ["Enemy", "敌人生命、处决与触手交互"],
  ["EnemySimpleAI", "小怪巡逻、追踪与近战"],
  ["EnemySimpleHealthBar", "敌人血条与处决提示"],
  ["EnemyDamagePopup", "敌人伤害数字"],
  ["EnemyLootDropper", "敌人掉落"],
  ["BossController", "Boss 战斗状态机"],
  ["BossArenaController", "Boss 战斗场地"],
  ["BossProjectile", "Boss 弹幕"],
  ["BossTrap", "Boss 陷阱"],
  ["BossHazardUtility", "Boss 危险物工具"],
  ["HookableBossAnchor", "Boss 可钩锚点"],
  ["HitFeedback", "命中顿帧与镜头震动"],
  ["CombatEntityBase", "通用战斗实体"],
  ["DeathPlace", "死亡区域与复活点"],
  ["PlayerInventory", "玩家背包"],
  ["LootPickup", "掉落物拾取"],
  ["TentacleProgression", "触手属性成长"],
  ["TentacleUpgradeInput", "触手升级输入与消耗"],
  ["PlayerProgressSave", "玩家进度存档"],
  ["TimeScaleHitStop", "全局顿帧工具"],
]);

const functionTranslations = new Map([
  ["状态", "状态"],
  ["绳索锚点", "绳索锚点/连接判定"],
  ["触手命中", "触手攻击命中"],
  ["触手受击", "触手受击与玩家扣血"],
  ["触手弹反", "触手弹反"],
  ["触手投掷通用", "触手投掷通用"],
  ["触手抓取敌人", "触手抓取敌人"],
  ["触手抓取场景物品", "触手抓取场景物品"],
  ["触手处决", "触手处决"],
  ["移动参数", "玩家地面移动"],
  ["跳跃", "玩家跳跃"],
  ["发射移动", "绳索发射期间移动"],
  ["刚体参数", "玩家刚体基础参数"],
  ["状态物理", "不同状态的物理参数"],
  ["下落控制", "下落控制"],
  ["速度限制", "速度限制"],
  ["角色朝向", "角色朝向"],
  ["角色动画", "角色动画参数"],
  ["碰撞保护", "碰撞保护"],
  ["受伤击退", "受伤击退"],
  ["地面检测", "地面检测"],
  ["绳索弯折", "绳索弯折"],
  ["战斗流程", "Boss 战斗流程"],
  ["战斗区域", "Boss 战斗区域"],
  ["弹幕", "Boss 弹幕"],
  ["陷阱", "Boss 陷阱"],
  ["动画", "动画"],
  ["弹反受制", "弹反后的受制状态"],
  ["打击反馈", "打击反馈"],
  ["目标", "目标查找"],
  ["移动", "移动 AI"],
  ["受击硬直", "受击硬直"],
  ["地形检测", "地形检测"],
  ["近战攻击", "近战攻击"],
  ["弹反反馈", "弹反反馈"],
  ["调试", "调试显示"],
  ["音频引用", "音频引用"],
  ["音量", "音量控制"],
  ["钩中停顿", "钩中停顿"],
  ["钩中子弹时间", "钩中子弹时间"],
  ["绳索视觉组件", "绳索视觉组件"],
  ["绳索发射波浪", "绳索发射波浪"],
  ["绳索钩中波浪", "绳索钩中波浪"],
  ["绳索弹性", "绳索弹性视觉"],
  ["摄像机缩放", "摄像机缩放"],
  ["鼠标前看", "鼠标前看"],
  ["生命", "生命值"],
  ["攻击", "攻击"],
  ["伤害", "伤害"],
  ["表现", "表现"],
  ["掉落配置", "掉落配置"],
  ["拾取", "拾取"],
  ["漂浮表现", "漂浮表现"],
  ["存档", "存档"],
  ["基础属性", "基础属性"],
  ["升级入口", "升级入口"],
  ["血条", "血条"],
  ["处决提示", "处决提示"],
  ["颜色", "颜色"],
  ["排序", "渲染排序"],
]);

const exactChineseNames = new Map(Object.entries({
  currentState: "当前 ODM 状态",
  anchorableLayer: "可钩连接层级",
  solidLayer: "实体障碍层级",
  groundLayer: "地面判定层级",
  cableBendLayer: "绳索弯折层级",
  enableCableBending: "启用绳索弯折",
  leftAnchorPoint: "左侧绳索发射点",
  rightAnchorPoint: "右侧绳索发射点",
  maxCableLength: "最大绳索长度",
  cableShootSpeed: "绳头发射速度",
  mouseAnchorSearchRadius: "鼠标附近钩点搜索半径",
  cablePathAnchorSearchRadius: "绳索路径钩点搜索半径",
  showAnchorAssistGizmos: "显示钩点辅助范围",
  mouseAnchorSearchGizmoColor: "鼠标钩点搜索范围颜色",
  cablePathAnchorSearchGizmoColor: "路径钩点搜索范围颜色",
  tentacleHitLayer: "触手命中层级",
  tentacleHitRadius: "触手命中半径",
  tentacleBaseDamage: "触手基础伤害",
  tentacleSpeedDamageScale: "触手速度伤害倍率",
  tentacleMinHitSpeed: "触手最低伤害速度",
  tentacleHitCooldown: "触手重复命中冷却",
  allowTentacleAnchorEnemies: "允许触手锚定敌人",
  allowCableDamagePlayer: "允许敌人攻击触手伤害玩家",
  cableHurtRadius: "触手受击半径",
  cableDamageCooldown: "触手受击冷却",
  cableToughness: "触手韧性",
  cableParryWindow: "触手弹反窗口",
  cableParryCooldown: "触手弹反冷却",
  cableParrySelfKnockback: "弹反后玩家后坐力",
  allowCableParryArea: "启用前方弹反判定区",
  cableParryAreaSize: "弹反判定区尺寸",
  cableParryAreaOffset: "弹反判定区偏移",
  showCableParryAreaGizmo: "显示弹反判定区",
  cableParryFlashColor: "弹反闪光颜色",
  cableParryFlashMaxAlpha: "弹反闪光最大强度",
  cableParryFlashShaderName: "弹反闪光 Shader 名",
  cableParrySuccessFlashDuration: "弹反成功强闪持续时间",
  cableParrySuccessFlashMaxAmount: "弹反成功强闪强度",
  enableCableParryHitStop: "启用弹反顿帧",
  cableParryHitStopTimeScale: "弹反顿帧时间倍率",
  cableParryHitStopDuration: "弹反顿帧持续时间",
  throwAimDirectionMinSpeed: "投掷方向最低参考速度",
  tentaclePower: "触手力量",
  allowEnemyGrab: "允许抓取敌人",
  enemyGrabFollowForce: "敌人抓取跟随力",
  enemyGrabFollowDamping: "敌人抓取阻尼",
  enemyGrabResistanceScale: "敌人抓取抗性倍率",
  enemyGrabMaxSpeed: "被抓敌人最大速度",
  enemyGrabLocalOffset: "敌人抓取吸附偏移",
  enemyThrowVelocityScale: "敌人投掷速度倍率",
  enemyThrowVelocitySmoothing: "敌人投掷速度平滑",
  enemyThrowMinSpeed: "敌人投掷最低速度",
  enemyThrowMaxSpeed: "敌人投掷最高速度",
  allowObjectGrab: "允许抓取场景物品",
  objectGrabFollowForce: "物品抓取跟随力",
  objectGrabFollowDamping: "物品抓取阻尼",
  objectGrabResistanceScale: "物品抓取抗性倍率",
  objectGrabMaxSpeed: "被抓物品最大速度",
  objectThrowVelocityScale: "物品投掷速度倍率",
  objectThrowVelocitySmoothing: "物品投掷速度平滑",
  objectThrowMinSpeed: "物品投掷最低速度",
  objectThrowMaxSpeed: "物品投掷最高速度",
  executionTargetLayer: "处决目标层级",
  executionSearchRadius: "处决目标搜索半径",
  allowTearExecution: "允许双触手撕裂处决",
  allowPierceExecution: "允许单触手穿刺处决",
  tearExecutionDuration: "撕裂处决时长",
  tearExecutionPullDistance: "撕裂处决拉开距离",
  tearExecutionCableSegments: "撕裂处决绳索分段数",
  tearExecutionCableOutwardBend: "撕裂处决外侧弯曲距离",
  tearExecutionCableLowerBend: "撕裂处决下压弯曲距离",
  tearExecutionCenterClearance: "撕裂处决中心避让距离",
  pierceExecutionDuration: "穿刺处决最低时长",
  pierceExecutionSpeed: "穿刺处决速度",
  pierceExecutionOverrunDistance: "穿刺穿过目标延伸距离",
  groundMoveSpeed: "地面移动速度",
  jumpSpeed: "跳跃速度",
  fullJumpHoldTime: "完整跳跃按住时间",
  coyoteTime: "土狼时间",
  jumpBufferTime: "跳跃预输入时间",
  ropeMobilityHorizontalSpeed: "绳索机动水平微调速度",
  pullForce: "绳索牵引力",
  allowGroundMoveWhileCableFlying: "允许绳索飞行时地面移动",
  airDashForce: "空中冲刺力",
  overrideRigidbodySettings: "覆盖刚体设置",
  bodyMass: "角色刚体质量",
  bodyLinearDamping: "角色线性阻尼",
  bodyGravityScale: "角色重力倍率",
  enableStatePhysics: "启用状态物理切换",
  dailyGroundGravityScale: "日常地面重力倍率",
  dailyAirGravityScale: "日常空中重力倍率",
  ropeMobilityGravityScale: "绳索机动重力倍率",
  dailyGroundDamping: "日常地面阻尼",
  dailyAirDamping: "日常空中阻尼",
  ropeMobilityDamping: "绳索机动阻尼",
  limitFallSpeed: "限制最大下落速度",
  maxSpeed: "最大移动速度",
  stopDamping: "急停阻尼",
  characterRenderer: "角色 SpriteRenderer",
  facingInputThreshold: "朝向输入阈值",
  characterAnimator: "角色 Animator",
  walkParameterName: "行走动画参数名",
  airborneParameterName: "空中动画参数名",
  groundCheckDistance: "碰撞 Cast 额外距离",
  playerDamageKnockbackControlLock: "受伤击退控制锁定时间",
  castSkin: "地面检测 Cast 距离",
  ropeBendOffset: "绳索拐角外偏距离",
  ropeCornerReleaseDistance: "绳索拐角释放距离",
  maxRopeBends: "最大绳索弯折点数",
  masterVolume: "总音量",
  shootVolume: "发射音量",
  anchorVolume: "钩中音量",
  releaseVolume: "松开音量",
  failVolume: "失败音量",
  enableAnchorBulletTime: "启用钩中子弹时间",
  anchorBulletTimeScale: "钩中子弹时间倍率",
  anchorBulletTimeDuration: "钩中子弹时间持续时间",
  enableAnchorHitStop: "启用钩中顿帧",
  anchorHitStopTimeScale: "钩中顿帧时间倍率",
  anchorHitStopDuration: "钩中顿帧持续时间",
  enableAnchorWave: "启用钩中波浪",
  enableShootWave: "启用发射波浪",
  shootWaveSegments: "发射波浪分段数",
  shootWaveAmplitude: "发射波浪主幅度",
  shootWaveFrequency: "发射波浪主频率",
  shootWaveSpeed: "发射波浪滚动速度",
  shootWaveFadeDistance: "发射波浪淡出距离",
  shootWaveSecondaryAmplitude: "发射波浪细幅度",
  shootWaveSecondaryFrequency: "发射波浪细频率",
  anchorWaveSegments: "钩中波浪分段数",
  anchorWaveAmplitude: "钩中波浪幅度",
  anchorWaveMaxLengthRatio: "钩中波浪最大绳长比例",
  anchorWaveDuration: "钩中波浪持续时间",
  anchorWaveOscillations: "钩中波浪震荡次数",
  elasticMaxLagDistance: "弹性视觉最大滞后距离",
  elasticBendVelocityScale: "弹性弯曲速度倍率",
  elasticBendSmoothTime: "弹性弯曲平滑时间",
  baseOrthographicSize: "基础摄像机尺寸",
  maxOrthographicSize: "最大摄像机尺寸",
  cameraSizeChangeSpeed: "摄像机缩放速度",
  enableMouseLookAhead: "启用鼠标前看",
  horizontalLookAheadDistance: "水平前看距离",
  verticalLookAheadDistance: "垂直前看距离",
  lookAheadDeadZone: "前看死区",
  lookAheadSmoothTime: "前看平滑时间",
  maxGasCapacity: "最大气体容量",
  gasConsumeRate: "气体消耗速率",
  showDistance: "小窗显示距离",
  preferFartherRope: "优先显示更远绳索",
  showAnchoredRope: "显示已锚定绳索",
  cameraOrthographicSize: "小窗摄像机尺寸",
  cameraZ: "小窗摄像机 Z 坐标",
  followSmoothTime: "摄像机跟随平滑时间",
  lookAheadDistance: "目标前看距离",
  attackTriggerName: "攻击动画触发名",
  attackActiveTime: "攻击有效时间",
  useHitBoxAnimationDuration: "使用判定框动画时长",
  attackCooldown: "攻击冷却",
  airborneDirectionMinSpeed: "空中朝向参考最低速度",
  baseDamage: "基础伤害",
  maxSpeedDamageBonus: "最大速度伤害加成",
  speedDamageExponent: "速度伤害曲线指数",
  damageReferenceSpeed: "伤害参考速度",
  logDamageDebugEveryFrame: "每帧输出伤害调试",
  maxHealth: "最大生命值",
  invulnerableDuration: "受伤无敌时间",
  fillUpdateMode: "填充更新模式",
  damageReduceDirection: "伤害减少方向",
  bufferLerpSpeed: "缓冲条追赶速度",
  hideWhenNoTarget: "无目标时隐藏",
  showHealthText: "显示生命文本",
  refreshInterval: "刷新间隔",
  showVelocityComponents: "显示速度分量",
  showOdmState: "显示 ODM 状态",
  showMaxSpeedRatio: "显示最大速度比例",
  enemyMass: "敌人质量",
  grabTenacity: "抓取抗性",
  canBeExecuted: "允许被处决",
  executionHealthRatio: "可处决血量比例",
  pierceExecutionSpecialDropMultiplier: "穿刺处决特殊掉落倍率",
  tearExecutionSpecialDropMultiplier: "撕裂处决特殊掉落倍率",
  pendingSpecialDropMultiplier: "待结算特殊掉落倍率",
  executionFragmentLifetime: "处决碎片存在时间",
  executionFragmentSpeed: "处决碎片飞散速度",
  executionFragmentShaderName: "处决碎片 Shader 名",
  executionFragmentSplit: "处决碎片切分位置",
  executionFragmentEdgeWidth: "处决碎片边缘宽度",
  executionFragmentEdgeColor: "处决碎片边缘颜色",
  tentacleThrowImpactWindow: "触手投掷撞击窗口",
  tentacleThrowMinImpactSpeed: "触手投掷最低撞击速度",
  tentacleThrowBaseDamage: "触手投掷基础伤害",
  tentacleThrowSpeedDamageScale: "触手投掷速度伤害倍率",
  tentacleThrowImpactLayer: "触手投掷撞击层级",
  allowTentacleHeldSmashDamage: "允许抓持中砸击伤害",
  tentacleHeldSmashMinImpactSpeed: "抓持砸击最低速度",
  tentacleHeldSmashBaseDamage: "抓持砸击基础伤害",
  tentacleHeldSmashPowerDamageScale: "抓持砸击力量伤害倍率",
  tentacleHeldSmashSpeedDamageScale: "抓持砸击速度伤害倍率",
  tentacleHeldSmashCooldown: "抓持砸击冷却",
  tentacleHeldSmashImpactLayer: "抓持砸击撞击层级",
  enableHitKnockback: "启用受击击退",
  hitKnockbackBaseForce: "受击基础击退力",
  hitKnockbackSpeedForce: "速度转击退力",
  hitKnockbackMaxSpeed: "受击最大击退速度",
  hitFlashDuration: "受击闪烁持续时间",
  hitFlashInterval: "受击闪烁间隔",
  hitFlashMinAlpha: "受击闪烁最低透明度",
  enableDeathFade: "启用死亡淡出",
  deathFadeDuration: "死亡淡出时长",
  disableCollidersOnDeath: "死亡时禁用碰撞体",
  autoFindPlayer: "自动查找玩家",
  targetPlayer: "目标玩家",
  detectRange: "检测范围",
  enablePatrol: "启用巡逻",
  patrolRadius: "巡逻半径",
  patrolSpeed: "巡逻速度",
  patrolPauseDuration: "巡逻停顿时间",
  moveSpeed: "移动速度",
  acceleration: "加速度",
  stopDistance: "停止距离",
  flipSpriteByDirection: "按移动方向翻转",
  enableHitStun: "启用受击硬直",
  hitStunDuration: "受击基础硬直时间",
  hitStunDurationPerDamage: "每点伤害增加硬直时间",
  maxHitStunDuration: "最大受击硬直时间",
  avoidLedges: "避免走下平台",
  obstacleLayer: "障碍检测层级",
  groundProbeOffset: "地面探测偏移",
  groundProbeDistance: "地面探测距离",
  wallProbeHeight: "墙体探测高度",
  wallProbeDistance: "墙体探测距离",
  enableMeleeAttack: "启用近战攻击",
  attackRange: "攻击范围",
  attackWindup: "攻击起手时间",
  attackDamage: "攻击伤害",
  attackKnockback: "攻击击退力",
  attackPower: "攻击力",
  canBeParried: "允许被弹反",
  attackBoxSize: "攻击判定框尺寸",
  attackBoxOffset: "攻击判定框偏移",
  attackLayer: "攻击检测层级",
  parriedStunDuration: "被弹反硬直时间",
  parriedKnockbackSpeed: "被弹反击退速度",
  showGizmos: "显示调试 Gizmos",
  localOffset: "本地偏移",
  width: "宽度",
  height: "高度",
  layerDepthStep: "层深步进",
  hideWhenFull: "满血时隐藏",
  backgroundRenderer: "血条背景渲染器",
  bufferRenderer: "血量缓冲渲染器",
  fillRenderer: "血量填充渲染器",
  showExecutePrompt: "显示处决提示",
  executePromptOffset: "处决提示偏移",
  executePrompt: "处决提示文本",
  executePromptFontSize: "处决提示字号",
  executePromptCharacterSize: "处决提示字符尺寸",
  backgroundColor: "背景颜色",
  bufferColor: "缓冲颜色",
  fillColor: "填充颜色",
  executePromptColor: "处决提示颜色",
  sortingLayerName: "排序层名称",
  sortingOrder: "排序顺序",
  spawnOffset: "生成偏移",
  horizontalSpawnJitter: "水平生成抖动",
  jumpVelocity: "跳起速度",
  horizontalVelocity: "水平速度",
  gravity: "重力",
  lifetime: "存在时间",
  fadeStartRatio: "开始淡出比例",
  textColor: "文本颜色",
  fontSize: "字号",
  characterSize: "字符尺寸",
  itemId: "物品 ID",
  dropChance: "掉落概率",
  affectedByExecutionBonus: "受处决奖励影响",
  minAmount: "最小数量",
  maxAmount: "最大数量",
  lootEntries: "掉落配置列表",
  spawnSpread: "生成散布范围",
  logDropsWithoutPrefab: "缺少预制体时输出掉落日志",
  spawnDefaultPickupWhenPrefabMissing: "缺少预制体时生成默认拾取物",
  lastSpecialDropMultiplier: "上次特殊掉落倍率",
  objectMass: "物品质量",
  throwImpactWindow: "投掷撞击窗口",
  throwMinImpactSpeed: "投掷最低撞击速度",
  throwBaseDamage: "投掷基础伤害",
  throwSpeedDamageScale: "投掷速度伤害倍率",
  throwDamageLayer: "投掷伤害层级",
  clearThrowStateAfterDamage: "造成伤害后清除投掷状态",
  amount: "数量",
  canAutoPickup: "允许自动拾取",
  logPickup: "输出拾取日志",
  pickupColor: "拾取物颜色",
  bobAmplitude: "上下漂浮幅度",
  bobSpeed: "上下漂浮速度",
  items: "物品列表",
  logItemChanges: "输出物品变化日志",
  saveKey: "存档键名",
  loadOnStart: "启动时读取存档",
  autoSaveOnInventoryChange: "背包变化时自动存档",
  autoSaveOnProgressionChange: "成长变化时自动存档",
  saveOnApplicationQuit: "退出应用时存档",
  logSaveEvents: "输出存档日志",
  controller: "目标控制器",
  inventory: "背包组件",
  progression: "成长组件",
  version: "存档版本",
  applyOnAwake: "Awake 时应用成长属性",
  powerLevel: "力量等级",
  basePower: "基础力量",
  powerPerLevel: "每级力量成长",
  toughnessLevel: "韧性等级",
  baseToughness: "基础韧性",
  toughnessPerLevel: "每级韧性成长",
  lengthLevel: "长度等级",
  baseMaxLength: "基础最大长度",
  lengthPerLevel: "每级长度成长",
  hitRadiusLevel: "命中半径等级",
  baseHitRadius: "基础命中半径",
  hitRadiusPerLevel: "每级命中半径成长",
  hurtRadiusLevel: "受击半径等级",
  baseHurtRadius: "基础受击半径",
  hurtRadiusPerLevel: "每级受击半径成长",
  upgrades: "升级配置列表",
  upgradeName: "升级名称",
  key: "升级按键",
  upgradeType: "升级类型",
  costItemId: "消耗物品 ID",
  baseCost: "基础消耗",
  costPerLevel: "每级递增消耗",
  levelsPerUpgrade: "每次升级等级数",
  logUpgradeResult: "输出升级结果日志",
  configId: "配置 ID",
  hitBoxDamage: "碰撞伤害",
  checkpointObjectName: "复活点对象名",
  respawnOffset: "复活偏移",
  onlyTeleportPlayer: "只传送玩家",
  clearPlayerTentacles: "复活时清空触手",
  resetPlayerVelocity: "复活时清空速度",
  autoFindCheckpointOnStart: "启动时自动查找复活点",
  logWarnings: "输出警告日志",
  checkpoint: "复活点引用",
  enableHitStop: "启用命中顿帧",
  hitStopTimeScale: "命中顿帧时间倍率",
  hitStopDuration: "命中顿帧持续时间",
  enableCameraShake: "启用镜头震动",
  cameraShakeForce: "镜头震动力度",
  scaleShakeByHitSpeed: "按命中速度缩放震动",
  maxSpeedShakeMultiplier: "最大速度震动倍率",
  horizontalOnlyShake: "仅水平震动",
  autoAddImpulseListener: "自动添加镜头震动监听",
  configureImpulseSourceOnAwake: "Awake 时配置震动源",
  impulseListener: "镜头震动监听器",
  state: "当前 Boss 状态",
  introDuration: "Boss 入场等待时间",
  vulnerableDuration: "Boss 可受伤窗口",
  hitDisappearDuration: "Boss 受击隐藏时间",
  bossPositions: "Boss 可移动位置",
  arenaCenter: "Boss 战区域中心",
  arenaSize: "Boss 战区域尺寸",
  hookableAnchorLayer: "Boss 可钩锚点层级",
  projectileSpeed: "Boss 弹幕速度",
  projectileLifetime: "Boss 弹幕存在时间",
  projectileDamage: "Boss 弹幕伤害",
  projectileKnockback: "Boss 弹幕击退",
  trapWarningDuration: "陷阱预警时间",
  trapActiveDuration: "陷阱生效时间",
  trapDamage: "陷阱伤害",
  trapKnockback: "陷阱击退",
  bossAnimator: "Boss Animator",
  hurtBoolParameterName: "受击动画 Bool 参数名",
  attackBoolParameterName: "攻击动画 Bool 参数名",
  fallbackHurtAnimationDuration: "受击动画兜底时长",
  parryStunDuration: "弹反后 Boss 停顿时间",
  parryStunTint: "弹反受制颜色",
  reflectedProjectileDamageMultiplier: "反弹弹幕伤害倍率",
  boss: "Boss 控制器",
  lockdownWalls: "封场墙体列表",
  startLocked: "启动时封场",
  odmController: "ODM 控制器",
  viewRoot: "小窗 UI 根节点",
  fillImage: "血量填充图像",
  bufferImage: "血量缓冲图像",
}));

const tokenTranslations = new Map(Object.entries({
  current: "当前",
  state: "状态",
  enable: "启用",
  allow: "允许",
  auto: "自动",
  find: "查找",
  player: "玩家",
  enemy: "敌人",
  boss: "Boss",
  bullet: "子弹",
  controller: "控制器",
  inventory: "背包",
  progression: "成长",
  view: "视图",
  root: "根节点",
  renderer: "渲染器",
  image: "图像",
  listener: "监听器",
  checkpoint: "复活点",
  lockdown: "封场",
  walls: "墙体",
  start: "启动",
  locked: "锁定",
  version: "版本",
  tentacle: "触手",
  cable: "绳索",
  rope: "绳索",
  anchor: "锚点",
  anchored: "已锚定",
  anchorable: "可钩",
  layer: "层级",
  mask: "遮罩",
  solid: "实体",
  ground: "地面",
  object: "物品",
  grab: "抓取",
  follow: "跟随",
  force: "力",
  damping: "阻尼",
  resistance: "抗性",
  scale: "倍率",
  speed: "速度",
  velocity: "速度",
  min: "最小",
  max: "最大",
  base: "基础",
  damage: "伤害",
  hurt: "受击",
  hit: "命中",
  cooldown: "冷却",
  duration: "持续时间",
  time: "时间",
  radius: "半径",
  distance: "距离",
  offset: "偏移",
  size: "尺寸",
  color: "颜色",
  flash: "闪光",
  shader: "Shader",
  name: "名称",
  animation: "动画",
  animator: "Animator",
  parameter: "参数",
  parry: "弹反",
  window: "窗口",
  area: "区域",
  execution: "处决",
  tear: "撕裂",
  pierce: "穿刺",
  throw: "投掷",
  move: "移动",
  jump: "跳跃",
  air: "空中",
  airborne: "空中",
  gravity: "重力",
  mass: "质量",
  body: "刚体",
  health: "生命",
  text: "文本",
  font: "字体",
  sorting: "排序",
  order: "顺序",
  volume: "音量",
  clip: "音效",
  audio: "音频",
  source: "源",
  camera: "摄像机",
  look: "前看",
  ahead: "前看",
  show: "显示",
  hide: "隐藏",
  gizmos: "Gizmos",
  debug: "调试",
  log: "日志",
  item: "物品",
  cost: "消耗",
  level: "等级",
  per: "每级",
  pickup: "拾取",
  prefab: "预制体",
  projectile: "弹幕",
  trap: "陷阱",
  knockback: "击退",
  patrol: "巡逻",
  attack: "攻击",
  target: "目标",
}));

function listFiles(dir) {
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) files.push(...listFiles(full));
    else if (entry.isFile() && entry.name.endsWith(".cs")) files.push(full);
  }
  return files.sort((a, b) => a.localeCompare(b, "zh-Hans-CN"));
}

function decodeAttributeString(value) {
  if (!value) return "";
  return value.replace(/\\n/g, "\n").replace(/\\"/g, '"').trim();
}

function splitCamel(name) {
  return name
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/_/g, " ")
    .split(/\s+/)
    .filter(Boolean)
    .map((part) => part.toLowerCase());
}

function chineseNameFor(fieldName, inspectorName) {
  if (inspectorName) return inspectorName;
  if (exactChineseNames.has(fieldName)) return exactChineseNames.get(fieldName);
  const tokens = splitCamel(fieldName);
  const translated = tokens.map((token) => tokenTranslations.get(token) ?? token);
  return translated.join("");
}

function normalizeHeader(header, componentName) {
  if (header && functionTranslations.has(header)) return functionTranslations.get(header);
  if (header) return header;
  return componentName;
}

function moduleFor(relPath) {
  const parts = relPath.split(/[\\/]/);
  if (parts[1] === "Scripts" && parts[2] === "玩家" && parts[3] === "界面") return "玩家界面系统";
  const folder = parts[2] ?? "";
  return moduleNames.get(folder) ?? folder;
}

function parseFields(filePath) {
  const relPath = path.relative(projectRoot, filePath).replaceAll(path.sep, "/");
  const text = fs.readFileSync(filePath, "utf8");
  const lines = text.split(/\r?\n/);
  const fields = [];
  const fileBase = path.basename(filePath, ".cs");
  const componentName = componentNames.get(fileBase) ?? fileBase;
  const module = moduleFor(relPath);
  let currentHeader = "";
  let pendingTooltip = "";
  let pendingInspectorName = "";
  let pendingRange = "";
  let pendingAttrs = [];
  let pendingSerializableType = false;
  let classStack = [];
  let braceDepth = 0;
  let pendingType = null;

  const fieldRegex = /^\s*public\s+(?!static\b)(?!const\b)(?!event\b)([A-Za-z_][\w.<>,\[\]?]*\s*(?:\[\])?)\s+([A-Za-z_]\w*)\s*(?:=\s*(.*))?;?\s*$/;

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    const trimmed = line.trim();

    if (trimmed === "[Serializable]" || trimmed === "[System.Serializable]") {
      pendingSerializableType = true;
      continue;
    }

    const typeMatch = trimmed.match(/^(?:(public|private|protected|internal)\s+)?(?:(static|sealed|abstract|partial|readonly)\s+)*(class|struct)\s+([A-Za-z_]\w*)\s*(?::\s*([^{]+))?/);
    if (typeMatch) {
      const [, visibility = "", modifier = "", kind, name, baseList = ""] = typeMatch;
      const isStatic = modifier.includes("static") || trimmed.includes(" static ");
      const isComponent = !isStatic && kind === "class" && /\b(MonoBehaviour|CombatEntityBase)\b/.test(baseList);
      const isSerializableConfig = pendingSerializableType && (visibility === "public" || name === fileBase);
      pendingType = {
        name,
        depth: braceDepth,
        includeFields: isComponent || isSerializableConfig,
      };
      pendingSerializableType = false;
    }

    if (trimmed.startsWith("[Header(")) {
      const match = trimmed.match(/\[Header\("([\s\S]*)"\)\]/);
      if (match) currentHeader = decodeAttributeString(match[1]);
      pendingAttrs.push(trimmed);
      continue;
    }
    if (trimmed.startsWith("[Tooltip(")) {
      const match = trimmed.match(/\[Tooltip\("([\s\S]*)"\)\]/);
      if (match) pendingTooltip = decodeAttributeString(match[1]);
      pendingAttrs.push(trimmed);
      continue;
    }
    if (trimmed.startsWith("[InspectorName(")) {
      const match = trimmed.match(/\[InspectorName\("([\s\S]*)"\)\]/);
      if (match) pendingInspectorName = decodeAttributeString(match[1]);
      pendingAttrs.push(trimmed);
      continue;
    }
    if (trimmed.startsWith("[Range(") || trimmed.startsWith("[Min(")) {
      pendingRange = trimmed.replace(/^\[/, "").replace(/\]$/, "");
      pendingAttrs.push(trimmed);
      continue;
    }

    const fieldMatch = line.match(fieldRegex);
    const looksLikeProperty = trimmed.includes("{") || trimmed.includes("=>") || trimmed.includes("(");
    const activeType = classStack.length > 0 ? classStack[classStack.length - 1] : null;
    const isFieldSyntax = trimmed.endsWith(";") || trimmed.endsWith("=");
    if (fieldMatch && isFieldSyntax && !looksLikeProperty && activeType?.includeFields) {
      const [, type, name, rawDefault] = fieldMatch;
      let defaultValue = (rawDefault ?? "").replace(/;$/, "").trim();
      if (!defaultValue && trimmed.endsWith("=")) defaultValue = "多行初始化";
      const activeClass = activeType.name;
      const component = activeClass === fileBase ? componentName : `${componentName}.${activeClass}`;
      const feature = normalizeHeader(currentHeader, componentName);
      const role = pendingTooltip || inferRole(name, type, feature);
      const notes = [pendingRange, pendingAttrs.filter((attr) => !attr.startsWith("[Header(") && !attr.startsWith("[Tooltip(") && !attr.startsWith("[InspectorName(") && !attr.startsWith("[Range(") && !attr.startsWith("[Min(")).join(" ")]
        .filter(Boolean)
        .join("；");
      fields.push({
        module,
        feature,
        component,
        parameter: name,
        chineseName: chineseNameFor(name, pendingInspectorName),
        type: type.trim(),
        defaultValue: defaultValue || "未在代码中指定",
        role,
        notes,
        file: relPath,
        line: i + 1,
      });
      pendingTooltip = "";
      pendingInspectorName = "";
      pendingRange = "";
      pendingAttrs = [];
    } else if (trimmed && !trimmed.startsWith("//") && !trimmed.startsWith("///") && !trimmed.startsWith("[") && !trimmed.startsWith("{") && !trimmed.startsWith("}")) {
      pendingTooltip = "";
      pendingInspectorName = "";
      pendingRange = "";
      pendingAttrs = [];
    }

    const openCount = (line.match(/{/g) ?? []).length;
    const closeCount = (line.match(/}/g) ?? []).length;
    if (pendingType && openCount > 0) {
      classStack.push({
        name: pendingType.name,
        depth: braceDepth + openCount - closeCount,
        includeFields: pendingType.includeFields,
      });
      pendingType = null;
    }
    braceDepth += openCount - closeCount;
    while (classStack.length > 0 && braceDepth < classStack[classStack.length - 1].depth) {
      classStack.pop();
    }
  }
  return fields;
}

function inferRole(name, type, feature) {
  const cn = chineseNameFor(name, "");
  if (type.includes("LayerMask")) return `控制“${feature}”使用的检测层级。`;
  if (type === "bool") return `开关“${cn}”相关行为。`;
  if (type === "Color") return `控制“${cn}”的显示颜色。`;
  if (type.startsWith("Vector")) return `控制“${cn}”的位置、尺寸或方向数值。`;
  if (type === "string") return `配置“${cn}”使用的名称或标识。`;
  if (type === "int" || type === "float") return `调节“${cn}”的数值大小。`;
  return `引用或配置“${cn}”所需对象。`;
}

function xmlEscape(value) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&apos;");
}

function colName(index) {
  let name = "";
  while (index > 0) {
    const rem = (index - 1) % 26;
    name = String.fromCharCode(65 + rem) + name;
    index = Math.floor((index - 1) / 26);
  }
  return name;
}

function cell(value, style = 0) {
  return `<c t="inlineStr"${style ? ` s="${style}"` : ""}><is><t>${xmlEscape(value)}</t></is></c>`;
}

function row(values, rowIndex, style = 0) {
  return `<row r="${rowIndex}">${values.map((value) => cell(value, style)).join("")}</row>`;
}

function sheetXml(headers, rows, widths, freezePane = true) {
  const lastCol = colName(headers.length);
  const headerRow = row(headers, 1, 1);
  const dataRows = rows.map((values, idx) => row(values, idx + 2, 0)).join("");
  const cols = widths.map((width, idx) => `<col min="${idx + 1}" max="${idx + 1}" width="${width}" customWidth="1"/>`).join("");
  const pane = freezePane
    ? `<sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>`
    : `<sheetViews><sheetView workbookViewId="0"/></sheetViews>`;
  return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  ${pane}
  <cols>${cols}</cols>
  <sheetData>${headerRow}${dataRows}</sheetData>
  <autoFilter ref="A1:${lastCol}${rows.length + 1}"/>
</worksheet>`;
}

function stylesXml() {
  return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <fonts count="2">
    <font><sz val="11"/><name val="Microsoft YaHei"/></font>
    <font><b/><sz val="11"/><name val="Microsoft YaHei"/><color rgb="FFFFFFFF"/></font>
  </fonts>
  <fills count="3">
    <fill><patternFill patternType="none"/></fill>
    <fill><patternFill patternType="gray125"/></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FF1F4E79"/><bgColor indexed="64"/></patternFill></fill>
  </fills>
  <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
  <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
  <cellXfs count="2">
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
    <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/>
  </cellXfs>
</styleSheet>`;
}

const crcTable = (() => {
  const table = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    table[n] = c >>> 0;
  }
  return table;
})();

function crc32(buffer) {
  let crc = 0xffffffff;
  for (const byte of buffer) crc = crcTable[(crc ^ byte) & 0xff] ^ (crc >>> 8);
  return (crc ^ 0xffffffff) >>> 0;
}

function dosDateTime(date = new Date()) {
  const time = (date.getHours() << 11) | (date.getMinutes() << 5) | Math.floor(date.getSeconds() / 2);
  const day = date.getDate();
  const month = date.getMonth() + 1;
  const year = Math.max(1980, date.getFullYear()) - 1980;
  const dosDate = (year << 9) | (month << 5) | day;
  return { time, date: dosDate };
}

function createZip(files, destination) {
  const localParts = [];
  const centralParts = [];
  let offset = 0;
  const stamp = dosDateTime();

  for (const [name, content] of files) {
    const nameBuffer = Buffer.from(name.replaceAll("\\", "/"), "utf8");
    const data = Buffer.isBuffer(content) ? content : Buffer.from(content, "utf8");
    const compressed = zlib.deflateRawSync(data, { level: 9 });
    const crc = crc32(data);

    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(20, 4);
    local.writeUInt16LE(0x0800, 6);
    local.writeUInt16LE(8, 8);
    local.writeUInt16LE(stamp.time, 10);
    local.writeUInt16LE(stamp.date, 12);
    local.writeUInt32LE(crc, 14);
    local.writeUInt32LE(compressed.length, 18);
    local.writeUInt32LE(data.length, 22);
    local.writeUInt16LE(nameBuffer.length, 26);
    local.writeUInt16LE(0, 28);
    localParts.push(local, nameBuffer, compressed);

    const central = Buffer.alloc(46);
    central.writeUInt32LE(0x02014b50, 0);
    central.writeUInt16LE(20, 4);
    central.writeUInt16LE(20, 6);
    central.writeUInt16LE(0x0800, 8);
    central.writeUInt16LE(8, 10);
    central.writeUInt16LE(stamp.time, 12);
    central.writeUInt16LE(stamp.date, 14);
    central.writeUInt32LE(crc, 16);
    central.writeUInt32LE(compressed.length, 20);
    central.writeUInt32LE(data.length, 24);
    central.writeUInt16LE(nameBuffer.length, 28);
    central.writeUInt16LE(0, 30);
    central.writeUInt16LE(0, 32);
    central.writeUInt16LE(0, 34);
    central.writeUInt16LE(0, 36);
    central.writeUInt32LE(0, 38);
    central.writeUInt32LE(offset, 42);
    centralParts.push(central, nameBuffer);

    offset += local.length + nameBuffer.length + compressed.length;
  }

  const centralDirectory = Buffer.concat(centralParts);
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);
  end.writeUInt16LE(0, 4);
  end.writeUInt16LE(0, 6);
  end.writeUInt16LE(files.length, 8);
  end.writeUInt16LE(files.length, 10);
  end.writeUInt32LE(centralDirectory.length, 12);
  end.writeUInt32LE(offset, 16);
  end.writeUInt16LE(0, 20);

  fs.writeFileSync(destination, Buffer.concat([...localParts, centralDirectory, end]));
}

const fields = listFiles(scriptsRoot).flatMap(parseFields);

const parameterHeaders = ["模块", "功能分类", "组件/脚本", "参数名", "中文标注", "类型", "默认值", "作用", "备注", "来源文件", "行号"];
const parameterRows = fields.map((f) => [
  f.module,
  f.feature,
  f.component,
  f.parameter,
  f.chineseName,
  f.type,
  f.defaultValue,
  f.role,
  f.notes,
  f.file,
  String(f.line),
]);

const moduleCounts = new Map();
for (const f of fields) {
  const key = `${f.module}\u0000${f.feature}`;
  if (!moduleCounts.has(key)) moduleCounts.set(key, { module: f.module, feature: f.feature, count: 0, components: new Set() });
  const item = moduleCounts.get(key);
  item.count += 1;
  item.components.add(f.component);
}

const overviewHeaders = ["模块", "功能分类", "参数数量", "涉及组件"];
const overviewRows = [...moduleCounts.values()]
  .sort((a, b) => a.module.localeCompare(b.module, "zh-Hans-CN") || a.feature.localeCompare(b.feature, "zh-Hans-CN"))
  .map((item) => [item.module, item.feature, String(item.count), [...item.components].sort().join("、")]);

const docHeaders = ["项目", "内容"];
const docRows = [
  ["生成时间", new Date().toLocaleString("zh-CN", { hour12: false })],
  ["扫描范围", "Assets/Scripts 下所有 C# 脚本中的 public Inspector 参数"],
  ["表格字段", "模块、功能分类、组件、参数名、中文标注、类型、默认值、作用、备注、来源文件、行号"],
  ["中文说明来源", "优先使用代码中的 InspectorName 和 Tooltip；缺失时根据变量名和类型生成中文名与基础用途说明"],
  ["维护方式", "新增或修改脚本参数后，可重新运行 .agents/generate_parameter_excel.mjs 生成新版表格"],
];

const files = [
  ["[Content_Types].xml", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
  <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
  <Override PartName="/xl/worksheets/sheet3.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
</Types>`],
  ["_rels/.rels", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>`],
  ["xl/_rels/workbook.xml.rels", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet3.xml"/>
  <Relationship Id="rId4" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
</Relationships>`],
  ["xl/workbook.xml", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets>
    <sheet name="参数总表" sheetId="1" r:id="rId1"/>
    <sheet name="模块总览" sheetId="2" r:id="rId2"/>
    <sheet name="说明" sheetId="3" r:id="rId3"/>
  </sheets>
</workbook>`],
  ["xl/styles.xml", stylesXml()],
  ["xl/worksheets/sheet1.xml", sheetXml(parameterHeaders, parameterRows, [18, 22, 30, 32, 32, 18, 28, 72, 24, 42, 8])],
  ["xl/worksheets/sheet2.xml", sheetXml(overviewHeaders, overviewRows, [18, 28, 12, 60])],
  ["xl/worksheets/sheet3.xml", sheetXml(docHeaders, docRows, [18, 90], false)],
];

createZip(files, outputPath);

console.log(`Generated ${outputPath}`);
console.log(`Parameters: ${fields.length}`);
console.log(`Modules/features: ${overviewRows.length}`);

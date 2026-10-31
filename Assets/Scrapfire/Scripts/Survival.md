# Survival prototype

本次范围：独立玩家余额、体积区域与付费解锁、配置驱动的波次和难度、怪物生成、通用敌人参数、玩家属性与倒地。敌人行为 AI 已加入，见 EnemyAI.md；网络传输尚未加入；背包已更新为独立组件及通用格，见 Inventory.md。

## 在 SampleScene 中试玩

打开 `Assets/Scrapfire/Scenes/SampleScene.unity` 后进入 Play。左上角显示余额、生命、波次、待生成与存活敌人，靠近锁定区域时显示 F 购买提示。初始余额 500，击杀赏金 100。建筑入口价格 500，二楼及楼梯价格 750。区域对整局开放，但仅扣购买者的钱。

玩家出生在建筑南侧室外 `(0, 1, -8)`。BuildingEntranceRegion 覆盖整栋建筑，购买后可进入一楼；UpperFloorAndStairsRegion 由二楼高空体积与楼梯体积组成，购买前一楼仍可活动，但无法进入楼梯或二楼。两区重叠处需全部解锁。旧的东西锁区已删除。锁定区域由独立的 RegionBoundaryVisual 显示橙色侧边轮廓、淡网格及半透明地面填充：距本地玩家 6 米内渐显，1.2 米内最清楚，解锁后立即隐藏；碰撞仍由 MapRegion 管理。组合体积只显示外侧，不显示盒子交叠处的内部隔墙。侧边带有细轮廓边带，避免与正交相机视线平行时完全不可见；Contour Width 控制边带宽度（默认 0.12 米）。

示例波次从 Play 后约 5 秒开始，敌人数为 5 / 8 / 12，最多同时存活 6 个；清场后休整 5 秒进入下一波。默认三波后 Completed；可在配置中启用 Repeat Last Wave。敌人目前保持原地，按本次范围不实现追踪、游荡、寻路、聚怪或破门决策，需要玩家移动寻找并击杀。

玩家默认生命 100、基础移速 6、冲刺倍率 1.6；受伤后延迟 5 秒，再每秒回复 5 生命。生命归零进入 Downed，不能移动、开火、购买或自行回血。已有 `Health.Revive` 接口保持可用（`ReviveAbility` 仍是预留空壳）；全员无法行动时波次进入 Failed。本次没有复活交互 UI 或重新开始菜单，重新进入 Play 可开始新会话。

## 配置入口

| 对象 / 资产 | 可编辑内容 |
| --- | --- |
| Capsule / Balance | Starting Amount，每个玩家各自保存运行时余额 |
| Capsule / CharacterAttributes | Maximum Health、Movement Speed、Sprint Multiplier、Regeneration Delay、Regeneration Per Second |
| Capsule / PlayerInteractor | Reach、检测层、是否读取本地 F 键 |
| SurvivalSession / BuildingEntranceRegion、UpperFloorAndStairsRegion / MapRegion | Region Name、Unlock Cost、Initially Unlocked、Volumes 数组，每个元素有本地 Center 和 Size |
| SurvivalSession / SessionPlayers | 当前会话玩家列表，后续联机适配通过 Register / Unregister 接入 |
| SurvivalSession / WaveDirector | Difficulty、Sequence、Difficulty Settings、Auto Start |
| Settings/Survival/Difficulty.asset | 玩家数量曲线，Easy / Hard / Nightmare 各自倍率 |
| Settings/Survival/Waves.asset | 每波准备时间、同时存活上限、生命/攻击倍率、各敌人组的预制体、数量和生成间隔；可选末波循环增长 |
| Settings/Survival/BasicEnemy.asset | 生命、移动速度、攻击力、攻击间隔、攻击距离、破坏速度 |
| Prefabs/SurvivalEnemy.prefab / EnemyBounty | 击杀奖励金额 |
| SurvivalSession / EntitySpawner | Points、Regions（可选额外区域）、Clearance Radius、Obstacle Layers |
| 各 Spawn 对象 / EnemySpawnPoint | 位置、旋转、Required Region（可选附加锁区条件）；所在锁定区域自动按几何位置识别 |
| 区域 / RegionBoundaryVisual | Boundary Material、Reveal Distance、Full Reveal Distance、Contour Width、Area Opacity、Surface Height |

路径均以 `Assets/Scrapfire/` 为根。配置资产创建后会保留编辑值，再次调用场景配置菜单不会重写它们。`Scrapfire > Configure Survival Prototype` 只用于给 SampleScene 补齐样例，不是运行时依赖。

新增区域：新建空对象并添加 MapRegion 和 RegionUnlockInteraction，编辑 Volumes 数组。选中区域后，Inspector 的 Edit volumes in Scene 默认打开；Selected volume 选择数组索引（从 0 开始），Scene 中拖动中心箭头移动该体积，拖动六个面的手柄调整大小。Frame selected volume 可聚焦该体积。关闭 Edit volumes in Scene 后，使用普通 Transform 手柄移动、旋转、缩放整个区域。支持 Undo/Redo 和预制体覆盖记录。多个体积的并集表示区域，锁定时整个体积禁止进入；不要求另行绘制门口碰撞体。让玩家初始位置处于已开放空间，避免生成在锁区内部。立方体的高度要覆盖角色可以经过的高度。场景外墙由关卡原有碰撞体承担。编辑器体积修改会通知几何变化，Play 中同步更新碰撞和轮廓；脚本直接改数组后调用 NotifyGeometryChanged。日常关卡编辑仍建议在进入 Play 前进行。新区域可添加 RegionBoundaryVisual 并绑定 LockedRegionBoundary 材质；每个本地玩家的相机添加 RegionViewObserver，并将 Target 绑定到该玩家。CharacterPlayer.BindCamera 会自动完成绑定，远端角色不要绑定本地相机。菜单 Scrapfire > Configure Region Presentation 可批量补齐显示组件，不改已有体积和显示参数。

Scene 视图开启 Gizmos 时，每个 EnemySpawnPoint 都显示蓝色小圆形标记，不必先选中；选中后另显示朝向线。刷怪严格使用关卡作者预设的 EnemySpawnPoint 对象，不生成随机坐标；将对象放在可站立的位置，并加入 EntitySpawner.Points，坐标是敌人根节点的位置。每次生成自动检查场景中所有 MapRegion：任何包含该点的锁定区域都会排除它，不需要手动绑定 RequiredRegion；若配置了 RequiredRegion，则作为额外限制。重叠区域必须全部开放。Obstacle Layers 是额外的障碍排除层，默认不启用，不能包含地面层；运行时还会避免在同一刷怪点与已跟踪敌人重叠。当前示例点启用 Require NavMesh，生成前验证同一楼层存在导航面；仍未加入玩家距离筛选。每波开始清零该波成功生成次数，优先选择可用且成功次数最少的点，同次数时轮流选择。堵塞、停用或锁定的点不消耗配额；解锁/移开障碍后自动重新参与并尽量补齐本波次数差。若全部不可用，保留波次剩余数量等待，不使用随机位置兜底。EntitySpawner 由原 EnemySpawner 重命名，脚本 GUID 保持不变以保留场景引用。

## 难度规则

每波实际开始时读取玩家数量和选择的难度：

```
玩家数量倍率 = Difficulty.asset 中 Player Count Multiplier 曲线的取值
设定难度倍率 = Easy / Hard / Nightmare 对应配置值
合成倍率 = 玩家数量倍率 × 设定难度倍率
每组数量 = ceil(配置数量 × 合成倍率)
生成间隔 = 配置间隔 ÷ 合成倍率
敌人生命 = 敌人基础生命 × 波次生命倍率 × 合成倍率
敌人攻击 = 敌人基础攻击 × 波次攻击倍率 × 合成倍率
```

速度、攻击间隔、破坏速度与同时存活上限不随合成倍率变化。人数变化在下一波生效，倒地玩家仍计入人数，死亡或停用/注销的玩家不计入。这样中途断线不会改写已经生成的怪物或本波剩余数量。末波循环开启后，另乘配置中的 Repeat Multiplier 的循环次数幂。

## 模块边界

- `Balance` 只管理整数余额和 Changed 事件，不知道区域、敌人和波次。
- `MapRegion` 仅管理几何体积与锁定状态，不引用经济、玩家或波次。`RegionUnlockInteraction` 是独立的购买适配器，负责验证距离、玩家状态与扣款。
- `PlayerInteractor` 管理距离与本地输入，依赖 `IPlayerInteractable`，可复用到之后的商店。
- `Health` 是当前生命和 Alive/Downed/Dead 状态的唯一来源；`CharacterAttributes` 管理角色配置和回复计时。移动输入读取属性，不保存另一份基础速度。
- `Enemy` 提供参数与带距离/冷却的 `TryAttack(Health)` 入口，不选择攻击目标，也不自动移动。Destruction Speed=0 表示不能破坏，后续障碍破坏模块可以读取此值；本次没有障碍耐久或破坏执行器。
- `EnemyBounty` 是受伤归属到钱包的适配器；只给最后一击的玩家奖励，不平分。环境击杀不发放奖励。Health / Enemy 本身不依赖经济。
- `EntitySpawner` 负责预设点筛选、每波成功次数配额与存活跟踪，通过 Removed 事件释放计数；死亡、停用、销毁均释放，已生成实例由生成器负责清理。WaveDirector 在真正开始一波时调用 BeginWave，生成器不依赖波次类型。
- `RegionBoundaryVisual` 分别读取各 RegionViewObserver 的 Target，使用带明确 Camera 参数的绘制调用和独立参数块显示边界与半透明地面区域。A 的接近状态不会启用一个全局可见 Renderer；同进程分屏和不同客户端均由各自本地相机决定显示。RegionBoundaryMesh 为盒子并集生成外侧和无重叠地面填充，不改变区域形状定义。
- `WaveDirector` 只调度配置并查询生成器/玩家列表；不处理钱、输入或角色移动。敌人全部生成且存活数为零才清场；无可用点时保留待生成数并显示等待原因。
- `PrototypeSessionHUD` 为可移除的原型调试反馈，游戏规则不依赖它。队友可订阅 Balance.Changed、Health.HealthChanged、WaveDirector.StateChanged、MapRegion.Unlocked 接入正式 UI。

## 联机衔接

Proposal 中的 2–4 人 Host/Client 模式仍是后续范围。当前项目没有新增 Netcode/Mirror 依赖；多名 CharacterPlayer 可以在同一进程中拥有独立状态，但这些状态尚未跨进程同步。后续由主机执行购买、伤害和生成，客户端发出请求并接收结果；不要让每个客户端各自运行 AutoStart 生成同一波。远端玩家禁用本地 PlayerInputDriver 与 PlayerInteractor.ReadLocalInput，玩家加入/退出时更新 SessionPlayers。

Balance 在 Awake 初始化一次，停用再启用不会重置；显式 ResetForSession 用于新会话。BeginSession 只启动/重启波次，不重置玩家生命、钱包或区域；完整新局当前通过重新载入场景实现。

## 验证

PlayMode 的 SurvivalSystemsTests 覆盖玩家余额隔离、无效交易、区域并集与扣款、距离和倒地限制、实体碰撞、角色属性/延迟回血、难度乘数及波次快照、刷怪点解锁、死亡/停用/销毁计数、击杀奖励、存活上限、清场、刷怪阻塞与全员倒地。已有武器、背包、对象池与保存场景测试一起回归。

新增 RegionAndSpawnTests 验证预设位置、均衡配额、自动几何锁区、重叠区域、移动点后重判、堵塞/停用恢复、无随机兜底、组合边界去内面，以及靠近显示、解锁隐藏和正交视角侧面的可见边带。


## 本地警告 建筑与碰撞更新

锁区现在同时显示轮廓和地面半透明填充，填充只覆盖对应玩家周围 Reveal Distance 半径内的锁定区域。Area Opacity 默认 0.22，Surface Height 默认本地高度 0.06，避免与地面重叠闪烁。解锁后两者一起消失。区域没有全局 Observer 字段；每个本地相机独立挂 RegionViewObserver，Target 仅指向该相机所属玩家。多人时共享区域解锁状态，不共享警告透明度或“是否正在警告”的显示状态。PrototypeSessionHUD 也限制在所属相机的视口内，只展示其绑定玩家的交互提示。此更新没有新增网络框架或网络同步。

RegionBarrier 专用碰撞层只与 Player、Enemies 层碰撞。角色在初始化时进入各自的角色层；子弹 HitscanBullet 额外跳过带 RegionMovementBarrier 标记的碰撞体，避免任何武器 HitLayers 配置意外让空气墙挡枪。实体建筑墙没有这个标记，仍然阻挡射击；Projectile 层的实体抛射物也不与锁区屏障碰撞。编辑器菜单 Scrapfire > Configure Prototype Environment 配置这些项目碰撞规则。

PrototypeBuilding 靠场地北侧，范围 x=-24..16、z=-1..24，占地 40×25=1000 平方米，为 50×50 场地的 40%。有地板、四面墙、四个角柱和边梁，主入口与侧入口保持开放；窗户是实际无碰撞洞口，窗台仍阻止角色直接通过。建筑现已升级两层，屋顶在室外完整显示，在室内按各相机所属玩家的楼层自动裁切。地板比原场地高 0.03 米以避免重叠闪烁，锁区填充位于所在楼层上方。结构由普通 Cube 对象组成，可在 Hierarchy 中独立编辑，不依赖运行时搭建脚本。

主相机使用 55° 视野角的透视投影，跟随偏移更新为 (14.4,18,-18)，俯视角约 38°，在保持角度的同时把距离拉近约 10%。摄像机绑定与射击瞄准继续使用原有流程。

LocalRegionPresentationTests 验证组合区域填充不重复叠加、两个相机同时看同一世界位置时只有近处玩家收到真实渲染（交换玩家位置后结果反转），以及锁区阻挡两类角色、子弹穿过锁区后仍被实体墙阻挡。


## 两层建筑与相机裁切

PrototypeBuilding 现在包含 GroundFloor（一楼及楼梯）、UpperFloor（二楼墙体、楼板和平台）、Roof（完整屋顶）三个独立分组，原占地和一楼门窗保留。二楼两个原门口补了窗台，避免直接从高处走出；屋顶是实体楼板。层高 3.8 米，一楼地面约 y=0.03，二楼地面约 y=3.83。楼梯位于 x=-8、z=6..14，16 级台阶；二楼楼板留有真实楼梯洞口，末端平台连接到 z=15 以北的楼面。

BuildingVisibility 根据各 Camera 上 RegionViewObserver.Target 的脚部位置判断建筑内外与当前楼层，实际渲染前只对该相机调整更高建筑分组的透明度，渲染结束恢复原材质、显示与阴影状态。Fade Duration 默认 0.5 秒，进入和离开楼层时双向线性渐变；中途折返从当前透明度继续。每台相机独立保存各楼层透明度和临时材质，不修改共享材质资源。过渡使用 URP 透明材质，完全显现时恢复原不透明材质；完全隐藏时跳过渲染。室外完整显示，一楼隐藏二楼与屋顶，二楼仅隐藏屋顶。多个玩家不会互相改变楼层显示；没有绑定玩家的 Scene/预览相机显示完整建筑。没有停用 GameObject 或 Collider，隐藏楼板仍然支撑角色和参与真实碰撞。

Inspector 中 Footprint Center/Size 定义本地平面范围，Ground Height、Storey Height、Storey Count 定义高度。Visual Storeys 为独立的同级视觉分组，编号从 0 起；屋顶编号等于总层数。不要把整个建筑根对象同时作为某一层 Root，否则会把其它层一起包含进去。运行时更换视觉分组后调用 RefreshRenderers。

相机在 TopDownCameraFollow.Offset 调整 XYZ 偏移，当前 (14.4,18,-18)。锁区地面填充随当前所在建筑楼层升高，二楼仍可看到自己附近的锁区提示。

BuildingVisibilityTests 覆盖局部坐标与楼层识别、两台相机同时观察时分别显示一楼/二楼/完整屋顶、显示状态与阴影恢复、碰撞保留，以及保存场景中的实际楼梯通行。


### 弹药箱

建筑南门外的 AmmoCrate 位于 `(3, 0.5, -3)`，使用简单箱体和盖子。AmmoRefillStation 接入既有 F 交互；每次花 $50 将当前远程武器对应弹药的备弹补至 60，而不是额外赠送 60。补给进入购买者的通用背包格；弹匣不变，其它玩家不受影响。备弹已有 60 或更多时不扣款、不削减；没有合适武器、金额不足、距离过远、倒地或背包装不下完整补给时拒绝交易，交易失败退款。Inspector 中 Cost 和 Target Reserve 可配置。


### 敌人 AI 与导航更新

现在普通敌人会游荡、发现玩家后追击并广播，邻近敌人会加入追击，近身后攻击。Seek/Pursue 分别可配置；丢失视野后只使用最后已知位置，随后搜索并返回默认行为。两层建筑已有烘焙导航，动态锁区通过 RegionNavigationGate 切割导航。修改静态地图后使用 Scrapfire > Rebake Survival Navigation。详细职责、参数、验证和 Loft3D 素材来源见 [EnemyAI.md](EnemyAI.md)。

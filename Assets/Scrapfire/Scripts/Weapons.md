# 枪械原型

## 职责

- `Weapon`：角色绑定、动作状态、计时和攻击事件。
- `WeaponData`：共享枪械参数。
- `WeaponAmmo`：弹药的库存物品引用、消费数量、弹丸数量、伤害与命中行为。
- `ItemStack.WeaponState`：每把枪独有的弹匣余量，复制、切换和转移时保留。
- `WeaponAmmoSupply`：访问弹匣或背包资源，执行消费和装填。
- `RangedWeapon`：协调扳机、预热、换弹和每次射击。
- `WeaponSpread`：根据移动、奔跑、开镜计算散布。
- `HitscanBullet`：射线命中，跳过持枪者，遇到第一个其它碰撞体停止。
- `WeaponPresentation`：枪口粒子与池化命中特效。
- `ShotTracer` / `BulletTrace`：每颗弹丸独立的池化视觉飞行和短尾迹。
- `PlayerAim`：瞄准状态与移动速度因子。
- `WeaponReloadAudio`：监听逐步装填事件和取消事件，播放装弹声。
- `WeaponAudio` / `PooledAudioVoice`：每次实际开火的声音和池化音轨。
- `SharedObjectPool` / `PooledObject`：Scene 持有的按预制体分类的共享池和回收入口。

这里沿用 `Weapon` 作为 MonoBehaviour 运行组件，所以配置资产命名为 `WeaponData`。
原来的 `Item.WeaponSettings` 已移除，`Item.Weapon` 直接引用 `WeaponData`。
伤害属于弹药；枪械组件不再重复保存枪械参数或弹药伤害。

## 参数语义

| WeaponData 字段 | 语义 |
|---|---|
| RecoilValue | 未开镜的三维圆锥散布半角，单位为度；不是摄像机抖动或累积后坐力 |
| RecoilMultiplierOnMove | 实际移动时的倍率 |
| RecoilMultiplierOnSprint | 实际奔跑时的倍率，与移动倍率互斥 |
| RecoilValueOnAim | 按住右键时使用的散布基数，之后仍应用移动/奔跑倍率 |
| FireOnSprint | false 时奔跑中不能开火，开始奔跑会取消尚未完成的预热 |
| MagazineCapacity | 弹匣能保存的弹药单位数 |
| MagazineUsed | false 时开火直接消费背包弹药，不能换弹 |
| MagazineReloadInterval | 整匣换弹的时间，或逐发装填每一发的时间 |
| MagazineReloadMode | Magazine 整匣；SingleRound 逐发 |
| Ammo | 默认弹药行为资产 |
| TimeBetweenUse | 两次射击的最短间隔；栓动枪包含拉栓等待时间 |
| TimeDelayBeforeUse | 首发预热/充能延迟；自动枪持续按住只预热一次，松开后重置 |
| Mode | SemiAutomatic、Automatic、BoltAction |
| Range | 射线最大射程 |

移动判断来自 CharacterController 的实际水平位移，撞墙时不会仅因按着方向键获得移动倍率。
按住右键由 `PlayerAim` 保存瞄准状态：禁用疾跑，移速乘以 0.4，使用开镜散布基数。
移动中的瞄准仍应用移动散布倍率。散布在圆锥内采样，包含水平和垂直偏移。
狙击枪的局部放大效果记录在 [TODO.md](TODO.md)，后续实现。

`UseTrigger(held, pressed, direction)` 负责按住/新按下语义。半自动与栓动每次按下最多
发射一次，冷却中按下不排队；全自动在按住时连续请求射击。
`TryAttack(direction)` 是单次攻击请求，也可供 AI 使用；返回 true 可表示已经开始预热，
实际射击以 Attacked 事件为准。预热不提前扣弹，松开、切枪、倒地、禁用或禁止开火的奔跑
会取消预热。状态使用普通 WeaponState 枚举。

逐发装填在每一发完成时从背包扣除一单位弹药；取消只保留已经装入的部分。
已有弹药时可开火打断逐发装填。整匣换弹在完成时消费实际装入量，取消不会扣弹。
新获得的枪默认满弹匣，无弹匣枪的弹匣值为零；初始备弹由出生/拾取/商店规则提供。

## 弹药行为

`WeaponAmmo.Item` 是背包中的弹药 Item 身份，仍使用分类格子和 ItemStack 存储。
`AmmoConsumptionPerShoot` 每次射击只消费一次；`BulletPerShoot` 决定本次发射的弹丸数；
每颗弹丸使用 `BulletDamage`，并乘以角色当前伤害倍率。

`OnBulletHit(DamageSource)` 是虚函数，每颗命中 Health 的弹丸调用一次；默认实现造成伤害，
并返回实际伤害。墙壁照常阻挡并播放命中特效，但没有 Health 时不调用伤害处理。

```csharp
public override float OnBulletHit(DamageSource source)
{
    float applied = base.OnBulletHit(source);
    if (applied > 0f)
    {
        // 在这里接入受击目标的减速、流血或燃烧组件。
    }
    return applied;
}
```

弹药资产是共享配置，不应存储目标、燃烧层数或流血计时器。持续效果的运行时状态属于
受击角色的效果组件。本轮只实现标准弹药和命中扩展入口，尚未实现减速、流血、燃烧传染、
护甲伤害规则或游玩中的弹种切换；这些能力依赖后续角色状态效果/护甲系统。

## SampleScene

| 数字键 | 枪械 | 开火 | 弹匣 | 每次弹丸 | 装填 |
|---|---|---|---|---|---|
| 1 | 步枪 | 全自动 | 12 | 1 | 整匣 |
| 2 | 霰弹枪 | 半自动 | 6 | 8 | 逐发 |
| 3 | 手枪 | 半自动 | 12 | 1 | 整匣 |
| 4 | 狙击枪 | 栓动 | 5 | 1 | 整匣 |

SampleScene 的 PrototypeLoadout 为试玩解锁全部武器格，提供四把枪及对应备弹。
常规 InventoryRules 仍初始化两格可用武器、两格锁定武器，商店系统尚未实现。
右键按住开镜精度，左键射击，R 换弹；按 Shift 移动时这四把枪均禁止开火。

模型、材质、枪口粒子和命中粒子来自 TopDownEngine Loft3D。
新预制体位于 Assets/Scrapfire/Prefabs，运行逻辑替换为 Scrapfire 组件。
材质副本位于 Assets/Scrapfire/Materials，并适配项目的 URP；原始 TopDownEngine 资产未修改。
栓动通过 Cycling 状态和射击间隔实现，尚未添加拉栓动画。

## 共享对象池、弹道和枪声

SampleScene 中有 SharedObjectPool。ForScene(scene) 会取得该 Scene 的共享服务；没有服务的
测试/新场景会自动创建。每个预制体拥有独立桶，所有枪共享同一 BulletTrace 和 AudioVoice
预制体。切枪不会销毁已借出的视觉弹丸、命中特效或枪声。Scene 卸载会销毁整个池。

Prewarm(prefab,count) 是该桶的总目标数量，多把枪重复请求不会重复叠加预热数量。
默认预热 32 条弹道、24 个命中特效和 16 条音轨；并发超过容量时会创建更多实例，之后复用。
池不会提前复用仍活动的对象，也不会因为池耗尽而丢掉弹道。重复 Release 不会重复入池。
池化对象不能直接 Destroy 或只 SetActive(false)，应调用 PooledObject.Release()。

BulletTrace 只绘制视觉飞行，没有 Collider、Rigidbody 或伤害处理。Hitscan 仍在开火时判定伤害。
每颗弹丸拥有独立的移动短尾迹，不再覆盖上一发，也不再将整条射线只显示 0.06 秒。
预制体可调整 VisualSpeed、MinimumTravelTime（0.12s）、MaximumTravelTime（0.35s）、
TailLength（2.5）、FadeTime（0.08s）以及 LineRenderer 的颜色/宽度。短距离也有最低可见时间。
视觉命中时间和即时伤害时间不同；这属于表现辅助，不是实体 Projectile 模拟。

命中特效使用 PooledEffect，播放时清除旧粒子，寿命结束后归还，切枪不会截断。
武器自身的枪口粒子保留在持枪模型上。

WeaponAudio 监听 Attacked，每次实际发射播放一次，霰弹枪八颗弹丸只播放一次枪声。
声音绑定在表现组件上，不加入 WeaponData 或 WeaponAmmo。空弹匣、冷却期间或预热阶段
没有实际发射时不会播放开火声。独立音轨允许连续射击重叠，不会被下一枪覆盖。
归还音轨会 Stop 并清除 AudioClip，重新借出后绑定新的声音。

步枪/霰弹枪采用 LoftAssaultRifle.wav / LoftShotgun.wav；手枪/狙击枪采用 LoftGun1.wav /
LoftGun3.wav。音轨采用部分 3D 空间化，保留距离感同时适配当前俯视摄像机位置。
音量在 WeaponAudio.FireVolume 调整；空间衰减在 WeaponAudioVoice.prefab 的 AudioSource 调整。
四把枪的 WeaponReloadAudio 已绑定整匣装弹 LoftReload.wav；霰弹枪每装入一发播放
LoftMechanism.wav。每把枪用独立 AudioSource，取消装弹、开火打断逐发装填、切枪
或完成装填时停止声音；音量在 WeaponReloadAudio.Volume 调整。
当前未增加拉栓、空击声音或混音器配置。


## Gameplay gunshot noise

WeaponData.GunshotRadius configures how far an actual discharge can attract AI (metres):
pistol 12, rifle 22, shotgun 28, sniper 35. A value of zero is silent. This is independent of
AudioSource volume. Each discharge publishes one anonymous WorldNoise event, including
shotguns with multiple pellets. Empty triggers, reloads and cancelled warmups are silent.
A hit still carries the existing Projectile damage attribution to its owning player; AI can
react to a confirmed hit even when a weapon is silent. See EnemyAI.md for hearing/search rules.

# 玩家背包逻辑

本次只实现数据与玩法逻辑，没有背包 UI、拾取模型或网络同步。

## 默认布局

每个 CharacterPlayer 自动拥有自己的 Inventory 组件，组件内部独立创建 InventoryContainer、Slot、ItemStack 和武器弹匣状态。仅共享 Item / WeaponData 等配置资产。

- 2 个武器槽：只放武器，按键 1/2 或滚轮切换。
- 1 个护甲槽：只放护甲，一次装备一件。
- 12 个通用背包格：Ammo、Medical、Supplies、Consumable、Attachment 共同占用；没有独立弹药格或医疗格。
- 通用格可显式扩容至 24 格；不能缩容或借扩容增加武器/护甲槽。

Inventory 的 Initial Backpack Slots 配置新会话的起始容量。运行中用 TryExpandBackpack 扩容；禁用/重新启用组件不会清空或重新发放物品。原有武器槽 3/4 和演示自动扩格逻辑已移除。

每种物品的 StackSize 决定一格上限。当前步枪弹药每格 60，医疗包每格 2。武器、护甲和有耐久度的物品强制一格一件。数量满后使用其它空通用格；容量不足时保留剩余物品，绝不会自动增加容量或覆盖旧物品。

## 分工

| 类型 | 职责 |
| --- | --- |
| Inventory | 玩家 MonoBehaviour 组件、初始容量、常用操作入口 |
| InventoryContainer | 纯存储逻辑、容量检查、事务、变更事件 |
| InventoryContainer.Slot | Kind、Locked、Content、接收规则 |
| Item | 共享类型配置、堆叠上限、模型、使用策略 |
| ItemStack | 单件/单堆运行时数量、耐久、武器弹匣与归属 |
| CharacterPlayer | 当前武器选择、持枪表现与切换 |
| WeaponAmmoSupply | 从当前玩家背包消耗备弹 |
| ItemUser / MedicalItemUser | 使用策略、扣除消耗品与医疗推荐 |
| PrototypeLoadout | 一次性的场景初始物资，独立于容量规则 |

Slot.Kind 是 Weapon / Armor / Backpack；Item.ItemCategory 是物品用途，两者不再混用。护甲槽目前管理装备物品和实例数据，不改变现有 Health 的伤害计算；护甲吸收伤害等效果由后续战斗模块接入。

## 操作接口

```csharp
var inventory = player.GetComponent<Scrapfire.Characters.Inventory.Inventory>();
var slots = inventory.Slots;                  // 只读槽列表，槽引用稳定
var armor = inventory.ArmorSlot.Content;
var remainder = inventory.Add(new ItemStack(ammoItem, 100));
bool accepted = inventory.TryAddAll(new ItemStack(medkitItem, 2));
inventory.TryMove(3, 4, 10);                   // 移动/拆分/合并，最多移动请求数量
inventory.TrySwap(3, 4);                       // 不同类型物品在通用格中交换
ItemStack dropped = inventory.Drop(3, 5);      // 返回脱离背包的实例，不生成世界模型
inventory.TryEquip(0, incomingWeapon, out ItemStack displaced);
inventory.TryExpandBackpack(16);
inventory.TryTransferTo(otherInventory, 4, 10);
```

默认索引：武器 0、1；护甲 2；通用格 3 起。界面代码优先使用 WeaponSlots / ArmorSlot / BackpackSlots 查询；GetItems(category) 在 Contents 上查询已有物品，不代表专用分类槽。CharacterPlayer.CharacterInventory 保留为纯存储引用，和 Inventory.Contents 指向同一个对象。

Add 会先填同物品的未满堆，再填空格，返回未放入部分；它会消耗传入的脱离背包 ItemStack。TryAddAll 则全量检查，失败时不改变物品或背包。成功装备新武器/护甲会把被替换物品作为 displaced 返回，调用方负责保存或生成掉落物，不能忽略它。

TryMove 在同一背包内移动、拆分或合并，受目标格接收规则和堆叠上限约束。TrySwap 先验证双方能否放入，失败不交换。武器不能塞进通用格，护甲不能塞进武器槽。

TryTransferTo 是明确的玩家间交接：目标容量不足、源数量不足或槽锁定时，两边都不改变；成功时先完成双方数据更新再触发通知。转移物品保留弹匣和耐久。不能直接把 A.Slots[i].Content 传给 B.Add，也不能对已经归属背包的 ItemStack 调用 Reset/Split；这些操作会拒绝，以免绕过数量和归属检查。只读快照用 Copy。

Changed 在一次逻辑操作提交后触发一次，未来 UI 可订阅并重新查询槽列表。换弹和医疗消耗仍走统一存储入口。Locked 为预留玩法接口，默认布局没有锁定格；扩容只会追加通用格，不改变原槽索引。

## 初始武器与有限弹药

Inventory 自身空包初始化，不发放武器或弹药。SampleScene 的 PrototypeLoadout 只在 Start 发放一把步枪、60 发备弹和两个医疗包；弹匣初始装弹量来自武器配置，当前步枪为 12 发，总可用弹药为 72 发。初始第二武器槽为空，后续拾取才会占用。

射击消耗弹匣；换弹完成时从该玩家通用背包取备弹。弹匣和备弹都耗尽后不能射击或换弹。换枪、移动武器、丢弃再拾取和玩家间转移都保留弹匣数量，不会补满。后续世界拾取应保存 Drop 返回的 ItemStack，不能每次拾取重新用 Item 定义生成一把满弹武器。

UseBehaviour 定义消耗品效果和消耗数量；ItemUser 验证角色状态、槽归属及数量后执行。MedicalItemUser 从通用格推荐可用的治疗行为，因此 Consumable 类治疗物品也可使用，不再要求专用 Medical 槽。满血、倒地、无效使用和效果失败不会扣物品。

## 验证

InventorySystemTests 覆盖独立组件/状态、共享格竞争、堆叠溢出、拆分/移动/合并/交换、装备限制与替换、归属保护、原子转移、扩容边界、通用消耗品和弹药耗尽。已有武器、医疗、场景移动、楼层和刷怪测试一起回归；保存场景测试中第二把武器改为显式拾取，验证开局不再自动获得多把枪。

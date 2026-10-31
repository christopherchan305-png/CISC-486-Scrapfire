using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scrapfire.Characters.Inventory
{
    /// <summary>One player's inventory component. Shared item assets never store player state.</summary>
    [DefaultExecutionOrder(-200), DisallowMultipleComponent]
    [AddComponentMenu("Scrapfire/Player/Inventory")]
    public sealed class Inventory : MonoBehaviour
    {
        [SerializeField, Range(1, InventoryRules.MaximumBackpackSlots)] private int _initialBackpackSlots = InventoryRules.DefaultBackpackSlots;
        private InventoryContainer _contents;
        public InventoryContainer Contents => _contents ??= InventoryRules.CreateInventory(_initialBackpackSlots);
        public IReadOnlyList<InventoryContainer.Slot> Slots => Contents.Slots;
        public IEnumerable<InventoryContainer.Slot> WeaponSlots => Contents.GetSlots(InventorySlotKind.Weapon);
        public IEnumerable<InventoryContainer.Slot> BackpackSlots => Contents.GetSlots(InventorySlotKind.Backpack);
        public InventoryContainer.Slot ArmorSlot => Contents.GetSlots(InventorySlotKind.Armor).First();
        public int BackpackCapacity => Contents.BackpackCapacity;
        public event Action Changed { add => Contents.Changed += value; remove => Contents.Changed -= value; }
        private void Awake() { _ = Contents; }
        private void OnValidate() => _initialBackpackSlots = Mathf.Clamp(_initialBackpackSlots, 1, InventoryRules.MaximumBackpackSlots);
        public ItemStack Add(ItemStack stack) => Contents.Add(stack);
        public bool TryAddAll(ItemStack stack) => Contents.TryAddAll(stack);
        public ItemStack Remove(int index, int amount = int.MaxValue) => Contents.Remove(index, amount);
        public ItemStack Drop(int index, int amount = int.MaxValue) => Contents.Drop(index, amount);
        public int Count(Item item) => Contents.Count(item);
        public bool TryMove(int from, int to, int amount = int.MaxValue) => Contents.TryMove(from, to, amount);
        public bool TrySwap(int first, int second) => Contents.TrySwap(first, second);
        public bool TryEquip(int index, ItemStack item, out ItemStack displaced) => Contents.TryEquip(index, item, out displaced);
        public bool TryExpandBackpack(int capacity) => Contents.TryExpandBackpack(capacity);
        public bool TryTransferTo(Inventory target, int index, int amount = int.MaxValue)
            => target != null && Contents.TryTransferTo(target.Contents, index, amount);
    }
}

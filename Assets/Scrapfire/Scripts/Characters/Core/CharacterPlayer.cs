using UnityEngine;
using Scrapfire.Characters.Inventory;
using CharacterWeapon = Scrapfire.Characters.Weapon.Weapon;
using PlayerInventory = Scrapfire.Characters.Inventory.InventoryContainer;

namespace Scrapfire.Characters.Core
{
    [AddComponentMenu("Scrapfire/Character/Core/CharacterPlayer")]
    [RequireComponent(typeof(CharacterAttributes), typeof(Scrapfire.Economy.Balance))]
    [RequireComponent(typeof(Scrapfire.Characters.Inventory.Inventory))]
    [RequireComponent(typeof(TargetMotion))]
    public class CharacterPlayer : Character
    {
        public Transform HandAnchor;
        public Camera PlayerCamera;
        public CharacterAttributes Attributes { get; private set; }
        public Scrapfire.Economy.Balance Balance { get; private set; }
        public PlayerInventory CharacterInventory { get; private set; }
        public Scrapfire.Characters.Inventory.Inventory Inventory { get; private set; }
        private Item SelectedData => SelectedWeaponSlot == null || SelectedWeaponSlot.Content.IsEmpty
            ? null : SelectedWeaponSlot.Content.Data;
        public float MoveSpeedMultiplier => SelectedData != null ? Mathf.Max(0f, SelectedData.MoveSpeedMultiplier) : 1f;
        public float DamageMultiplier => SelectedData != null ? Mathf.Max(0f, SelectedData.DamageMultiplier) : 1f;
        public PlayerInventory.Slot SelectedWeaponSlot { get; private set; }

        public bool SelectWeapon(int weaponIndex)
        {
            int index = 0;

            foreach (PlayerInventory.Slot slot in CharacterInventory.GetSlots(InventorySlotKind.Weapon))
            {
                if (index == weaponIndex)
                {
                    if (slot.Locked)
                    {
                        return false;
                    }

                    SelectedWeaponSlot = slot;
                    RefreshHeldItem();
                    return true;
                }

                index++;
            }

            return false;
        }

        public bool CycleWeapon(int direction)
        {
            if (direction == 0)
            {
                return false;
            }

            var weapons = new System.Collections.Generic.List<PlayerInventory.Slot>(
                CharacterInventory.GetSlots(InventorySlotKind.Weapon));

            int current = weapons.IndexOf(SelectedWeaponSlot);
            int step = direction > 0 ? 1 : -1;

            for (int offset = 1; offset <= weapons.Count; offset++)
            {
                int index = (current + step * offset + weapons.Count) % weapons.Count;
                PlayerInventory.Slot slot = weapons[index];

                if (!slot.Locked && !slot.Content.IsEmpty)
                {
                    return SelectWeapon(index);
                }
            }

            return false;
        }

        private ItemStack _heldStack = ItemStack.EMPTY;
        private GameObject _heldInstance;

        protected override void Awake()
        {
            CharacterCollisionLayers.Assign(gameObject, "Player");
            base.Awake();
            Attributes = GetComponent<CharacterAttributes>();
            Balance = GetComponent<Scrapfire.Economy.Balance>();
            CharacterHealth.CanBeDowned = true;
            EquipWeapon(null);
            Inventory = GetComponent<Scrapfire.Characters.Inventory.Inventory>();
            CharacterInventory = Inventory.Contents;
            CharacterInventory.Changed += RefreshHeldItem;
            SelectWeapon(0);
            if (PlayerCamera != null) BindCamera(PlayerCamera);
        }

        private void RefreshHeldItem()
        {
            var selected = SelectedWeaponSlot?.Content ?? ItemStack.EMPTY;
            if (ReferenceEquals(_heldStack, selected)) return;
            if (CurrentWeapon != null) CurrentWeapon.Attacked -= OnWeaponAttacked;
            EquipWeapon(null);
            if (_heldInstance != null)
            {
                _heldInstance.SetActive(false);
                Destroy(_heldInstance);
                _heldInstance = null;
            }
            _heldStack = selected;
            if (selected.IsEmpty) return;
            var data = selected.Data;
            GameObject prefab = data.ItemCategory == Item.Category.Weapon && data.WeaponPrefab != null
                ? data.WeaponPrefab.gameObject : data.Model;
            if (prefab == null) return;
            _heldInstance = Instantiate(prefab, HandAnchor != null ? HandAnchor : transform, false);
            if (data.Material != null)
                foreach (var renderer in _heldInstance.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = data.Material;
            if (data.ItemCategory != Item.Category.Weapon) return;
            var weapon = _heldInstance.GetComponentInChildren<CharacterWeapon>();
            if (weapon is Scrapfire.Characters.Weapon.RangedWeapon ranged) ranged.BindItem(selected, CharacterInventory);
            EquipWeapon(weapon);
            if (weapon != null) weapon.Attacked += OnWeaponAttacked;
        }

        private void OnWeaponAttacked(Vector3 origin, Vector3 direction)
        {
            var stack = SelectedWeaponSlot?.Content;
            if (stack == null || stack.IsEmpty || stack.Data.Durability <= 0f) return;
            stack.Wear(1f);
            if (stack.Durability == 0f) CharacterInventory.Remove(CharacterInventory.IndexOf(SelectedWeaponSlot));
        }

        public override bool TryAttack(Vector3 direction)
        {
            if (!CanAct || SelectedData == null) return false;
            return SelectedData.ItemCategory == Item.Category.Weapon && base.TryAttack(direction);
        }

        protected override void OnDestroy()
        {
            if (CharacterInventory != null) CharacterInventory.Changed -= RefreshHeldItem;
            if (CurrentWeapon != null) CurrentWeapon.Attacked -= OnWeaponAttacked;
            base.OnDestroy();
            if (_heldInstance != null) Destroy(_heldInstance);
        }

        public void BindCamera(Camera camera)
        {
            PlayerCamera = camera;
            if (camera == null) return;
            var observer = camera.GetComponent<Scrapfire.Levels.RegionViewObserver>();
            if (observer == null) observer = camera.gameObject.AddComponent<Scrapfire.Levels.RegionViewObserver>();
            observer.Target = transform;
            var follow = camera.GetComponent<Scrapfire.Characters.Player.TopDownCameraFollow>();
            if (follow == null) follow = camera.gameObject.AddComponent<Scrapfire.Characters.Player.TopDownCameraFollow>();
            follow.Target = transform;
            follow.SnapToTarget();
        }
    }
}

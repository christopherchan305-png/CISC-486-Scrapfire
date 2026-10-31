using System;
using UnityEngine;
using UnityEngine.Serialization;
using CharacterWeapon = Scrapfire.Characters.Weapon.Weapon;
using Scrapfire.Characters.Inventory.Use;

namespace Scrapfire.Characters.Inventory
{
    [CreateAssetMenu(menuName = "Scrapfire/Inventory/Item")]
    public class Item : ScriptableObject
    {
        public enum Category
        {
            Weapon = 1 << 0,
            Ammo = 1 << 1,
            Medical = 1 << 2,
            Attachment = 1 << 3,
            Armor = 1 << 4,
            Supplies = 1 << 5,
            Consumable = 1 << 6
        }

        public string DisplayName;
        public Category ItemCategory = Category.Medical;
        public Sprite Icon;
        public Material Material;
        public GameObject Model;
        [FormerlySerializedAs("MaximumStackSize"), Min(1)] public int StackSize = 1;
        [FormerlySerializedAs("MaximumDurability"), Min(0)] public float Durability;
        public CharacterWeapon WeaponPrefab;
        public Scrapfire.Characters.Weapon.WeaponData Weapon;
        public ItemUseBehaviour UseBehaviour;
        [Min(0f)] public float MoveSpeedMultiplier = 1f;
        [Min(0f)] public float DamageMultiplier = 1f;

        public int StackLimit => ItemCategory == Category.Weapon || ItemCategory == Category.Armor || Durability > 0f ? 1 : Mathf.Max(1, StackSize);
        public bool HasValidCategory => Enum.IsDefined(typeof(Category), ItemCategory);
    }
}

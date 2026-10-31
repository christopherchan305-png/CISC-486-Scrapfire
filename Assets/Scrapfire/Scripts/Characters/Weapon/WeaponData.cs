using UnityEngine;

namespace Scrapfire.Characters.Weapon
{
    [CreateAssetMenu(menuName = "Scrapfire/Weapons/Weapon Data")]
    public sealed class WeaponData : ScriptableObject
    {
        public enum FireMode
        {
            SemiAutomatic,
            Automatic,
            BoltAction
        }

        public enum ReloadMode
        {
            Magazine,
            SingleRound
        }

        public FireMode Mode = FireMode.SemiAutomatic;
        [Tooltip("Three-dimensional spread cone half-angle, in degrees.")]
        [Range(0f, 90f)] public float RecoilValue = 2f;
        [Min(0f)] public float RecoilMultiplierOnMove = 1.5f;
        [Min(0f)] public float RecoilMultiplierOnSprint = 2.5f;
        [Range(0f, 90f)] public float RecoilValueOnAim = 0.5f;
        public bool FireOnSprint;
        [Min(1)] public int MagazineCapacity = 12;
        public bool MagazineUsed = true;
        [Min(0.01f)] public float MagazineReloadInterval = 1.5f;
        public ReloadMode MagazineReloadMode = ReloadMode.Magazine;
        public WeaponAmmo Ammo;
        [Min(0.01f)] public float TimeBetweenUse = 0.25f;
        [Min(0f)] public float TimeDelayBeforeUse;
        [Min(0.01f)] public float Range = 30f;
        [Tooltip("Gameplay hearing radius in metres. Zero means silent; independent of audio playback volume.")]
        [Min(0f)] public float GunshotRadius = 18f;
    }
}

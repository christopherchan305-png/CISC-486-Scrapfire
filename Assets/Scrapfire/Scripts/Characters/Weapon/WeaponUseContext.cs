namespace Scrapfire.Characters.Weapon
{
    public readonly struct WeaponUseContext
    {
        public bool IsMoving { get; }
        public bool IsSprinting { get; }
        public bool IsAiming { get; }

        public WeaponUseContext(bool isMoving, bool isSprinting, bool isAiming)
        {
            IsMoving = isMoving;
            IsSprinting = isSprinting;
            IsAiming = isAiming;
        }
    }
}

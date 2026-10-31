using UnityEngine;
using Scrapfire.Characters.Core;
using Scrapfire.Levels;

namespace Scrapfire.Interaction
{
    /// <summary>Purchase adapter. MapRegion owns geometry/access; Balance owns money.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(MapRegion))]
    public sealed class RegionUnlockInteraction : MonoBehaviour, IPlayerInteractable
    {
        private MapRegion _region;
        private bool _purchasing;
        private void Awake() => _region = GetComponent<MapRegion>();
        public string GetPrompt(CharacterPlayer player) => $"F  Unlock {_region.RegionName} — ${_region.UnlockCost}";
        public bool CanInteract(CharacterPlayer player) => isActiveAndEnabled && _region.isActiveAndEnabled
            && !_region.IsUnlocked && !_purchasing && player != null && player.CanAct;

        public bool TryInteract(CharacterPlayer player)
        {
            if (!CanInteract(player)) return false;
            var interactor = player.GetComponent<PlayerInteractor>();
            if (interactor == null || !_region.IsWithinReach(player.transform.position, interactor.Reach)) return false;
            _purchasing = true;
            try
            {
                if (!player.Balance.TrySpend(_region.UnlockCost)) return false;
                if (_region.IsUnlocked || _region.TryUnlock()) return true;
                player.Balance.Credit(_region.UnlockCost);
                return false;
            }
            finally { _purchasing = false; }
        }
    }
}

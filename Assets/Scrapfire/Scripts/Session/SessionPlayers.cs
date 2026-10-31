using System.Collections.Generic;
using UnityEngine;
using Scrapfire.Characters.Core;

namespace Scrapfire.Session
{
    /// <summary>Explicit session roster; a future networking adapter registers/unregisters actors here.</summary>
    public sealed class SessionPlayers : MonoBehaviour
    {
        [SerializeField] private List<CharacterPlayer> _players = new List<CharacterPlayer>();
        public IReadOnlyList<CharacterPlayer> Players => _players;
        public int ActiveCount
        {
            get
            {
                int count = 0;
                foreach (var player in _players)
                    if (player != null && player.isActiveAndEnabled && player.HealthState != HealthState.Dead) count++;
                return count;
            }
        }
        public bool HasStandingPlayer
        {
            get
            {
                foreach (var player in _players) if (player != null && player.CanAct) return true;
                return false;
            }
        }
        public void Register(CharacterPlayer player)
        {
            if (player != null && !_players.Contains(player)) _players.Add(player);
        }
        public void Unregister(CharacterPlayer player) => _players.Remove(player);
    }
}

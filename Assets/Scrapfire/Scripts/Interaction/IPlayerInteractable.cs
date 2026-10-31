using Scrapfire.Characters.Core;

namespace Scrapfire.Interaction
{
    public interface IPlayerInteractable
    {
        string GetPrompt(CharacterPlayer player);
        bool CanInteract(CharacterPlayer player);
        bool TryInteract(CharacterPlayer player);
    }
}

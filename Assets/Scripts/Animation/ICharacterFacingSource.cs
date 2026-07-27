/// <summary>
/// Optional authoritative facing source for a directional visual component.
/// Player movement implements this contract so animation and combat can read
/// one shared eight-direction value without reading each other.
/// </summary>
public interface ICharacterFacingSource
{
    CharacterFacingDirection CurrentFacing { get; }
}

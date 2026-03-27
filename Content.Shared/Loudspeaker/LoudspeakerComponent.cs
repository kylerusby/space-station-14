using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Loudspeaker;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LoudspeakerComponent : Component
{
    /// <summary>
    /// Which department group this speaker belongs to.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<LoudspeakerGroupPrototype> SpeakerGroup;

    /// <summary>
    /// Whether the speaker is enabled. Toggled by crew with access or wire cutting.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;
}

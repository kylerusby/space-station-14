using Robust.Shared.Prototypes;

namespace Content.Shared.Loudspeaker;

[Prototype]
public sealed partial class LoudspeakerGroupPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name = string.Empty;

    [DataField(required: true)]
    public Color Color = Color.White;
}

using Content.Server.Wires;
using Content.Shared.Loudspeaker;
using Content.Shared.Wires;

namespace Content.Server.Loudspeaker;

public sealed partial class LoudspeakerWireAction : ComponentWireAction<LoudspeakerComponent>
{
    public override Color Color { get; set; } = Color.Green;
    public override string Name { get; set; } = "wire-name-loudspeaker-toggle";

    public override StatusLightState? GetLightState(Wire wire, LoudspeakerComponent comp)
        => comp.Enabled ? StatusLightState.On : StatusLightState.Off;

    public override object StatusKey { get; } = LoudspeakerWireStatus.ToggleIndicator;

    public override bool Cut(EntityUid user, Wire wire, LoudspeakerComponent comp)
    {
        EntityManager.System<LoudspeakerSystem>().SetEnabled(wire.Owner, false);
        return true;
    }

    public override bool Mend(EntityUid user, Wire wire, LoudspeakerComponent comp)
    {
        EntityManager.System<LoudspeakerSystem>().SetEnabled(wire.Owner, true);
        return true;
    }

    public override void Pulse(EntityUid user, Wire wire, LoudspeakerComponent comp)
    {
        EntityManager.System<LoudspeakerSystem>().SetEnabled(wire.Owner, !comp.Enabled);
    }
}

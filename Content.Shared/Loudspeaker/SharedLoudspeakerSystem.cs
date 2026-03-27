using Content.Shared.Silicons.StationAi;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.Shared.Loudspeaker;

public abstract class SharedLoudspeakerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LoudspeakerComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
    }

    private void OnOpenAttempt(EntityUid uid, LoudspeakerComponent component, ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<StationAiHeldComponent>(args.User))
            args.Cancel();
    }
}

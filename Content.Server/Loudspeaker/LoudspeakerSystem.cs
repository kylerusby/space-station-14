using Content.Server.Instruments;
using Content.Server.Power.Components;
using Content.Shared.Instruments;
using Content.Shared.Loudspeaker;
using Content.Shared.Power;
using Content.Shared.Silicons.StationAi;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Loudspeaker;

public sealed class LoudspeakerSystem : SharedLoudspeakerSystem
{
    [Dependency] private readonly UserInterfaceSystem _bui = default!;
    [Dependency] private readonly InstrumentSystem _instrument = default!;
    [Dependency] private readonly SharedInstrumentSystem _sharedInstrument = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<LoudspeakerComponent>(LoudspeakerUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnBuiOpened);
            subs.Event<LoudspeakerRequestGroupsMessage>(OnRequestGroups);
            subs.Event<LoudspeakerPlayMessage>(OnPlay);
            subs.Event<LoudspeakerStopMessage>(OnStop);
            subs.Event<LoudspeakerSetInstrumentMessage>(OnSetInstrument);
        });

        SubscribeLocalEvent<LoudspeakerComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<StationAiHeldComponent, ToggleLoudspeakerEvent>(OnToggleLoudspeaker);
    }

    private void OnToggleLoudspeaker(EntityUid uid, StationAiHeldComponent component, ToggleLoudspeakerEvent args)
    {
        if (args.Handled)
            return;

        // Find the first powered+enabled loudspeaker on the station to use as the UI host.
        var query = EntityQueryEnumerator<LoudspeakerComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out var speakerUid, out var speaker, out var power))
        {
            if (!speaker.Enabled || !power.Powered)
                continue;

            if (!TryComp<ActorComponent>(uid, out var actor))
                return;

            args.Handled = true;
            _bui.TryToggleUi(speakerUid, LoudspeakerUiKey.Key, actor.PlayerSession);
            return;
        }
    }

    private void OnBuiOpened(EntityUid uid, LoudspeakerComponent component, BoundUIOpenedEvent args)
    {
        SendGroupInfo(uid, args.Actor);
        SendState(uid, args.Actor);
    }

    private void OnRequestGroups(EntityUid uid, LoudspeakerComponent component, LoudspeakerRequestGroupsMessage args)
    {
        SendGroupInfo(uid, args.Actor);
    }

    private void SendGroupInfo(EntityUid uid, EntityUid actor)
    {
        // Count powered + enabled speakers per group.
        var counts = new Dictionary<string, int>();

        var query = EntityQueryEnumerator<LoudspeakerComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out _, out var speaker, out var power))
        {
            if (!speaker.Enabled || !power.Powered)
                continue;

            var groupId = speaker.SpeakerGroup.Id;
            counts[groupId] = counts.GetValueOrDefault(groupId) + 1;
        }

        var groups = new List<LoudspeakerGroupInfo>();
        foreach (var (groupId, count) in counts)
        {
            if (!_proto.TryIndex<LoudspeakerGroupPrototype>(groupId, out var proto))
                continue;

            groups.Add(new LoudspeakerGroupInfo(
                groupId,
                Loc.GetString(proto.Name),
                proto.Color,
                count));
        }

        _bui.ServerSendUiMessage(uid, LoudspeakerUiKey.Key, new LoudspeakerGroupsResponseMessage(groups), actor);
    }

    private void SendState(EntityUid uid, EntityUid actor)
    {
        if (!TryComp<InstrumentComponent>(uid, out var instrument))
            return;

        // Determine which groups are actively being broadcast to by this master.
        var activeGroups = new List<ProtoId<LoudspeakerGroupPrototype>>();
        if (instrument.Playing)
        {
            var puppetQuery = EntityQueryEnumerator<LoudspeakerComponent, InstrumentComponent>();
            while (puppetQuery.MoveNext(out _, out var speaker, out var puppetInst))
            {
                if (puppetInst.Master == uid && puppetInst.Playing)
                {
                    var groupId = speaker.SpeakerGroup;
                    if (!activeGroups.Contains(groupId))
                        activeGroups.Add(groupId);
                }
            }
        }

        _bui.ServerSendUiMessage(
            uid,
            LoudspeakerUiKey.Key,
            new LoudspeakerStateMessage(instrument.Playing, activeGroups),
            actor);
    }

    private void OnPlay(EntityUid uid, LoudspeakerComponent component, LoudspeakerPlayMessage args)
    {
        if (!TryComp<InstrumentComponent>(uid, out var masterInstrument))
            return;

        if (args.SelectedGroups.Count == 0)
            return;

        // Set up puppet instruments on all powered+enabled speakers matching the selected groups.
        var selectedSet = new HashSet<string>();
        foreach (var group in args.SelectedGroups)
        {
            selectedSet.Add(group.Id);
        }

        var query = EntityQueryEnumerator<LoudspeakerComponent, InstrumentComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out var speakerUid, out var speaker, out var instrument, out var power))
        {
            if (speakerUid == uid)
                continue;

            if (!speaker.Enabled || !power.Powered)
                continue;

            if (!selectedSet.Contains(speaker.SpeakerGroup.Id))
                continue;

            // Set as puppet of the master, sync instrument program and enforce it.
            instrument.Master = uid;
            instrument.Playing = true;
            instrument.AllowProgramChange = masterInstrument.AllowProgramChange;
            instrument.FilteredChannels.SetAll(false);
            _sharedInstrument.SetInstrumentProgram(speakerUid, instrument, masterInstrument.InstrumentProgram, masterInstrument.InstrumentBank);
            Dirty(speakerUid, instrument);
        }

        SendState(uid, args.Actor);
    }

    private void OnStop(EntityUid uid, LoudspeakerComponent component, LoudspeakerStopMessage args)
    {
        CleanAllPuppets(uid);
        SendState(uid, args.Actor);
    }

    private void OnSetInstrument(EntityUid uid, LoudspeakerComponent component, LoudspeakerSetInstrumentMessage args)
    {
        // Set on the master speaker and disable program change so the selected instrument is enforced.
        if (TryComp<InstrumentComponent>(uid, out var master))
        {
            master.AllowProgramChange = false;
            _sharedInstrument.SetInstrumentProgram(uid, master, args.Program, args.Bank);
        }

        // Propagate to all puppets.
        var query = EntityQueryEnumerator<LoudspeakerComponent, InstrumentComponent>();
        while (query.MoveNext(out var speakerUid, out _, out var instrument))
        {
            if (instrument.Master == uid)
            {
                instrument.AllowProgramChange = false;
                _sharedInstrument.SetInstrumentProgram(speakerUid, instrument, args.Program, args.Bank);
            }
        }
    }

    /// <summary>
    /// Cleans all puppet instruments linked to this master.
    /// </summary>
    private void CleanAllPuppets(EntityUid masterUid)
    {
        var query = EntityQueryEnumerator<LoudspeakerComponent, InstrumentComponent>();
        while (query.MoveNext(out var speakerUid, out _, out var instrument))
        {
            if (instrument.Master == masterUid)
            {
                _instrument.Clean(speakerUid, instrument);
            }
        }
    }

    private void OnPowerChanged(EntityUid uid, LoudspeakerComponent component, ref PowerChangedEvent args)
    {
        if (args.Powered)
            return;

        if (!TryComp<InstrumentComponent>(uid, out var instrument))
            return;

        // If this speaker is a puppet, clean it.
        if (instrument.Master != null)
        {
            _instrument.Clean(uid, instrument);
            return;
        }

        // If this speaker is a master (playing), stop the whole broadcast.
        if (instrument.Playing)
        {
            CleanAllPuppets(uid);
            _instrument.Clean(uid, instrument);
        }
    }

    /// <summary>
    /// Toggles the enabled state of a loudspeaker. Used by wire actions.
    /// </summary>
    public void SetEnabled(EntityUid uid, bool enabled, LoudspeakerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Enabled = enabled;
        Dirty(uid, component);

        if (!enabled && TryComp<InstrumentComponent>(uid, out var instrument))
        {
            _instrument.Clean(uid, instrument);
        }
    }
}

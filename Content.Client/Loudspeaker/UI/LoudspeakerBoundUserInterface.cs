using Content.Client.Instruments;
using Content.Shared.Loudspeaker;
using Robust.Client.Audio.Midi;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client.Loudspeaker.UI;

public sealed class LoudspeakerBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IFileDialogManager _fileDialogManager = default!;

    private readonly InstrumentSystem _instruments;

    [ViewVariables]
    private LoudspeakerMenu? _menu;

    private bool _midiLoaded;

    public LoudspeakerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
        _instruments = EntMan.System<InstrumentSystem>();
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<LoudspeakerMenu>();

        _menu.OnFilePressed += OnFilePressed;
        _menu.OnPlayPressed += OnPlayPressed;
        _menu.OnStopPressed += OnStopPressed;
        _menu.OnLoopToggled += OnLoopToggled;
        _menu.OnInstrumentChanged += OnInstrumentChanged;

        SendMessage(new LoudspeakerRequestGroupsMessage());
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        switch (message)
        {
            case LoudspeakerGroupsResponseMessage groups:
                _menu?.PopulateGroups(groups.Groups);
                break;
            case LoudspeakerStateMessage state:
                _menu?.SetPlaying(state.Playing, state.ActiveGroups);
                break;
        }
    }

    public override void Update()
    {
        base.Update();

        if (_menu == null || _menu.Disposed)
            return;

        // Update slider and status from the renderer.
        if (!EntMan.TryGetComponent(Owner, out InstrumentComponent? instrument) || instrument.Renderer == null)
        {
            _menu.UpdatePlayback(0, 0, false);
            return;
        }

        var renderer = instrument.Renderer;
        var isPlaying = renderer.Status == MidiRendererStatus.File;
        var tick = renderer.PlayerTick;
        var total = renderer.PlayerTotalTick;

        _menu.UpdatePlayback(tick, total, isPlaying);
    }

    private async void OnFilePressed()
    {
        var filters = new FileDialogFilters(new FileDialogFilters.Group("mid", "midi"));

        await using var file = await _fileDialogManager.OpenFile(filters);

        if (file == null)
            return;

        if (_menu == null || _menu.Disposed)
            return;

        var data = file.CopyToArray();
        _instruments.OpenMidi(Owner, data);
        _midiLoaded = true;
        _menu.SetMidiLoaded(true);
    }

    private void OnPlayPressed()
    {
        if (_menu == null)
            return;

        var groups = _menu.GetSelectedGroups();
        SendMessage(new LoudspeakerPlayMessage(groups));
    }

    private void OnStopPressed()
    {
        _instruments.EndRenderer(Owner, false);
        SendMessage(new LoudspeakerStopMessage());
        _midiLoaded = false;
        _menu?.SetMidiLoaded(false);
    }

    private void OnLoopToggled(bool looping)
    {
        if (!EntMan.TryGetComponent(Owner, out InstrumentComponent? instrument))
            return;

        instrument.LoopMidi = looping;
        _instruments.UpdateRenderer(Owner);
    }

    private void OnInstrumentChanged(byte program, byte bank)
    {
        SendMessage(new LoudspeakerSetInstrumentMessage(program, bank));
    }
}

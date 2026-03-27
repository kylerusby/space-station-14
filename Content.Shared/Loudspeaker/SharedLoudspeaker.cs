using System;
using System.Collections.Generic;
using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Loudspeaker;

[Serializable, NetSerializable]
public enum LoudspeakerUiKey
{
    Key,
}

/// <summary>
/// Sent by client to request available loudspeaker groups on the station.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoudspeakerRequestGroupsMessage : BoundUserInterfaceMessage;

/// <summary>
/// Server responds with available groups and their enabled speaker counts.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoudspeakerGroupsResponseMessage : BoundUserInterfaceMessage
{
    public List<LoudspeakerGroupInfo> Groups;

    public LoudspeakerGroupsResponseMessage(List<LoudspeakerGroupInfo> groups)
    {
        Groups = groups;
    }
}

[Serializable, NetSerializable]
public sealed class LoudspeakerGroupInfo
{
    public string GroupId;
    public string Name;
    public Color Color;
    public int SpeakerCount;

    public LoudspeakerGroupInfo(string groupId, string name, Color color, int speakerCount)
    {
        GroupId = groupId;
        Name = name;
        Color = color;
        SpeakerCount = speakerCount;
    }
}

/// <summary>
/// AI requests playback to start on selected groups.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoudspeakerPlayMessage : BoundUserInterfaceMessage
{
    public List<ProtoId<LoudspeakerGroupPrototype>> SelectedGroups;

    public LoudspeakerPlayMessage(List<ProtoId<LoudspeakerGroupPrototype>> selectedGroups)
    {
        SelectedGroups = selectedGroups;
    }
}

/// <summary>
/// AI requests playback to stop.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoudspeakerStopMessage : BoundUserInterfaceMessage;

/// <summary>
/// Server tells the client which groups are currently broadcasting.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoudspeakerStateMessage : BoundUserInterfaceMessage
{
    public bool Playing;
    public List<ProtoId<LoudspeakerGroupPrototype>> ActiveGroups;

    public LoudspeakerStateMessage(bool playing, List<ProtoId<LoudspeakerGroupPrototype>> activeGroups)
    {
        Playing = playing;
        ActiveGroups = activeGroups;
    }
}

/// <summary>
/// Raised when the AI uses the loudspeaker action button.
/// </summary>
public sealed partial class ToggleLoudspeakerEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum LoudspeakerWireStatus
{
    ToggleIndicator,
}

[Serializable, NetSerializable]
public enum LoudspeakerVisualLayers : byte
{
    Playing,
}

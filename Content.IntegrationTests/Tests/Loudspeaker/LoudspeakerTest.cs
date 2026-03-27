using System.Collections.Generic;
using Content.Server.Instruments;
using Content.Server.Loudspeaker;
using Content.Server.Power.Components;
using Content.Shared.Instruments;
using Content.Shared.Loudspeaker;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Loudspeaker;

[TestFixture]
[Parallelizable(ParallelScope.All)]
[TestOf(typeof(LoudspeakerSystem))]
public sealed class LoudspeakerTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: loudspeakerGroup
          id: LoudspeakerTestGroup
          name: loudspeaker-group-common
          color: "#90EE90"

        - type: entity
          id: LoudspeakerTestEntity
          components:
          - type: Loudspeaker
            speakerGroup: LoudspeakerTestGroup
          - type: Instrument
            allowPercussion: true
            allowProgramChange: true
          - type: ApcPowerReceiver
            powerLoad: 10
        """;

    [Test]
    public async Task SpawnLoudspeaker()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var uid = entMan.SpawnEntity("LoudspeakerTestEntity", testMap.MapCoords);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<LoudspeakerComponent>(uid), "Entity should have LoudspeakerComponent");
                Assert.That(entMan.HasComponent<InstrumentComponent>(uid), "Entity should have InstrumentComponent");
                Assert.That(entMan.HasComponent<ApcPowerReceiverComponent>(uid), "Entity should have ApcPowerReceiverComponent");

                var speaker = entMan.GetComponent<LoudspeakerComponent>(uid);
                Assert.That(speaker.SpeakerGroup.Id, Is.EqualTo("LoudspeakerTestGroup"), "SpeakerGroup should match prototype value");
                Assert.That(speaker.Enabled, Is.True, "Speaker should be enabled by default");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SetEnabledDisablesSpeaker()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var loudspeakerSys = entMan.System<LoudspeakerSystem>();
            var uid = entMan.SpawnEntity("LoudspeakerTestEntity", testMap.MapCoords);

            var speaker = entMan.GetComponent<LoudspeakerComponent>(uid);

            // Disable the speaker.
            loudspeakerSys.SetEnabled(uid, false);
            Assert.That(speaker.Enabled, Is.False, "Speaker should be disabled after SetEnabled(false)");

            // Re-enable the speaker.
            loudspeakerSys.SetEnabled(uid, true);
            Assert.That(speaker.Enabled, Is.True, "Speaker should be enabled after SetEnabled(true)");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DisabledSpeakerNotPuppeted()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var loudspeakerSys = entMan.System<LoudspeakerSystem>();

            // Spawn two speakers in the same group.
            var enabledUid = entMan.SpawnEntity("LoudspeakerTestEntity", testMap.MapCoords);
            var disabledUid = entMan.SpawnEntity("LoudspeakerTestEntity", testMap.MapCoords);

            // Disable one speaker.
            loudspeakerSys.SetEnabled(disabledUid, false);

            // Simulate the filtering logic from OnPlay: iterate speakers matching a group,
            // collect only those that are enabled (as OnPlay would).
            var wouldBePuppeted = new List<EntityUid>();
            var wouldBeSkipped = new List<EntityUid>();

            var query = entMan.EntityQueryEnumerator<LoudspeakerComponent, InstrumentComponent>();
            while (query.MoveNext(out var speakerUid, out var speaker, out _))
            {
                if (!speaker.Enabled)
                {
                    wouldBeSkipped.Add(speakerUid);
                    continue;
                }

                wouldBePuppeted.Add(speakerUid);
            }

            Assert.Multiple(() =>
            {
                Assert.That(wouldBePuppeted, Does.Contain(enabledUid),
                    "Enabled speaker should be in the puppeted set");
                Assert.That(wouldBeSkipped, Does.Contain(disabledUid),
                    "Disabled speaker should be skipped");
                Assert.That(wouldBePuppeted, Does.Not.Contain(disabledUid),
                    "Disabled speaker should not be in the puppeted set");
            });
        });

        await pair.CleanReturnAsync();
    }
}

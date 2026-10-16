namespace MUnique.OpenMU.Tests;

using System.Buffers.Binary;
using System.Text;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameServer.RemoteView.World;
using MUnique.OpenMU.GameServer.MessageHandler;

[TestFixture]
public class MagicEffectTooltipTests
{
    [Test]
    public async Task ElfBuffCombinesAppliedFlatAndMasterMultiplierAsync()
    {
        await using var effect = Effect(1,
            Power(Stats.GreaterDamageBonus, 100),
            Power(Stats.GreaterDamageBonus, 1.25f, AggregateType.Multiplicate));
        Assert.That(MagicEffectTooltip.Build(effect, Attributes((Stats.GreaterDamageBonus, 999))).Single().Text,
            Is.EqualTo("Attack damage: +125"));
    }

    [Test]
    public async Task AllClassBuffNumericSemanticsAreReportedAsync()
    {
        var cases = new (short Effect, AttributeDefinition Target, float Value, AggregateType Aggregation, string Text)[]
        {
            (2, Stats.GreaterDefenseBonus, 80, AggregateType.AddRaw, "Defense: +80"),
            (5, Stats.CriticalDamageBonus, 45, AggregateType.AddRaw, "Critical damage: +45"),
            (8, Stats.SwellLifeHealthIncrease, 0.5f, AggregateType.AddRaw, "Maximum HP: +50%"),
            (135, Stats.SwellLifeManaIncrease, 0.2f, AggregateType.AddRaw, "Maximum mana: +20%"),
            (82, Stats.MinimumWizBaseDmg, 44, AggregateType.AddRaw, "Minimum wizardry damage: +44"),
            (138, Stats.MaximumWizBaseDmg, 88, AggregateType.AddRaw, "Maximum wizardry damage: +88"),
            (139, Stats.CriticalDamageChance, 0.1f, AggregateType.AddRaw, "Critical hit chance: +10%"),
            (129, Stats.DefenseIgnoreChance, 0.15f, AggregateType.AddRaw, "Ignore defense chance: +15%"),
            (130, Stats.TotalVitality, 75, AggregateType.AddFinal, "Vitality: +75"),
            (154, Stats.DefenseFinal, 20, AggregateType.AddFinal, "Defense: +20"),
            (71, Stats.DamageReflection, 0.25f, AggregateType.AddRaw, "Damage reflection: +25%"),
            (58, Stats.DefenseDecrement, 0.9f, AggregateType.Multiplicate, "Defense: -10%"),
            (76, Stats.WeaknessPhysDmgDecrement, 0.2f, AggregateType.AddRaw, "Physical damage: -20%"),
            (6, Stats.AmmunitionConsumptionRate, 0, AggregateType.Multiplicate, "Ammunition consumption: -100%"),
        };
        foreach (var test in cases)
        {
            await using var effect = Effect(test.Effect, Power(test.Target, test.Value, test.Aggregation));
            Assert.That(MagicEffectTooltip.Build(effect, Attributes()).Single().Text, Is.EqualTo(test.Text));
        }
    }

    [Test]
    public async Task SoulBarrierUsesRecipientCapAndAppliedManaTollAsync()
    {
        await using var effect = Effect(4,
            Power(Stats.SoulBarrierReceiveDecrement, 1.4f),
            Power(Stats.SoulBarrierManaTollPerHit, 5));
        var lines = MagicEffectTooltip.Build(effect, Attributes((Stats.SoulBarrierReceiveDecrement, 0.7f), (Stats.SoulBarrierManaTollPerHit, 37)));
        Assert.That(lines.Select(line => line.Text), Is.EqualTo(new[] { "Damage reduction: +70%", "Mana consumed per hit: +37" }));
        Assert.That(lines[1].IsPenalty, Is.True);
    }

    [Test]
    public async Task RageFighterBlockUsesActualDerivedBonusAsync()
    {
        await using var effect = Effect(153, Power(Stats.IncreaseBlockBonus, 40));
        Assert.That(MagicEffectTooltip.Build(effect, Attributes((Stats.IncreaseBlockBonus, 140))).Single().Text,
            Is.EqualTo("Defense success rate (PvM): +140"));
    }

    [Test]
    public async Task BerserkerShowsClampedPenaltyAndSeparateMasterBonusesAsync()
    {
        await using var effect = Effect(81, Power(Stats.BerserkerManaMultiplier, 0.5f));
        var recipient = Attributes(
            (Stats.BerserkerManaMultiplier, 0.5f), (Stats.BerserkerWizardryMultiplier, 0.6f),
            (Stats.BerserkerCurseMultiplier, 0.7f), (Stats.BerserkerHealthDecrement, 0.2f),
            (Stats.BerserkerMinPhysDmgBonus, 100), (Stats.BerserkerMaxPhysDmgBonus, 150),
            (Stats.BerserkerProficiencyMultiplier, 0.1f));
        var lines = MagicEffectTooltip.Build(effect, recipient);
        Assert.That(lines, Does.Contain(new MagicEffectTooltipLine("Maximum mana: +50%")));
        Assert.That(lines, Does.Contain(new MagicEffectTooltipLine("Base wizardry damage: +60%")));
        Assert.That(lines, Does.Contain(new MagicEffectTooltipLine("Base curse damage: +70%")));
        Assert.That(lines, Does.Contain(new MagicEffectTooltipLine("Maximum HP: -10%", true)));
        Assert.That(lines, Does.Contain(new MagicEffectTooltipLine("Stat-based defense: -10%", true)));
        Assert.That(lines, Does.Contain(new MagicEffectTooltipLine("Final wizardry / curse damage: +10%")));
    }

    [Test]
    public async Task RemainingTimeRefreshesWithoutChangingDurationOrPowerAsync()
    {
        await using var effect = Effect(1, Power(Stats.GreaterDamageBonus, 42));
        var first = effect.RemainingDuration;
        await Task.Delay(40).ConfigureAwait(false);
        Assert.That(effect.RemainingDuration, Is.LessThan(first));
        effect.ResetTimer();
        Assert.That(effect.RemainingDuration, Is.GreaterThan(TimeSpan.FromSeconds(59)));
        Assert.That(effect.Duration, Is.EqualTo(TimeSpan.FromMinutes(1)));
        Assert.That(effect.Value, Is.EqualTo(42));
    }

    [Test]
    public void PacketUsesVersionedBoundedUtf8AndRemainingTime()
    {
        var bytes = new byte[4096];
        var length = MagicEffectDetailsPacket.Write(bytes, 81, 1, 123456, [new(new string('界', 80), true)]);
        Assert.That(bytes[0], Is.EqualTo(0xC2));
        Assert.That(BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1)), Is.EqualTo(length));
        Assert.That(bytes[3], Is.EqualTo(0xD7));
        Assert.That(bytes[4], Is.EqualTo(2));
        Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)), Is.EqualTo(123456));
        Assert.That(bytes[12], Is.EqualTo(1));
        Assert.That(bytes[13], Is.EqualTo(1));
        Assert.That(bytes[14], Is.LessThanOrEqualTo(96));
        Assert.That(new UTF8Encoding(false, true).GetString(bytes, 15, bytes[14]), Is.EqualTo(new string('界', 32)));
    }

    [Test]
    public async Task RemovalDetailsAreSentOnlyToAffectedOwnerAsync()
    {
        var (owner, ownerOutput) = CastleSiegeRemoteViewTestHelper.CreatePlayer();
        var (observer, observerOutput) = CastleSiegeRemoteViewTestHelper.CreatePlayer();
        await using var effect = Effect(8, Power(Stats.SwellLifeHealthIncrease, 0.3f));
        await new DeActivateMagicEffectPlugIn(owner).DeactivateMagicEffectAsync(effect, owner).ConfigureAwait(false);
        await new DeActivateMagicEffectPlugIn(observer).DeactivateMagicEffectAsync(effect, owner).ConfigureAwait(false);
        var ownerBytes = ownerOutput.ToArray();
        var observerBytes = observerOutput.ToArray();
        Assert.That(ownerBytes.Length, Is.EqualTo(observerBytes.Length + MagicEffectDetailsPacket.HeaderLength));
        Assert.That(ownerBytes[^MagicEffectDetailsPacket.HeaderLength], Is.EqualTo(0xC2));
        Assert.That(ownerBytes[^(MagicEffectDetailsPacket.HeaderLength - 3)], Is.EqualTo(0xD7));
        Assert.That(ownerBytes[^(MagicEffectDetailsPacket.HeaderLength - 5)], Is.Zero);
    }

    [Test]
    public async Task HoverRequestReportsActiveEffectsAndIsRateLimitedPerPlayerAsync()
    {
        var initialized = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var (owner, output) = CastleSiegeRemoteViewTestHelper.CreatePlayer();
        typeof(Player).GetProperty(nameof(Player.Attributes))!.SetValue(owner, initialized.Attributes);
        await using var effect = Effect(1, Power(Stats.GreaterDamageBonus, 125));
        await owner.MagicEffectList.AddEffectAsync(effect).ConfigureAwait(false);
        var handler = new MagicEffectDetailsRequestHandlerPlugIn();
        byte[] request = [0xC1, 4, 0xD7, 1];
        await handler.HandlePacketAsync(owner, request).ConfigureAwait(false);
        var bytes = output.ToArray();
        Assert.That(bytes[5], Is.EqualTo(2));
        Assert.That(bytes[MagicEffectDetailsPacket.HeaderLength + 5], Is.EqualTo(1));
        Assert.That(Encoding.UTF8.GetString(bytes), Does.Contain("Attack damage: +125"));
        await handler.HandlePacketAsync(owner, request).ConfigureAwait(false);
        Assert.That(output.Length, Is.EqualTo(bytes.Length));
        await owner.MagicEffectList.ClearAllEffectsAsync().ConfigureAwait(false);
    }

    private static MagicEffect Effect(short number, params MagicEffect.ElementWithTarget[] powers) =>
        new(TimeSpan.FromMinutes(1), new MagicEffectDefinition { Number = number }, powers);

    private static MagicEffect.ElementWithTarget Power(AttributeDefinition target, float value, AggregateType aggregation = AggregateType.AddRaw) =>
        new(new SimpleElement(value, aggregation), target);

    private static IAttributeSystem Attributes(params (AttributeDefinition Target, float Value)[] values)
    {
        var attributes = new Mock<IAttributeSystem>();
        foreach (var value in values)
        {
            attributes.Setup(system => system[value.Target]).Returns(value.Value);
        }
        return attributes.Object;
    }
}

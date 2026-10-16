namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Premium;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.InMemory;

[TestFixture]
public class PremiumBuffTests
{
    [Test]
    public void CatalogCreatesAllReferenceItemsAndIsIdempotent()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = context.CreateNew<MUnique.OpenMU.Persistence.BasicModel.GameConfiguration>();
        var initializer = new PremiumBuffs(context, configuration);
        initializer.Initialize();
        var attributeCount = configuration.Attributes.Count;
        initializer.Initialize();
        Assert.That(PremiumBuffCatalog.Items.Count, Is.EqualTo(26));
        Assert.That(configuration.Items.Count, Is.EqualTo(26));
        Assert.That(configuration.MagicEffects.Count, Is.EqualTo(26));
        Assert.That(configuration.Attributes.Count, Is.EqualTo(attributeCount));
        foreach (var entry in PremiumBuffCatalog.Items)
        {
            var item = configuration.Items.Single(item => item.Group == entry.Group && item.Number == entry.Number);
            Assert.That(item.ConsumeEffect!.Number, Is.EqualTo(entry.Effect));
            Assert.That(item.ConsumeEffect.StopByDeath, Is.False);
            Assert.That(item.DropsFromMonsters, Is.False);
            Assert.That(item.Width, Is.EqualTo(entry.Group == 14 && entry.SubType != 242 ? 2 : 1));
            Assert.That(item.Height, Is.EqualTo(entry.Group == 14 ? 2 : 1));
        }
    }

    [Test]
    public void ConfigurationUpdatePreservesExistingCustomBonuses()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = context.CreateNew<MUnique.OpenMU.Persistence.BasicModel.GameConfiguration>();
        var initializer = new PremiumBuffs(context, configuration);
        initializer.Initialize();
        var ascension = configuration.MagicEffects.Single(effect => effect.Number == 40);
        ascension.PowerUpDefinitions.Single().Boost!.ConstantValue.Value = 3;
        initializer.Initialize();
        Assert.That(ascension.PowerUpDefinitions.Single().Boost!.ConstantValue.Value, Is.EqualTo(3));
    }

    [Test]
    public async Task AscensionAndWealthApplyReplaceAndRestoreWithoutDoublingAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var ascension = Definition(player, 40);
        Assert.That(await PremiumBuffService.ActivateAsync(player, ascension, TimeSpan.FromDays(30)).ConfigureAwait(false), Is.True);
        Assert.That(player.Attributes![Stats.ExperienceRate], Is.EqualTo(1.5f));
        var expiry = PremiumBuffService.ReadExpiry(player, 40);
        Assert.That(expiry, Is.GreaterThan(DateTimeOffset.UtcNow.AddDays(29)));
        await player.MagicEffectList.ClearAllEffectsAsync().ConfigureAwait(false);
        Assert.That(PremiumBuffService.ReadExpiry(player, 40), Is.EqualTo(expiry));
        await PremiumBuffService.RestoreAsync(player).ConfigureAwait(false);
        await PremiumBuffService.RestoreAsync(player).ConfigureAwait(false);
        Assert.That(player.Attributes[Stats.ExperienceRate], Is.EqualTo(1.5f));
        await PremiumBuffService.ActivateAsync(player, Definition(player, 41), TimeSpan.FromHours(1)).ConfigureAwait(false);
        Assert.That(player.MagicEffectList.ContainsEffect(40), Is.False);
        Assert.That(PremiumBuffService.ReadExpiry(player, 40), Is.Null);
        Assert.That(player.Attributes[Stats.ExperienceRate], Is.EqualTo(2));
        Assert.That(player.Attributes[PremiumBuffStats.ItemDropBonus], Is.EqualTo(0.5f));
        await player.MagicEffectList.ClearAllEffectsAsync().ConfigureAwait(false);
    }

    [Test]
    public async Task EveryPremiumEffectAppliesItsConfiguredPowerUpsAndTooltipAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        foreach (var entry in PremiumBuffCatalog.Items)
        {
            await player.MagicEffectList.ClearAllEffectsAsync().ConfigureAwait(false);
            Assert.That(await PremiumBuffService.ActivateAsync(player, Definition(player, entry.Effect), TimeSpan.FromMinutes(30)).ConfigureAwait(false), Is.True, entry.Name);
            Assert.That(player.MagicEffectList.TryGetEffect(entry.Effect, out var effect), Is.True);
            Assert.That(MagicEffectTooltip.Build(effect!, player.Attributes!), Is.Not.Empty, entry.Name);
            Assert.That(PremiumBuffService.ReadExpiry(player, entry.Effect), Is.GreaterThan(DateTimeOffset.UtcNow.AddMinutes(29)));
        }
        await player.MagicEffectList.ClearAllEffectsAsync().ConfigureAwait(false);
    }

    [Test]
    public async Task ExpiryRemovesAppliedBonusAndPersistentMarkerAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        await PremiumBuffService.ActivateAsync(player, Definition(player, 40), TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        player.MagicEffectList.TryGetEffect(40, out var effect);
        effect!.EffectTimeOut += _ => { completed.TrySetResult(); return ValueTask.CompletedTask; };
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.That(player.MagicEffectList.ContainsEffect(40), Is.False);
        Assert.That(PremiumBuffService.ReadExpiry(player, 40), Is.Null);
    }

    [Test]
    public async Task ScrollAndElixirFamiliesDoNotStackWithTheirOwnFamilyAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        foreach (var effect in new byte[] { 44, 45, 50, 51, 121 })
        {
            await PremiumBuffService.ActivateAsync(player, Definition(player, effect), TimeSpan.FromHours(1)).ConfigureAwait(false);
        }
        Assert.That(player.MagicEffectList.ContainsEffect(44), Is.False);
        Assert.That(player.MagicEffectList.ContainsEffect(45), Is.True);
        Assert.That(player.MagicEffectList.ContainsEffect(50), Is.False);
        Assert.That(player.MagicEffectList.ContainsEffect(51), Is.True);
        Assert.That(player.MagicEffectList.ContainsEffect(121), Is.True);
        await player.MagicEffectList.ClearAllEffectsAsync().ConfigureAwait(false);
    }

    [Test]
    public void WealthBoostsOnlyOrdinaryChanceDrops()
    {
        var random = new DropItemGroup { Chance = 0.3, ItemType = SpecialItemType.RandomItem };
        var guaranteed = new DropItemGroup { Chance = 1, ItemType = SpecialItemType.Excellent };
        var quest = new DropItemGroup { Chance = 0.2 };
        Assert.That(PremiumDropChance.Weight(random, 0.5f), Is.EqualTo(0.45).Within(0.00001));
        Assert.That(PremiumDropChance.Weight(random, 0), Is.EqualTo(0.3));
        Assert.That(PremiumDropChance.Weight(guaranteed, 0.5f), Is.EqualTo(1));
        Assert.That(PremiumDropChance.Weight(quest, 0.5f), Is.EqualTo(0.2));
    }

    [Test]
    public async Task ItemProtectionAppliesToMountsButNotPetsOrWeaponsAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        await PremiumBuffService.ActivateAsync(player, Definition(player, 100), TimeSpan.FromHours(1)).ConfigureAwait(false);
        foreach (var number in new short[] { 3, 4, 37 })
        {
            Assert.That(PremiumBuffCatalog.ProtectsMount(new Item { Definition = new ItemDefinition { Group = 13, Number = number } }, player.Attributes!), Is.True);
        }
        Assert.That(PremiumBuffCatalog.ProtectsMount(new Item { Definition = new ItemDefinition { Group = 13, Number = 80 } }, player.Attributes!), Is.False);
        Assert.That(PremiumBuffCatalog.ProtectsMount(new Item { Definition = new ItemDefinition { Group = 0, Number = 0 } }, player.Attributes!), Is.False);
        await player.MagicEffectList.ClearAllEffectsAsync().ConfigureAwait(false);
    }

    [TestCase(30)]
    [TestCase(90)]
    public async Task PremiumTimersSupportLongPurchaseDurationsAsync(int days)
    {
        await using var effect = new MagicEffect(TimeSpan.FromDays(days), new MagicEffectDefinition { Number = 40 });
        Assert.That(effect.RemainingDuration.TotalDays, Is.GreaterThan(days - 0.01));
        effect.ResetTimer();
        Assert.That(effect.RemainingDuration.TotalDays, Is.GreaterThan(days - 0.01));
    }

    [Test]
    public async Task CancellationDoesNotRestoreThePaidEffectAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        await PremiumBuffService.ActivateAsync(player, Definition(player, 40), TimeSpan.FromDays(30)).ConfigureAwait(false);
        await PremiumBuffService.CancelAsync(player, 40).ConfigureAwait(false);
        await PremiumBuffService.RestoreAsync(player).ConfigureAwait(false);
        Assert.That(player.MagicEffectList.ContainsEffect(40), Is.False);
        Assert.That(PremiumBuffService.ReadExpiry(player, 40), Is.Null);
    }

    [Test]
    public async Task SustenanceBlocksBothKindsOfExperienceAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var normal = player.SelectedCharacter!.Experience;
        var master = player.SelectedCharacter.MasterExperience;
        await PremiumBuffService.ActivateAsync(player, Definition(player, 42), TimeSpan.FromHours(1)).ConfigureAwait(false);
        await player.AddExperienceAsync(100, null).ConfigureAwait(false);
        await player.AddMasterExperienceAsync(100, null).ConfigureAwait(false);
        Assert.That(player.SelectedCharacter.Experience, Is.EqualTo(normal));
        Assert.That(player.SelectedCharacter.MasterExperience, Is.EqualTo(master));
        await PremiumBuffService.CancelAsync(player, 42).ConfigureAwait(false);
    }

    [Test]
    public async Task InventoryConsumptionActivatesAndConsumesExactlyOnceAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var item = new Item { Definition = player.GameContext.Configuration.Items.Single(item => item.Group == 13 && item.Number == 43), Durability = 1 };
        var handler = new MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions.ApplyMagicEffectConsumeHandlerPlugIn();
        Assert.That(await handler.ConsumeItemAsync(player, item, null, default).ConfigureAwait(false), Is.True);
        Assert.That(item.Durability, Is.Zero);
        Assert.That(player.MagicEffectList.ContainsEffect(40), Is.True);
        var expiry = PremiumBuffService.ReadExpiry(player, 40);
        Assert.That(await handler.ConsumeItemAsync(player, item, null, default).ConfigureAwait(false), Is.False);
        Assert.That(PremiumBuffService.ReadExpiry(player, 40), Is.EqualTo(expiry));
        await PremiumBuffService.CancelAsync(player, 40).ConfigureAwait(false);
    }

    [Test]
    public async Task MissingExpiryConfigurationDoesNotRemoveExistingBuffAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        await PremiumBuffService.ActivateAsync(player, Definition(player, 40), TimeSpan.FromHours(1)).ConfigureAwait(false);
        var missing = player.GameContext.Configuration.Attributes.Single(attribute => attribute == PremiumBuffStats.ExpiryDay(41));
        player.GameContext.Configuration.Attributes.Remove(missing);
        Assert.That(await PremiumBuffService.ActivateAsync(player, Definition(player, 41), TimeSpan.FromHours(1)).ConfigureAwait(false), Is.False);
        Assert.That(player.MagicEffectList.ContainsEffect(40), Is.True);
        await PremiumBuffService.CancelAsync(player, 40).ConfigureAwait(false);
    }

    [Test]
    public async Task ExpiredOfflineBuffIsClearedInsteadOfRestoredAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        await PremiumBuffService.ActivateAsync(player, Definition(player, 40), TimeSpan.FromHours(1)).ConfigureAwait(false);
        await player.MagicEffectList.ClearAllEffectsAsync().ConfigureAwait(false);
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds();
        player.SelectedCharacter!.Attributes.Single(attribute => attribute.Definition == PremiumBuffStats.ExpiryDay(40)).Value = yesterday / 86400;
        player.SelectedCharacter.Attributes.Single(attribute => attribute.Definition == PremiumBuffStats.ExpirySecond(40)).Value = yesterday % 86400;
        await PremiumBuffService.RestoreAsync(player).ConfigureAwait(false);
        Assert.That(player.MagicEffectList.ContainsEffect(40), Is.False);
        Assert.That(PremiumBuffService.ReadExpiry(player, 40), Is.Null);
    }

    [Test]
    public async Task DisplayedPremiumBonusesMatchActualAttributeChangesAsync()
    {
        var cases = new (byte Effect, AttributeDefinition Target, float Value, bool Multiply, string Text)[]
        {
            (40, Stats.ExperienceRate, 1.5f, true, "Normal EXP: +50%"),
            (41, Stats.ExperienceRate, 2, true, "Normal EXP: +100%"),
            (41, PremiumBuffStats.ItemDropBonus, 0.5f, false, "Item drop bonus: +50%"),
            (44, Stats.AttackSpeedAny, 15, false, "Attack and magic speed: +15"),
            (45, Stats.DefenseFinal, 60, false, "Defense: +60"),
            (46, Stats.PhysicalBaseDmg, 30, false, "Physical attack: +30"),
            (47, Stats.MinimumWizBaseDmg, 30, false, "Minimum wizardry damage: +30"),
            (47, Stats.MaximumWizBaseDmg, 30, false, "Maximum wizardry damage: +30"),
            (48, Stats.MaximumHealth, 200, false, "Maximum HP: +200"),
            (49, Stats.MaximumMana, 300, false, "Maximum mana: +300"),
            (50, Stats.TotalStrength, 50, false, "Strength: +50"),
            (51, Stats.TotalAgility, 50, false, "Agility: +50"),
            (52, Stats.TotalVitality, 50, false, "Vitality: +50"),
            (53, Stats.TotalEnergy, 50, false, "Energy: +50"),
            (54, Stats.TotalLeadership, 50, false, "Command: +50"),
            (87, Stats.ExperienceRate, 1.5f, true, "Normal EXP: +50%"),
            (87, Stats.HealthRecoveryMultiplier, 0.05f, false, "Max HP recovered per tick: +5%"),
            (88, PremiumBuffStats.ItemDropBonus, 0.5f, false, "Item drop bonus: +50%"),
            (88, Stats.ManaRecoveryMultiplier, 0.05f, false, "Max mana recovered per tick: +5%"),
            (89, Stats.CriticalDamageChance, 0.2f, false, "Critical hit chance: +20%"),
            (90, Stats.ExcellentDamageChance, 0.2f, false, "Excellent hit chance: +20%"),
            (101, Stats.MasterExperienceRate, 1.5f, true, "Master EXP: +50%"),
            (102, Stats.MasterExperienceRate, 2, true, "Master EXP: +100%"),
            (102, PremiumBuffStats.ItemDropBonus, 0.5f, false, "Item drop bonus: +50%"),
            (104, Stats.AttackRatePvm, 50, false, "Attack success rate (PvM): +50"),
            (104, Stats.DefenseRatePvm, 60, false, "Defense success rate (PvM): +60"),
            (119, PremiumBuffStats.ItemDropBonus, 0.5f, false, "Item drop bonus: +50%"),
            (121, Stats.MaximumHealth, 100, false, "Maximum HP: +100"),
            (121, Stats.HealthRecoveryMultiplier, 0.03f, false, "Max HP recovered per tick: +3%"),
        };
        foreach (var test in cases)
        {
            var player = await CreatePlayerAsync().ConfigureAwait(false);
            var before = player.Attributes![test.Target];
            await PremiumBuffService.ActivateAsync(player, Definition(player, test.Effect), TimeSpan.FromMinutes(30)).ConfigureAwait(false);
            player.MagicEffectList.TryGetEffect(test.Effect, out var effect);
            Assert.That(player.Attributes[test.Target], Is.EqualTo(test.Multiply ? before * test.Value : before + test.Value).Within(0.0001), test.Text);
            Assert.That(MagicEffectTooltip.Build(effect!, player.Attributes).Select(line => line.Text), Does.Contain(test.Text));
            await PremiumBuffService.CancelAsync(player, test.Effect).ConfigureAwait(false);
            Assert.That(player.Attributes[test.Target], Is.EqualTo(before).Within(0.0001), test.Text);
        }
    }

    private static MagicEffectDefinition Definition(Player player, byte effect) => player.GameContext.Configuration.MagicEffects.Single(definition => definition.Number == effect);

    private static async ValueTask<Player> CreatePlayerAsync()
    {
        var game = GameContextTestHelper.CreateGameContext();
        using var context = game.PersistenceContextProvider.CreateNewContext();
        new PremiumBuffs(context, game.Configuration).Initialize();
        return await PlayerTestHelper.CreatePlayerAsync(game).ConfigureAwait(false);
    }
}

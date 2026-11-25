// <copyright file="GoldenMonsterBoostTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MonsterAttribute = MUnique.OpenMU.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = MUnique.OpenMU.Persistence.BasicModel.MonsterDefinition;

/// <summary>
/// Verifies that the Golden monsters get their own boost only on server 1 and the other monsters keep the normal scaling.
/// </summary>
[TestFixture]
public class GoldenMonsterBoostTests
{
    private const short GoldenNumber = 82;
    private const short NormalNumber = 1;

    private IGameContext _gameContext = null!;

    /// <summary>
    /// Creates the game context.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this._gameContext = GameContextTestHelper.CreateGameContext();
    }

    /// <summary>
    /// A Golden monster on server 1 gets defense, attack rate, defense rate and health x10 and damage x2, from its base values.
    /// </summary>
    [Test]
    public async Task GoldenMonsterOnServerOneIsBoostedFromTheBaseValues()
    {
        var (plugIn, map) = await this.PrepareAsync(0).ConfigureAwait(false);
        var golden = this.CreateMonster(map, GoldenNumber);

        await plugIn.ObjectAddedToMapAsync(map, golden).ConfigureAwait(false);

        Assert.That(golden.Attributes[Stats.DefenseBase], Is.EqualTo(3000f).Within(0.01f));
        Assert.That(golden.Attributes[Stats.AttackRatePvm], Is.EqualTo(4500f).Within(0.01f));
        Assert.That(golden.Attributes[Stats.DefenseRatePvm], Is.EqualTo(1850f).Within(0.01f));
        Assert.That(golden.Attributes[Stats.MaximumHealth], Is.EqualTo(320000f).Within(0.01f));
        Assert.That(golden.Attributes[Stats.MinimumPhysBaseDmg], Is.EqualTo(900f).Within(0.01f));
        Assert.That(golden.Attributes[Stats.MaximumPhysBaseDmg], Is.EqualTo(1120f).Within(0.01f));

        await plugIn.ObjectRemovedFromMapAsync(map, golden).ConfigureAwait(false);
        Assert.That(golden.Attributes[Stats.DefenseBase], Is.EqualTo(300f).Within(0.01f));
        Assert.That(golden.Attributes[Stats.MaximumHealth], Is.EqualTo(32000f).Within(0.01f));
    }

    /// <summary>
    /// A normal monster on server 1 keeps the normal scaling (+150 % attack rate, +20 % health).
    /// </summary>
    [Test]
    public async Task NormalMonsterOnServerOneKeepsTheNormalScaling()
    {
        var (plugIn, map) = await this.PrepareAsync(0).ConfigureAwait(false);
        var normal = this.CreateMonster(map, NormalNumber);

        await plugIn.ObjectAddedToMapAsync(map, normal).ConfigureAwait(false);

        Assert.That(normal.Attributes[Stats.DefenseBase], Is.EqualTo(300f).Within(0.01f));
        Assert.That(normal.Attributes[Stats.AttackRatePvm], Is.EqualTo(1125f).Within(0.01f));
        Assert.That(normal.Attributes[Stats.MaximumHealth], Is.EqualTo(38400f).Within(0.01f));
        Assert.That(normal.Attributes[Stats.MaximumPhysBaseDmg], Is.EqualTo(560f).Within(0.01f));
    }

    /// <summary>
    /// A Golden monster on server 2 is not boosted, it only gets the scaling of that server.
    /// </summary>
    [Test]
    public async Task GoldenMonsterOnTheOtherServerIsNotBoosted()
    {
        var (plugIn, map) = await this.PrepareAsync(1).ConfigureAwait(false);
        var golden = this.CreateMonster(map, GoldenNumber);

        await plugIn.ObjectAddedToMapAsync(map, golden).ConfigureAwait(false);

        Assert.That(golden.Attributes[Stats.DefenseBase], Is.EqualTo(300f).Within(0.01f));
        Assert.That(golden.Attributes[Stats.AttackRatePvm], Is.EqualTo(675f).Within(0.01f));
        Assert.That(golden.Attributes[Stats.DefenseRatePvm], Is.EqualTo(185f).Within(0.01f));
        Assert.That(golden.Attributes[Stats.MaximumHealth], Is.EqualTo(38400f).Within(0.01f));
        Assert.That(golden.Attributes[Stats.MaximumPhysBaseDmg], Is.EqualTo(560f).Within(0.01f));
    }

    private async Task<(MonsterAttributeScaler PlugIn, GameMap Map)> PrepareAsync(byte serverId)
    {
        var map = (await this._gameContext.GetMapAsync(0).ConfigureAwait(false))!;
        map.ServerId = serverId;
        var plugIn = new MonsterAttributeScaler
        {
            Configuration = new MonsterAttributeScalerConfiguration
            {
                DamagePercentage = 0f,
                AttackRatePercentage = 150f,
                DefenseRatePercentage = 0f,
                DefensePercentage = 0f,
                HealthPercentage = 20f,
                OnlyServerId = 0,
                OtherServerId = 1,
                OtherServerAttackRatePercentage = 50f,
                OtherServerHealthPercentage = 20f,
                BoostedMonsterNumbers = "43,53,54,78,79,80,81,82,83",
                BoostedDamagePercentage = 100f,
                BoostedAttackRatePercentage = 900f,
                BoostedDefenseRatePercentage = 900f,
                BoostedDefensePercentage = 900f,
                BoostedHealthPercentage = 900f,
            },
        };

        return (plugIn, map);
    }

    private Monster CreateMonster(GameMap map, short number)
    {
        // Base values of the Golden Tantallos (number 82).
        var definition = new MonsterDefinition
        {
            Id = Guid.NewGuid(),
            Number = number,
            ObjectKind = NpcObjectKind.Monster,
            RespawnDelay = TimeSpan.FromHours(1),
        };
        definition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = 32000f });
        definition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.DefenseBase, Value = 300f });
        definition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.AttackRatePvm, Value = 450f });
        definition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.DefenseRatePvm, Value = 185f });
        definition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MinimumPhysBaseDmg, Value = 450f });
        definition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumPhysBaseDmg, Value = 560f });

        var spawnArea = new MonsterSpawnArea { MonsterDefinition = definition, GameMap = map.Definition, X1 = 100, Y1 = 100, X2 = 100, Y2 = 100, Quantity = 1 };
        var monster = new Monster(spawnArea, definition, map, NullDropGenerator.Instance, new Mock<INpcIntelligence>().Object, this._gameContext.PlugInManager, this._gameContext.PathFinderPool);
        monster.Initialize();
        return monster;
    }
}

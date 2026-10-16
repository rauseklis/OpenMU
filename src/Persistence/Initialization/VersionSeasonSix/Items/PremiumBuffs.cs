namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Attributes;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Premium;

/// <summary>
/// Adds missing premium item effects without replacing existing configured bonuses.
/// </summary>
public sealed class PremiumBuffs : InitializerBase
{
    public PremiumBuffs(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    public override void Initialize()
    {
        foreach (var entry in PremiumBuffCatalog.Items)
        {
            foreach (var bonus in entry.Bonuses)
            {
                this.EnsureAttribute(bonus.Attribute);
            }
            this.EnsureAttribute(PremiumBuffStats.ExpiryDay(entry.Effect));
            this.EnsureAttribute(PremiumBuffStats.ExpirySecond(entry.Effect));
            this.AddItemEffect(entry);
        }
    }

    private void EnsureAttribute(AttributeDefinition definition)
    {
        if (this.GameConfiguration.Attributes.All(attribute => attribute.Id != definition.Id))
        {
            this.GameConfiguration.Attributes.Add(this.Context.CreateNew<AttributeDefinition>(definition.Id, definition.Designation!, definition.Description!));
        }
    }

    private void AddItemEffect(PremiumBuffDefinition entry)
    {
        var item = this.GameConfiguration.Items.FirstOrDefault(item => item.Group == entry.Group && item.Number == entry.Number);
        if (item is null)
        {
            item = this.Context.CreateNew<ItemDefinition>();
            item.Group = entry.Group;
            item.Number = entry.Number;
            item.Name = entry.Name;
            item.Width = (byte)(entry.Group == 14 && entry.SubType != 242 ? 2 : 1);
            item.Height = (byte)(entry.Group == 14 ? 2 : 1);
            item.Durability = 1;
            item.SetGuid(item.Group, item.Number);
            this.GameConfiguration.Items.Add(item);
        }
        if (item.ConsumeEffect is not null)
        {
            return;
        }
        var effect = this.GameConfiguration.MagicEffects.FirstOrDefault(effect => effect.Number == entry.Effect);
        if (effect is null)
        {
            effect = this.Context.CreateNew<MagicEffectDefinition>();
            effect.Number = entry.Effect;
            effect.Name = entry.Name;
            effect.SubType = entry.SubType;
            effect.StopByDeath = false;
            effect.InformObservers = false;
            effect.SendDuration = true;
            effect.Duration = this.Context.CreateNew<PowerUpDefinitionValue>();
            effect.Duration.ConstantValue.Value = entry.DefaultSeconds;
            foreach (var bonus in entry.Bonuses)
            {
                var power = this.Context.CreateNew<PowerUpDefinition>();
                power.TargetAttribute = bonus.Attribute.GetPersistent(this.GameConfiguration);
                power.Boost = this.Context.CreateNew<PowerUpDefinitionValue>();
                power.Boost.ConstantValue.Value = bonus.Value;
                power.Boost.ConstantValue.AggregateType = bonus.Aggregation;
                effect.PowerUpDefinitions.Add(power);
            }
            this.GameConfiguration.MagicEffects.Add(effect);
        }
        item.ConsumeEffect = effect;
    }
}

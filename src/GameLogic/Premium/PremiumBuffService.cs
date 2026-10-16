namespace MUnique.OpenMU.GameLogic.Premium;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Activates premium effects and keeps their UTC expiry in existing character stat storage.
/// </summary>
public static class PremiumBuffService
{
    private const long SecondsPerDay = 86400;

    public static async ValueTask<bool> ActivateAsync(Player player, MagicEffectDefinition definition, TimeSpan duration)
    {
        var entry = PremiumBuffCatalog.FindEffect(definition.Number);
        if (entry is null || player.SelectedCharacter is null || player.Attributes is null || duration <= TimeSpan.Zero)
        {
            return false;
        }
        var powers = CreatePowers(player, definition);
        if (powers.Length == 0)
        {
            return false;
        }
        var expires = DateTimeOffset.UtcNow.Add(duration);
        var configuredAttributes = player.GameContext.Configuration.Attributes;
        if (!configuredAttributes.Any(attribute => attribute == PremiumBuffStats.ExpiryDay(entry.Effect))
            || !configuredAttributes.Any(attribute => attribute == PremiumBuffStats.ExpirySecond(entry.Effect)))
        {
            return false;
        }

        foreach (var other in PremiumBuffCatalog.Items.Where(item => item.SubType == entry.SubType))
        {
            if (player.MagicEffectList.TryGetEffect(other.Effect, out var previous))
            {
                await previous.DisposeAsync().ConfigureAwait(false);
            }
            SetExpiry(player, other.Effect, null);
        }
        SetExpiry(player, entry.Effect, expires);
        await AddEffectAsync(player, definition, duration, powers).ConfigureAwait(false);
        return true;
    }

    public static async ValueTask RestoreAsync(Player player)
    {
        if (player.SelectedCharacter is null || player.Attributes is null)
        {
            return;
        }
        foreach (var entry in PremiumBuffCatalog.Items)
        {
            if (ReadExpiry(player, entry.Effect) is not { } expires)
            {
                continue;
            }
            var remaining = expires - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                SetExpiry(player, entry.Effect, null);
                continue;
            }
            if (player.MagicEffectList.ContainsEffect(entry.Effect))
            {
                continue;
            }
            if (player.GameContext.Configuration.MagicEffects.FirstOrDefault(effect => effect.Number == entry.Effect) is { } definition)
            {
                await AddEffectAsync(player, definition, remaining, CreatePowers(player, definition)).ConfigureAwait(false);
            }
        }
    }

    public static DateTimeOffset? ReadExpiry(Player player, byte effect)
    {
        var day = FindMarker(player, PremiumBuffStats.ExpiryDay(effect))?.Value ?? 0;
        var second = FindMarker(player, PremiumBuffStats.ExpirySecond(effect))?.Value ?? 0;
        return day > 0 ? DateTimeOffset.FromUnixTimeSeconds(((long)day * SecondsPerDay) + (long)second) : null;
    }

    public static async ValueTask CancelAsync(Player player, byte effect)
    {
        if (PremiumBuffCatalog.FindEffect(effect) is null || player.SelectedCharacter is null)
        {
            return;
        }
        SetExpiry(player, effect, null);
        if (player.MagicEffectList.TryGetEffect(effect, out var active))
        {
            await active.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async ValueTask AddEffectAsync(Player player, MagicEffectDefinition definition, TimeSpan duration, MagicEffect.ElementWithTarget[] powers)
    {
        var effect = new MagicEffect(duration, definition, powers);
        effect.EffectTimeOut += expired =>
        {
            if (ReadExpiry(player, (byte)expired.Id) is { } expires && DateTimeOffset.UtcNow >= expires)
            {
                SetExpiry(player, (byte)expired.Id, null);
            }
            return ValueTask.CompletedTask;
        };
        await player.MagicEffectList.AddEffectAsync(effect).ConfigureAwait(false);
    }

    private static MagicEffect.ElementWithTarget[] CreatePowers(Player player, MagicEffectDefinition definition) => definition.PowerUpDefinitions
        .Where(power => power.TargetAttribute is not null && power.Boost is not null)
        .Select(power => new MagicEffect.ElementWithTarget(player.Attributes!.CreateElement(power), power.TargetAttribute!))
        .ToArray();

    private static StatAttribute? FindMarker(Player player, AttributeDefinition definition) => player.SelectedCharacter?.Attributes
        .FirstOrDefault(attribute => attribute.Definition == definition);

    private static void SetExpiry(Player player, byte effect, DateTimeOffset? expires)
    {
        var seconds = expires?.ToUnixTimeSeconds() ?? 0;
        SetMarker(player, PremiumBuffStats.ExpiryDay(effect), seconds / SecondsPerDay);
        SetMarker(player, PremiumBuffStats.ExpirySecond(effect), seconds % SecondsPerDay);
    }

    private static void SetMarker(Player player, AttributeDefinition definition, long value)
    {
        if (FindMarker(player, definition) is { } marker)
        {
            marker.Value = value;
            return;
        }
        if (value == 0)
        {
            return;
        }
        var configured = player.GameContext.Configuration.Attributes.FirstOrDefault(attribute => attribute == definition)
            ?? throw new InvalidOperationException("Premium buff configuration update is required before activation.");
        player.SelectedCharacter!.Attributes.Add(player.PersistenceContext.CreateNew<StatAttribute>(configured, (float)value));
    }
}

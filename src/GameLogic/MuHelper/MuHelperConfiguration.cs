// <copyright file="MuHelperConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MuHelper;

/// <summary>
/// Configuration for the <see cref="MuHelper"/>.
/// </summary>
public class MuHelperConfiguration
{
    /// <summary>
    /// Gets or sets the cost of the helper per stage. This value is applied per
    /// <see cref="PayInterval"/>, and multiplied with the total character level.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MuHelperConfiguration_CostPerStage_Name), Description = nameof(PlugInResources.MuHelperConfiguration_CostPerStage_Description))]
    public IList<int> CostPerStage { get; set; } = new List<int>
    {
        20, 50, 80, 100, 120,
    };

    /// <summary>
    /// Gets or sets the pay interval.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MuHelperConfiguration_PayInterval_Name))]
    public TimeSpan PayInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the stage interval.
    /// After each interval, the stage gets increased to the next level with
    /// usually increasing costs.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MuHelperConfiguration_StageInterval_Name), Description = nameof(PlugInResources.MuHelperConfiguration_StageInterval_Description))]
    public TimeSpan StageInterval { get; set; } = TimeSpan.FromMinutes(200);

    /// <summary>
    /// Gets or sets the minimum character level.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MuHelperConfiguration_MinLevel_Name))]
    public int MinLevel { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum character level.
    /// </summary>
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MuHelperConfiguration_MaxLevel_Name))]
    public int MaxLevel { get; set; } = 400;

    /// <summary>
    /// Gets or sets the id of the game server (internal server id; the x5 experience server is 1) which has its own minimum level.
    /// </summary>
    [Display(Name = "Server with its own minimum level", Description = "The internal id of the game server (the x5 experience server = 1) which uses the minimum level below instead of the general one.")]
    public byte LimitedServerId { get; set; } = 1;

    /// <summary>
    /// Gets or sets the minimum character level on the server <see cref="LimitedServerId"/>.
    /// </summary>
    [Display(Name = "Minimum level on that server", Description = "The minimum character level for the MU Helper on the server above.")]
    public int LimitedServerMinLevel { get; set; } = 50;

    /// <summary>
    /// Gets the minimum character level on the given game server.
    /// </summary>
    /// <param name="serverId">The internal id of the game server.</param>
    /// <returns>The minimum character level.</returns>
    public int GetMinLevel(byte serverId)
        => serverId == this.LimitedServerId ? Math.Max(this.MinLevel, this.LimitedServerMinLevel) : this.MinLevel;
}
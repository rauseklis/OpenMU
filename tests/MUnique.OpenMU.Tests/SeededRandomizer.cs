// <copyright file="SeededRandomizer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic;

/// <summary>
/// Reproducible random draws for real drop-generator simulations.
/// </summary>
internal sealed class SeededRandomizer(int seed) : IRandomizer
{
    private readonly Random _random = new(seed);

    public bool NextRandomBool() => this._random.Next(2) == 0;
    public bool NextRandomBool(int percent) => this.NextRandomBool(percent / 100.0);
    public bool NextRandomBool(int chance, int basis) => this.NextRandomBool((double)chance / basis);
    public bool NextRandomBool(double chance) => this._random.NextDouble() < chance;
    public int NextInt(int min, int max) => this._random.Next(min, max);
    public int NextInt(uint min, uint max) => (int)this._random.NextInt64(min, max);
    public uint NextUInt(uint min, uint max) => (uint)this._random.NextInt64(min, max);
    public double NextDouble() => this._random.NextDouble();
}

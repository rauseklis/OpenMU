// <copyright file="BatchATests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.WebsiteApi.Tests;

/// <summary>
/// Tests the read-only endpoints of batch A (rankings by kills, online list, bans, search, castle siege, unsupported features)
/// at the HTTP layer with an in-memory store.
/// </summary>
[TestFixture]
public class BatchATests
{
    private FakeStore _store = null!;
    private FakeStatus _status = null!;
    private ApiServer _api = null!;

    /// <summary>Starts a fresh API for every test.</summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task StartAsync()
    {
        this._store = new FakeStore();
        this._status = new FakeStatus();
        this._api = await ApiServer.StartAsync(this._store, this._status);
    }

    /// <summary>Stops the API.</summary>
    /// <returns>The task.</returns>
    [TearDown]
    public async Task StopAsync() => await this._api.DisposeAsync();

    /// <summary>
    /// The killers ranking has the value shape; the rankings the database cannot count answer 404 not_supported and unknown ones not_found.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task RankingsByKillsAndUnsupportedRankings()
    {
        var killers = await this._api.SendAsync("GET", "/api/v1/rankings/killers?limit=5");
        Assert.That(killers.Status, Is.EqualTo(200));
        Assert.That(killers.Json[0].EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "rank", "name", "class", "value", "guild" }));
        Assert.That(killers.Json[0].GetProperty("value").GetInt32(), Is.EqualTo(7));

        foreach (var kind in new[] { "online-time", "gens", "bc", "ds", "cc", "duel" })
        {
            var response = await this._api.SendAsync("GET", "/api/v1/rankings/" + kind);
            Assert.That((response.Status, response.Error), Is.EqualTo((404, "not_supported")), kind);
        }

        Assert.That((await this._api.SendAsync("GET", "/api/v1/rankings/whatever")).Error, Is.EqualTo("not_found"));
        Assert.That((await this._api.SendAsync("GET", "/api/v1/rankings/players")).Status, Is.EqualTo(200), "the existing rankings still work");
        Assert.That((await this._api.SendAsync("GET", "/api/v1/events")).Error, Is.EqualTo("not_supported"));
    }

    /// <summary>
    /// The online list shows the characters the status source reports.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ListsOnlineCharacters()
    {
        Assert.That((await this._api.SendAsync("GET", "/api/v1/online")).Json.GetArrayLength(), Is.EqualTo(0));

        this._status.Online.Add("Hero");
        var online = (await this._api.SendAsync("GET", "/api/v1/online")).Json;
        Assert.That(online.GetArrayLength(), Is.EqualTo(1));
        Assert.That(online[0].EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "name", "class", "level", "guild" }));
    }

    /// <summary>
    /// Bans list character names with no reason and no end date, never an account name.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ListsBannedCharacters()
    {
        var bans = (await this._api.SendAsync("GET", "/api/v1/bans")).Json;
        Assert.That(bans[0].EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "name", "reason", "until" }));
        Assert.That(bans[0].GetProperty("name").GetString(), Is.EqualTo("Cheater"));
        Assert.That(bans[0].GetProperty("reason").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null));
        Assert.That(bans[0].GetProperty("until").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null));
    }

    /// <summary>
    /// Search needs 2 to 20 characters and answers with at most the two lists.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SearchesCharactersAndGuilds()
    {
        var found = (await this._api.SendAsync("GET", "/api/v1/search?q=Her")).Json;
        Assert.That(found.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "characters", "guilds" }));
        Assert.That(found.GetProperty("characters")[0].GetString(), Is.EqualTo("Hero"));

        Assert.That((await this._api.SendAsync("GET", "/api/v1/search?q=Gui")).Json.GetProperty("guilds")[0].GetString(), Is.EqualTo("Guild1"));

        foreach (var query in new[] { string.Empty, "a", new string('x', 21) })
        {
            var bad = await this._api.SendAsync("GET", "/api/v1/search?q=" + query);
            Assert.That((bad.Status, bad.Error), Is.EqualTo((422, "invalid_input")), query);
        }

        Assert.That((await this._api.SendAsync("GET", "/api/v1/search")).Error, Is.EqualTo("invalid_input"));
    }

    /// <summary>
    /// The castle siege answer has the contract shape, with <c>nextSiege</c> always null.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ShowsTheCastleSiege()
    {
        var siege = (await this._api.SendAsync("GET", "/api/v1/castle-siege")).Json;
        Assert.That(siege.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "owner", "nextSiege", "registered" }));
        Assert.That(siege.GetProperty("owner").GetProperty("guild").GetString(), Is.EqualTo("Guild1"));
        Assert.That(siege.GetProperty("owner").GetProperty("master").GetString(), Is.EqualTo("Hero"));
        Assert.That(siege.GetProperty("nextSiege").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null));
        Assert.That(siege.GetProperty("registered")[0].GetProperty("guild").GetString(), Is.EqualTo("Guild2"));
        Assert.That(siege.GetProperty("registered")[0].GetProperty("master").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null));
    }
}

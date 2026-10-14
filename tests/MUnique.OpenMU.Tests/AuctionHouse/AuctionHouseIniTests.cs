// <copyright file="AuctionHouseIniTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using System.IO;
using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Verifies the hand-editable <c>AuctionHouse.ini</c>: values are read over the stored configuration, a bad line is
/// reported and skipped without disturbing the rest, and the file is created from the current settings when absent.
/// </summary>
[TestFixture]
public class AuctionHouseIniTests
{
    private string _directory = string.Empty;

    /// <summary>
    /// Creates a scratch directory for the file tests.
    /// </summary>
    [SetUp]
    public void CreateDirectory()
    {
        this._directory = Path.Combine(Path.GetTempPath(), "auction-ini-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this._directory);
    }

    /// <summary>
    /// Removes the scratch directory.
    /// </summary>
    [TearDown]
    public void DeleteDirectory()
    {
        Directory.Delete(this._directory, true);
    }

    /// <summary>
    /// Verifies that values in the file replace the configuration's values and leave the others alone.
    /// </summary>
    [Test]
    public void ValuesInTheFileOverrideTheConfiguration()
    {
        var configuration = new AuctionHouseConfiguration();

        var result = AuctionHouseIni.Apply(configuration, "[General]\nEnabled=true\nPageSize=40\n[Fees]\nSaleFeeAmount=250000\n");

        Assert.That(result.Errors, Is.Empty);
        Assert.That(result.AppliedCount, Is.EqualTo(3));
        Assert.That(configuration.Enabled, Is.True);
        Assert.That(configuration.PageSize, Is.EqualTo(40));
        Assert.That(configuration.SaleFeeAmount, Is.EqualTo(250_000));
        Assert.That(configuration.MinimumCharacterLevel, Is.EqualTo(new AuctionHouseConfiguration().MinimumCharacterLevel));
    }

    /// <summary>
    /// Verifies that comments, blank lines, spacing and key case do not matter.
    /// </summary>
    [Test]
    public void CommentsBlankLinesSpacingAndCaseAreIgnored()
    {
        var configuration = new AuctionHouseConfiguration();
        var text = "; a comment\n# another\n\n[general]\n   pagesize   =   12   \n\r\nENABLED = Yes\n";

        var result = AuctionHouseIni.Apply(configuration, text);

        Assert.That(result.Errors, Is.Empty);
        Assert.That(configuration.PageSize, Is.EqualTo(12));
        Assert.That(configuration.Enabled, Is.True);
    }

    /// <summary>
    /// Verifies the accepted spellings of a true/false setting.
    /// </summary>
    /// <param name="spelling">The spelling in the file.</param>
    /// <param name="expected">The meaning.</param>
    [TestCase("true", true)]
    [TestCase("False", false)]
    [TestCase("1", true)]
    [TestCase("0", false)]
    [TestCase("yes", true)]
    [TestCase("No", false)]
    [TestCase("on", true)]
    [TestCase("off", false)]
    public void BooleansAcceptCommonSpellings(string spelling, bool expected)
    {
        var configuration = new AuctionHouseConfiguration { NotificationsEnabled = !expected };

        var result = AuctionHouseIni.Apply(configuration, "NotificationsEnabled=" + spelling);

        Assert.That(result.Errors, Is.Empty);
        Assert.That(configuration.NotificationsEnabled, Is.EqualTo(expected));
    }

    /// <summary>
    /// Verifies that a bad line is reported with its line number, is skipped, and does not stop the rest.
    /// </summary>
    [Test]
    public void BadValueIsReportedAndSkippedWithoutStoppingTheRest()
    {
        var configuration = new AuctionHouseConfiguration();
        var original = configuration.PageSize;

        var result = AuctionHouseIni.Apply(configuration, "PageSize=lots\nMinimumCharacterLevel=20\n");

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0], Does.Contain("line 1").And.Contain("PageSize"));
        Assert.That(configuration.PageSize, Is.EqualTo(original));
        Assert.That(configuration.MinimumCharacterLevel, Is.EqualTo(20));
    }

    /// <summary>
    /// Verifies that values outside the sane range of a setting are rejected.
    /// </summary>
    /// <param name="line">The line.</param>
    [TestCase("PageSize=0")]
    [TestCase("PageSize=100000")]
    [TestCase("MinimumCharacterLevel=0")]
    [TestCase("SaleFeeAmount=-1")]
    [TestCase("MaximumPriceComponents=0")]
    [TestCase("MaximumPriceComponents=14")]
    [TestCase("ExpiryIntervalSeconds=0")]
    public void OutOfRangeValueIsRejected(string line)
    {
        var configuration = new AuctionHouseConfiguration();

        var result = AuctionHouseIni.Apply(configuration, line);

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.AppliedCount, Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies that a setting the server does not know is reported.
    /// </summary>
    [Test]
    public void UnknownSettingIsReported()
    {
        var result = AuctionHouseIni.Apply(new AuctionHouseConfiguration(), "[General]\nColour=blue\n");

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0], Does.Contain("Colour"));
    }

    /// <summary>
    /// Verifies that a line which is not <c>key=value</c> is reported.
    /// </summary>
    [Test]
    public void LineWithoutEqualsSignIsReported()
    {
        var result = AuctionHouseIni.Apply(new AuctionHouseConfiguration(), "PageSize 20\n");

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0], Does.Contain("line 1"));
    }

    /// <summary>
    /// Verifies the sale fee currency setting, including the reserved wCoin value.
    /// </summary>
    [Test]
    public void SaleFeeCurrencyAcceptsZenAndWCoinOnly()
    {
        var configuration = new AuctionHouseConfiguration();

        var wcoin = AuctionHouseIni.Apply(configuration, "SaleFeeCurrency=wcoin");
        var invalid = AuctionHouseIni.Apply(configuration, "SaleFeeCurrency=Gold");

        Assert.That(wcoin.Errors, Is.Empty);
        Assert.That(configuration.SaleFeeCurrency, Is.EqualTo(SaleFeeCurrency.WCoin));
        Assert.That(invalid.Errors, Has.Count.EqualTo(1));
        Assert.That(configuration.SaleFeeCurrency, Is.EqualTo(SaleFeeCurrency.WCoin));
    }

    /// <summary>
    /// Verifies that a minimum above its maximum is rejected and neither value changes.
    /// </summary>
    [Test]
    public void MinimumAboveMaximumIsRejectedAndLeavesBothUnchanged()
    {
        var configuration = new AuctionHouseConfiguration();
        var minimum = configuration.ZenMinimumPrice;
        var maximum = configuration.ZenMaximumPrice;

        var result = AuctionHouseIni.Apply(configuration, "ZenMinimum=9000000\nZenMaximum=5000\n");

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0], Does.Contain("Zen"));
        Assert.That(configuration.ZenMinimumPrice, Is.EqualTo(minimum));
        Assert.That(configuration.ZenMaximumPrice, Is.EqualTo(maximum));
    }

    /// <summary>
    /// Verifies that the price settings reach the options as price limits and the sale fee.
    /// </summary>
    [Test]
    public void PriceAndFeeSettingsReachTheOptions()
    {
        var configuration = new AuctionHouseConfiguration();

        AuctionHouseIni.Apply(configuration, "MaximumPriceComponents=3\nJewelMaximum=500\nSaleFeeAmount=7");
        var options = configuration.ToOptions();

        Assert.That(options.PriceLimits.MaximumComponents, Is.EqualTo(3));
        Assert.That(options.PriceLimits.JewelMaximum, Is.EqualTo(500));
        Assert.That(options.SaleFee, Is.EqualTo(new SaleFee(SaleFeeCurrency.Zen, 7)));
    }

    /// <summary>
    /// Verifies that the text written for a configuration reads back as the same configuration.
    /// </summary>
    [Test]
    public void CreatedTextReadsBackAsTheSameConfiguration()
    {
        var source = new AuctionHouseConfiguration
        {
            Enabled = true,
            MinimumCharacterLevel = 50,
            MinimumAccountAgeHours = 3,
            PageSize = 17,
            MaximumActiveListingsPerAccount = 21,
            MaximumActiveListingsPerCharacter = 9,
            SaleFeeCurrency = SaleFeeCurrency.WCoin,
            SaleFeeAmount = 123,
            ListingFeeBasisPoints = 50,
            SuccessFeeBasisPoints = 250,
            MaximumPriceComponents = 4,
            ZenMinimumPrice = 2000,
            ZenMaximumPrice = 3_000_000,
            JewelMinimumPrice = 3,
            JewelMaximumPrice = 4000,
            FruitMinimumPrice = 2,
            FruitMaximumPrice = 6000,
            ExpiryIntervalSeconds = 30,
            ExpiryBatchSize = 60,
            NotificationsEnabled = true,
            NotificationBatchSize = 70,
            OperationRetentionDays = 8,
            PurgeBatchSize = 600,
        };

        var copy = new AuctionHouseConfiguration();
        var result = AuctionHouseIni.Apply(copy, AuctionHouseIni.CreateText(source));

        Assert.That(result.Errors, Is.Empty);
        Assert.That(copy, Is.EqualTo(source).Using<AuctionHouseConfiguration>(SameSettings));
    }

    /// <summary>
    /// Verifies that the written file documents every setting the reader understands.
    /// </summary>
    [Test]
    public void CreatedTextListsEverySetting()
    {
        var text = AuctionHouseIni.CreateText(new AuctionHouseConfiguration());

        foreach (var key in AuctionHouseIni.SettingNames)
        {
            Assert.That(text, Does.Contain(key + "="), key);
        }

        Assert.That(AuctionHouseIni.SettingNames, Has.Count.EqualTo(24));
    }

    /// <summary>
    /// Verifies that a missing file is created from the current settings and read as-is.
    /// </summary>
    [Test]
    public void MissingFileIsCreatedFromTheCurrentSettings()
    {
        var path = Path.Combine(this._directory, "AuctionHouse.ini");
        var configuration = new AuctionHouseConfiguration { PageSize = 33 };
        var file = new AuctionHouseIniFile(path);

        var result = file.RefreshIfChanged(configuration);

        Assert.That(File.Exists(path), Is.True);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Errors, Is.Empty);
        Assert.That(File.ReadAllText(path), Does.Contain("PageSize=33"));
        Assert.That(configuration.PageSize, Is.EqualTo(33));
    }

    /// <summary>
    /// Verifies that the file is read again only after it changes.
    /// </summary>
    [Test]
    public void FileIsReadAgainOnlyWhenItChanges()
    {
        var path = Path.Combine(this._directory, "AuctionHouse.ini");
        File.WriteAllText(path, "PageSize=20\n");
        var configuration = new AuctionHouseConfiguration();
        var file = new AuctionHouseIniFile(path);

        var first = file.RefreshIfChanged(configuration);
        var second = file.RefreshIfChanged(configuration);
        File.WriteAllText(path, "PageSize=45\n");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        var third = file.RefreshIfChanged(configuration);

        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Null);
        Assert.That(third, Is.Not.Null);
        Assert.That(configuration.PageSize, Is.EqualTo(45));
    }

    /// <summary>
    /// Verifies that an unreadable file leaves the configuration alone and is reported.
    /// </summary>
    [Test]
    public void BadLinesInTheFileAreReportedByTheRefresh()
    {
        var path = Path.Combine(this._directory, "AuctionHouse.ini");
        File.WriteAllText(path, "PageSize=nope\nExpiryBatchSize=5\n");
        var configuration = new AuctionHouseConfiguration();

        var result = new AuctionHouseIniFile(path).RefreshIfChanged(configuration);

        Assert.That(result!.Errors, Has.Count.EqualTo(1));
        Assert.That(configuration.ExpiryBatchSize, Is.EqualTo(5));
    }

    private static bool SameSettings(AuctionHouseConfiguration left, AuctionHouseConfiguration right)
    {
        return AuctionHouseIni.CreateText(left) == AuctionHouseIni.CreateText(right);
    }
}

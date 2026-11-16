using System;
using System.Collections.Generic;
using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class OptionResetTests
{
    private sealed class FakeOption : IResettableOption
    {
        public FakeOption(string name, int defaultIndex = 0, int valueCount = 2, bool hasUi = true, bool throws = false)
        {
            Name = name;
            DefaultIndex = defaultIndex;
            ValueCount = valueCount;
            HasUi = hasUi;
            Throws = throws;
        }

        public string Name { get; }
        public int DefaultIndex { get; }
        public int ValueCount { get; }
        public bool HasUi { get; }
        public bool Throws { get; }
        public int? AppliedIndex { get; private set; }
        public bool UiRefreshed { get; private set; }

        public void ApplyIndex(int index)
        {
            if (Throws)
            {
                throw new InvalidOperationException("boom");
            }
            AppliedIndex = index;
        }

        public void RefreshUi() => UiRefreshed = true;
    }

    [Theory]
    [InlineData(1, 3, 1)]   // valid default is used as-is
    [InlineData(0, 3, 0)]
    [InlineData(-1, 3, 0)]  // game uses -1 for "no default found" -> first value
    [InlineData(5, 3, 0)]   // out of range -> first value (same fallback the game logs)
    [InlineData(0, 0, -1)]  // no values at all -> nothing to apply
    public void ResolveDefaultIndex(int defaultIndex, int count, int expected)
    {
        Assert.Equal(expected, OptionReset.ResolveDefaultIndex(defaultIndex, count));
    }

    [Fact]
    public void ResetAll_AppliesDefaults_RefreshesUi_AndSkipsExcluded()
    {
        var a = new FakeOption("Clash of Might Enabled", defaultIndex: 1);
        var b = new FakeOption("Debug Logging", defaultIndex: 0);
        var reset = new FakeOption("Reset To Defaults");

        var count = OptionReset.ResetAll(new[] { a, b, reset }, "Reset To Defaults", null);

        Assert.Equal(2, count);
        Assert.Equal(1, a.AppliedIndex);
        Assert.Equal(0, b.AppliedIndex);
        Assert.True(a.UiRefreshed);
        Assert.Null(reset.AppliedIndex);
        Assert.False(reset.UiRefreshed);
    }

    [Fact]
    public void ResetAll_WithoutUi_StillAppliesValue()
    {
        var a = new FakeOption("A", defaultIndex: 1, hasUi: false);

        OptionReset.ResetAll(new[] { a }, null, null);

        Assert.Equal(1, a.AppliedIndex);
    }

    [Fact]
    public void ResetAll_SkipsOptionsWithoutValues()
    {
        var empty = new FakeOption("Empty", valueCount: 0);

        var count = OptionReset.ResetAll(new[] { empty }, null, null);

        Assert.Equal(0, count);
        Assert.Null(empty.AppliedIndex);
    }

    [Fact]
    public void ResetAll_OneFailingOption_DoesNotStopTheOthers_AndIsLogged()
    {
        var lines = new List<string>();
        var log = new DmcsLog(lines.Add);
        var bad = new FakeOption("Bad", throws: true);
        var good = new FakeOption("Good", defaultIndex: 1);

        var count = OptionReset.ResetAll(new[] { bad, good }, null, log);

        Assert.Equal(1, count);
        Assert.Equal(1, good.AppliedIndex);
        Assert.Contains(lines, l => l.Contains("Bad") && l.Contains("boom"));
    }

    [Fact]
    public void ResetAll_NullList_ReturnsZero()
    {
        Assert.Equal(0, OptionReset.ResetAll(null, null, null));
    }
}

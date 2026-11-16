using System.Collections.Generic;
using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class DmcsLogTests
{
    private static (DmcsLog log, List<string> lines) NewLog()
    {
        var lines = new List<string>();
        return (new DmcsLog(lines.Add), lines);
    }

    [Fact]
    public void Debug_IsDropped_WhenDebugLoggingIsOff()
    {
        var (log, lines) = NewLog();

        log.Debug("Clash", "start");

        Assert.Empty(lines);
    }

    [Fact]
    public void Debug_IsWritten_WithPrefixAndChannel_WhenDebugLoggingIsOn()
    {
        var (log, lines) = NewLog();
        log.DebugEnabled = true;

        log.Debug("Clash", "start");

        Assert.Equal(new[] { "[DMCS][Clash] start" }, lines);
    }

    [Fact]
    public void Info_IsAlwaysWritten_EvenWithDebugLoggingOff()
    {
        var (log, lines) = NewLog();

        log.Info("loaded");

        Assert.Equal(new[] { "[DMCS] loaded" }, lines);
    }

    [Fact]
    public void NullSink_DoesNotThrow()
    {
        var log = new DmcsLog(null) { DebugEnabled = true };

        log.Info("a");
        log.Debug("b", "c");
    }

    [Fact]
    public void ThrowingSink_IsSwallowed_SoLoggingNeverBreaksGameplay()
    {
        var log = new DmcsLog(_ => throw new System.InvalidOperationException()) { DebugEnabled = true };

        log.Info("a");
        log.Debug("b", "c");
    }
}

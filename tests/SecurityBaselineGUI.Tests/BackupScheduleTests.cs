using SecurityBaselineGUI.Core.Models;
using SecurityBaselineGUI.Core.Services;
using Xunit;

namespace SecurityBaselineGUI.Tests;

public sealed class BackupScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsDue_Disabled_ReturnsFalse()
    {
        var settings = new BackupSettings { Enabled = false, DestinationDirectory = @"C:\backup", LastBackupUtc = null };
        Assert.False(BackupSchedule.IsDue(settings, Now));
    }

    [Fact]
    public void IsDue_NoDestination_ReturnsFalse()
    {
        var settings = new BackupSettings { Enabled = true, DestinationDirectory = null };
        Assert.False(BackupSchedule.IsDue(settings, Now));
    }

    [Fact]
    public void IsDue_NeverBackedUp_ReturnsTrue()
    {
        var settings = new BackupSettings { Enabled = true, DestinationDirectory = @"C:\backup", LastBackupUtc = null };
        Assert.True(BackupSchedule.IsDue(settings, Now));
    }

    [Fact]
    public void IsDue_IntervalElapsed_ReturnsTrue()
    {
        var settings = new BackupSettings
        {
            Enabled = true,
            DestinationDirectory = @"C:\backup",
            IntervalHours = 24,
            LastBackupUtc = Now.AddHours(-25),
        };
        Assert.True(BackupSchedule.IsDue(settings, Now));
    }

    [Fact]
    public void IsDue_IntervalNotYetElapsed_ReturnsFalse()
    {
        var settings = new BackupSettings
        {
            Enabled = true,
            DestinationDirectory = @"C:\backup",
            IntervalHours = 24,
            LastBackupUtc = Now.AddHours(-1),
        };
        Assert.False(BackupSchedule.IsDue(settings, Now));
    }
}

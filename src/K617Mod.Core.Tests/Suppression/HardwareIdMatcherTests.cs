using K617Mod.Core.Suppression;
using Xunit;

namespace K617Mod.Core.Tests.Suppression;

public class HardwareIdMatcherTests
{
    [Fact]
    public void RealK617HardwareId_Matches()
    {
        // Same shape hardware ID string that showed up in the "Unable to
        // open HID class device" error messages during Part 1 testing.
        var id = @"HID\VID_2E3C&PID_C365&MI_01&Col01\8&1D157207&0&0000";
        Assert.True(HardwareIdMatcher.IsK617(id));
    }

    [Fact]
    public void CaseDoesNotMatter()
    {
        var id = @"hid\vid_2e3c&pid_c365&mi_01";
        Assert.True(HardwareIdMatcher.IsK617(id));
    }

    [Fact]
    public void DifferentDevice_DoesNotMatch()
    {
        // An unrelated device's VID/PID, for contrast.
        var id = @"HID\VID_046D&PID_C52B&MI_00";
        Assert.False(HardwareIdMatcher.IsK617(id));
    }

    [Fact]
    public void NullHardwareId_DoesNotMatch()
    {
        Assert.False(HardwareIdMatcher.IsK617(null));
    }

    [Fact]
    public void EmptyString_DoesNotMatch()
    {
        Assert.False(HardwareIdMatcher.IsK617(""));
    }

    [Fact]
    public void PartialVendorIdOnly_DoesNotFalsePositive()
    {
        // Guards against a substring match that's too loose - a device
        // sharing only the vendor ID but not the product ID should not
        // be treated as the K617.
        var id = @"HID\VID_2E3C&PID_0001&MI_00";
        Assert.False(HardwareIdMatcher.IsK617(id));
    }
}

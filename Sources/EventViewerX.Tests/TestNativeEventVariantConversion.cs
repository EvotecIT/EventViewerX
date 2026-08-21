using System.Runtime.InteropServices;
using EventViewerX.Native;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestNativeEventVariantConversion {
    private const uint ArrayFlag = 0x80;

    [Fact]
    public void HexScalarsRemainUnsigned() {
        var hex32 = new WindowsEventNativeMethods.EventVariant {
            UInt32Value = uint.MaxValue,
            Type = (uint)WindowsEventNativeMethods.VariantType.HexInt32
        };
        var hex64 = new WindowsEventNativeMethods.EventVariant {
            UInt64Value = ulong.MaxValue,
            Type = (uint)WindowsEventNativeMethods.VariantType.HexInt64
        };

        Assert.Equal(uint.MaxValue, WindowsEventPayloadRenderer.ReadValue(hex32));
        Assert.Equal(ulong.MaxValue, WindowsEventPayloadRenderer.ReadValue(hex64));
    }

    [Fact]
    public void NumericAndBooleanArraysKeepTheirNativeTypes() {
        var integers = new[] { int.MinValue, -1, 0, 42, int.MaxValue };
        var doubles = new[] { double.MinValue, -1.25, 0, 42.5, double.MaxValue };
        var booleans = new[] { 0, 1, -1, 0 };

        Assert.Equal(
            integers,
            ReadArray<int, int[]>(
                integers,
                WindowsEventNativeMethods.VariantType.Int32,
                static (pointer, values) => Marshal.Copy(values, 0, pointer, values.Length)));
        Assert.Equal(
            doubles,
            ReadArray<double, double[]>(
                doubles,
                WindowsEventNativeMethods.VariantType.Double,
                static (pointer, values) => Marshal.Copy(values, 0, pointer, values.Length)));
        Assert.Equal(
            new[] { false, true, true, false },
            ReadArray<int, bool[]>(
                booleans,
                WindowsEventNativeMethods.VariantType.Boolean,
                static (pointer, values) => Marshal.Copy(values, 0, pointer, values.Length)));
    }

    [Fact]
    public void SystemTimeUsesTheSameLocalDateTimeShapeAsEventRecords() {
        var native = new WindowsEventNativeMethods.SystemTime {
            Year = 2026,
            Month = 7,
            Day = 23,
            Hour = 12,
            Minute = 34,
            Second = 56,
            Milliseconds = 789
        };
        IntPtr pointer = Marshal.AllocHGlobal(
            Marshal.SizeOf<WindowsEventNativeMethods.SystemTime>());
        try {
            Marshal.StructureToPtr(native, pointer, fDeleteOld: false);
            var variant = new WindowsEventNativeMethods.EventVariant {
                PointerValue = pointer,
                Type = (uint)WindowsEventNativeMethods.VariantType.SystemTime
            };

            DateTime actual = Assert.IsType<DateTime>(
                WindowsEventPayloadRenderer.ReadValue(variant));
            DateTime expected = new DateTime(
                2026,
                7,
                23,
                12,
                34,
                56,
                789,
                DateTimeKind.Utc).ToLocalTime();
            Assert.Equal(expected, actual);
        } finally {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private static TExpected ReadArray<TInput, TExpected>(
        TInput[] values,
        WindowsEventNativeMethods.VariantType type,
        Action<IntPtr, TInput[]> copy) {

        int elementSize = Marshal.SizeOf<TInput>();
        IntPtr pointer = Marshal.AllocHGlobal(checked(elementSize * values.Length));
        try {
            copy(pointer, values);
            var variant = new WindowsEventNativeMethods.EventVariant {
                PointerValue = pointer,
                Count = checked((uint)values.Length),
                Type = (uint)type | ArrayFlag
            };
            return Assert.IsType<TExpected>(
                WindowsEventPayloadRenderer.ReadValue(variant));
        } finally {
            Marshal.FreeHGlobal(pointer);
        }
    }
}

using System.Reflection;
using Xunit;

namespace EventViewerX.Tests {
    public class TestPowerShellScriptExecutionInfo {
        [Fact]
        public void ResetStateClearsCounter() {
            var type = typeof(EventViewerX.PowerShellScriptExecutionInfo);
            var field = type.GetField("_executionCount", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);
            field!.SetValue(null, 5);
            var method = type.GetMethod("ResetState", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(method);
            method!.Invoke(null, null);
            Assert.Equal(0, (int)field.GetValue(null)!);
        }
    }
}

using System;

namespace EventViewerX.Examples {
    internal partial class Examples {
        public static void QueryPowerShellScripts() {
            foreach (var script in PowerShellEventEngine.GetPowerShellScripts(PowerShellEdition.WindowsPowerShell, format: true)) {
                var timeCreated = script.Event?.TimeCreated ?? DateTime.MinValue;
                Console.WriteLine($"[{timeCreated}] {script.ScriptBlockId}");
                Console.WriteLine(script.Script);
                // Save reconstructed script to disk
                script.Save(@"C:\Temp\Scripts");
            }
        }
    }
}

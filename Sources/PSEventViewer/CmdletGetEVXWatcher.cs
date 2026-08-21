using System;
using System.Linq;
using System.Management.Automation;
using EventViewerX;

namespace PSEventViewer {
    /// <summary>
    /// <para type="synopsis">Retrieves information about active EVX watchers.</para>
    /// <para type="description">Filters by watcher Id or Name and returns watcher metadata such as log, machine, filters, and runtime state.</para>
    /// </summary>
    /// <example>
    ///   <summary>List all watchers</summary>
    ///   <code>Get-EVXWatcher</code>
    ///   <para>Shows every currently running watcher.</para>
    /// </example>
    /// <example>
    ///   <summary>Filter by name</summary>
    ///   <code>Get-EVXWatcher -Name SecurityWatcher</code>
    ///   <para>Returns only watchers whose name matches SecurityWatcher.</para>
    /// </example>
    /// <example>
    ///   <summary>Select by Id</summary>
    ///   <code>Get-EVXWatcher -Id 'd9b0e4d1-2d0e-4fa2-9b8f-5b6d2a0ad111'</code>
    ///   <para>Retrieves a specific watcher instance using its identifier.</para>
    /// </example>
    [Cmdlet(VerbsCommon.Get, "EVXWatcher")]
    [OutputType(typeof(WatcherInfo))]
    public sealed class CmdletGetEVXWatcher : PSCmdlet {
        /// <summary>
        /// Identifiers of watchers to retrieve.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipelineByPropertyName = true)]
        public Guid[] Id { get; set; } = Array.Empty<Guid>();

        /// <summary>
        /// Name of the watcher to return.
        /// </summary>
        [Parameter]
        public string? Name { get; set; }

        /// <summary>
        /// Outputs watcher information matching the provided parameters.
        /// </summary>
        protected override void ProcessRecord() {
            var watchers = WatcherManager.GetWatchers(Name);
            if (watchers == null) {
                return;
            }
            if (Id != null && Id.Length > 0) {
                watchers = watchers.Where(w => Id.Contains(w.Id)).ToList();
            }
            foreach (var watcher in watchers) {
                WriteObject(watcher);
            }
        }
    }
}

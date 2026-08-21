using System;
using System.Data;
using System.Linq;
using EventViewerX.Helpers;

namespace EventViewerX.Examples {
    internal partial class Examples {
        public static void DataTableConversion() {
            var events = EventLogEngine.ReadChannel(
                    "Application",
                    new EventFilter { EventIds = [1000] },
                    options: new EventLogQueryOptions { MaxEvents = 5 })
                .ToList();
            DataTable table = events.ToDataTable();
            Console.WriteLine($"Converted rows: {table.Rows.Count}");
        }
    }
}

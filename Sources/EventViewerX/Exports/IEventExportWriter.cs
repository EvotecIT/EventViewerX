using System;

namespace EventViewerX.Exports;

internal interface IEventExportWriter : IDisposable {
    void Write(EventObject eventObject);
    void Complete();
}

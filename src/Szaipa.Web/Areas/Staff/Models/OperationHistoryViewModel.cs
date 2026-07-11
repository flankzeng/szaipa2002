using Szaipa.Data.Services.Admin;

namespace Szaipa.Web.Areas.Staff.Models;

public sealed class OperationHistoryViewModel
{
    public bool DataAvailable { get; init; }

    public string? UnavailableReason { get; init; }

    public OperationRecordPage History { get; init; } = new() { Page = 1, PageSize = 15 };
}

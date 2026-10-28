namespace Szaipa.Web.Services.Traffic;

public interface ITrafficEventStore
{
    Task<bool> RecordAsync(TrafficEventRequest request, string clientKey, CancellationToken cancellationToken);
    Task<TrafficSummary> ReadSummaryAsync(int days, int limit, CancellationToken cancellationToken);
}

public interface IJobQueue
{
    public Task SendJob(IJobItem item, CancellationToken ct);
    public Task FinishJob(CancellationToken ct);
}
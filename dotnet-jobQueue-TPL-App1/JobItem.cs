public class JobItem : IJobItem
{
    public string Id;
    public JobType ItemType;
    public DateTime CreateTime;
    public double LifeRate;
    public JobItem(string jobType)
    {
        Id = Guid.NewGuid().ToString().Substring(0, 4);
        if (!Enum.TryParse<JobType>(jobType, out ItemType)) ItemType = JobType.Unknown;
    }
    public double GetJobPriority() => Math.Ceiling((DateTime.Now - CreateTime).TotalSeconds * LifeRate);
    public override string ToString()
    {
        return $"{Id} - {ItemType} - {CreateTime}";
    }
}

public enum JobType { Fedex, UPS, Unknown }
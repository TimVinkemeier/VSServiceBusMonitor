namespace TimVinkemeier.VSServiceBusMonitor.Models
{
    public abstract class ServiceBusEntityStatus
    {
        public long ActiveCount { get; set; }

        public long DeadletterCount { get; set; }

        public string EntityName { get; set; }

        public string ShortDisplayName { get; set; }
    }
}

namespace TimVinkemeier.VSServiceBusMonitor.Models
{
    public class SubscriptionStatus : ServiceBusEntityStatus
    {
        public string TopicName { get; set; }
    }
}

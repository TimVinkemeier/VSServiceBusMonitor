using Newtonsoft.Json;

namespace TimVinkemeier.VSServiceBusMonitor.Models.Configuration
{
    public class SubscriptionDefinition
    {
        public DisplayMode Display { get; set; }

        public string ShortName { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string SubscriptionName { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string TopicName { get; set; }
    }
}

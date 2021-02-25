using System.Collections.Generic;

using Newtonsoft.Json;

namespace TimVinkemeier.VSServiceBusMonitor.Models
{
    public class Profile
    {
        [JsonProperty(Required = Required.Always)]
        public string ConnectionString { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Name { get; set; }

        public IReadOnlyList<QueueDefinition> Queues { get; set; }

        public GeneralSettings Settings { get; set; }

        public IReadOnlyList<SubscriptionDefinition> Subscriptions { get; set; }
    }
}

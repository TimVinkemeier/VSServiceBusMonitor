using Newtonsoft.Json;

namespace TimVinkemeier.VSServiceBusMonitor.Models
{
    public class QueueDefinition
    {
        public DisplayMode Display { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string QueueName { get; set; }

        public string ShortName { get; set; }
    }
}
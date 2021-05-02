using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace TimVinkemeier.VSServiceBusMonitor.Models
{
    public class Config
    {
        private static readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Converters = new List<JsonConverter>
            {
                new StringEnumConverter(true)
            },
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy
                {
                    OverrideSpecifiedNames = false
                }
            }
        };

        public static Config Empty { get; } = new Config
        {
            ActiveProfileName = "MyFirstProfile",
            DebugProfileName = "MyFirstProfile",
            Profiles = new List<Profile>
            {
                new Profile
                {
                    Name = "MyFirstProfile",
                    ConnectionString = "<your-connection-string>",
                    Queues = new List<QueueDefinition>
                    {
                        new QueueDefinition
                        {
                            QueueName ="<your-first-queue-name-to-monitor>"
                        },
                        new QueueDefinition
                        {
                            QueueName ="<your-second-queue-name-to-monitor>",
                            Display = DisplayMode.OnlyDlq,
                            ShortName = "<your-short-name>"
                        }
                    },
                    Subscriptions = new List<SubscriptionDefinition>
                    {
                        new SubscriptionDefinition
                        {
                            TopicName = "<your-topic-name>",
                            SubscriptionName="<your-first-subscription-name-in-topic-to-monitor>"
                        },
                        new SubscriptionDefinition
                        {
                            TopicName = "<your-topic-name>",
                            SubscriptionName="<your-second-subscription-name-in-topic-to-monitor>"
                        }
                    },
                    Settings = new GeneralSettings
                    {
                        RefreshIntervalMillis = 2500
                    }
                }
            },
            ProfileDefaultSettings = new GeneralSettings
            {
                RefreshIntervalMillis = 5000
            },
            Settings = new ConfigSettings
            {
                NoColorization = false
            }
        };

        [JsonProperty(Required = Required.DisallowNull)]
        public string ActiveProfileName { get; set; }

        public string DebugProfileName { get; set; }

        public GeneralSettings ProfileDefaultSettings { get; set; }

        [JsonProperty(Required = Required.Always)]
        public IReadOnlyList<Profile> Profiles { get; set; }

        [JsonProperty(Order = 0, PropertyName = "$schema")]
        public string Schema => "https://raw.githubusercontent.com/TimVinkemeier/VSServiceBusMonitor/master/TimVinkemeier.VSServiceBusMonitor/configFileSchema.json";

        public ConfigSettings Settings { get; set; }

        public static Config LoadFromFile(string path)
        {
            return JsonConvert.DeserializeObject<Config>(File.ReadAllText(path), _serializerSettings);
        }

        public void WriteToFile(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, _serializerSettings));
        }
    }
}

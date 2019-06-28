using Newtonsoft.Json;

namespace AKBDLib.ResourceResolver
{
    public class UncShare
    {
        [JsonProperty(Required = Required.Always)]
        public string Name { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Path { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string CredentialsName { get; set; }

        [JsonProperty(Required = Required.Default)]
        public bool ForceLogin { get; set; }

        public bool NeedsCredentials()
        {
            return !string.IsNullOrWhiteSpace(CredentialsName);
        }
    }
}
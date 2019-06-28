using Newtonsoft.Json;
using System.Collections.Generic;

namespace AKBDLib.ResourceResolver
{
    public class Values
    {
        [JsonProperty(Required = Required.Always)]
        public string TeamCityUrl { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Gitlab { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string GitlabToken { get; set; }

        public string[] GitlabPublicGroups { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string BucketTicket { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string WikiUrl { get; set; }

        [JsonProperty(Required = Required.Always)]
        public IList<Credential> Credentials { get; set; }

        [JsonProperty(Required = Required.Always)]
        public IList<UncShare> Shares { get; set; }
    }
}
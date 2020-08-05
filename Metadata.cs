using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

namespace AstroModIntegrator
{
    public enum SyncMode
    {
        [EnumMember(Value = "serverclient")]
        ServerClient,
        [EnumMember(Value = "server")]
        Server,
        [EnumMember(Value = "client")]
        Client
    }

    public class Metadata
    {
        public string name;

        [DefaultValue("")]
        public string author;

        [DefaultValue("")]
        public string description;

        [JsonConverter(typeof(VersionConverter))]
        public Version version;

        [JsonConverter(typeof(VersionConverter))]
        public Version astro_build;

        [JsonConverter(typeof(StringEnumConverter))]
        public SyncMode sync;

        public string[] linked_actor_components;
    }
}

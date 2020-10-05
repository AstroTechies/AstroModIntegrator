﻿using System;
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
        [JsonProperty("name")]
        public string Name;

        [JsonProperty("mod_id")]
        public string ModID;

        [JsonProperty("author")]
        [DefaultValue("")]
        public string Author;

        [JsonProperty("description")]
        [DefaultValue("")]
        public string Description;

        [JsonProperty("version")]
        [JsonConverter(typeof(VersionConverter))]
        public Version ModVersion;

        [JsonProperty("sync")]
        [JsonConverter(typeof(StringEnumConverter))]
        public SyncMode Sync;

        [JsonProperty("homepage")]
        [DefaultValue("")]
        public string Homepage;

        [JsonProperty("linked_actor_components")]
        public Dictionary<string, List<string>> LinkedActorComponents;

        [JsonProperty("item_list_entries")]
        public Dictionary<string, Dictionary<string, List<string>>> ItemListEntries;
    }
}

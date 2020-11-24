using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace AstroModIntegrator
{
    public enum DownloadMode
    {
        [EnumMember(Value = "github_repository")]
        Repository,
        [EnumMember(Value = "index_file")]
        IndexFile,
    }

    public class DownloadInfo
    {
        [JsonProperty("type")]
        [JsonConverter(typeof(StringEnumConverter))]
        public DownloadMode Type;

        [JsonProperty("repository")]
        [DefaultValue("")]
        public string Repository;

        [JsonProperty("url")]
        [DefaultValue("")]
        public string URL;
    }

    public enum SyncMode
    {
        [EnumMember(Value = "serverclient")]
        ServerAndClient,
        [EnumMember(Value = "server")]
        ServerOnly,
        [EnumMember(Value = "client")]
        ClientOnly,
        [EnumMember(Value = "none")]
        None
    }

    public class Metadata : ICloneable
    {
        [JsonProperty("schema_version")]
        [DefaultValue(1)]
        public int SchemaVersion;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("mod_id")]
        [DefaultValue("")]
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

        [JsonProperty("astro_build")]
        [JsonConverter(typeof(VersionConverter))]
        public Version AstroBuild;

        [JsonProperty("sync")]
        [JsonConverter(typeof(StringEnumConverter))]
        public SyncMode Sync;

        [JsonProperty("homepage")]
        [DefaultValue("")]
        public string Homepage;

        [JsonProperty("download")]
        public DownloadInfo Download;

        [JsonProperty("linked_actor_components")]
        public Dictionary<string, List<string>> LinkedActorComponents;

        [JsonProperty("item_list_entries")]
        public Dictionary<string, Dictionary<string, List<string>>> ItemListEntries;

        [JsonProperty("persistent_actors")]
        public List<string> PersistentActors;

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}

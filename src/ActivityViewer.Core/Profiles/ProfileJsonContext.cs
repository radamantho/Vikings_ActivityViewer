using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ActivityViewer.Core.Profiles
{
    [JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
    [JsonSerializable(typeof(List<ServerProfile>))]
    internal partial class ProfileJsonContext : JsonSerializerContext
    {
    }
}

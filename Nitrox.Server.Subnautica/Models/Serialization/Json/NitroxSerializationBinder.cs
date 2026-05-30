using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Nitrox.Server.Subnautica.Models.Serialization.Json;

/// <summary>
///     Restricts Newtonsoft.Json type-name deserialization (<see cref="Newtonsoft.Json.TypeNameHandling" />) to
///     Nitrox-owned types only, preventing arbitrary .NET type instantiation from untrusted save data.
/// </summary>
/// <remarks>
///     Without this binder, <c>TypeNameHandling.Auto</c> allows any type reachable by the runtime to be instantiated
///     via a crafted <c>$type</c> field in JSON — a known remote code execution vector when loading untrusted save files.
/// </remarks>
public sealed class NitroxSerializationBinder : ISerializationBinder
{
    private static readonly string[] AllowedNamespacePrefixes =
    [
        "Nitrox.",
        "NitroxClient.",
        "NitroxPatcher."
    ];

    private static readonly DefaultSerializationBinder DefaultBinder = new();

    public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
    {
        DefaultBinder.BindToName(serializedType, out assemblyName, out typeName);
    }

    public Type BindToType(string? assemblyName, string typeName)
    {
        foreach (string prefix in AllowedNamespacePrefixes)
        {
            if (typeName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return DefaultBinder.BindToType(assemblyName, typeName);
            }
        }

        throw new JsonSerializationException($"Deserialization of type '{typeName}' is not permitted. Only Nitrox types are allowed in save data.");
    }
}

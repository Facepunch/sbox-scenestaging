using System.Text.Json.Nodes;
using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Stores a raw gameplay recording.
/// </summary>
public sealed record ProjectSourceClip( Guid Id, CompiledClip Clip, JsonObject? Metadata )
{

}

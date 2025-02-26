using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

[JsonConverter( typeof( CompiledClipConverter ) )]
partial record CompiledClip;

file sealed class CompiledClipConverter : JsonConverter<CompiledClip>
{
	public override CompiledClip Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		return JsonSerializer.Deserialize<ClipModel>( ref reader, options )!.Deserialize( options );
	}

	public override void Write( Utf8JsonWriter writer, CompiledClip value, JsonSerializerOptions options )
	{
		JsonSerializer.Serialize( new ClipModel( value, options ), options );
	}

	private record ClipModel(
		[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		ImmutableArray<TrackModel>? Tracks )
	{
		public ClipModel( CompiledClip clip, JsonSerializerOptions? options )
			: this( clip.Tracks is { Length: > 0 }
				? clip.Tracks.OrderBy( x => x.GetDepth() ).ThenBy( x => x.Name ).Select( x => new TrackModel( x, options ) ).ToImmutableArray()
				: null )
		{

		}

		public CompiledClip Deserialize( JsonSerializerOptions? options )
		{
			if ( Tracks is not { Length: > 0 } trackModels ) return CompiledClip.Empty;

			var trackDict = new Dictionary<Guid, CompiledTrack>();

			return new CompiledClip( [..trackModels.Select( x => x.Deserialize( trackDict, options ) )] );
		}
	}

	private record TrackModel( Guid Id, string Name, Type Type,
		[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		Guid? ParentId,
		[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		ImmutableArray<JsonObject>? Blocks )
	{
		public TrackModel( CompiledTrack track, JsonSerializerOptions? options )
			: this( track.Id, track.Name, track.TargetType, track.Parent?.Id,
				track.Blocks is { Length: > 0 }
					? track.Blocks.Select( x => SerializeBlock( x, options ) ).ToImmutableArray()
					: null )
		{

		}

		public CompiledTrack Deserialize( IReadOnlyDictionary<Guid, CompiledTrack> trackDict, JsonSerializerOptions? options )
		{
			// TODO: handle missing Type?

			return new CompiledTrack( Id, Name, Type,
				ParentId is { } parentId ? trackDict[parentId] : null,
				Blocks?.Select( x => DeserializeBlock( Type, x, options ) ).ToImmutableArray() ?? [] );
		}
	}

	private static JsonObject SerializeBlock( CompiledBlock block, JsonSerializerOptions? options ) =>
		JsonSerializer.SerializeToNode( block, block.GetType(), options )!.AsObject();

	private static CompiledBlock DeserializeBlock( Type targetType, JsonObject node, JsonSerializerOptions? options )
	{
		var kind = node[nameof(CompiledBlock.Kind)]?.GetValue<BlockKind>();
		Type blockType;

		switch ( kind )
		{
			case BlockKind.Action:
				blockType = typeof(ActionBlock);
				break;

			case BlockKind.Constant:
				blockType = TypeLibrary.GetType( typeof(ConstantBlock<>) ).MakeGenericType( [targetType] );
				break;

			case BlockKind.Sample:
				blockType = TypeLibrary.GetType( typeof(SampleBlock<>) ).MakeGenericType( [targetType] );
				break;

			default:
				throw new NotImplementedException();
		}

		return (CompiledBlock)node.Deserialize( blockType, options )!;
	}
}

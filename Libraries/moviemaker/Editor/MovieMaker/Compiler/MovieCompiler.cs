using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Sandbox.MovieMaker.Compiled;
using Sandbox.Resources;

namespace Editor.MovieMaker;

#nullable enable

[ResourceIdentity( "movie" )]
public sealed class MovieCompiler : ResourceCompiler
{
	private record ResourceModel(
		[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		CompiledClip? Compiled,
		[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		MovieProject? EditorData );

	protected override async Task<bool> Compile()
	{
		var source = await File.ReadAllTextAsync( Context.AbsolutePath );

		source = Context.ScanJson( source );

		var model = JsonSerializer.Deserialize<ResourceModel>( source, EditorJsonOptions )!;
		var compiled = model.EditorData?.Compile() ?? model.Compiled;

		model = new ResourceModel( compiled, null );

		Context.Data.Write( JsonSerializer.Serialize( model, EditorJsonOptions ) );

		return true;
	}
}

using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;
using Sandbox.Resources;

namespace Editor.MovieMaker;

#nullable enable

[ResourceIdentity( "movie" )]
public sealed class MovieCompiler : ResourceCompiler
{
	private record CompiledResourceModel( CompiledClip? Clip );

	protected override async Task<bool> Compile()
	{
		var source = await File.ReadAllTextAsync( Context.AbsolutePath );

		source = Context.ScanJson( source );

		var resource = JsonSerializer.Deserialize<MovieResource>( source, EditorJsonOptions )!;
		var project = resource.EditorData?.Deserialize<MovieProject>( EditorJsonOptions );

		var clip = project?.Compile() ?? resource.Clip;

		Context.Data.Write( JsonSerializer.Serialize( new CompiledResourceModel( clip ) ) );

		return true;
	}
}

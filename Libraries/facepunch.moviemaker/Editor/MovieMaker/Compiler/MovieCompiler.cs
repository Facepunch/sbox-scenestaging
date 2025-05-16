using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Sandbox.MovieMaker;
using Sandbox.Resources;

namespace Editor.MovieMaker;

#nullable enable

[ResourceIdentity( "movie" )]
public sealed class MovieCompiler : ResourceCompiler
{
	protected override async Task<bool> Compile()
	{
		var source = await File.ReadAllTextAsync( Context.AbsolutePath );

		source = Context.ScanJson( source );

		var model = JsonSerializer.Deserialize<EmbeddedMovieResource>( source, EditorJsonOptions )!;
		var compiled = model.EditorData?.Deserialize<MovieProject>( EditorJsonOptions )?.Compile() ?? model.Compiled;

		model = new EmbeddedMovieResource { Compiled = compiled };

		Context.Data.Write( JsonSerializer.Serialize( model, EditorJsonOptions ) );

		return true;
	}
}

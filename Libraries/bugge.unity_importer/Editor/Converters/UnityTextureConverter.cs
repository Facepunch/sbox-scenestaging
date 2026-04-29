using System.IO;
using System.Linq;
using static Bugge.UnityImporter.UnityPackageExtractor;

namespace Bugge.UnityImporter;

public static class UnityTextureConverter
{
	public static void CreateTextures( UnityFile[] files )
	{
		UnityFile[] textures = [.. files.Where( f => f.Included && Path.GetExtension( f.Path ) == ".png" )];
		Log.Info( "Converting " + textures.Length + " materials..." );
	}
}

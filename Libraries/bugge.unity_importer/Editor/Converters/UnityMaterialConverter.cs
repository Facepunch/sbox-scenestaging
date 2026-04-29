using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static Bugge.UnityImporter.UnityPackageExtractor;

namespace Bugge.UnityImporter;

public static class UnityMaterialConverter
{
	public struct MaterialAttributes
	{
		public string Texture;
		public Color Color;
	}

	public static void ConvertUnityMaterials( UnityFile[] files )
	{
		UnityFile[] materials = [.. files.Where( f => f.Included && Path.GetExtension( f.Path ) == ".mat" )];
		Log.Info( "Converting " + materials.Length + " materials..." );
		foreach ( UnityFile material in materials )
			ConvertUnityMaterial( material, files );
	}

	private static void ConvertUnityMaterial( UnityFile material, UnityFile[] files )
	{
		Log.Info( "Converting: " + material.Path );
		string name = Path.GetFileNameWithoutExtension( material.Path );

		string fileName = Path.ChangeExtension( material.AbsolutePath, "vmat" );

		string origMat = File.ReadAllText( material.AbsolutePath );
		string mainTextureGuid = GetMainTextureGuid( origMat );
		string mainTexturePath = GetPathFromGuid( mainTextureGuid, files );
		Color color = GetColor( origMat );

		var attr = new MaterialAttributes()
		{
			Texture = mainTexturePath,
			Color = color
		};
		Save( attr, fileName );
		File.Delete( material.AbsolutePath );
	}

	public static Color GetColor( string mat )
	{
		var match = Regex.Match( mat, @"m_Colors:\r?\n\s*- _Color: \{([^\}]*)\}" );
		var colorLine = match.Success ? match.Groups[1].Value : "";
		var color = new Color( 1f, 1f, 1f, 1f );

		foreach ( var part in colorLine.Split( ',' ) )
		{
			var kv = part.Split( ':', 2 );
			if ( kv.Length != 2 ) continue;

			var key = kv[0].Trim();
			if ( !float.TryParse( kv[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value ) )
				continue;

			switch ( key )
			{
				case "r": color.r = value; break;
				case "g": color.g = value; break;
				case "b": color.b = value; break;
				case "a": color.a = value; break;
			}
		}

		return color;
	}

	public static string GetMainTextureGuid( string mat )
	{
		var match = Regex.Match( mat, @"- _MainTex:\r?\n\s*m_Texture: \{([^\}]*)\}" );
		string textureLine = match.Success ? match.Groups[1].Value : "";
		int fileID = int.Parse( textureLine.Split( "fileID: " )[1].Split( "," )[0] );
		if ( fileID == 0 ) return "";

		string guid = textureLine.Split( "guid: " )[1].Split( "," )[0];
		return guid;
	}

	public static string GetPathFromGuid( string guid, UnityFile[] files )
	{
		string path = files.FirstOrDefault( f => f.Guid == guid ).AbsolutePath;
		return path;
	}

	public static void Save( MaterialAttributes mat, string fileName )
	{
		var sb = new StringBuilder();
		sb.AppendLine( "// THIS FILE IS AUTO-GENERATED" );
		sb.AppendLine();
		sb.AppendLine( "Layer0" );
		sb.AppendLine( "{" );

		sb.AppendLine( $"\tshader \"shaders/complex.shader\"" );
		sb.AppendLine();

		// ---- Color ----
		sb.AppendLine( "\t//---- Color ----" );
		sb.AppendLine( $"\tg_vColorTint \"[{mat.Color.r} {mat.Color.g} {mat.Color.b} {mat.Color.a}]\"" );
		sb.AppendLine( $"\tTextureColor \"{mat.Texture}\"" );

		sb.AppendLine( "}" );

		File.WriteAllText( fileName, sb.ToString() );
	}
}

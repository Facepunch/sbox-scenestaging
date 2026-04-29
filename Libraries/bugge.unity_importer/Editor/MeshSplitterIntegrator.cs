using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Editor;
using static Bugge.UnityImporter.UnityPackageExtractor;

namespace Bugge.UnityImporter;

public static class MeshSplitterIntegrator
{
	private const string TargetType = "Bugge.MeshSplitter.SplitMeshContextMenu";
	private const string AssemblyName = "bugge.meshsplitter.editor";

	private static readonly HashSet<string> MeshExtensions = new( StringComparer.OrdinalIgnoreCase )
	{
		".fbx",
		".obj",
		".dmx"
	};

	public static bool IsAvailable()
	{
		// Check if the assembly is even loaded
		var assembly = AppDomain.CurrentDomain.GetAssemblies()
			.FirstOrDefault( x => x.FullName.Contains( AssemblyName ) );

		if ( assembly == null ) return false;

		// Check if the specific class exists
		return assembly.GetType( TargetType ) != null;
	}

	public static void SplitMeshes( UnityFile[] files )
	{
		UnityFile[] meshes = [.. files.Where( f => f.Included && MeshExtensions.Contains( Path.GetExtension( f.Path ) ) )];

		foreach ( var file in meshes )
		{
			string path = CleanPath( file.Path );
			var asset = AssetSystem.FindByPath( path );
			asset ??= AssetSystem.RegisterFile( file.AbsolutePath );

			Log.Info( path );
			Log.Info( asset.Name );
			CallMeshSplitter( asset );
		}
	}

	public static string CleanPath( string path )
	{
		if ( string.IsNullOrEmpty( path ) ) return null;

		bool hasAssetsStart = path.StartsWith( "Assets/", StringComparison.OrdinalIgnoreCase );
		return hasAssetsStart ? path[7..] : path;
	}

	public static Asset CallMeshSplitter( Asset meshFile, string customPath = null )
	{
		// 1. Locate the external assembly
		var assembly = AppDomain.CurrentDomain.GetAssemblies()
			.FirstOrDefault( x => x.FullName.Contains( "bugge.meshsplitter.editor" ) );

		if ( assembly == null )
		{
			Log.Warning( "MeshSplitter not found. Falling back to default behavior." );
			return null;
		}

		// 2. Get the specific class by its FULL name
		var type = assembly.GetType( "Bugge.MeshSplitter.SplitMeshContextMenu" );

		// 3. Find the 'SplitMesh' method
		// We specify the parameter types to ensure we get the right overload
		var method = type?.GetMethod( "SplitMesh", [typeof( Asset ), typeof( string )] );

		if ( method != null )
		{
			// 4. Invoke the static method
			// null because it's static, followed by our arguments array
			return method.Invoke( null, [meshFile, customPath] ) as Asset;
		}

		Log.Error( "Could not find the SplitMesh method via reflection!" );
		return null;
	}
}

using System;
using System.IO;
using System.IO.Compression;
using System.Formats.Tar;
using Editor;
using System.Linq;

namespace Bugge.UnityImporter;

public static class UnityPackageExtractor
{
	public class UnityFile
	{
		public string Path;
		public string AbsolutePath;
		public string TempPath;
		public string Guid;
		public bool Included;
	}

	public static void Extract( string packagePath, string outputDirectory )
	{
		string tempPath = Path.Combine( Path.GetTempPath(), "UnityUnpack_" + Guid.NewGuid() );
		Directory.CreateDirectory( tempPath );

		using ( var fs = File.OpenRead( packagePath ) )
		using ( var gzip = new GZipStream( fs, CompressionMode.Decompress ) )
			TarFile.ExtractToDirectory( gzip, tempPath, overwriteFiles: true );

		var directories = Directory.GetDirectories( tempPath );
		var files = new UnityFile[directories.Length];

		for ( int i = 0; i < files.Length; i++ )
		{
			string dir = directories[i];
			string guid = Path.GetFileName( dir );
			string pathnameFile = Path.Combine( dir, "pathname" );
			if ( !File.Exists( pathnameFile ) ) continue;

			string path = File.ReadAllLines( pathnameFile )[0].Replace( "\\", "/" ).Trim();
			string absolutePath = Path.GetFullPath( Path.Combine( outputDirectory, path ) );

			var file = new UnityFile()
			{
				Path = path,
				AbsolutePath = absolutePath,
				TempPath = dir,
				Guid = guid,
				Included = true
			};

			files[i] = file;
		}

		files = [.. files.OrderBy( f => f.Path )];

		var window = new ImportWindow( files );
		window.Show();

		window.OnConfirm += ( convertMaterials, createTextures, splitMeshes ) =>
		{
			ExtractFiles( files );

			if ( createTextures )
				UnityTextureConverter.CreateTextures( files );

			if ( convertMaterials )
				UnityMaterialConverter.ConvertUnityMaterials( files );

			if ( splitMeshes )
				MeshSplitterIntegrator.SplitMeshes( files );

			DeleteTempPath( tempPath );
			PromptRestart();
		};
	}

	private static void ExtractFiles( UnityFile[] files )
	{
		foreach ( var file in files )
		{
			if ( !file.Included ) continue;
			string pathnameFile = Path.Combine( file.TempPath, "pathname" );
			string assetFile = Path.Combine( file.TempPath, "asset" );

			if ( File.Exists( pathnameFile ) && File.Exists( assetFile ) )
			{
				Directory.CreateDirectory( Path.GetDirectoryName( file.AbsolutePath )! );
				Log.Info( "assetFile: " + assetFile );
				Log.Info( "file.AbsolutePath: " + file.AbsolutePath );
				File.Move( assetFile, file.AbsolutePath, true );
			}
		}
	}

	private static void DeleteTempPath( string tempPath )
	{
		bool doesTempPathExist = Directory.Exists( tempPath );
		if ( !doesTempPathExist ) return;
		Directory.Delete( tempPath, true );
	}

	private static void PromptRestart()
	{
		EditorUtility.RestartEditorPrompt(
			"""
			Importing complete.
			A restart of the editor is needed to register the assets properly.
			"""
		);
	}
}

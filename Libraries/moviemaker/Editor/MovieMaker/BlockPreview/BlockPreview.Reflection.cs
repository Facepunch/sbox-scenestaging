using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial class BlockPreview
{
	public static BlockPreview? Create( DopeSheetTrack parent, MovieBlock block )
	{
		foreach ( var typeDesc in EditorTypeLibrary.GetTypes<BlockPreview>() )
		{
			var type = typeDesc.TargetType;

			if ( type.IsAbstract ) continue;
			if ( type.IsGenericType )
			{
				if ( !TryMakeGenericType( type, block.Track.PropertyType, out var newType ) )
				{
					continue;
				}

				type = newType;
			}

			if ( !SupportsPropertyType( type, block.Track.PropertyType ) ) continue;

			try
			{
				var inst = (BlockPreview)Activator.CreateInstance( type )!;

				inst.Initialize( parent, block );

				return inst;
			}
			catch
			{
				continue;
			}
		}

		return null;
	}

	private static bool TryMakeGenericType( Type trackPreviewType, Type propertyType,
		[NotNullWhen( true )] out Type? newTrackPreviewType )
	{
		newTrackPreviewType = null;

		if ( trackPreviewType.GetGenericArguments().Length != 1 )
		{
			return false;
		}

		try
		{
			newTrackPreviewType = trackPreviewType.MakeGenericType( propertyType );
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool SupportsPropertyType( Type trackPreviewType, Type propertyType )
	{
		return trackPreviewType.GetInterfaces()
			.Where( x => x.IsConstructedGenericType )
			.Where( x => x.GetGenericTypeDefinition() == typeof(IBlockPreview<>) )
			.Any( x => x.GetGenericArguments()[0].IsAssignableFrom( propertyType ) );
	}
}

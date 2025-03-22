using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker.BlockDisplays;

#nullable enable

partial class BlockItem
{
	public static BlockItem? Create( DopeSheetTrack parent, IPropertyBlock block, MovieTime offset )
	{
		var valueType = block.PropertyType;

		foreach ( var typeDesc in EditorTypeLibrary.GetTypes<BlockItem>() )
		{
			var type = typeDesc.TargetType;

			if ( type.IsAbstract ) continue;
			if ( type.IsGenericType )
			{
				if ( !TryMakeGenericType( type, valueType, out var newType ) )
				{
					continue;
				}

				type = newType;
			}

			if ( !SupportsPropertyType( type, valueType ) ) continue;

			try
			{
				var inst = (BlockItem)Activator.CreateInstance( type )!;

				inst.Initialize( parent, block, offset );

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
			.Where( x => x.GetGenericTypeDefinition() == typeof(IBlockItem<>) )
			.Any( x => x.GetGenericArguments()[0].IsAssignableFrom( propertyType ) );
	}
}

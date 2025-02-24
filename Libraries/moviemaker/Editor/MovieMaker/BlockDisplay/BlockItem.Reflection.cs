using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker.BlockDisplays;

#nullable enable

partial class BlockItem
{
	public static BlockItem? Create( DopeSheetTrack parent, IPropertyBlock block, MovieTime offset )
	{
		if ( GetBlockItemType( block.PropertyType ) is not { } blockType )
		{
			return null;
		}
		try
		{
			var inst = (BlockItem)Activator.CreateInstance( blockType )!;

			inst.Initialize( parent, block, offset );

			return inst;
		}
		catch ( Exception ex )
		{
			Log.Error( ex );
			return null;
		}
	}

	[SkipHotload] private static Dictionary<Type, Type?> BlockItemTypeCache { get; } = new();

	[EditorEvent.Hotload]
	private static void OnHotload()
	{
		BlockItemTypeCache.Clear();
	}

	private static Type? GetBlockItemType( Type targetType )
	{
		if ( BlockItemTypeCache.TryGetValue( targetType, out var blockType ) ) return blockType;

		Type? bestBlockType = null;
		var bestScore = int.MaxValue;

		foreach ( var typeDesc in EditorTypeLibrary.GetTypes<BlockItem>() )
		{
			var type = typeDesc.TargetType;
			var baseDistance = 0;

			if ( type.IsAbstract ) continue;
			if ( type.IsGenericType )
			{
				if ( !TryMakeGenericType( type, targetType, out var newType ) )
				{
					continue;
				}

				type = newType;
				baseDistance = 1;
			}

			var score = baseDistance + GetScore( type, targetType );

			if ( score > bestScore ) continue;

			bestBlockType = type;
			bestScore = score;
		}

		BlockItemTypeCache[targetType] = bestBlockType;

		return bestBlockType;
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

	private static int GetScore( Type blockType, Type targetType )
	{
		var score = int.MaxValue;

		foreach ( var iFace  in blockType.GetInterfaces() )
		{
			if ( !iFace.IsConstructedGenericType ) continue;
			if ( iFace.GetGenericTypeDefinition() != typeof(IBlockItem<>) ) continue;

			var iFaceTargetType = iFace.GetGenericArguments()[0];

			score = Math.Min( score, GetDistance( iFaceTargetType, targetType ) );
		}

		return score;
	}

	private static int GetDistance( Type baseType, Type? derivedType )
	{
		if ( !baseType.IsAssignableFrom( derivedType ) ) return int.MaxValue;
		if ( baseType.IsInterface && !derivedType.IsInterface ) return 1;

		var distance = 0;

		while ( baseType != derivedType && derivedType != null )
		{
			derivedType = derivedType.BaseType;
			distance++;
		}

		return distance;
	}
}

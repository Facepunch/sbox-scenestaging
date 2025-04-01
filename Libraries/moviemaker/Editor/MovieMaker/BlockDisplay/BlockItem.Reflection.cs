using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker.BlockDisplays;

#nullable enable

partial class BlockItem
{
	public static BlockItem? Create( DopeSheetTrack parent, ITrackBlock block, MovieTime offset )
	{
		if ( GetBlockItemType( block.GetType(), (block as IPropertyBlock)?.PropertyType ) is not { } blockType )
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

	private static Type? GetBlockItemType( Type targetBlockType, Type? propertyType )
	{
		if ( BlockItemTypeCache.TryGetValue( targetBlockType, out var blockItemType ) ) return blockItemType;

		Type? bestBlockItemType = null;
		var bestScore = int.MaxValue;

		foreach ( var typeDesc in EditorTypeLibrary.GetTypes<BlockItem>() )
		{
			var type = typeDesc.TargetType;
			var baseDistance = 0;

			if ( type.IsAbstract ) continue;
			if ( type.IsGenericType )
			{
				if ( propertyType is null ) continue;

				if ( !TryMakeGenericType( type, propertyType, out var newType ) )
				{
					continue;
				}

				type = newType;
				baseDistance = 1;
			}

			var score = baseDistance + GetScore( type, targetBlockType, propertyType );

			if ( score > bestScore ) continue;

			bestBlockItemType = type;
			bestScore = score;
		}

		BlockItemTypeCache[targetBlockType] = bestBlockItemType;

		return bestBlockItemType;
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

	private static int GetScore( Type blockItemType, Type targetBlockType, Type? propertyType )
	{
		var score = int.MaxValue;

		foreach ( var iFace in blockItemType.GetInterfaces() )
		{
			if ( !iFace.IsConstructedGenericType ) continue;

			if ( iFace.GetGenericTypeDefinition() == typeof(IBlockItem<>) )
			{
				var iFaceTargetType = iFace.GetGenericArguments()[0];

				score = Math.Min( score, GetDistance( iFaceTargetType, targetBlockType ) );
			}

			if ( iFace.GetGenericTypeDefinition() == typeof(IPropertyBlockItem<>) && propertyType != null )
			{
				var iFaceTargetType = iFace.GetGenericArguments()[0];

				score = Math.Min( score, GetDistance( iFaceTargetType, propertyType ) );
			}
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

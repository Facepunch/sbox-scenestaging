using System;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker;

#nullable enable

partial class MoviePlayer
{
	internal record struct MappingModel( Guid TrackId,
		[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		Guid? GameObject = null,
		[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
		Guid? Component = null );

	[Property, Hide]
	internal IReadOnlyList<MappingModel> Mapping
	{
		get => _sceneRefMap
			.Where( x => x.Value.GameObject is not null )
			.Where( x => MovieClip is null || MovieClip.GetTrack( x.Key ) is not null )
			.Select( x => x.Value.Component is { } comp
				? new MappingModel( x.Key, Component: comp.Id )
				: new MappingModel( x.Key, x.Value.GameObject!.Id ) )
			.OrderBy( x => x.TrackId )
			.ToArray();

		set
		{
			_sceneRefMap.Clear();
			_memberMap.Clear();

			// Map game objects first

			foreach ( var mapping in value )
			{
				if ( mapping.GameObject is not { } goId ) continue;
				if ( Scene.Directory.FindByGuid( goId ) is not { } go ) continue;

				_sceneRefMap.Add( mapping.TrackId, MovieProperty.FromGameObject( go ) );
			}

			foreach ( var mapping in value )
			{
				if ( mapping.Component is not { } cmpId ) continue;
				if ( Scene.Directory.FindComponentByGuid( cmpId ) is not { } cmp ) continue;
				if ( GetTrack( cmp.GameObject ) is not { } goTrack ) continue;
				if ( GetProperty( goTrack ) is not { } goProperty ) continue;

				_sceneRefMap.Add( mapping.TrackId, MovieProperty.FromComponent( goProperty, cmp ) );
			}
		}
	}
}

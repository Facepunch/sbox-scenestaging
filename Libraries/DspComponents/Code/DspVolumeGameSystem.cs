using Sandbox;
using Sandbox.Audio;
using System.Collections.Generic;
using System.Linq;

public class DspVolumeGameSystem : Sandbox.Volumes.VolumeSystem<DspVolume>
{
	public DspVolumeGameSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, Update, "Dsp Update" );
	}

	public override void Dispose()
	{
		base.Dispose();

		var gameMixer = Mixer.FindMixerByName( "Game" );
		if ( gameMixer is null ) return;

		foreach ( var processor in _entries.Values )
		{
			gameMixer.RemoveProcessor( processor.processor );
		}
	}

	HashSet<string> _active { get; set; }

	record class Entry( DspProcessor processor, bool active );
	Dictionary<string, Entry> _entries = new();

	void Update()
	{
		var gameMixer = Mixer.FindMixerByName( "Game" );
		if ( gameMixer is null ) return;


		float lastPriority = 0;
		string found = default;

		foreach ( var volume in FindAll( Sound.Listener.Position ) )
		{
			float priority = volume.GetPriority();

			if ( priority < lastPriority )
				continue;

			lastPriority = priority;
			found = volume.Dsp.Name;
		}

		if ( !string.IsNullOrWhiteSpace( found ) && !_entries.ContainsKey( found ) )
		{
			var processor = new DspProcessor();

			processor.Effect = found;
			processor.Mix = 0;

			gameMixer.AddProcessor( processor );
			_entries[found] = new Entry( processor, true );
		}

		foreach ( var entry in _entries )
		{
			if ( found == entry.Key )
			{
				entry.Value.processor.Mix = entry.Value.processor.Mix.Approach( 1.0f, Time.Delta * 2.0f );
			}
			else
			{
				entry.Value.processor.Mix = entry.Value.processor.Mix.Approach( 0.0f, Time.Delta * 2.0f );
			}
		}


		foreach ( var entry in _entries.Where( x => x.Value.processor.Mix <= 0 ).ToArray() )
		{
			gameMixer.RemoveProcessor( entry.Value.processor );
			_entries.Remove( entry.Key );
		}



	}

	private void TryAdd( string name )
	{
		if ( _active.Contains( name ) ) return;
		_active.Add( name );
	}

	private void TryRemove( string name )
	{
		if ( !_active.Contains( name ) ) return;
		_active.Remove( name );


	}
}

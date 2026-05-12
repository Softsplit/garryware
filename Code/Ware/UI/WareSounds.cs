public static class WareSounds
{
	public const string NewWare = "sounds/ware/game_new.sound";
	public const string Phase = "sounds/ware/game_phase.sound";
	public const string Win = "sounds/ware/game_win.sound";
	public const string Lose = "sounds/ware/game_lose.sound";
	public const string LocalWin = "sounds/ware/local_win.sound";
	public const string LocalLose = "sounds/ware/local_lose.sound";
	public const string OtherWin = "sounds/ware/other_win.sound";
	public const string OtherLose = "sounds/ware/other_lose.sound";
	public const string EveryoneWon = "sounds/ware/everyone_won.sound";
	public const string EveryoneLost = "sounds/ware/everyone_lost.sound";
	public const string Prologue = "sounds/ware/game_prologue.sound";
	public const string Information = "sounds/ware/game_information.sound";
	public const string Epilogue = "sounds/ware/game_epilogue.sound";
	public const string Confirmation = "sounds/ware/confirmation.sound";
	public const string CountdownTick = "sounds/ware/countdown_tick.sound";
	public const string CountdownTickLow = "sounds/ware/countdown_tick_low.sound";
	public const string AmbientLoop1 = "sounds/ware/ambient_loop_1.sound";
	public const string AmbientLoop2 = "sounds/ware/ambient_loop_2.sound";

	private const float AmbientStartVolume = 0.1f;
	private const float AmbientTargetVolume = 0.7f;
	private static readonly List<QueuedSound> QueuedSounds = new();
	private static readonly List<SoundHandle> ActiveHandles = new();
	private static SoundHandle _ambientHandle;
	private static string _ambientSoundEvent;
	private static float _ambientTargetVolume;
	private static string _finalCountdownCue;

	private readonly record struct QueuedSound( string SoundEvent, float PlayTime, bool IsCountdown = false );
	public static float SpeedPercent { get; set; } = 100f;
	public static bool HasActiveSounds => QueuedSounds.Count > 0 || ActiveHandles.Any( x => x.IsValid() ) || _ambientHandle.IsValid() || !string.IsNullOrWhiteSpace( _ambientSoundEvent );

	public static string Countdown( int announcer, int second )
	{
		if ( announcer == 2 )
		{
			return second switch
			{
				1 => "sounds/ware/countdown_dos_1.sound",
				2 => "sounds/ware/countdown_dos_2.sound",
				3 => "sounds/ware/countdown_dos_3.sound",
				4 => "sounds/ware/countdown_dos_4.sound",
				5 => "sounds/ware/countdown_dos_5.sound",
				_ => null
			};
		}

		if ( announcer == 3 )
		{
			return second is 2 or 4 ? CountdownTickLow : CountdownTick;
		}

		return second switch
		{
			1 => "sounds/ware/countdown_1.sound",
			2 => "sounds/ware/countdown_2.sound",
			3 => "sounds/ware/countdown_3.sound",
			4 => "sounds/ware/countdown_4.sound",
			5 => "sounds/ware/countdown_5.sound",
			_ => null
		};
	}

	public static void Play( string soundEvent )
	{
		if ( Application.IsDedicatedServer ) return;
		if ( string.IsNullOrWhiteSpace( soundEvent ) ) return;

		Track( Sound.Play( soundEvent ) );
	}

	public static void PlayPeerStatus( Vector3 position, bool achieved )
	{
		if ( Application.IsDedicatedServer ) return;

		Track( Sound.Play( achieved ? OtherWin : OtherLose, position ) );
	}

	public static void StartWare( bool intro, int ambientLoop )
	{
		if ( Application.IsDedicatedServer ) return;

		QueuedSounds.Clear();
		StopCountdown();
		Play( NewWare );
		StartAmbient( ambientLoop );

		if ( intro )
			Queue( Prologue, 2f );
	}

	public static void StartPhase( int ambientLoop )
	{
		if ( Application.IsDedicatedServer ) return;

		Play( Phase );
		StartAmbient( ambientLoop );
	}

	public static void StartCountdown( float duration, int announcer )
	{
		if ( Application.IsDedicatedServer ) return;

		StopCountdown();

		if ( duration <= 0f || announcer <= 0 )
			return;

		announcer = Math.Clamp( announcer, 1, 3 );

		for ( var second = 5; second >= 1; second-- )
		{
			var delay = (duration / 6f) * (6 - second);
			Queue( Countdown( announcer, second ), delay, true );
		}

		_finalCountdownCue = Countdown( announcer, 1 );

		if ( announcer == 3 )
			Queue( CountdownTick, duration, true );
	}

	public static void PlayEnd( bool achieved )
	{
		if ( Application.IsDedicatedServer ) return;

		PlayFinalCountdownCue();
		StopCountdown();
		_ambientTargetVolume = 0f;
		Play( achieved ? Win : Lose );
	}

	public static void Update()
	{
		if ( Application.IsDedicatedServer ) return;

		ActiveHandles.RemoveAll( handle => !handle.IsValid() || handle.IsStopped );

		for ( var index = QueuedSounds.Count - 1; index >= 0; index-- )
		{
			var queued = QueuedSounds[index];
			if ( Time.Now < queued.PlayTime ) continue;

			Play( queued.SoundEvent );
			QueuedSounds.RemoveAt( index );
		}

		UpdateAmbient();
	}

	public static void StopAll()
	{
		QueuedSounds.Clear();
		StopCountdown();

		foreach ( var handle in ActiveHandles )
		{
			if ( handle.IsValid() )
				handle.Stop( 0f );
		}

		ActiveHandles.Clear();

		_ambientHandle?.Stop( 0f );
		_ambientHandle = null;
		_ambientSoundEvent = null;
		_ambientTargetVolume = 0f;
	}

	private static void StopCountdown()
	{
		QueuedSounds.RemoveAll( sound => sound.IsCountdown );
		_finalCountdownCue = null;
	}

	private static void PlayFinalCountdownCue()
	{
		if ( string.IsNullOrWhiteSpace( _finalCountdownCue ) ) return;
		if ( !QueuedSounds.Any( sound => sound.IsCountdown && sound.SoundEvent == _finalCountdownCue ) ) return;

		Play( _finalCountdownCue );
	}

	private static void Queue( string soundEvent, float delay, bool isCountdown = false )
	{
		if ( string.IsNullOrWhiteSpace( soundEvent ) ) return;

		QueuedSounds.Add( new QueuedSound( soundEvent, Time.Now + delay, isCountdown ) );
	}

	private static void StartAmbient( int loop )
	{
		var soundEvent = loop == 2 ? AmbientLoop2 : AmbientLoop1;

		if ( _ambientSoundEvent != soundEvent )
		{
			_ambientHandle?.Stop( 0.15f );
			_ambientHandle = null;
			_ambientSoundEvent = soundEvent;
		}

		_ambientTargetVolume = AmbientTargetVolume;

		if ( !_ambientHandle.IsValid() )
		{
			_ambientHandle = Sound.Play( soundEvent );
			Track( _ambientHandle );
			_ambientHandle.Volume = AmbientStartVolume;
		}
	}

	private static void Track( SoundHandle handle )
	{
		if ( !handle.IsValid() ) return;

		handle.Pitch = SpeedPercent / 100f;
		ActiveHandles.Add( handle );
	}

	private static void UpdateAmbient()
	{
		if ( string.IsNullOrWhiteSpace( _ambientSoundEvent ) ) return;

		if ( !_ambientHandle.IsValid() )
		{
			if ( _ambientTargetVolume <= 0f ) return;

			_ambientHandle = Sound.Play( _ambientSoundEvent );
			Track( _ambientHandle );
			_ambientHandle.Volume = AmbientStartVolume;
		}

		var volume = _ambientHandle.Volume;
		var step = Time.Delta * 1.2f;
		_ambientHandle.Pitch = SpeedPercent / 100f;
		if ( volume < _ambientTargetVolume )
			volume = MathF.Min( _ambientTargetVolume, volume + step );
		else if ( volume > _ambientTargetVolume )
			volume = MathF.Max( _ambientTargetVolume, volume - step );

		_ambientHandle.Volume = volume;

		if ( _ambientTargetVolume <= 0f && volume <= 0.01f )
		{
			_ambientHandle.Stop( 0.1f );
			_ambientHandle = null;
		}
	}
}
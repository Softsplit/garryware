public abstract class WareMinigame
{
	public WareRoundSystem System { get; private set; }
	public int ActionPhase { get; private set; }

	public abstract string Name { get; }
	public abstract string Title { get; }
	public virtual string Room => "generic";
	public virtual float Windup => 2f;
	public virtual float Duration => 5f;
	public virtual int OccurrencesPerCycle => 1;
	public virtual bool? InitialPlayerResult => false;
	public virtual bool UseCountdown => true;
	public virtual WareAnnouncer Announcer => WareAnnouncer.Spoken;
	public virtual bool ShowResults => true;

	protected WareEnvironment Environment => System.Environment;
	protected Scene Scene => System.Scene;

	internal void Bind( WareRoundSystem system )
	{
		System = system;
	}

	public virtual bool CanPlay( IReadOnlyList<Player> players ) => true;
	public virtual void Initialize() { }
	public virtual void StartAction() { }
	public virtual void UpdateAction() { }
	public virtual float? GetNextPhaseDuration() => null;
	public virtual void PhaseSignal( int phase ) { }
	public virtual void EndAction() { }

	internal void BeginAction()
	{
		ActionPhase = 1;
		StartAction();
	}

	internal void BeginNextActionPhase( int phase )
	{
		ActionPhase = phase;
		PhaseSignal( phase );
	}

	protected IEnumerable<Player> Players => System.Players;

	protected void SetInstruction( string instruction )
	{
		System.SetInstruction( instruction );
	}

	protected void SetAchieved( Player player, bool achieved )
	{
		System.SetAchieved( player, achieved );
	}

	protected void LockResult( Player player, bool achieved )
	{
		System.LockResult( player, achieved );
	}

	protected void FailAndSimulateDeath( Player player, Vector3 impulse = default )
	{
		System.FailAndSimulateDeath( player, impulse );
	}

	protected void SendDone( Player player )
	{
		System.SendDone( player );
	}

	protected void GiveWeapon( Player player, string prefabPath )
	{
		System.GiveWeapon( player, prefabPath );
	}

	protected GameObject CreateTemporaryObject( string name, Vector3 position, bool networked = false )
	{
		return System.CreateTemporaryObject( name, position, networked );
	}

	protected Vector3 PlayerVelocity( Player player )
	{
		if ( !player.IsValid() ) return Vector3.Zero;

		return player.IsLocalPlayer
			? player.Controller?.Velocity ?? Vector3.Zero
			: player.WareVelocity;
	}

	protected float MovementSpeed( Player player )
	{
		return PlayerVelocity( player ).Length;
	}

	protected bool IsOnAnyLocation( Player player, string group, float halfSize = 30f, float height = 64f )
	{
		if ( !player.IsValid() ) return false;

		foreach ( var location in Environment.GetLocations( group ) )
		{
			if ( IsPlayerTouchingLocationBox( player, location.WorldPosition, halfSize, height ) )
				return true;
		}

		return false;
	}

	protected bool IsInsideLocationBox( Vector3 position, Vector3 origin, float halfSize = 30f, float height = 64f )
	{
		return MathF.Abs( position.x - origin.x ) <= halfSize
			&& MathF.Abs( position.y - origin.y ) <= halfSize
			&& position.z >= origin.z
			&& position.z <= origin.z + height;
	}

	private static bool IsPlayerTouchingLocationBox( Player player, Vector3 origin, float halfSize, float height )
	{
		var radius = player.Controller.IsValid() ? player.Controller.BodyRadius : 0f;
		var playerHeight = player.Controller.IsValid() ? player.Controller.CurrentHeight : 72f;
		var position = player.WorldPosition;

		return MathF.Abs( position.x - origin.x ) <= halfSize + radius
			&& MathF.Abs( position.y - origin.y ) <= halfSize + radius
			&& position.z + playerHeight >= origin.z
			&& position.z <= origin.z + height;
	}
}

public enum WareAnnouncer
{
	None = 0,
	Spoken = 1,
	Dos = 2,
	Ticks = 3
}

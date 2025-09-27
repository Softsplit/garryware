/// <summary>
/// Base implementation for minigames
/// </summary>
public abstract class Minigame : IMinigame
{
	public abstract string Name { get; }
	public abstract string Instructions { get; }
	public abstract MinigameType Type { get; }
	public virtual TimeSpan Duration => TimeSpan.FromSeconds( 10 );
	public virtual TimeSpan PreparationTime => TimeSpan.FromSeconds( 3 );
	
	protected MinigameContext Context { get; private set; }
	protected IReadOnlyList<Player> Players => Context.Players;
	protected Scene Scene => Context.Scene;
	
	private readonly List<GameObject> spawnedObjects = new();
	private readonly HashSet<Player> achievers = new();
	
	public void Initialize( MinigameContext context )
	{
		Context = context;
		spawnedObjects.Clear();
		achievers.Clear();
		OnInitialize();
	}
	
	public void Start()
	{
		OnStart();
	}
	
	public void Update()
	{
		OnUpdate();
		UpdateAchievements();
	}
	
	public void End()
	{
		OnEnd();
	}
	
	public bool HasPlayerAchieved( Player player )
	{
		return achievers.Contains( player );
	}
	
	public IReadOnlyList<Player> GetAchievers()
	{
		return achievers.ToList().AsReadOnly();
	}
	
	protected virtual void OnInitialize() { }
	protected virtual void OnStart() { }
	protected virtual void OnUpdate() { }
	protected virtual void OnEnd() { }
	
	/// <summary>
	/// Check player achievements and update the achievers set
	/// </summary>
	protected virtual void UpdateAchievements()
	{
		foreach ( var player in Players )
		{
			if ( !achievers.Contains( player ) && CheckPlayerAchievement( player ) )
			{
				achievers.Add( player );
			}
		}
	}
	
	/// <summary>
	/// Override this to implement achievement logic
	/// </summary>
	protected abstract bool CheckPlayerAchievement( Player player );
	
	/// <summary>
	/// Helper to spawn objects with automatic cleanup
	/// </summary>
	protected GameObject SpawnObject( string prefabPath, Transform transform )
	{
		var obj = GameObject.Clone( prefabPath, new CloneConfig { Transform = transform } );
		obj.NetworkSpawn();
		spawnedObjects.Add( obj );
		return obj;
	}
	
	/// <summary>
	/// Helper to create a simple object with automatic cleanup
	/// </summary>
	protected GameObject CreateObject( string name )
	{
		var obj = new GameObject( true, name );
		obj.NetworkSpawn();
		spawnedObjects.Add( obj );
		return obj;
	}
	
	/// <summary>
	/// Get a random spawn point from available ones
	/// </summary>
	protected Transform GetRandomSpawnPoint()
	{
		if ( Context.SpawnPoints.Count == 0 )
			return Transform.Zero;
		
		return Random.Shared.FromList( Context.SpawnPoints.ToList() ).Transform.World;
	}
	
	public void Dispose()
	{
		// Clean up spawned objects
		foreach ( var obj in spawnedObjects )
		{
			if ( obj.IsValid() )
				obj.Destroy();
		}
		spawnedObjects.Clear();
		achievers.Clear();
		
		OnDispose();
	}
	
	protected virtual void OnDispose() { }
}
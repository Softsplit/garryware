
/// <summary>
/// A projectile. It explodes when it hits something.
/// </summary>
public class Projectile : Component, Component.ICollisionListener
{
	[RequireComponent] public Rigidbody Rigidbody { get; set; }

	[Sync( SyncFlags.FromHost )] public Guid InstigatorId { get; set; }

	protected TimeSince TimeSinceCreated;

	protected override void OnStart()
	{
		Tags.Add( "projectile" );
	}

	protected override void OnEnabled()
	{
		TimeSinceCreated = 0;
	}

	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( IsProxy )
			return;

		var player = collision.Other.GameObject.GetComponentInParent<Player>();
		if ( InstigatorId != Guid.Empty && player?.PlayerId == InstigatorId )
		{
			return;
		}

		OnHit( collision );
	}

	protected virtual void OnHit( Collision collision = default )
	{
		GameObject.Destroy();
	}

	/// <summary>
	/// Updates the current direction of the projectile
	/// </summary>
	/// <param name="direction"></param>
	/// <param name="speed"></param>
	public void UpdateDirection( Vector3 direction, float speed )
	{
		WorldRotation = Rotation.LookAt( direction, Vector3.Up );
		Rigidbody.Velocity = WorldRotation.Forward * speed;
	}
}

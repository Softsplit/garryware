public class Colt1911Weapon : IronSightsWeapon
{
	[Property] public float PrimaryFireRate { get; set; } = 0.2f;

	protected override float GetPrimaryFireRate() => PrimaryFireRate;

	protected override bool WantsPrimaryAttack()
	{
		return Input.Pressed( "attack1" );
	}

	public override void PrimaryAttack()
	{
		ShootBullet( PrimaryFireRate, GetBullet() );
	}
}

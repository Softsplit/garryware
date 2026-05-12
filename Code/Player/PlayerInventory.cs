using Sandbox.Citizen;

public sealed class PlayerInventory : Component, Local.IPlayerEvents
{
	[Property] public int MaxSlots { get; set; } = 6;

	[RequireComponent] public Player Player { get; set; }

	/// <summary>
	/// All weapons currently in the inventory, ordered by slot.
	/// </summary>
	public IEnumerable<BaseCarryable> Weapons => 
		GetComponentsInChildren<BaseCarryable>( true ).OrderBy( x => x.InventorySlot );

	[Sync( SyncFlags.FromHost ), Change] public BaseCarryable ActiveWeapon { get; private set; }

	public void OnActiveWeaponChanged( BaseCarryable oldWeapon, BaseCarryable newWeapon )
	{
		if ( oldWeapon.IsValid() )
			oldWeapon.GameObject.Enabled = false;

		if ( newWeapon.IsValid() )
		{
			newWeapon.GameObject.Enabled = true;
			newWeapon.SetCarried();
		}
	}

	/// <summary>
	/// Returns the weapon in the given slot, or null if the slot is empty.
	/// </summary>
	public BaseCarryable GetSlot( int slot )
	{
		if ( slot < 0 || slot >= MaxSlots ) return null;
		foreach ( var w in Weapons )
		{
			if ( w.InventorySlot == slot ) return w;
		}
		return null;
	}

	/// <summary>
	/// Returns the first empty slot index, or -1 if the inventory is full.
	/// </summary>
	public int FindEmptySlot()
	{
		var weapons = Weapons;
		for ( int i = 0; i < MaxSlots; i++ )
		{
			bool occupied = false;
			foreach ( var w in weapons )
			{
				if ( w.InventorySlot == i ) { occupied = true; break; }
			}
			if ( !occupied ) return i;
		}

		return -1;
	}

	public bool Give( string prefabName )
	{
		if ( !Networking.IsHost )
			return false;

		var prefab = GameObject.GetPrefab( prefabName );
		if ( prefab is null )
		{
			Log.Warning( $"Prefab not found: {prefabName}" );
			return false;
		}

		var slot = FindEmptySlot();
		if ( slot < 0 )
			return false;

		return Give( prefabName, slot );
	}

	public bool HasWeapon( GameObject prefab )
	{
		var baseCarry = prefab.GetComponent<BaseCarryable>( true );
		if ( !baseCarry.IsValid() )
			return false;

		return Weapons.Where( x => x.GetType() == baseCarry.GetType() )
			.FirstOrDefault()
			.IsValid();
	}

	public bool HasWeapon<T>() where T : BaseCarryable
	{
		return GetWeapon<T>().IsValid();
	}

	public T GetWeapon<T>() where T : BaseCarryable
	{
		return Weapons.OfType<T>().FirstOrDefault();
	}

	public bool Give( GameObject prefab )
	{
		var slot = FindEmptySlot();
		if ( slot < 0 )
			return false;

		return Give( prefab, slot );
	}

	public bool Give( string prefabName, int targetSlot )
	{
		if ( !Networking.IsHost )
			return false;

		var prefab = GameObject.GetPrefab( prefabName );
		if ( prefab is null )
		{
			Log.Warning( $"Prefab not found: {prefabName}" );
			return false;
		}

		if ( !Give( prefab, targetSlot ) )
			return false;

		return true;
	}

	public bool Give( GameObject prefab, int targetSlot )
	{
		if ( !Networking.IsHost )
			return false;

		if ( targetSlot < 0 || targetSlot >= MaxSlots )
			return false;

		var baseCarry = prefab.Components.Get<BaseCarryable>( true );
		if ( !baseCarry.IsValid() )
			return false;

		var existing = Weapons.Where( x => x.GameObject.Name == prefab.Name ).FirstOrDefault();
		if ( existing.IsValid() )
		{
			if ( existing is BaseWeapon existingWeapon && baseCarry is BaseWeapon pickupWeapon && existingWeapon.UsesAmmo )
			{
				if ( existingWeapon.ReserveAmmo >= existingWeapon.MaxReserveAmmo )
					return false;

				var ammoToGive = pickupWeapon.UsesClips ? pickupWeapon.ClipContents : pickupWeapon.StartingAmmo;
				existingWeapon.AddReserveAmmo( ammoToGive );
				OnClientWeaponAdded( existing );

				return true;
			}
		}

		// Reject if the target slot is already occupied
		var occupant = GetSlot( targetSlot );
		if ( occupant.IsValid() )
			return false;

		var clone = prefab.Clone( new CloneConfig { Parent = GameObject, StartEnabled = false } );
		clone.NetworkSpawn( false, Network.Owner );

		var weapon = clone.GetComponent<BaseCarryable>( true );
		Assert.NotNull( weapon );
		weapon.SetCarried();

		weapon.InventorySlot = targetSlot;
		weapon.OnAdded( Player );
		OnClientWeaponAdded( weapon );

		return true;
	}

	[Rpc.Owner]
	private void OnClientWeaponAdded( BaseCarryable weapon )
	{
		if ( !weapon.IsValid() ) return;

		if ( ShouldAutoswitchTo( weapon ) )
		{
			SwitchWeapon( weapon );
		}
	}

	private bool ShouldAutoswitchTo( BaseCarryable item )
	{
		Assert.True( item.IsValid(), "item invalid" );

		if ( !ActiveWeapon.IsValid() )
			return true;

		if ( !GamePreferences.AutoSwitch )
			return false;

		if ( ActiveWeapon.IsInUse() )
			return false;

		if ( item is BaseWeapon weapon && weapon.UsesAmmo )
		{
			if ( !weapon.HasAmmo() && !weapon.CanReload() )
			{
				return false;
			}
		}

		return item.Value > ActiveWeapon.Value;
	}

	/// <summary>
	/// Moves the item in <paramref name="fromSlot"/> to <paramref name="toSlot"/>.
	/// If both slots are occupied the weapons are swapped; if <paramref name="toSlot"/> is
	/// empty the weapon is simply relocated.
	/// </summary>
	public void MoveSlot( int fromSlot, int toSlot )
	{
		if ( !Networking.IsHost )
		{
			HostMoveSlot( fromSlot, toSlot );
			return;
		}

		if ( fromSlot == toSlot ) return;
		if ( fromSlot < 0 || fromSlot >= MaxSlots ) return;
		if ( toSlot < 0 || toSlot >= MaxSlots ) return;

		var fromWeapon = GetSlot( fromSlot );
		if ( !fromWeapon.IsValid() ) return;

		var moveEvent = new PlayerMoveSlotEvent { Player = Player, FromSlot = fromSlot, ToSlot = toSlot };
		Local.IPlayerEvents.PostToGameObject( Player.GameObject, e => e.OnMoveSlot( moveEvent ) );
		Global.IPlayerEvents.Post( e => e.OnPlayerMoveSlot( moveEvent ) );

		if ( moveEvent.Cancelled )
			return;

		var toWeapon = GetSlot( toSlot );

		fromWeapon.InventorySlot = toSlot;
		if ( toWeapon.IsValid() )
			toWeapon.InventorySlot = fromSlot;
	}

	[Rpc.Host]
	private void HostMoveSlot( int fromSlot, int toSlot )
	{
		MoveSlot( fromSlot, toSlot );
	}

	public BaseCarryable GetBestWeapon()
	{
		return Weapons.OrderByDescending( x => x.Value ).FirstOrDefault();
	}

	public void SwitchWeapon( BaseCarryable weapon, bool allowHolster = false )
	{
		if ( !Networking.IsHost )
		{
			HostSwitchWeapon( weapon, allowHolster );
			return;
		}

		if ( weapon == ActiveWeapon )
		{
			if ( allowHolster )
			{
				ActiveWeapon = null;
			}
			return;
		}

		var switchEvent = new PlayerSwitchWeaponEvent { Player = Player, From = ActiveWeapon, To = weapon };
		Local.IPlayerEvents.PostToGameObject( Player.GameObject, e => e.OnSwitchWeapon( switchEvent ) );
		Global.IPlayerEvents.Post( e => e.OnPlayerSwitchWeapon( switchEvent ) );

		if ( switchEvent.Cancelled )
			return;

		ActiveWeapon = weapon;
	}

	[Rpc.Host]
	private void HostSwitchWeapon( BaseCarryable weapon, bool allowHolster = false )
	{
		SwitchWeapon( weapon, allowHolster );
	}

	protected override void OnUpdate()
	{
		var renderer = Player?.Controller?.Renderer;

		if ( ActiveWeapon.IsValid() )
		{
			ActiveWeapon.OnFrameUpdate( Player );

			if ( renderer.IsValid() )
			{
				renderer.Set( "holdtype", (int)ActiveWeapon.HoldType );
			}
		}
		else
		{
			if ( renderer.IsValid() )
			{
				renderer.Set( "holdtype", (int)CitizenAnimationHelper.HoldTypes.None );
			}
		}
	}

	public void OnControl()
	{
		if ( ActiveWeapon.IsValid() && !ActiveWeapon.IsProxy )
			ActiveWeapon.OnPlayerUpdate( Player );
	}

	/// <summary>
	/// Removes a weapon from the inventory and destroys it without dropping it into the world.
	/// </summary>
	public void Remove( BaseCarryable weapon )
	{
		if ( !Networking.IsHost )
		{
			HostRemove( weapon );
			return;
		}
		_ = RemoveAsync( weapon );
	}

	private async Task RemoveAsync( BaseCarryable weapon )
	{
		if ( !weapon.IsValid() ) return;
		if ( weapon.Owner != Player ) return;

		var removeEvent = new PlayerRemoveWeaponEvent { Player = Player, Weapon = weapon };
		Local.IPlayerEvents.PostToGameObject( Player.GameObject, e => e.OnRemoveWeapon( removeEvent ) );
		Global.IPlayerEvents.Post( e => e.OnPlayerRemoveWeapon( removeEvent ) );

		if ( removeEvent.Cancelled )
			return;

		if ( ActiveWeapon == weapon )
			SwitchWeapon( null, true );

		weapon.DestroyGameObject();
		await Task.Yield();

		var best = GetBestWeapon();
		if ( best.IsValid() )
			SwitchWeapon( best );
	}

	[Rpc.Host]
	private void HostRemove( BaseCarryable weapon )
	{
		Remove( weapon );
	}

	// --- Event Handlers ---

	void Local.IPlayerEvents.OnDied( PlayerDiedParams args )
	{
		if ( ActiveWeapon.IsValid() )
		{
			ActiveWeapon.OnPlayerDeath( args );
		}
	}

	void Local.IPlayerEvents.OnCameraMove( ref Angles angles )
	{
		if ( !ActiveWeapon.IsValid() ) return;

		ActiveWeapon.OnCameraMove( Player, ref angles );
	}

	void Local.IPlayerEvents.OnCameraPostSetup( Sandbox.CameraComponent camera )
	{
		if ( !ActiveWeapon.IsValid() ) return;

		ActiveWeapon.OnCameraSetup( Player, camera );
	}
}

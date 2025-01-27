using System.Collections.Generic;
using System.Linq;
using BetterCommands;
using BetterCommands.Permissions;
using Compendium.Events;
using Compendium.Extensions;
using Compendium.Updating;
using InventorySystem.Items.Pickups;
using InventorySystem.Items.ThrowableProjectiles;
using MEC;
using PlayerRoles;
using PluginAPI.Events;
using UnityEngine;

namespace Compendium.Gameplay.Landmines;

public class Landmine
{
	public static readonly List<Landmine> Landmines = new List<Landmine>();

	private ExplosionGrenade grenade;

	private Vector3 pos;

	public ushort Serial { get; }

	public bool Triggered { get; private set; }

	public bool Ignored { get; private set; }

	public Vector3 Position
	{
		get
		{
			return grenade?.transform?.position ?? Vector3.zero;
		}
		set
		{
			Spawn(value);
		}
	}

	public ExplosionGrenade Grenade => grenade;

	public Landmine()
	{
		grenade = World.SpawnNonActiveProjectile<ExplosionGrenade>(ItemType.GrenadeHE, Vector3.zero, new Vector3(5f, 0.1f, 5f), Vector3.forward, Vector3.up, Quaternion.identity, Vector3.zero, 0f);
		Serial = grenade.Info.Serial;
		Landmines.Add(this);
	}

	public void Spawn(Vector3 position)
	{
		pos = position;
		if (grenade.PhysicsModule is PickupStandardPhysics pickupStandardPhysics && pickupStandardPhysics.Rb != null)
		{
			pickupStandardPhysics.Rb.isKinematic = true;
			pickupStandardPhysics.Rb.freezeRotation = true;
			pickupStandardPhysics.Rb.constraints = RigidbodyConstraints.FreezeAll;
		}
		grenade.transform.position = position;
		grenade.transform.rotation = Quaternion.identity;
		Plugin.Info($"Spawned a mine at {pos}");
	}

	public void Trigger()
	{
		if (!Triggered)
		{
			Triggered = true;
			grenade._fuseTime = 0.1f;
			grenade.ServerActivate();
			Plugin.Info($"Triggered mine '{Serial}'");
		}
	}

	[Event]
	private static void OnGrenadeExploded(GrenadeExplodedEvent ev)
	{
		GrenadeExplodedEvent ev2 = ev;
		Landmines.RemoveAll((Landmine mine) => (object)mine.grenade == null || (mine.grenade != null && mine.Serial == ev2.Grenade.Info.Serial) || mine.Triggered);
	}

	[Update]
	private static void Update()
	{
		foreach (Landmine mine2 in Landmines)
		{
			try
			{
				if (!mine2.Triggered && !mine2.Ignored && (object)mine2.grenade != null && Hub.Hubs.Any((ReferenceHub hub) => hub.IsAlive() && UnityExtensions.IsWithinDistance(hub.transform.position, mine2.pos, 0.7f)))
				{
					mine2.Trigger();
				}
			}
			catch
			{
			}
		}
		Landmines.RemoveAll(delegate(Landmine mine)
		{
			try
			{
				_ = mine.Position;
				return false;
			}
			catch
			{
				return true;
			}
		});
	}

	[Command("spawnmine", new CommandType[] { CommandType.RemoteAdmin })]
	[Description("Spawns a landmine.")]
	[Permission(PermissionLevel.Administrator)]
	private static string SpawnMineCommand(ReferenceHub sender, float yFactor = 0.94f)
	{
		Landmine mine = new Landmine();
		Vector3 position = sender.transform.position;
		if (yFactor > 0f)
		{
			position.y -= yFactor;
		}
		mine.Ignored = true;
		mine.Spawn(position);
		Timing.CallDelayed(5f, delegate
		{
			mine.Ignored = false;
		});
		return "Landmine spawned. You have 5 seconds to get of out it's range.";
	}

	[Command("spawnmineon", new CommandType[] { CommandType.RemoteAdmin })]
	[Description("Spawns a landmine on a target.")]
	[Permission(PermissionLevel.Administrator)]
	private static string SpawnMineTargetCommand(ReferenceHub sender, ReferenceHub target, float yFactor = 0.94f)
	{
		Landmine mine = new Landmine();
		Vector3 position = target.transform.position;
		if (yFactor > 0f)
		{
			position.y -= yFactor;
		}
		mine.Ignored = true;
		mine.Spawn(position);
		Timing.CallDelayed(5f, delegate
		{
			mine.Ignored = false;
		});
		return "Landmine spawned. You have 5 seconds to get of out it's range.";
	}
}

using System;
using System.Collections.Generic;
using Compendium.Attributes;
using Compendium.Enums;
using Compendium.Events;
using Compendium.Messages;
using Compendium.Staff;
using CustomPlayerEffects;
using helpers;
using helpers.Configuration;
using helpers.Extensions;
using helpers.Patching;
using helpers.Random;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp106;
using PlayerStatsSystem;
using PluginAPI.Events;
using UnityEngine;

namespace Compendium.Gameplay.Pocket;

public static class PocketExitHandler
{
	private static Dictionary<ReferenceHub, int> _escapedTimes;

	private static int _totalEscapes;

	[Config(Name = "Failed Hint", Description = "The hint to display if a player fails to escape.")]
	public static HintMessage EscapeFailedHint { get; set; }

	[Config(Name = "Escaped Hint", Description = "The hint to display if a player succesfully escapes.")]
	public static HintMessage EscapeSuccessHint { get; set; }

	[Config(Name = "Escaped Player Hint", Description = "The hint to display to all SCP-106 players when a player escapes.")]
	public static HintMessage EscapedScpHint { get; set; }

	[Config(Name = "Exit Count", Description = "The amount of exits that are always correct.")]
	public static int AlwaysExitCount { get; set; }

	[Config(Name = "Escape Window", Description = "The amount of milliseconds to keep a count of player's escapes for. Chance of escape decreases based on this.")]
	public static int EscapeTimeWindow { get; set; }

	[Config(Name = "Regenerate Count", Description = "The amount of escapes requiRedValue for the pocket dimension to regenerate. Set to zero to disable.")]
	public static int RegenerateAfterEscapes { get; set; }

	[Config(Name = "Escape Chances", Description = "A list of chances of escape.")]
	public static Dictionary<string, int> EscapeChances { get; set; }

	[Patch(typeof(PocketDimensionTeleport), "OnTriggerEnter")]
	private static bool ExitPatch(PocketDimensionTeleport __instance, Collider other)
	{
		NetworkIdentity component = other.GetComponent<NetworkIdentity>();
		if (component == null)
		{
			return false;
		}
		if (!ReferenceHub.TryGetHubNetID(component.netId, out var hub))
		{
			return false;
		}
		if (hub.roleManager.CurrentRole.ActiveTime < 1f)
		{
			return false;
		}
		if (!(HubRoleExtensions.Role(hub) is IFpcRole fpcRole))
		{
			return false;
		}
		if (__instance._type == PocketDimensionTeleport.PDTeleportType.Exit || hub.characterClassManager.GodMode)
		{
			Exit(hub, fpcRole);
			return false;
		}
		int value = EscapeChances["*"];
		if (StaffHandler.Members.TryGetValue(HubDataExtensions.UserId(hub), out var value2))
		{
			string[] array = value2;
			foreach (string key in array)
			{
				if (EscapeChances.TryGetValue(key, out value))
				{
					break;
				}
			}
		}
		if (_escapedTimes.ContainsKey(hub))
		{
			value -= _escapedTimes[hub] * 10;
		}
		if (value < 0)
		{
			value = 0;
		}
		if (value <= 0 || !WeightedRandomGeneration.Default.GetBool(value))
		{
			FailExit(hub);
		}
		else
		{
			Exit(hub, fpcRole);
		}
		return false;
	}

	private static void FailExit(ReferenceHub hub)
	{
		if (EventManager.ExecuteEvent(new PlayerExitPocketDimensionEvent(hub, isSuccessful: false)))
		{
			hub.playerStats.DealDamage(new UniversalDamageHandler(-1f, DeathTranslations.PocketDecay));
			if (EscapeFailedHint != null && EscapeFailedHint.IsValid)
			{
				EscapeFailedHint.Send(hub);
			}
			_escapedTimes.Remove(hub);
		}
	}

	private static void Exit(ReferenceHub hub, IFpcRole fpcRole)
	{
		if (!EventManager.ExecuteEvent(new PlayerExitPocketDimensionEvent(hub, isSuccessful: true)))
		{
			return;
		}
		fpcRole.FpcModule.ServerOverridePosition(Scp106PocketExitFinder.GetBestExitPosition(fpcRole), Vector3.zero);
		hub.playerEffectsController.EnableEffect<Disabled>(10f, addDuration: true);
		hub.playerEffectsController.EnableEffect<Traumatized>();
		hub.playerEffectsController.DisableEffect<PocketCorroding>();
		hub.playerEffectsController.DisableEffect<Corroding>();
		typeof(PocketDimensionTeleport).TryInvokeEvent("OnPlayerEscapePocketDimension", hub);
		if (EscapeSuccessHint != null && EscapeSuccessHint.IsValid)
		{
			EscapeSuccessHint.Send(hub);
		}
		if (EscapedScpHint != null && EscapedScpHint.IsValid)
		{
			Hub.ForEach(delegate(ReferenceHub h)
			{
				HubWorldExtensions.Hint(h, EscapedScpHint.Value.Replace("%player%", HubDataExtensions.Nick(hub)).Replace("%role%", "<color=" + HubRoleExtensions.GetRoleColorHexPrefixed(hub) + ">" + HubRoleExtensions.RoleId(hub).ToString().SpaceByPascalCase() + "</color>").Replace("%zone%", HubWorldExtensions.Zone(hub).ToString().SpaceByPascalCase())
					.Replace("%room%", HubWorldExtensions.RoomId(hub).ToString().SpaceByPascalCase()), (float)EscapedScpHint.Duration);
			}, RoleTypeId.Scp106);
		}
		_totalEscapes++;
		if (RegenerateAfterEscapes > 0 && _totalEscapes >= RegenerateAfterEscapes)
		{
			PocketDimensionGenerator.RandomizeTeleports();
			_totalEscapes = 0;
		}
		if (_escapedTimes.ContainsKey(hub))
		{
			_escapedTimes[hub]++;
			return;
		}
		_escapedTimes.Add(hub, 1);
		Calls.Delay(EscapeTimeWindow, delegate
		{
			_escapedTimes.Remove(hub);
		});
	}

	[Event]
	private static void OnPlayerLeft(PlayerLeftEvent ev)
	{
		_escapedTimes.Remove(ev.Player.ReferenceHub);
	}

	[RoundStateChanged(new RoundState[] { RoundState.Restarting })]
	private static void OnRoundRestart()
	{
		_escapedTimes.Clear();
	}

	static PocketExitHandler()
	{
		EscapeFailedHint = HintMessage.Create("<b><color=#33FFA5><color=#FF0000>Nepovedlo</color> se ti utéct .. možná příště.</color></b>", 5.0);
		EscapeSuccessHint = HintMessage.Create("<b><color=#33FFA5><color=#90FF33>Povedlo</color> se ti utéct! Dobrá práce.</color></b>", 5.0);
		EscapedScpHint = HintMessage.Create("<b><color=#90FF33>Hráči <color=#FF0000>%player%</color> (<color=#33FFA5>%role%</color>)se povedlo utéct z dimenze! Nyní se nacházejí v %zone%.</color></b>", 10.0);
		AlwaysExitCount = 1;
		EscapeTimeWindow = 60000;
		RegenerateAfterEscapes = 0;
		_escapedTimes = new Dictionary<ReferenceHub, int>();
		_totalEscapes = 0;
		EscapeChances = new Dictionary<string, int> { ["*"] = 20 };
	}

	[Patch(typeof(PocketDimensionGenerator), "RandomizeTeleports", PatchType.Prefix, new Type[] { })]
	private static bool GeneratePatch()
	{
		PocketDimensionTeleport[] array = PocketDimensionGenerator.PrepTeleports();
		if (array == null || array.Length == 0)
		{
			return false;
		}
		array.ForEach(delegate(PocketDimensionTeleport t)
		{
			t.SetType(PocketDimensionTeleport.PDTeleportType.Killer);
		});
		if (AlwaysExitCount > 0)
		{
			for (int i = 0; i < AlwaysExitCount; i++)
			{
				int random = RandomGeneration.Default.GetRandom(0, array.Length - 1);
				while (array[random]._type == PocketDimensionTeleport.PDTeleportType.Exit)
				{
					random = RandomGeneration.Default.GetRandom(0, array.Length - 1);
				}
				array[random].SetType(PocketDimensionTeleport.PDTeleportType.Exit);
			}
		}
		for (int j = 0; j < array.Length; j++)
		{
			if (array[j]._type != PocketDimensionTeleport.PDTeleportType.Exit)
			{
				array[j].SetType(PocketDimensionTeleport.PDTeleportType.Killer);
			}
		}
		return false;
	}
}

using System;
using BetterCommands;
using helpers.Configuration;
using helpers.Patching;
using PluginAPI.Core;
using Respawning;

namespace Compendium.Gameplay.Respawning;

public static class RespawnController
{
	[Config(Name = "Respawn Enabled", Description = "Whether or not to enable team respawning.")]
	public static bool IsEnabled { get; set; } = true;

	[Patch(typeof(WaveManager), nameof(WaveManager.Update), PatchType.Prefix)]
	private static bool _RespawnPatch()
	{
		return IsEnabled;
	}

	[Command("switchrespawns", new CommandType[]
	{
		CommandType.RemoteAdmin,
		CommandType.GameConsole
	})]
	[Description("Disables/enables team respawning for the entire round.")]
	private static string SwitchRespawns(Player sender)
	{
		IsEnabled = !IsEnabled;
		if (IsEnabled)
		{
			return "Enabled team respawns.";
		}
		return "Disabled team respawns.";
	}
}

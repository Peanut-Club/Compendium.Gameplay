using Compendium.Input;
using PlayerRoles;
using UnityEngine;

namespace Compendium.Gameplay.Stalking;

public class StalkKeybind : IInputHandler
{
	public KeyCode Key { get; } = KeyCode.T;


	public bool IsChangeable { get; } = true;


    public string Id { get; } = "stalk";

    public string Label { get; } = "SCP - SCP106 - Stalk";


    public void OnPressed(ReferenceHub player)
	{
		if (StalkController.Controllers.TryGetValue(player, out var value))
		{
			if (value.State == StalkState.None)
			{
				value.ServerStartStalk();
			}
		}
		else if (HubRoleExtensions.RoleId(player) == RoleTypeId.Scp106)
		{
			StalkController stalkController2 = (StalkController.Controllers[player] = new StalkController());
			value = stalkController2;
			value.Player = player;
			value.ServerStartStalk();
		}
	}
}

using Godot;
using Networking;
using Steamworks;
using System;

public partial class MainMenu : Control
{
	[Export] public Control JoinStuff;
	[Export] public Control LobbyStuff;

	public override void _Ready()
	{
		JoinStuff.Visible = true;
		LobbyStuff.Visible = false;

		NetworkManager.JoinedServer += () =>
		{
			JoinStuff.Visible = false;
			LobbyStuff.Visible = false;
		};
	}

	public void Host()
	{
		// NetworkManager.Host();
		NetworkManager.HostLocal();

		JoinStuff.Visible = false;
		LobbyStuff.Visible = true;
	}

	public void Join()
	{
		// SteamFriends.ActivateGameOverlay("friends");
		NetworkManager.JoinLocal();
	}

	public void Start()
	{
		Game.Start();

		LobbyStuff.Visible = false;
	}
}

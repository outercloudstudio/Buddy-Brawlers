using Godot;
using Networking;
using Riptide;
using Steamworks;
using System;
using System.Collections.Generic;

public partial class Game : Node3D, NetworkPointUser
{
	[Export] public PackedScene[] Characters = new PackedScene[0];

	public NetworkPoint NetworkPoint { get; set; } = new NetworkPoint();

	public override void _Ready()
	{
		if (!SteamAPI.Init())
		{
			GD.PushError("SteamAPI.Init() failed!");

			return;
		}

		NetworkPoint.Setup(this);

		NetworkPoint.Register(nameof(StartRpc), StartRpc);

		if (OS.HasFeature("host"))
		{
			NetworkManager.HostLocal();

			NetworkManager.ClientConnected += (ServerConnectedEventArgs eventArguments) =>
			{
				if (!NetworkManager.IsHost) return;

				if (NetworkManager.LocalServer.ClientCount != 2) return;

				Start();
			};
		}

		if (OS.HasFeature("client")) NetworkManager.JoinLocal();
	}

	public override void _Process(double delta)
	{
		SteamAPI.RunCallbacks();
	}

	private void Start()
	{
		List<int> clientIds = new List<int>();

		foreach (Connection connection in NetworkManager.LocalServer.Clients)
		{
			clientIds.Add(connection.Id);
		}

		NetworkPoint.SendRpcToClients(nameof(StartRpc), message =>
		{
			message.AddInts(clientIds.ToArray());
		});
	}

	private void StartRpc(Message message)
	{
		int[] clientIds = message.GetInts();

		foreach (int clientId in clientIds)
		{
			Player player = NetworkManager.SpawnNetworkSafe<Player>(Characters[clientId - 1], "Player", clientId);
			AddChild(player);

			player.GlobalPosition = new Vector3(0, 0.359f + 2 * (clientId - 1), 0);
		}
	}
}

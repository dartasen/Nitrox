﻿using System.IO;
using NitroxModel.DataStructures.GameLogic;
using NitroxModel.Packets;
using NitroxModel.Serialization;
using NitroxModel.Server;
using NitroxServer.ConsoleCommands.Abstract;
using NitroxServer.ConsoleCommands.Abstract.Type;
using NitroxServer.GameLogic;

namespace NitroxServer.ConsoleCommands;

internal class ChangeServerGamemodeCommand : Command
{
    private readonly Server server;
    private readonly PlayerManager playerManager;
    private readonly SubnauticaServerConfig serverConfig;

    public ChangeServerGamemodeCommand(Server server, PlayerManager playerManager, SubnauticaServerConfig serverConfig) : base("changeservergamemode", Perms.ADMIN, "Changes server gamemode")
    {
        AddParameter(new TypeEnum<NitroxGameMode>("gamemode", true, "Gamemode to change to"));

        this.server = server;
        this.playerManager = playerManager;
        this.serverConfig = serverConfig;
    }

    protected override void Execute(CallArgs args)
    {
        NitroxGameMode sgm = args.Get<NitroxGameMode>(0);

        using (serverConfig.Update(Path.Combine(KeyValueStore.Instance.GetSavesFolderDir(), server.Name)))
        {
            if (serverConfig.GameMode != sgm)
            {
                serverConfig.GameMode = sgm;

                foreach (Player player in playerManager.GetAllPlayers())
                {
                    player.GameMode = sgm;
                }
                playerManager.SendPacketToAllPlayers(GameModeChanged.ForAllPlayers(sgm));
                SendMessageToAllPlayers($"Server gamemode changed to \"{sgm}\" by {args.SenderName}");
            }
            else
            {
                SendMessage(args.Sender, "Server is already using this gamemode");
            }
        }
    }
}

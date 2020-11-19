using DrawioFunctions.Models;
using DrawioFunctions.Requests;
using DrawioFunctions.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Scribble.Functions.Functions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Scribble.Functions.Functions
{
    public static class LobbyFunctions
    {
        private static Random _random = new Random();

        [FunctionName("create")]
        public static async Task<IActionResult> Create(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log,
            [SignalR(HubName = "game")] IAsyncCollector<SignalRGroupAction> signalRGroupActions,
            [DurableClient] IDurableOrchestrationClient starter)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<CreateRequest>(requestBody);

            data.UserName = data.UserName.Replace(" ", "");

            if (string.IsNullOrEmpty(data.UserName) || data.UserName.Length < 5 )
                return new BadRequestResult();

            var player = new Player
            {
                UserName = data.UserName,
                ID = Guid.NewGuid().ToString(),
            };
            var gamecode = CreateGameCode();
            var newGame = new Game
            {
                GameCode = gamecode,
                OwnerID = player.ID,
                RoundsLeft = 10,
                SecondsPerRound = 30, 
                Players = new List<Player>(new[] { player })
            };
            try
            {
                await starter.StartNewAsync(nameof(LobbyOrchestrator), gamecode, newGame);
            }
            catch (Exception e)
            {
                log.LogError("EXCEPTION: " + e.Message);
                return new BadRequestObjectResult($"Failed to create game with code {gamecode} and playerid {player.ID}.");
            }

            var response = new GameState
            {
                GameCode = gamecode,
                Players = new List<SlimPlayer> { new SlimPlayer { UserName = player.UserName } },
                Started = false,
                PlayerID = player.ID
            };
            return new OkObjectResult(response);
        }

        [FunctionName("join")]
        public static async Task<IActionResult> Join(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log,
            [DurableClient] IDurableOrchestrationClient client)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<JoinRequest>(requestBody);

            data.UserName = data.UserName.Replace(" ", "");

            if (string.IsNullOrWhiteSpace(data.UserName) || data.UserName.Length < 5 || string.IsNullOrWhiteSpace(data.GameCode))
                return new BadRequestResult();

            var player = new Player
            {
                UserName = data.UserName,
                ID = Guid.NewGuid().ToString(),
            };

            var status = await client.GetStatusAsync(data.GameCode);

            if(status == null)
                return new NotFoundResult();

            if (status.RuntimeStatus != OrchestrationRuntimeStatus.Running)
                return new BadRequestResult();

            if (status.CustomStatus == null || status.CustomStatus.ToObject<Game>().Players.Any(p => p.UserName == data.UserName))
                return new BadRequestResult();

            await client.RaiseEventAsync(data.GameCode, "Join", player);

            var players = status.CustomStatus.ToObject<Game>().Players;
            players.Add(player);
            var response = new GameState
            {
                GameCode = data.GameCode,
                Players = players.Select(p => new SlimPlayer { UserName = p.UserName }).ToList(),
                Started = false,
                PlayerID = player.ID
            };

            return new OkObjectResult(response);
        }

        [FunctionName("start")]
        public static async Task<IActionResult> Start(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log,
            [DurableClient] IDurableOrchestrationClient client)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<StartRequest>(requestBody);

            if(data.GameCode == null || data.PlayerID == null)
                return new BadRequestResult();

            var status = await client.GetStatusAsync(data.GameCode);

            if (status == null)
                return new NotFoundResult();

            if (status.RuntimeStatus != OrchestrationRuntimeStatus.Running)
                return new BadRequestResult();

            var game = status.CustomStatus.ToObject<Game>();
            
            if (game.OwnerID != data.PlayerID)
                return new BadRequestResult();

            await client.RaiseEventAsync(game.GameCode, "Start");

            return new OkResult();
        }

        private static string CreateGameCode()
        {
            string code = "";
            for (int i = 0; i < 6; i++)
            {
                int val = _random.Next(0, 26 + 10);
                if (val < 10) code += val;
                else code += (char)('A' + val - 10);
            }
            return code;
        }
    }
}

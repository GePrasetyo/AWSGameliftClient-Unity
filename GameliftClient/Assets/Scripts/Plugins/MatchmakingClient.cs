using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.GameLift;
using Amazon.GameLift.Model;

public class MatchmakingClient : GameLiftClient
{
    public Action MatchmakingComplete;
    string _cacheTicketID;
    public event Action<string> LoadingUpdate;
    public Action UncanceledRequest;


    public MatchmakingClient(string clientID, AmazonGameLiftClient clientIdentity, Amazon.DynamoDBv2.AmazonDynamoDBClient dbClient)
    {
        Console.WriteLine("GameLiftClient created");
        
        _playerUuid = clientID;
        _clientIdentity = clientIdentity;
        _ddbIdentity = dbClient;
        
        Setup();
    }

    async private void Setup()
    {
        Console.WriteLine("Trying matchmaking");
        LoadingUpdate?.Invoke("Start Matchmaking");

        MatchmakingTicket ticketMatch = null;
        var maxRetryAttempts = 3;

        await RetryHelper.RetryOnExceptionAsync<Exception>
            (maxRetryAttempts, async () =>
            {
                ticketMatch = await StartMatchmaking();
            }, _cancelToken.Token);



        if (ticketMatch != null)
            CheckStatus(ticketMatch);
        else
        { 
            Console.WriteLine("FAILED to start matchmaking.");
            MatchmakingComplete?.Invoke();
        }
    }

    async public void TriggerCancelMatchmaking()
    {
        Console.WriteLine("Stop matchmaking");

        int statusCode = 0;
        var maxRetryAttempts = 3;

        await RetryHelper.RetryOnExceptionAsync<Exception>
            (maxRetryAttempts, async () =>
            {
                statusCode = await CancelMatchmaking();
            }, _cancelToken.Token);

        if (statusCode == 200)
        {
            CancelToken();
            Console.WriteLine("SUCCESS to cancel matchmaking.");
        }
        else
        {
            Console.WriteLine("FAILED to cancel matchmaking.");
        }

        DetachHookGameInstanceDead();
    }

    #region Matchmaking

    async private Task<MatchmakingTicket> StartMatchmaking() 
    {
        Console.WriteLine("Start Matchmaking");
        var startMatchmakingRequest = new StartMatchmakingRequest
        {
            ConfigurationName = "10v10Demo",
            TicketId = Guid.NewGuid().ToString()
        };

        var playerData = new Amazon.GameLift.Model.Player
        {
            PlayerId = _playerUuid,
            Team = "players"
        };

        playerData.LatencyInMs.Add("ap-southeast-2", 60);
        playerData.LatencyInMs.Add("ap-southeast-1", 60);
        playerData.PlayerAttributes.Add("level", new AttributeValue { N = 10 });//LEVEL Player

        var attValueCharacter = new AttributeValue();
        attValueCharacter.SL.Add("warrior");
        playerData.PlayerAttributes.Add("character", attValueCharacter);
        startMatchmakingRequest.Players.Add(playerData);

        Task<StartMatchmakingResponse> startMatchmakingTask = _clientIdentity.StartMatchmakingAsync(startMatchmakingRequest);
        Console.WriteLine("after task startMatchmakingTask");
        StartMatchmakingResponse startMatchmakingResponse = await startMatchmakingTask;
        Console.WriteLine("after startMatchmakingTask");

        return startMatchmakingResponse.MatchmakingTicket;
    }

    async private void CheckStatus(MatchmakingTicket ticketMatch)
    {
        DebugHelper.Default("status " + ticketMatch.Status);
        DynamoDBService poolingTicket = new DynamoDBService(_ddbIdentity);
        var dbTicket = new Dictionary<string, string>();

        var maxAttempts = 100; // 2*30 timeout = 1 menit
        var attempts = 0;
        var status = "";
        _cacheTicketID = ticketMatch.TicketId;

        do
        {
            //_cancelToken.Token.ThrowIfCancellationRequested();

            dbTicket = await poolingTicket.PoolFlexmatchTicket(ticketMatch.TicketId);

            if (dbTicket != null)
            {
                status = dbTicket["StatusAttribute"]; //Ticket Status Attribute in DynamoDB
                DebugHelper.Default("Attempt #" + attempts + " Result : " + status);

                switch (status)
                {
                    case "MatchmakingSucceeded":
                        DebugHelper.Default("Matchmaking Success");
                        UncanceledRequest?.Invoke();

                        var maxRetryAttempts = 3;
                        await RetryHelper.RetryOnExceptionAsync<Exception>
                        (maxRetryAttempts, async () =>
                        {
                            ticketMatch = await CheckMatchmakingRequest(ticketMatch.TicketId);
                        }, _cancelToken.Token);

                        if (ticketMatch != null)
                        {
                            // created a player session in there                
                            DebugHelper.Default("Matchmaking : getting my session ID");

                            var mySessionID = ticketMatch.GameSessionConnectionInfo.MatchedPlayerSessions.Find((p) => p.PlayerId == _playerUuid).PlayerSessionId;
                            DebugHelper.Default("Matchmaking : I got my session ID " + mySessionID);

                            //Disconnect before jump to arena
                            for (int i = 6; i >= 0; i--)
                            { 
                                LoadingUpdate?.Invoke("Match Start in "+i+"s");

                                await Task.Delay(GlobalVariables.wait1sec);
                            }

                            // establish connection with server
                            MatchmakingComplete?.Invoke();

                            //Disconnect to old connection first

                            // Then go to server from matchmaking
                        }
                        else
                            MatchmakingComplete?.Invoke();

                        attempts -= 100;
                        break;
                    case "MatchmakingSearching":
                        //Update your loading info "Searching Players  : <Player Count>" 
                        break;
                    case "PotentialMatchCreated":
                        UncanceledRequest?.Invoke();
                        //Update your loading info "Searching Players  : <Player Count>" 
                        break;
                    case "MatchmakingTimedOut":
                    case "MatchmakingFailed":
                        DebugHelper.Warning("Matchmaking Status : " + status);
                        UncanceledRequest?.Invoke();
                        MatchmakingComplete?.Invoke();
                        attempts -= 100;
                        break;
                    case "MatchmakingCancelled":
                        DebugHelper.Warning("Matchmaking Status : " + status);
                        attempts -= 100;
                        break;
                }
            }

            if (attempts < 0)
                break;

            attempts++;
            await Task.Delay(GlobalVariables.wait3sec);

        } while (attempts < maxAttempts);

        if (attempts >= maxAttempts)
        {
            DebugHelper.Default("Matchmaking Client Timed Out");
            MatchmakingComplete?.Invoke();
        }

    }

    async private Task<MatchmakingTicket> CheckMatchmakingRequest(string ticketID) 
    {
        Console.WriteLine("Check Matchmaking");
        var describeMatchmakingRequest = new DescribeMatchmakingRequest();
        describeMatchmakingRequest.TicketIds.Add(ticketID);

        Task<DescribeMatchmakingResponse> describeMatchmakingTask = _clientIdentity.DescribeMatchmakingAsync(describeMatchmakingRequest);
        Console.WriteLine("after task describeMatchmakingTask");
        DescribeMatchmakingResponse describeMatchmakingResponse = await describeMatchmakingTask;
        Console.WriteLine("after startMatchmakingTask");

        if (describeMatchmakingResponse.TicketList.Count > 0)
            return describeMatchmakingResponse.TicketList[0];
        else
            return null;
    }

    async private Task<int> CancelMatchmaking()
    {
        Console.WriteLine("Check Matchmaking");
        var cancelMatchmakingRequest = new StopMatchmakingRequest
        {
            TicketId = _cacheTicketID
        };

        Task<StopMatchmakingResponse> cancelMatchmakingTask = _clientIdentity.StopMatchmakingAsync(cancelMatchmakingRequest);
        Console.WriteLine("after task cancelMatchmakingTask");

        StopMatchmakingResponse cancelMatchmakingResponse = await cancelMatchmakingTask;
        Console.WriteLine("after cancelMatchmakingTask");

        UncanceledRequest?.Invoke();
        MatchmakingComplete?.Invoke();

        return (int)cancelMatchmakingResponse.HttpStatusCode;
    }

    #endregion

    internal override void DetachHookGameInstanceDead()
    {
        base.DetachHookGameInstanceDead();
        GameLiftClientHandler.GameInstanceDead -= TriggerCancelMatchmaking;
        GameLiftClientHandler.ClientLostConnection -= TriggerCancelMatchmaking;
    }
}

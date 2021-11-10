using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.GameLift;
using Amazon.GameLift.Model;

public class PlacementSessionClient : GameLiftClient
{
    private readonly string[] _aliases = new string[2] { "alias-9a6bea37-d2cb-4cb3-8ca0-62656ad3c4b5", "alias-8441478a-d300-4f6d-aeef-8b09e076a248" };

    private readonly string _mapIndex;
    private readonly string _gameMode;    

    public PlacementSessionClient(string mapIdx, string gameMode, string clientID, AmazonGameLiftClient clientIdentity, Amazon.DynamoDBv2.AmazonDynamoDBClient dbClient)
    {
        DebugHelper.Default("GameLiftClient created");

        _mapIndex = mapIdx;
        _gameMode = gameMode;

        _playerUuid = clientID;
        _clientIdentity = clientIdentity;
        _ddbIdentity = dbClient;

        Setup();
    }

    #region player session

    async private void CreatePlayerSession(GameSession gameSession)
    {
        PlayerSession playerSession = null;

        var maxRetryAttempts = 3;
        await RetryHelper.RetryOnExceptionAsync<Exception>
        (maxRetryAttempts, async () =>
        {
            playerSession = await CreatePlayerSessionAsync(gameSession);
        }, _cancelToken.Token);

        if (playerSession != null)
        {
            // created a player session in there
            DebugHelper.Default("Player session created.");
            DebugHelper.Default($"CLIENT CONNECT INFO: {playerSession.IpAddress}, {playerSession.Port}, {playerSession.PlayerSessionId} ");

            // establish connection with server
            await Task.Delay(GlobalVariables.wait3sec);
            // Enter Server Now
        }
        else
            DebugHelper.Warning("Failed Enter Server : Failed create player session");

        DetachHookGameInstanceDead();
    }

    async private Task<PlayerSession> CreatePlayerSessionAsync(GameSession gameSession)
    {
        var createPlayerSessionRequest = new CreatePlayerSessionRequest
        {
            GameSessionId = gameSession.GameSessionId,
            PlayerId = _playerUuid
        };

        Task<CreatePlayerSessionResponse> createPlayerSessionResponseTask = _clientIdentity.CreatePlayerSessionAsync(createPlayerSessionRequest);
        CreatePlayerSessionResponse createPlayerSessionResponse = await createPlayerSessionResponseTask;

        string playerSessionId = createPlayerSessionResponse.PlayerSession != null ? createPlayerSessionResponse.PlayerSession.PlayerSessionId : "N/A";
        DebugHelper.Default((int)createPlayerSessionResponse.HttpStatusCode + " PLAYER SESSION CREATED: " + playerSessionId);
        return createPlayerSessionResponse.PlayerSession;
    }

    #endregion

    #region GameSession
    async private Task<GameSessionPlacement> StartGameSessionPlacementAsync() //Placement Doesn't need create player session seperate
    {
        DebugHelper.Default("CreateGameSessionPlacement");
        var startGameSessionPlacementRequest = new StartGameSessionPlacementRequest
        {
            GameSessionQueueName = "AsteriaIQ",
            PlacementId = Guid.NewGuid().ToString(),
            MaximumPlayerSessionCount = 50,
            GameSessionName = _mapIndex + _playerUuid.Substring(0, 3)
        };

        startGameSessionPlacementRequest.GameProperties.Add(new GameProperty
        {
            Key = "MAP_NAME",
            Value = _mapIndex
        });

        startGameSessionPlacementRequest.GameProperties.Add(new GameProperty
        {
            Key = "GAME_MODE",
            Value = _gameMode
        });

        startGameSessionPlacementRequest.PlayerLatencies.Add(new PlayerLatency
        {
            LatencyInMilliseconds = 60f,
            RegionIdentifier = "ap-southeast-1", //REGION Identifier
            PlayerId = _playerUuid
        });

        startGameSessionPlacementRequest.DesiredPlayerSessions.Add(new DesiredPlayerSession
        {
            PlayerId = _playerUuid
        });

        Task<StartGameSessionPlacementResponse> startGameSessionPlacementTask = _clientIdentity.StartGameSessionPlacementAsync(startGameSessionPlacementRequest);
        DebugHelper.Default("after task startGameSessionPlacementTask");
        StartGameSessionPlacementResponse startPlacementResponse = await startGameSessionPlacementTask;
        DebugHelper.Default("after startGameSessionPlacementTask");

        DebugHelper.Default((int)startPlacementResponse.HttpStatusCode + " GAME SESSION PLACED: " + startPlacementResponse.GameSessionPlacement.PlacementId);

        return startPlacementResponse.GameSessionPlacement;
    }

    async private Task<GameSessionPlacement> CheckGameSessionPlacementAsync(GameSessionPlacement sessionPlacement)
    {
        DebugHelper.Default("CheckGameSessionPlacement");
        var describeGameSessionPlacementRequest = new DescribeGameSessionPlacementRequest
        {
            PlacementId = sessionPlacement.PlacementId
        };

        Task<DescribeGameSessionPlacementResponse> describeGameSessionPlacementTask = _clientIdentity.DescribeGameSessionPlacementAsync(describeGameSessionPlacementRequest);
        DebugHelper.Default("after task describeGameSessionRequestTask");
        DescribeGameSessionPlacementResponse describePlacementResponse = await describeGameSessionPlacementTask;
        DebugHelper.Default("after createGameSessionRequestTask");

        string placementId = describePlacementResponse.GameSessionPlacement != null ? describePlacementResponse.GameSessionPlacement.PlacementId : "N/A";

        DebugHelper.Default((int)describePlacementResponse.HttpStatusCode + " GAME SESSION CREATED: " + placementId);

        return describePlacementResponse.GameSessionPlacement;
    }

    async private Task<GameSessionDetail> DescribeGameSessionsDetailAsync(string gameSessionID)
    {
        DebugHelper.Default("Describe Game Sessions");
        var describeGameSessionDetailRequest = new DescribeGameSessionDetailsRequest
        {
            GameSessionId = gameSessionID
        };

        Task<DescribeGameSessionDetailsResponse> describeGameSessionsDetailsTask = _clientIdentity.DescribeGameSessionDetailsAsync(describeGameSessionDetailRequest);
        DescribeGameSessionDetailsResponse describeGameSessionsDetailsResponse = await describeGameSessionsDetailsTask;

        var sessionDetail = describeGameSessionsDetailsResponse.GameSessionDetails.Count > 0 ? describeGameSessionsDetailsResponse.GameSessionDetails[0] : null;

        return sessionDetail;
    }

    async private Task<GameSession> SearchGameSessionsAsync(string aliasID)
    {
        var filter = string.Format("hasAvailablePlayerSessions=true AND gameSessionProperties.{0}='{1}'", "MAP_NAME", _mapIndex);
        DebugHelper.Default("SearchGameSessions for " + filter);

        var searchGameSessionsRequest = new SearchGameSessionsRequest
        {
            AliasId = aliasID, // can also use AliasId
            FilterExpression = filter,
            SortExpression = "creationTimeMillis ASC", // return oldest first
            Limit = 1 // only one session even if there are other valid ones
        };

        Task<SearchGameSessionsResponse> SearchGameSessionsResponseTask = _clientIdentity.SearchGameSessionsAsync(searchGameSessionsRequest);
        SearchGameSessionsResponse searchGameSessionsResponse = await SearchGameSessionsResponseTask;

        int gameSessionCount = searchGameSessionsResponse.GameSessions.Count;
        DebugHelper.Default($"GameSessionCount:  {gameSessionCount}");

        if (gameSessionCount > 0)
        {
            DebugHelper.Default("We have game sessions!");
            DebugHelper.Default(searchGameSessionsResponse.GameSessions[0].GameSessionId);
            return searchGameSessionsResponse.GameSessions[0];
        }
        return null;
    }

    #endregion


    async private Task CheckStatus(GameSessionPlacement sessionPlacement)
    {
        DebugHelper.Default("status " + sessionPlacement.Status);
        DynamoDBService poolingTicket = new DynamoDBService(_ddbIdentity);
        var dbTicket = new Dictionary<string, string>();

        DebugHelper.Default("CheckStatus Pending");

        var maxAttempts = 30; // 2*30 timeout = 1 menit
        var attempts = 0;
        var status = "";

        do
        {
            _cancelToken.Token.ThrowIfCancellationRequested();
            dbTicket = await poolingTicket.PoolPlacemenTicket(sessionPlacement.PlacementId);

            if (dbTicket != null)
            {
                status = dbTicket["StatusAttribute"]; //Ticket Status Attribute in DynamoDB

                DebugHelper.Default("Attempt #" + attempts + " Result : " + status);
                switch (status)
                {
                    case "PlacementFulfilled":
                        DebugHelper.Default("Placement Success");
                        GameSessionDetail sessionDetail = null;

                        var maxRetryAttempts = 3;
                        await RetryHelper.RetryOnExceptionAsync<Exception>
                        (maxRetryAttempts, async () =>
                        {
                            sessionPlacement = await CheckGameSessionPlacementAsync(sessionPlacement);
                        }, _cancelToken.Token);

                        await RetryHelper.RetryOnExceptionAsync<Exception>
                        (maxRetryAttempts, async () =>
                        {
                            sessionDetail = await DescribeGameSessionsDetailAsync(sessionPlacement.GameSessionId);
                        }, _cancelToken.Token);

                        if (sessionDetail != null)
                        {
                            DebugHelper.Default("Placement Flow Complete - Go To Server Now.");
                            DebugHelper.Default($"CLIENT CONNECT INFO: {sessionDetail.GameSession.IpAddress}, {sessionDetail.GameSession.Port}");

                            // establish connection with server
                            await Task.Delay(GlobalVariables.wait3sec);
                            // Enter Server Now
                        }
                        else
                            DebugHelper.Warning("Failed Enter Server : No Session Details");

                        attempts = -10;
                        break;
                    case "PlacementTimedOut":
                    case "PlacementFailed":
                    case "PlacementCancelled":
                        DebugHelper.Warning("Placement Result : " +status);
                        
                        //Server Placement Failed/ Cancelled

                        attempts = -10;
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
            DebugHelper.Default("Client Connection Timed Out"); //Failed server placement : too much attempt
        }

        DetachHookGameInstanceDead();
    }

    async private void Setup()
    {
        DebugHelper.Default("setup");
        GameSession gameSession = null;

        foreach (var a in _aliases)
        {
            try
            {
                DebugHelper.Default("Add task search each aliases : " + a);
                gameSession = await SearchGameSessionsAsync(a);

                if (gameSession != null)
                    break;
            }
            catch (Exception ex)
            {
                DebugHelper.Excpetion(ex);
            }

            await Task.Delay(GlobalVariables.wait1sec);
        }


        if (gameSession == null)
        {
            DebugHelper.Default("Trying Session Placement");
            GameSessionPlacement sessionPlacement = null;
            var maxRetryAttempts = 3;

            await RetryHelper.RetryOnExceptionAsync<Exception>
            (maxRetryAttempts, async () =>
            {
                sessionPlacement = await StartGameSessionPlacementAsync();
            }, _cancelToken.Token);

            if (sessionPlacement != null)
                await CheckStatus(sessionPlacement);
            else
            {
                DebugHelper.Default("FAILED to create new game session.");
                //Failed create new game session
            }
        }
        else
        {
            DebugHelper.Default("Game session found.");

            // game session found, create player session and connect to server
            CreatePlayerSession(gameSession);
        }
    }
}

using Amazon.GameLift;
using System.Threading;

public class GameLiftClient
{
    internal AmazonGameLiftClient _clientIdentity;
    internal Amazon.DynamoDBv2.AmazonDynamoDBClient _ddbIdentity;

    //internal MultiplayerPortal _portal;
    internal string _playerUuid;

    internal readonly CancellationTokenSource _cancelToken = new CancellationTokenSource();
    public void CancelToken() => _cancelToken.Cancel();

    internal virtual void DetachHookGameInstanceDead()
    {
        GameLiftClientHandler.GameInstanceDead -= CancelToken;
        GameLiftClientHandler.ClientLostConnection -= CancelToken;
    } 
}

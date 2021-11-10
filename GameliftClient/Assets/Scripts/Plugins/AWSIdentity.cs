using Amazon.CognitoIdentity;
using Amazon.GameLift;
using Amazon.DynamoDBv2;
using Amazon;
using System;

public class GameLiftIdentity
{
    public AmazonGameLiftClient AmazonGameLiftClient;
    public AmazonGameLiftClient AmazonMatcmakingClient;

    public string PlayerUuid;

    private string _cognitoIdentityPool = "<Cognito Identity Pool for placement>";
    private string _cognitoFlexmatch = "<Cognito Identity Pool for flexmatch>";


    public GameLiftIdentity()
    {        
        PlayerUuid = Guid.NewGuid().ToString();
        CreateGameLiftClient();
    }

    private void CreateGameLiftClient()
    {
        CognitoAWSCredentials credentials = new CognitoAWSCredentials(
           _cognitoIdentityPool,
           RegionEndpoint.APSoutheast1
        );

        CognitoAWSCredentials credentialsFlexmatch = new CognitoAWSCredentials (
            _cognitoFlexmatch, // Identity pool ID
            RegionEndpoint.APNortheast1 // Region
        );

        //if (!_isLocal)
            AmazonGameLiftClient = new AmazonGameLiftClient(credentials, RegionEndpoint.APSoutheast1);
            AmazonMatcmakingClient = new AmazonGameLiftClient(credentialsFlexmatch, RegionEndpoint.APNortheast1);

        //else
        //{
        //    // local testing
        //    // guide: https://docs.aws.amazon.com/gamelift/latest/developerguide/integration-testing-local.html
        //    AmazonGameLiftConfig amazonGameLiftConfig = new AmazonGameLiftConfig()
        //    {
        //        ServiceURL = "http://localhost:9080"
        //    };
        //    AmazonGameLiftClient = new AmazonGameLiftClient("asdfasdf", "asdf", amazonGameLiftConfig);
        //}
    }
}


public class DynamoDBIdentity
{
    public AmazonDynamoDBClient AmazonDDBClient;
    public AmazonDynamoDBClient AmazonDDBMatchmakingClient;

    private string _cognitoPlacementDB = "ap-southeast-1:77b40861-1dbb-4dc7-9082-c29e6ad7e724";
    private string _cognitoFlexmatchDB = "ap-northeast-1:e6f014c8-7a28-43e2-8a50-fe4b54cbbd63";


    public DynamoDBIdentity()
    {
        CreateDynamoDBClient();
    }

    private void CreateDynamoDBClient()
    {
        CognitoAWSCredentials credentials = new CognitoAWSCredentials(_cognitoPlacementDB, RegionEndpoint.APSoutheast1);

        AmazonDDBClient = new AmazonDynamoDBClient(
            credentials,
            RegionEndpoint.APSoutheast1);

        CognitoAWSCredentials credentialsFlexmatch = new CognitoAWSCredentials(_cognitoFlexmatchDB, RegionEndpoint.APNortheast1);

        AmazonDDBMatchmakingClient = new AmazonDynamoDBClient(
            credentialsFlexmatch,
            RegionEndpoint.APNortheast1);
    }
}
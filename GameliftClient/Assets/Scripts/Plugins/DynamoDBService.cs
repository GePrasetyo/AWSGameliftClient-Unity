using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.DynamoDBv2.DocumentModel;


public class DynamoDBService
{
    private readonly AmazonDynamoDBClient _ddbClient;
    protected IAmazonDynamoDB Client
    {
        get
        {
            return _ddbClient;
        }
    }

    public DynamoDBService(AmazonDynamoDBClient ddbIdentity)
    {
        _ddbClient = ddbIdentity;
    }

    async public Task<int> WritingPlacementTicket(string placementID, string placementStatus)
    {
        // Define item attributes
        Dictionary<string, AttributeValue> attributes = new Dictionary<string, AttributeValue>();

        // Author is hash-key
        attributes["ID"] = new AttributeValue { S = placementID };
        attributes["STATUS"] = new AttributeValue { S = placementStatus };
        attributes["ttl"] = new AttributeValue { N = (Math.Floor(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000f) + 10).ToString() };

        // Create PutItem request
        PutItemRequest request = new PutItemRequest
        {
            TableName = "PLACEMENT_TABLE",
            Item = attributes
        };
        
        Task<PutItemResponse> putItemTask = Client.PutItemAsync(request);
        PutItemResponse putItemResponse = await putItemTask;

        return (int)putItemResponse.HttpStatusCode;
    }

    async public Task<Dictionary<string, string>> PoolPlacemenTicket(string placementID)
    {
        // Define item key
        //  Hash-key of the target item is string value "Mark Twain"
        Dictionary<string, AttributeValue> key = new Dictionary<string, AttributeValue>
        {
            { "ID", new AttributeValue { S = placementID } },
        };

        // Create GetItem request
        GetItemRequest request = new GetItemRequest
        {
            TableName = "PLACEMENT_TABLE",
            Key = key,
        };

        Task<GetItemResponse> getItemTask = Client.GetItemAsync(request);
        GetItemResponse getItemResponse = await getItemTask;

        if (getItemResponse.Item.Count == 0)
            return null;

        Dictionary<string, string> ticket = new Dictionary<string, string>
        {
            { "ID", getItemResponse.Item["ID"].S },
            { "STATUS", getItemResponse.Item["STATUS"].S }
        };

        return ticket;
    }

    async public Task<int> WritingFlexMatchTicket(string ticketID, string ticketStatus)
    {
        // Define item attributes
        Dictionary<string, AttributeValue> attributes = new Dictionary<string, AttributeValue>();

        // Author is hash-key
        attributes["ID"] = new AttributeValue { S = ticketID };
        attributes["STATUS"] = new AttributeValue { S = ticketStatus };
        attributes["ttl"] = new AttributeValue { N = (Math.Floor(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000f) + 10).ToString() };

        // Create PutItem request
        PutItemRequest request = new PutItemRequest
        {
            TableName = "MATCHMAKING_TABLE",
            Item = attributes
        };

        Task<PutItemResponse> putItemTask = Client.PutItemAsync(request);
        PutItemResponse putItemResponse = await putItemTask;

        return (int)putItemResponse.HttpStatusCode;
    }

    async public Task<Dictionary<string, string>> PoolFlexmatchTicket(string ticketID)
    {
        // Define item key
        //  Hash-key of the target item is string value "Mark Twain"
        Dictionary<string, AttributeValue> key = new Dictionary<string, AttributeValue>
        {
            { "ID", new AttributeValue { S = ticketID } },
        };

        // Create GetItem request
        GetItemRequest request = new GetItemRequest
        {
            TableName = "MATCHMAKING_TABLE",
            Key = key,
        };

        Task<GetItemResponse> getItemTask = Client.GetItemAsync(request);
        GetItemResponse getItemResponse = await getItemTask;

        if (getItemResponse.Item.Count == 0 || getItemResponse.Item["STATUS"].S == "QUEUED")
            return null;

        DebugHelper.Default(getItemResponse.Item["STATUS"].S);

        Dictionary<string, string> ticket = new Dictionary<string, string>
        {
            { "ID", getItemResponse.Item["ID"].S },
            { "STATUS", getItemResponse.Item["STATUS"].S },
            { "PLAYER_COUNT", getItemResponse.Item["PLAYER_COUNT"].N }
        };

        return ticket;
    }
}

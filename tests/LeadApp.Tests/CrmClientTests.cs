using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LeadApp.Domain;
using LeadApp.Options;
using LeadApp.Services.Crm;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LeadApp.Tests;

public class CrmClientTests
{
    private static Lead SampleLead(string? urlId = null, string? email = "marko.markovic@example.com") => new()
    {
        FullName = "Marko Marković",
        CustomerType = CustomerType.FL,
        PhoneNumber = "+38267123456",
        Email = email,
        CityId = 1,
        LocalCommunityId = 101,
        SourceCode = SourceCodes.Email,
        RequestedAmount = 25000m,
        CampaignUrlId = urlId
    };

    [Fact]
    public void Request_json_matches_the_spec_example()
    {
        var json = JsonSerializer.Serialize(CreateSalesOpportunityRequest.FromLead(SampleLead()), CrmJson.Options);
        var node = JsonNode.Parse(json)!.AsObject();

        Assert.Equal(
            ["fullName", "customerTypeCode", "phoneNumber", "email", "cityId", "localCommunityId", "sourceCode", "requestedAmount"],
            node.Select(kv => kv.Key));
        Assert.Equal("FL", (string?)node["customerTypeCode"]);
        Assert.Equal(25000m, (decimal)node["requestedAmount"]!);
        Assert.Equal(101, (long)node["localCommunityId"]!);
    }

    [Fact]
    public void Optional_fields_are_omitted_when_empty_and_urlId_is_sent_for_campaigns()
    {
        var withoutEmail = JsonNode.Parse(JsonSerializer.Serialize(CreateSalesOpportunityRequest.FromLead(SampleLead(email: null)), CrmJson.Options))!.AsObject();
        Assert.False(withoutEmail.ContainsKey("email"));
        Assert.False(withoutEmail.ContainsKey("urlId"));

        var campaign = JsonNode.Parse(JsonSerializer.Serialize(CreateSalesOpportunityRequest.FromLead(SampleLead(urlId: "agro-viber")), CrmJson.Options))!;
        Assert.Equal("agro-viber", (string?)campaign["urlId"]);
    }

    [Fact]
    public async Task Created_response_is_parsed_and_api_key_header_is_sent()
    {
        var handler = new StubHandler(HttpStatusCode.Created,
            """{"salesOpportunityId":812,"phaseCode":"CREATED","customerId":null,"leadId":231,"matchResult":"LEAD_CREATED"}""");
        var client = CreateClient(handler);

        var result = await client.CreateSalesOpportunityAsync(CreateSalesOpportunityRequest.FromLead(SampleLead()));

        Assert.True(result.Success);
        Assert.Equal(812, result.Response!.SalesOpportunityId);
        Assert.Equal(231, result.Response.LeadId);
        Assert.Null(result.Response.CustomerId);
        Assert.Equal("LEAD_CREATED", result.Response.MatchResult);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://crm.test/api/sales-opportunity/web/create", handler.Request.RequestUri!.ToString());
        Assert.Equal("App secret-key", handler.Request.Headers.GetValues(CrmClient.ApiKeyHeader).Single());
    }

    [Fact]
    public async Task Validation_error_model_is_turned_into_a_readable_message()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest,
            """{"errorType":"VALIDATION_ERROR","message":"Neispravan integracioni zahtjev.","validationErrors":[{"field":"localCommunityId","message":"Ne pripada opštini."}],"timestamp":"2026-09-01 14:30:00"}""");

        var result = await CreateClient(handler).CreateSalesOpportunityAsync(CreateSalesOpportunityRequest.FromLead(SampleLead()));

        Assert.False(result.Success);
        Assert.Contains("400", result.Error);
        Assert.Contains("VALIDATION_ERROR", result.Error);
        Assert.Contains("localCommunityId: Ne pripada opštini.", result.Error);
    }

    [Fact]
    public async Task Invalid_api_key_is_reported()
    {
        var handler = new StubHandler(HttpStatusCode.Unauthorized,
            """{"errorType":"API_KEY_ERROR","message":"Nevažeći API ključ.","validationErrors":null,"timestamp":"2026-09-01 14:30:00"}""");

        var result = await CreateClient(handler).CreateSalesOpportunityAsync(CreateSalesOpportunityRequest.FromLead(SampleLead()));

        Assert.Contains("API_KEY_ERROR", result.Error);
    }

    [Fact]
    public async Task Network_failure_does_not_throw()
    {
        var handler = new StubHandler(new HttpRequestException("connection refused"));

        var result = await CreateClient(handler).CreateSalesOpportunityAsync(CreateSalesOpportunityRequest.FromLead(SampleLead()));

        Assert.False(result.Success);
        Assert.Contains("connection refused", result.Error);
    }

    [Fact]
    public async Task Missing_api_key_fails_without_calling_the_crm()
    {
        var handler = new StubHandler(HttpStatusCode.Created, "{}");

        var result = await CreateClient(handler, apiKey: null).CreateSalesOpportunityAsync(CreateSalesOpportunityRequest.FromLead(SampleLead()));

        Assert.False(result.Success);
        Assert.Null(handler.Request);
    }

    private static CrmClient CreateClient(StubHandler handler, string? apiKey = "secret-key")
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://crm.test") };
        var options = MsOptions.Create(new CrmOptions { BaseUrl = "https://crm.test", ApiKey = apiKey });
        return new CrmClient(http, options, NullLogger<CrmClient>.Instance);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body = "";
        private readonly Exception? _exception;

        public StubHandler(HttpStatusCode status, string body) => (_status, _body) = (status, body);
        public StubHandler(Exception exception) => _exception = exception;

        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            if (_exception is not null)
                throw _exception;

            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }
}

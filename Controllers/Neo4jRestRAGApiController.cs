using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver.Preview.Mapping;
using Neo4j.Driver;
using GroqSharp.Models;
using System.Text.Json.Nodes;
using System.Net.Mime;
using System.Text;
using System.Net.Http.Headers;
using Microsoft.SemanticKernel.Embeddings;
using RagRest.Clients;

namespace RagRest.Controllers
{
    [ApiController]
    [Microsoft.AspNetCore.Mvc.Route("/v1/chat")]
    public class Neo4jRestRAGApiController : ControllerBase
    {
        private readonly ILogger<Neo4jRestRAGApiController> _logger;
        private readonly ApplicationSettings _settings;
        private readonly ITextEmbeddingGenerationService _embeddingService;
        private readonly MyNeo4jClient _client;
        public Neo4jRestRAGApiController(ILogger<Neo4jRestRAGApiController> logger, ApplicationSettings settings, ITextEmbeddingGenerationService embeddingService, MyNeo4jClient client)
        {
            _logger = logger;
            _client = client;
            _settings = settings;
            _embeddingService = embeddingService;
        }
        
        private MessageRoleType getRole(String role)
        {
            switch (role)
            {
                case "assistant": return MessageRoleType.Assistant;
                case "system": return MessageRoleType.System;
                case "tool": return MessageRoleType.Tool;
                case "user": return MessageRoleType.User;
            }
            throw new Exception("unknown role");
        }
        public struct QueryResult : IEquatable<QueryResult> 
        {
            public Dictionary<string,object> Metadata{get;set ;} 
            public string Id { get; set; }
            public string Text { get; set; }
            public double Score { get; set; }
            public static QueryResult fromRecord(IRecord record)
            {
                return new QueryResult()
                {
                    Metadata = record.Get<Dictionary<string, object>>("metadata"),
                    Id = record.Get<string>("id"),
                    Text = record.Get<string>("text"),
                    Score = record.Get<double>("score")
                };
                
            }
            public bool Equals(QueryResult other)
            {
                return Metadata.Equals(other.Metadata) && Id.Equals(other.Id) && Text.Equals(other.Text) && Score == other.Score;
            }
        }

    
        [Microsoft.AspNetCore.Mvc.HttpPost]
        [Microsoft.AspNetCore.Mvc.Route("completions")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        public async Task Completions(JsonObject request)
        {
            var messages = request.Root["messages"]?.AsArray();
            if (messages == null) throw new Exception("invalid request");
            var lastMessage = messages.LastOrDefault();
            if (lastMessage == null) { throw new Exception("invalid request"); }
            var lastMessageContent = lastMessage["content"]?.GetValue<string>() ?? "";

            var lastMessageRole = lastMessage["role"]?.GetValue<string>() ?? "";
            await _client.ConnectAsync();

            var embeddingT = _embeddingService.GenerateEmbeddingAsync(lastMessageContent);
            var embedding = await embeddingT;
            var queryString =
                $"""
                CALL db.index.vector.queryNodes("{_settings.Neo4jIndexName}", 20, [{string.Join(",", embedding.ToArray())}])
                YIELD node, score 
                WHERE score >= 0.7
                RETURN properties(node) AS metadata, node.id AS id, node.text AS text, score
                """;
            var session = _client.Driver.AsyncSession();
            var query = await session.RunAsync(queryString);

            String context = "Context results from memory:\n=== START CONTEXTUAL RESULTS ===\n\n";
            bool any = false;
            var results = await query.ToListAsync();
            foreach (var res in results)
            {
                any = true;
                var qr = QueryResult.fromRecord(res);
                context += "=== START RESULT ===\n";
                context += "Result id: " + qr.Id + "\n";
                context += "Result score: " + qr.Score.ToString() + "\n";
                if (qr.Metadata.ContainsKey("file_name")) context += "Result source filename: " + qr.Metadata["file_name"].ToString() + "\n";
                context += "Result text content:\n" + qr.Text + "\n";
                context += "=== END RESULT ===\n";
            }
            context += "\n=== END CONTEXTUAL RESULTS ===\n\n";
            String question = !any ? lastMessageContent :
                "Use the following pieces of context results fetched from your memory to answer the question at the end.\n\n" +
                $"{context}\n\n" +
                $"Question: {lastMessageContent}";
            lastMessage["content"] = question;


            var _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? "");

            request["stream"] = true;
            var content = new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json");
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions") { Content = content };
            using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            Response.Headers.Append("Connection", "keep-alive");
            Response.Headers.Append("TransferEncoding", "chunked");
            foreach(var h in response.TrailingHeaders)
            {
                Response.AppendTrailer(h.Key, new Microsoft.Extensions.Primitives.StringValues(h.Value.ToArray()));
            }
            Response.StatusCode = response.StatusCode.As<int>();
            Response.ContentType = "text/event-stream";
            using var stream = await response.Content.ReadAsStreamAsync();
            stream.CopyTo(Response.BodyWriter.AsStream());
            await Response.CompleteAsync();
        }
    }
}
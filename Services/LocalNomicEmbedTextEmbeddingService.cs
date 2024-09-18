using LangChain.Providers.OpenAI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

namespace Neo4jRestRAGApi.Services
{
    public class LocalNomicEmbedTextEmbeddingService : ITextEmbeddingGenerationService
    {
        private OpenAiEmbeddingModel service;
        private OpenAiProvider provider;
        private OpenAI.OpenAIClient client = new OpenAI.OpenAIClient(new OpenAI.OpenAIAuthentication(""), new OpenAI.OpenAIClientSettings("localhost:11434", https: false));

        public LocalNomicEmbedTextEmbeddingService() : base()
        {
            provider = new OpenAiProvider(client);
            service = new OpenAiEmbeddingModel(
                provider,
                "29E1CA22-5713-40E1-A13F-03B2CE174A8D"
                );
        }

        public IReadOnlyDictionary<string, object?> Attributes
        {
            get
            {
                return null;
            }
        }

        Task<IList<ReadOnlyMemory<float>>> IEmbeddingGenerationService<string, float>.GenerateEmbeddingsAsync(IList<string> data, Kernel? kernel, CancellationToken cancellationToken)
        {
            IList<ReadOnlyMemory<float>> o = new List<ReadOnlyMemory<float>>();
            foreach (var d in data)
            {
                var request = LangChain.Providers.EmbeddingRequest.ToEmbeddingRequest(d);
                var embeddingResponseAsync = service.CreateEmbeddingsAsync(request, provider.EmbeddingSettings, cancellationToken);
                embeddingResponseAsync.Wait(cancellationToken);
                var embeddingResponse = embeddingResponseAsync.Result;
                var mem = new ReadOnlyMemory<float>(embeddingResponse.ToSingleArray());
                o.Add(mem);
            }
            return Task.FromResult(o);
        }
    }
}

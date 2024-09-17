using Neo4jClient;

namespace Neo4jRestRAGApi.Clients
{
    public class MyNeo4jClient : BoltGraphClient
    {
        public MyNeo4jClient(ApplicationSettings settings) : base(settings.Neo4jConnection, username: settings.Neo4jUser, settings.Neo4jPassword, realm: settings.Neo4jDatabase)
        {
        }
    }
}

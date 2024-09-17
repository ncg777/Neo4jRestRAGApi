using Neo4jClient;

namespace RagRest.Clients
{
    public class MyNeo4jClient : BoltGraphClient
    {
        public MyNeo4jClient(ApplicationSettings settings) : base(settings.Neo4jConnection, username: settings.Neo4jUser, settings.Neo4jPassword, realm: settings.Neo4jDatabase)
        {
        }
    }
}

using GamingCafe.ClientAgent.Configuration;

namespace GamingCafe.ClientAgent.Tests;

public sealed class MockConfigurationService : IConfigurationService
{
    public MockConfigurationService(AgentConfiguration config)
    {
        CurrentConfiguration = config;
    }

    public AgentConfiguration CurrentConfiguration { get; set; }
    public void Reload() { }
}

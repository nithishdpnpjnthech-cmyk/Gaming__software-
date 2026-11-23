namespace GamingCafe.ClientAgent.Configuration;

public interface IConfigurationService
{
    AgentConfiguration CurrentConfiguration { get; }
    void Reload();
}

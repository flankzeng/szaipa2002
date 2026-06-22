using Szaipa.Data.Scaffolding;

namespace Szaipa.Data.Abstractions;

public interface ILegacyScaffoldCommandService
{
    IReadOnlyList<LegacyScaffoldCommandSuggestion> GetSuggestions();
}

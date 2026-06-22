using Szaipa.Web.Models;

namespace Szaipa.Web.Services;

public interface IMigrationWorkspaceDiagnosticsService
{
    MigrationWorkspaceDiagnosticsModel GetDiagnostics();
}

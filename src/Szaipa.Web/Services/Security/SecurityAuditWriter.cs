using System.Text;
using Microsoft.Extensions.Options;
using Szaipa.Web.Configuration;
using Szaipa.Web.Infrastructure;

namespace Szaipa.Web.Services.Security;

/// <summary>
/// File-backed audit writer. One lock guards all appends; files roll by UTC day (requests-20260804.log,
/// logins-20260804.log), keep open writers per file so interleaved prefixes never churn handles, prune to
/// <see cref="SecurityMonitoringOptions.AuditKeepDays"/>, and every field is sanitized on write.
/// Lazy directory creation on first write; no file-system I/O in the constructor.
/// </summary>
public sealed class SecurityAuditWriter : ISecurityAuditWriter, IDisposable
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private readonly object _sync = new();
    private readonly SecurityMonitoringPaths _paths;
    private readonly IOptions<SecurityMonitoringOptions> _options;

    private readonly Dictionary<string, StreamWriter> _writers = new(StringComparer.Ordinal);
    private DateTime _lastPruneUtc = DateTime.MinValue;

    public SecurityAuditWriter(SecurityMonitoringPaths paths, IOptions<SecurityMonitoringOptions> options)
    {
        _paths = paths;
        _options = options;
    }

    public void WriteRequest(string formattedLine) => Append("requests", formattedLine);

    public void WriteLoginAttempt(string username, string clientIp, bool success)
    {
        var line = RequestAuditEntry.FormatLogin(
            DateTimeOffset.Now,
            username,
            clientIp,
            success);
        Append("logins", line);
    }

    private void Append(string prefix, string line)
    {
        if (!_options.Value.RequestAuditEnabled)
        {
            return;
        }

        var sanitized = RequestAuditEntry.SanitizeLine(line, _paths.AuditMaxLineLength);
        var nowUtc = DateTime.UtcNow;
        var file = Path.Combine(_paths.AuditDirectory, $"{prefix}-{nowUtc:yyyyMMdd}.log");

        lock (_sync)
        {
            try
            {
                if (!_writers.TryGetValue(file, out var writer))
                {
                    Directory.CreateDirectory(_paths.AuditDirectory);
                    writer = new StreamWriter(file, append: true, Utf8NoBom)
                    {
                        AutoFlush = true,
                    };
                    _writers[file] = writer;
                }

                writer.WriteLine(sanitized);
            }
            catch (UnauthorizedAccessException)
            {
                // Auditing must never take the request down; a permissions problem just drops the entry.
                CloseWriter(file);
            }
            catch (IOException)
            {
                CloseWriter(file);
            }

            if ((nowUtc - _lastPruneUtc).TotalHours >= 24)
            {
                _lastPruneUtc = nowUtc;
                PruneLocked();
            }
        }
    }

    private void PruneLocked()
    {
        try
        {
            if (!Directory.Exists(_paths.AuditDirectory))
            {
                return;
            }

            var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, _paths.AuditKeepDays));
            foreach (var file in Directory.EnumerateFiles(_paths.AuditDirectory, "*-*.log"))
            {
                DateTime lastWriteUtc;
                try
                {
                    lastWriteUtc = File.GetLastWriteTimeUtc(file);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                if (lastWriteUtc < cutoff)
                {
                    CloseWriter(file);
                    try
                    {
                        File.Delete(file);
                    }
                    catch (IOException)
                    {
                        // Ignore a concurrent delete/rotate race.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Ignore.
                    }
                }
            }
        }
        catch (IOException)
        {
            // Prune is best-effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Prune is best-effort.
        }
    }

    private void CloseWriter(string file)
    {
        if (_writers.Remove(file, out var writer))
        {
            try
            {
                writer.Flush();
                writer.Dispose();
            }
            catch (IOException)
            {
                // Ignore.
            }
            catch (ObjectDisposedException)
            {
                // Ignore.
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            foreach (var file in _writers.Keys.ToList())
            {
                CloseWriter(file);
            }
        }
    }
}

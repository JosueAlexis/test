using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ProyectoRH2025.Services
{
    public class SharePointHealthCheck : IHealthCheck
    {
        private readonly ISharePointTestService _sharePointService;

        public SharePointHealthCheck(ISharePointTestService sharePointService)
        {
            _sharePointService = sharePointService;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _sharePointService.TestConnectionAsync();

                return result.IsSuccess
                    ? HealthCheckResult.Healthy("SharePoint connection is healthy", result.Details)
                    : HealthCheckResult.Unhealthy("SharePoint connection failed",
                        new Exception(result.Error ?? "Unknown error"), result.Details);
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("SharePoint health check failed", ex);
            }
        }
    }
}
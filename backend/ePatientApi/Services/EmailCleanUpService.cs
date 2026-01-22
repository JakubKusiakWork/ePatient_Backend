using ePatientApi.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ePatientApi.Services
{
    public sealed class EmailCleanUpService(IServiceScopeFactory FacScope) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                using var scope = FacScope.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.UtcNow;

                var expired = await db.DoctorEmails
                    .Where(d => d.ExpiresAt != null && d.ExpiresAt < now)
                    .ToListAsync(cancelToken);

                if (expired.Count > 0)
                {
                    foreach (var d in expired)
                    {
                        d.RegistrationCode = null;
                        d.ExpiresAt = null;
                        d.GeneratedAt = null;
                    }

                    await db.SaveChangesAsync(cancelToken);
                }

                await Task.Delay(TimeSpan.FromMinutes(1), cancelToken);
            }
        }
    }
}
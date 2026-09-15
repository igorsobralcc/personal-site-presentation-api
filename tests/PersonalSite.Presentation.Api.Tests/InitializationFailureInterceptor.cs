using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PersonalSite.Presentation.Api.Tests;

internal sealed class InitializationFailureInterceptor(Exception failure) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromException<InterceptionResult<int>>(failure);
    }
}

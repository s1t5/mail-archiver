using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace MailArchiver.Data
{
    /// <summary>
    /// Includes the credential protector in the model cache key. The credential value
    /// converters close over the protector, so contexts created with different protectors
    /// (e.g. the no-op protector used by tests) must not share a cached model.
    /// </summary>
    public sealed class CredentialModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => (context.GetType(), designTime, (context as MailArchiverDbContext)?.CredentialProtector);
    }
}

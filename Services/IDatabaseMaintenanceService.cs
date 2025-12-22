namespace MailArchiver.Services
{
    public interface IDatabaseMaintenanceService
    {
        /// <summary>
        /// Performs database maintenance operations (VACUUM ANALYZE)
        /// </summary>
        /// <returns>True if maintenance was successful, false otherwise</returns>
        Task<bool> PerformMaintenanceAsync();
        
        /// <summary>
        /// Gets the last maintenance execution time
        /// </summary>
        DateTime? LastMaintenanceTime { get; }
        
        /// <summary>
        /// Gets the next scheduled maintenance time
        /// </summary>
        DateTime? NextScheduledMaintenanceTime { get; }
    }
}

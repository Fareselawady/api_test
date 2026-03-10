namespace api_test.Services
{
    /// <summary>
    /// Checks drug interactions between a newly added medication and all
    /// medications the user already has, using the same DB logic as
    /// GET /api/medications/check-interaction — without making an HTTP call.
    /// </summary>
    public interface IInteractionService
    {
        /// <summary>
        /// Returns a list of human-readable warning strings, one per interaction
        /// found. Returns an empty list when there are no interactions.
        /// </summary>
        /// <param name="userId">The user whose existing medications are scanned.</param>
        /// <param name="newMedName">Trade name of the medication being added.</param>
        Task<List<string>> CheckInteractionsForNewMedAsync(int userId, string newMedName);
    }
}
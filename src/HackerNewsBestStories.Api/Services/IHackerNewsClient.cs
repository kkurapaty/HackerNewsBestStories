using HackerNewsBestStories.Api.Models;
using System.Collections.Generic;

namespace HackerNewsBestStories.Api.Services
{
    /// <summary>
    /// Thin wrapper around the Hacker News API. This is the only place we should be making HTTP calls to Hacker News, and it should be easy to mock for testing.
    /// </summary>
    public interface IHackerNewsClient
    {
        /// <summary>
        /// Gets the IDs of the best stories from Hacker News.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<int[]> GetBestStoryIdsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a Hacker News item by its ID.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>null if the item doesn't exist or the call fails after retries, rather than throwing an exception, so a single bad id can't sink a whole request.</returns>
        Task<HackerNewsItem?> GetItemAsync(int id, CancellationToken cancellationToken = default);
    }
}
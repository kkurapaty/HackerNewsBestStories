using HackerNewsBestStories.Api.Models;
using System.Collections.Generic;

namespace HackerNewsBestStories.Api.Services
{
    public interface IHackerNewsService
    {
        /// <summary>
        /// Gets the best stories from Hacker News.
        /// Returns a list of StoryDto objects representing the best stories. 
        /// Ordered by score in descending order. If fewer stories are available than the requested count, returns all available stories.
        /// </summary>
        /// <param name="count">The number of best stories to retrieve.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The result of the task is a list of StoryDto objects.</returns>
        Task<IReadOnlyList<StoryDto>> GetBestStoriesAsync(int count, CancellationToken cancellationToken = default);
    }
}
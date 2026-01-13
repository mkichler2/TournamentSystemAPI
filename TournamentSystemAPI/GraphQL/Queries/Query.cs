using System.Linq;
using System.Security.Claims;
using HotChocolate;
using HotChocolate.Data;
using HotChocolate.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using TournamentSystemAPI.Data;
using TournamentSystemAPI.Models;

namespace TournamentSystemAPI.GraphQL.Queries
{
    public class Query
    {
        public IQueryable<Tournament> GetTournaments([Service] AppDbContext context) => context.Tournaments;

        [Authorize]
        public IQueryable<Match> GetMyMatches([Service] AppDbContext context, [Service] IHttpContextAccessor httpContextAccessor)
        {
            var userIdClaim = httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Enumerable.Empty<Match>().AsQueryable();

            if (!int.TryParse(userIdClaim, out var userId)) return Enumerable.Empty<Match>().AsQueryable();

            return context.Matches.Where(m => m.Player1Id == userId || m.Player2Id == userId);
        }

        public IQueryable<Match> GetBracket(int tournamentId, [Service] AppDbContext context)
        {
            return context.Matches.Where(m => m.Bracket != null && m.Bracket.TournamentId == tournamentId);
        }
    }
}

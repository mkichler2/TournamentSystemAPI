using System.Security.Claims;
using HotChocolate;
using HotChocolate.AspNetCore.Authorization;
using TournamentSystemAPI.Data;
using TournamentSystemAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace TournamentSystemAPI.GraphQL.Queries
{
    public class Query
    {
        // Publiczny dostęp do turniejów
        public IQueryable<Tournament> GetTournaments([Service] AppDbContext context) 
    => context.Tournaments
        .Include(t => t.Bracket)
            .ThenInclude(b => b.Matches)
                .ThenInclude(m => m.Winner)
        .Include(t => t.Participants);

        // Użytkownik po zalogowaniu się ma możliwość pobrania informacji o swoich meczach"
        [Authorize]
        public IQueryable<Match> GetMyMatches([Service] AppDbContext context, [Service] IHttpContextAccessor httpContextAccessor)
        {
            var userIdString = httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Enumerable.Empty<Match>().AsQueryable();
            }

            return context.Matches.Where(m => m.Player1Id == userId || m.Player2Id == userId);
        }
    }
}
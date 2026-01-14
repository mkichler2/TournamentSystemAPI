using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HotChocolate;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using TournamentSystemAPI.Data;
using TournamentSystemAPI.Models;

namespace TournamentSystemAPI.GraphQL.Mutations
{
    public class Mutations
    {
        // --- Auth (Wymagane przez polecenie) ---

        public async Task<User> Register(string firstName, string lastName, string email, string password, [Service] AppDbContext context)
        {
            if (await context.Users.AnyAsync(u => u.Email == email)) 
                throw new Exception("Email already registered");

            var user = new User
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email
            };

            var hasher = new PasswordHasher<User>();
            user.Password = hasher.HashPassword(user, password);

            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Create response without password hash
            return new User
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email
            };
        }

        public async Task<string> Login(string email, string password, [Service] AppDbContext context, [Service] IConfiguration config)
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) 
                throw new Exception("Invalid credentials");

            var hasher = new PasswordHasher<User>();
            if (user.Password == null)
                throw new Exception("Invalid credentials");
                
            var verify = hasher.VerifyHashedPassword(user, user.Password, password);
            if (verify == PasswordVerificationResult.Failed) 
                throw new Exception("Invalid credentials");

            var key = config["Jwt:Key"];
            if (string.IsNullOrEmpty(key))
                throw new Exception("JWT Key is not configured");
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email ?? "")
            };

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
            
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return await Task.FromResult(tokenString);
        }

        // --- Metody z diagramu klas (jako Mutacje) ---

        // Diagram: Tournament.addParticipant(user)
        public async Task<Tournament> AddParticipant(int tournamentId, int userId, [Service] AppDbContext context)
        {
            var tournament = await context.Tournaments.Include(t => t.Participants).FirstOrDefaultAsync(t => t.Id == tournamentId);
            if (tournament == null) throw new Exception("Tournament not found");
            if (tournament.Status != "NotStarted") throw new Exception("Cannot add participants to a started tournament");

            var user = await context.Users.FindAsync(userId);
            if (user == null) throw new Exception("User not found");

            if (!tournament.Participants.Any(p => p.Id == userId))
            {
                tournament.Participants.Add(user);
                await context.SaveChangesAsync();
            }
            return tournament;
        }

        // Diagram: Bracket.generateBracket(participants)
        // Ta metoda odpowiada wyłącznie za stworzenie struktury meczów
        public async Task<Bracket> GenerateBracket(int tournamentId, [Service] AppDbContext context)
        {
            var tournament = await context.Tournaments
                .Include(t => t.Participants)
                .Include(t => t.Bracket)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null) throw new Exception("Tournament not found");
                if (tournament.Status != "NotStarted") 
                    throw new Exception("Cannot generate bracket for started tournament");
            if (tournament.Bracket != null) throw new Exception("Bracket already exists");
            if (tournament.Participants.Count < 2) throw new Exception("Not enough participants");

            // Tasowanie i tworzenie par
            var participants = tournament.Participants.OrderBy(_ => Guid.NewGuid()).ToList();
            var bracket = new Bracket { TournamentId = tournament.Id };
            
            for (int i = 0; i < participants.Count; i += 2)
            {
                var match = new Match
                {
                    Round = 1,
                    Bracket = bracket,
                    Player1Id = participants[i].Id,
                    Player2Id = (i + 1 < participants.Count) ? participants[i + 1].Id : null
                };
                context.Matches.Add(match);
                bracket.Matches.Add(match);
            }

            context.Brackets.Add(bracket);
            tournament.Bracket = bracket;
            await context.SaveChangesAsync();
            
            // Reload bracket with matches and players
            return await context.Brackets
                .Include(b => b.Matches)
                    .ThenInclude(m => m.Player1)
                .Include(b => b.Matches)
                    .ThenInclude(m => m.Player2)
                .FirstOrDefaultAsync(b => b.Id == bracket.Id) ?? bracket;
        }

        // Diagram: Tournament.start()
        // Odpowiada tylko za zmianę statusu (zakłada, że drabinka może być już wygenerowana lub generuje ją automatycznie jeśli brak)
        public async Task<Tournament> StartTournament(int id, [Service] AppDbContext context)
        {
            var tournament = await context.Tournaments.Include(t => t.Bracket).FirstOrDefaultAsync(t => t.Id == id);
            if (tournament == null) throw new Exception("Tournament not found");
            
            if (tournament.Bracket == null) 
            {
                // Opcjonalnie: Automatyczne wywołanie logiki generowania, jeśli użytkownik zapomniał
                await GenerateBracket(id, context); 
            }

            tournament.Status = "InProgress";
            await context.SaveChangesAsync();
            return tournament;
        }

        // Diagram: Tournament.finish()
        public async Task<Tournament> FinishTournament(int id, [Service] AppDbContext context)
        {
            var tournament = await context.Tournaments.FindAsync(id);
            if (tournament == null) throw new Exception("Tournament not found");

            tournament.Status = "Finished";
            await context.SaveChangesAsync();
            return tournament;
        }

        // Diagram: Match.play(winner)
        public async Task<Match> PlayMatch(int matchId, int winnerId, [Service] AppDbContext context)
        {
            var match = await context.Matches
                .Include(m => m.Player1)
                .Include(m => m.Player2)
                .Include(m => m.Winner)
                .FirstOrDefaultAsync(m => m.Id == matchId);
            if (match == null) throw new Exception("Match not found");

            if (match.Player1Id != winnerId && match.Player2Id != winnerId)
                throw new Exception("Winner is not a participant of this match");

            match.WinnerId = winnerId;
            // Tutaj można dodać logikę awansu do kolejnej rundy, ale polecenie prosi o prostotę ("niezwykle prostym narzędziem")
            
            await context.SaveChangesAsync();
            
            // Reload to ensure all related data is loaded
            return await context.Matches
                .Include(m => m.Player1)
                .Include(m => m.Player2)
                .Include(m => m.Winner)
                .FirstOrDefaultAsync(m => m.Id == matchId) ?? match;
        }

        // Diagram: Bracket.getMatchesForRound(round)
        // Zgodnie z poleceniem: metoda z diagramu jako mutacja (mimo że to pobieranie danych)
        public async Task<IEnumerable<Match>> GetMatchesForRound(int tournamentId, int round, [Service] AppDbContext context)
        {
             return await context.Matches
                .Include(m => m.Bracket)
                .Include(m => m.Player1)
                .Include(m => m.Player2)
                .Include(m => m.Winner)
                .Where(m => m.Bracket!.TournamentId == tournamentId && m.Round == round)
                .ToListAsync();
        }

        // Helper: CreateTournament (konstruktor Turnieju na diagramie)
        public async Task<Tournament> CreateTournament(string name, DateTime? startDate, [Service] AppDbContext context)
        {
            var tournament = new Tournament
            {
                Name = name,
                StartDate = startDate,
                Status = "NotStarted"
            };
            context.Tournaments.Add(tournament);
            await context.SaveChangesAsync();
            return tournament;
        }
    }
}
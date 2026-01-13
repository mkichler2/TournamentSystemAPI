using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HotChocolate;
// Używamy oryginalnego atrybutu z biblioteki, nie lokalnej podróbki
using HotChocolate.AspNetCore.Authorization; 
using Microsoft.EntityFrameworkCore;
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
                Email = email,
                Password = password // Plain text zgodnie z Twoim komentarzem "as requested"
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        public async Task<string> Login(string email, string password, [Service] AppDbContext context, [Service] IConfiguration config)
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email && u.Password == password);
            if (user == null) throw new Exception("Invalid credentials");

            var key = config["Jwt:Key"] ?? throw new Exception("JWT Key missing");
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email!)
            };

            var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
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
            return bracket;
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
            var match = await context.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
            if (match == null) throw new Exception("Match not found");

            if (match.Player1Id != winnerId && match.Player2Id != winnerId)
                throw new Exception("Winner is not a participant of this match");

            match.WinnerId = winnerId;
            // Tutaj można dodać logikę awansu do kolejnej rundy, ale polecenie prosi o prostotę ("niezwykle prostym narzędziem")
            
            await context.SaveChangesAsync();
            return match;
        }

        // Diagram: Bracket.getMatchesForRound(round)
        // Zgodnie z poleceniem: metoda z diagramu jako mutacja (mimo że to pobieranie danych)
        public async Task<IEnumerable<Match>> GetMatchesForRound(int tournamentId, int round, [Service] AppDbContext context)
        {
             return await context.Matches
                .Include(m => m.Bracket)
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
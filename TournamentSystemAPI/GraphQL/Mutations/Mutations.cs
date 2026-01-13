using System;
using System.Linq;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using HotChocolate;
using HotChocolate.AspNetCore.Authorization;
using TournamentSystemAPI.Data;
using TournamentSystemAPI.Models;

namespace TournamentSystemAPI.GraphQL.Mutations
{
    public class Mutations
    {

        public async Task<User> Register(string firstName, string lastName, string email, string password, [Service] AppDbContext context)
        {
            if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required");
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password is required");

            var existing = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (existing != null) throw new Exception("Email already registered");

            var user = new User
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Password = password // plain text as requested
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        public async Task<string> Login(string email, string password, [Service] AppDbContext context, [Service] IConfiguration config)
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email && u.Password == password);
            if (user == null) throw new Exception("Invalid credentials");

            var key = config["Jwt:Key"] ?? string.Empty;
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email ?? string.Empty)
            };

            var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<Tournament> StartTournament(int id, [Service] AppDbContext context)
        {
            var tournament = await context.Tournaments
                .Include(t => t.Participants)
                .Include(t => t.Bracket)
                    .ThenInclude(b => b.Matches)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null) throw new Exception("Tournament not found");
            if (tournament.Status == "InProgress") throw new Exception("Tournament already in progress");

            // Update status
            tournament.Status = "InProgress";

            // Shuffle participants randomly
            var participants = tournament.Participants.ToList();
            var rnd = new Random();
            participants = participants.OrderBy(_ => rnd.Next()).ToList();

            // Create bracket if missing
            var bracket = tournament.Bracket ?? new Bracket { TournamentId = tournament.Id };
            bracket.Matches = bracket.Matches ?? new List<Match>();

            // Pair participants into round 1 matches
            for (int i = 0; i < participants.Count; i += 2)
            {
                var player1 = participants[i];
                var player2 = (i + 1 < participants.Count) ? participants[i + 1] : null;

                var match = new Match
                {
                    Round = 1,
                    Bracket = bracket,
                    Player1Id = player1?.Id,
                    Player2Id = player2?.Id
                };

                bracket.Matches.Add(match);
                context.Matches.Add(match);
            }

            if (tournament.Bracket == null)
            {
                tournament.Bracket = bracket;
                context.Brackets.Add(bracket);
            }

            await context.SaveChangesAsync();

            // Reload to include created matches and bracket
            var updated = await context.Tournaments
                .Include(t => t.Participants)
                .Include(t => t.Bracket)
                    .ThenInclude(b => b.Matches)
                .FirstOrDefaultAsync(t => t.Id == id);

            return updated!;
        }

        public async Task<Match> PlayMatch(int matchId, int winnerId, [Service] AppDbContext context)
        {
            var match = await context.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
            if (match == null) throw new Exception("Match not found");

            if (match.Player1Id != winnerId && match.Player2Id != winnerId)
                throw new Exception("Winner must be one of the match participants");

            match.WinnerId = winnerId;
            await context.SaveChangesAsync();
            return match;
        }

        // CreateTournament - available to anyone
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

        // AddParticipant - available to anyone
        public async Task<Tournament> AddParticipant(int tournamentId, int userId, [Service] AppDbContext context)
        {
            var tournament = await context.Tournaments.Include(t => t.Participants).FirstOrDefaultAsync(t => t.Id == tournamentId);
            if (tournament == null) throw new Exception("Tournament not found");

            var user = await context.Users.FindAsync(userId);
            if (user == null) throw new Exception("User not found");

            if (!tournament.Participants.Any(p => p.Id == userId))
            {
                tournament.Participants.Add(user);
                await context.SaveChangesAsync();
            }

            return tournament;
        }

        // GenerateBracket - corresponds to Bracket.generateBracket(participants)
        public async Task<Bracket> GenerateBracket(int tournamentId, [Service] AppDbContext context)
        {
            var tournament = await context.Tournaments
                .Include(t => t.Participants)
                .Include(t => t.Bracket)
                    .ThenInclude(b => b.Matches)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null) throw new Exception("Tournament not found");

            var participants = tournament.Participants.OrderBy(_ => Guid.NewGuid()).ToList();
            var bracket = tournament.Bracket ?? new Bracket { TournamentId = tournament.Id };
            bracket.Matches = new List<Match>();

            for (int i = 0; i < participants.Count; i += 2)
            {
                var player1 = participants[i];
                var player2 = (i + 1 < participants.Count) ? participants[i + 1] : null;

                var match = new Match
                {
                    Round = 1,
                    Bracket = bracket,
                    Player1Id = player1?.Id,
                    Player2Id = player2?.Id
                };

                bracket.Matches.Add(match);
                context.Matches.Add(match);
            }

            if (tournament.Bracket == null)
            {
                tournament.Bracket = bracket;
                context.Brackets.Add(bracket);
            }

            await context.SaveChangesAsync();
            return bracket;
        }

        // GetMatchesForRound - returns all matches for a given round in a tournament
        public async Task<IEnumerable<Match>> GetMatchesForRound(int tournamentId, int round, [Service] AppDbContext context)
        {
            var matches = await context.Matches
                .Include(m => m.Bracket)
                .Where(m => m.Bracket != null && m.Bracket.TournamentId == tournamentId && m.Round == round)
                .ToListAsync();

            return matches;
        }

        // FinishTournament - sets status to Finished
        public async Task<Tournament> FinishTournament(int id, [Service] AppDbContext context)
        {
            var tournament = await context.Tournaments.FirstOrDefaultAsync(t => t.Id == id);
            if (tournament == null) throw new Exception("Tournament not found");

            tournament.Status = "Finished";
            await context.SaveChangesAsync();
            return tournament;
        }
    }
}


























































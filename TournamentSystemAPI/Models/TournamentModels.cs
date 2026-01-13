using System;
using System.Collections.Generic;

namespace TournamentSystemAPI.Models
{
    public class User
    {
        public int Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        // Plain text password as requested
        public string? Password { get; set; }
    }

    public class Tournament
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public DateTime? StartDate { get; set; }
        public string? Status { get; set; }

        public List<User> Participants { get; set; } = new List<User>();
        public Bracket? Bracket { get; set; }
    }

    public class Bracket
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public Tournament? Tournament { get; set; }

        public List<Match> Matches { get; set; } = new List<Match>();
    }

    public class Match
    {
        public int Id { get; set; }
        public int Round { get; set; }

        public int? BracketId { get; set; }
        public Bracket? Bracket { get; set; }

        // Foreign keys (optional) to keep EF mapping explicit
        public int? Player1Id { get; set; }
        public User? Player1 { get; set; }

        public int? Player2Id { get; set; }
        public User? Player2 { get; set; }

        public int? WinnerId { get; set; }
        public User? Winner { get; set; }
    }
}

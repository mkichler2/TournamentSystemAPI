Tournament System API
API do zarządzania systemem turniejowym, oparte na .NET 8 i GraphQL (HotChocolate).

Wymagania wstępne
.NET 8.0 SDK
SQL Server (LocalDB lub pełna instancja)
Narzędzie dotnet-ef (Global tool)

Instalacja i konfiguracja
1. Zainstaluj narzędzia Entity Framework (opcjonalnie)
Jeśli nie posiadasz globalnego narzędzia EF Core:

PowerShell
dotnet tool install --global dotnet-ef

2. Przygotuj bazę danych
Wykonaj reset bazy danych (usunięcie i ponowna migracja), aby zapewnić czyste środowisko testowe i zgodność ID.

PowerShell
dotnet ef database drop --force
dotnet ef database update

3. Uruchom aplikację (u mnie działa)
PowerShell
dotnet run --project "c:\Users\macie\Desktop\System Pucharowy API\TournamentSystemAPI\TournamentSystemAPI\TournamentSystemAPI.csproj" --urls "http://localhost:5000"

Wtedy interfejs GraphQL (Banana Cake Pop) jest dostępny pod adresem: http://localhost:5000/graphql

Scenariusz testowy:
1. Rejestracja użytkowników

mutation Rejestracja {
  u1: register(firstName: "Maciej", lastName: "Maciejowski", email: "maciej@test.com", password: "123") {
    id
  }
  u2: register(firstName: "Paweł", lastName: "Pawłowski", email: "pawel@game.com", password: "123") {
    id
  }
}
2. Utworzenie turnieju

mutation UtworzTurniej {
  createTournament(name: "Mistrzostwa programowania", startDate: "2026-05-01T12:00:00Z") {
    id
    status
  }
}
3. Przypisanie uczestników

mutation DodajUczestnikow {
  p1: addParticipant(tournamentId: 1, userId: 1) {
    id
  }
  p2: addParticipant(tournamentId: 1, userId: 2) {
    id
  }
}
4. Generowanie drabinki
Zwraca strukturę meczów wraz z ich identyfikatorami.

mutation GenerujDrabinke {
  generateBracket(tournamentId: 1) {
    id
    matches {
      id
      round
      player1 { firstName }
      player2 { firstName }
    }
  }
}
5. Start turnieju
Zmiana statusu na InProgress.

mutation Start {
  startTournament(id: 1) {
    id
    status
  }
}
6. Rozegranie meczu
Symulacja wygranej gracza o ID 2 (Paweł).

mutation RozegrajMecz {
  playMatch(matchId: 1, winnerId: 2) {
    id
    winner {
      firstName
      lastName
    }
  }
}
7. Weryfikacja wyników
Pobranie pełnej struktury turnieju z zwycięzcami.

query SprawdzWyniki {
  tournaments {
    id
    bracket {
      matches {
        id
        round
        winner {
          firstName
          lastName
        }
      }
    }
  }
}
8. Zakończenie turnieju

mutation ZakonczTurniej {
  finishTournament(id: 1) {
    id
    status
  }
}